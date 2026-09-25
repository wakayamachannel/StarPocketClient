// Copying Steam's Among Us into the mod's copy, in-process (v0.2). This replaces the launcher's
//   robocopy <src> <dst> /E /XD BepInEx dotnet /XF winhttp.dll doorstop_config.ini steam_appid.txt /R:2 /W:2
// plus its list-only verification pass (ps1:848-863), with the same rules:
//   - every folder (also empty ones), except folders NAMED BepInEx or dotnet anywhere in the tree (/XD by name)
//   - except files named winhttp.dll, doorstop_config.ini or steam_appid.txt anywhere (/XF by name): those belong to
//     BepInEx and to the mod copy, never to Steam's folder
//   - a file is copied when the destination is missing or its size or last-write time differs (robocopy's default: also
//     "older", so no /XO - a half-written file with a newer time is repaired), and the time is copied with it
//   - a failed file is tried again twice, 2 seconds apart (/R:2 /W:2), then the copy fails
//   - afterwards the same scan runs again (the /L pass): anything still to copy means "incomplete" and the caller does
//     not record the copy as done
// Nothing is ever deleted in the destination (robocopy without /MIR), and the destination is never inside the source.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Starpocket.Client.Core
{
    internal sealed class CopyPlan
    {
        public readonly List<string> Files = new List<string>();      // paths relative to the source
        public readonly List<string> Folders = new List<string>();    // relative folders that have to exist
        public long Bytes;
    }

    internal sealed class CopyResult
    {
        public bool Ok;
        public int Copied, Same, Failed;
        public long Bytes;
        public string Error;
        /// <summary>The destination's drive does not hold what has to be copied: nothing was copied at all.</summary>
        public bool NotEnoughSpace;
        public long NeedBytes, FreeBytes;
        /// <summary>A path in the destination would pass Windows' 259-character limit: nothing was copied at all. The
        /// cause is the chosen folder sitting too deep - never the game, never Steam.</summary>
        public bool PathTooLong;
        public int LongestLength;
        public string LongestPath;
    }

    internal sealed class FileCopy
    {
        public static readonly string[] SkipFolders = { "BepInEx", "dotnet" };
        public static readonly string[] SkipFiles = { "winhttp.dll", "doorstop_config.ini", "steam_appid.txt" };

        public int Retries = 2;
        public int RetryWaitMs = 2000;
        /// <summary>{done bytes, total bytes} while copying (the UI's progress card).</summary>
        public Action<long, long> Progress = (_, __) => { };
        public Action<string> Log = _ => { };
        public Func<bool> Cancelled = () => false;
        /// <summary>The self-test makes one file fail a few times to check the retries.</summary>
        internal Action<string> BeforeCopyFile = _ => { };
        /// <summary>Room left over after the copy (a game needs space to run). Asked for on top of what is copied.</summary>
        public long FreeSpaceMarginBytes = 256L * 1024 * 1024;
        /// <summary>Free bytes on the drive of that folder, or -1 when it cannot be read (then nothing is refused).</summary>
        public Func<string, long> FreeSpaceOf = DriveFreeBytes;

        public static long DriveFreeBytes(string path)
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).AvailableFreeSpace; }
            catch (Exception) { return -1; }
        }

        /// <summary>Windows' classic limit: a full path may hold 259 characters plus the terminator.</summary>
        public const int MaxPath = 259;

        /// <summary>The longest path this copy would write, so a destination folder that sits too deep is found out
        /// before the copy rather than halfway down the tree. A file is written as "&lt;name&gt;.part" first
        /// (<see cref="CopyOne"/>), so those five characters count too.</summary>
        public static string LongestDestination(string dst, CopyPlan plan)
        {
            string full = dst;
            try { full = Path.GetFullPath(dst); } catch (Exception) { }
            string root = full.EndsWith("\\", StringComparison.Ordinal) ? full : full + "\\";
            string longest = root;
            foreach (var rel in plan.Folders) if (root.Length + rel.Length > longest.Length) longest = root + rel;
            foreach (var rel in plan.Files) if (root.Length + rel.Length + 5 > longest.Length) longest = root + rel + ".part";
            return longest;
        }

        /// <summary>What robocopy would copy: the files that are missing or different, and every folder.</summary>
        public static CopyPlan Plan(string src, string dst)
        {
            var plan = new CopyPlan();
            Walk(src, dst, "", plan);
            return plan;
        }

        static void Walk(string src, string dst, string rel, CopyPlan plan)
        {
            var dir = new DirectoryInfo(rel.Length == 0 ? src : Path.Combine(src, rel));
            foreach (var sub in dir.GetDirectories())
            {
                if (IsSkippedFolder(sub.Name)) continue;
                string childRel = rel.Length == 0 ? sub.Name : rel + "\\" + sub.Name;
                plan.Folders.Add(childRel);
                Walk(src, dst, childRel, plan);
            }
            foreach (var f in dir.GetFiles())
            {
                if (IsSkippedFile(f.Name)) continue;
                string childRel = rel.Length == 0 ? f.Name : rel + "\\" + f.Name;
                if (!NeedsCopy(f, Path.Combine(dst, childRel))) continue;
                plan.Files.Add(childRel);
                plan.Bytes += f.Length;
            }
        }

        public static bool IsSkippedFolder(string name) => Array.FindIndex(SkipFolders, s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) >= 0;

        public static bool IsSkippedFile(string name) => Array.FindIndex(SkipFiles, s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) >= 0;

        /// <summary>robocopy's "same file" test: same size and same last-write time (NTFS, no /FFT tolerance).</summary>
        public static bool NeedsCopy(FileInfo source, string destPath)
        {
            var d = new FileInfo(destPath);
            if (!d.Exists) return true;
            if (d.Length != source.Length) return true;
            return d.LastWriteTimeUtc != source.LastWriteTimeUtc;
        }

        /// <summary>The destination may not be the source or inside it (an in-process copy would walk into itself). Both
        /// sides are expanded first, so the same folder written two ways (C:\PROGRA~2\... and C:\Program Files (x86)\...)
        /// is recognised as one folder.</summary>
        public static bool SameOrInside(string outer, string inner)
        {
            string a = Normalize(outer), b = Normalize(inner);
            if (a == null || b == null) return false;
            return b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }

        static string Normalize(string path)
        {
            string full = Uninstaller.Full(path);
            return full == null ? null : full + "\\";
        }

        /// <summary>Half-written files a copy that was cut off left behind (&lt;file&gt;.part). The verification pass only
        /// looks at names on the source side, so these would stay invisible for ever.</summary>
        public int SweepPartFiles(string dst)
        {
            int n = 0;
            try
            {
                if (!Directory.Exists(dst)) return 0;
                foreach (var f in Directory.GetFiles(dst, "*.part", SearchOption.AllDirectories))
                {
                    try { File.Delete(f); n++; } catch (Exception) { }
                }
            }
            catch (Exception) { }
            if (n > 0) Log("copy: " + n + " half-written file(s) of an earlier run removed");
            return n;
        }

        /// <summary>Copies like the launcher's Copy-GameFiles; false with <see cref="CopyResult.Error"/> set when the copy
        /// failed or is incomplete (the caller then keeps the "not copied" state and the viewer can press again).</summary>
        public CopyResult Run(string src, string dst)
        {
            var result = new CopyResult();
            try
            {
                if (!Directory.Exists(src)) { result.Error = "source missing"; return result; }
                if (SameOrInside(src, dst) || SameOrInside(dst, src)) { result.Error = "the copy's folder is inside the source folder"; return result; }
                var plan = Plan(src, dst);
                // before anything is written, and before the folder is even made: Windows' 260-character limit. A folder
                // that sits too deep lets the copy start and then die somewhere far down the tree, on a file whose name
                // the viewer has never heard of, with "a part of the path could not be found" - which reads like a
                // missing game and made the app tell the viewer to close Steam (2026-09-23, a path of exactly 260).
                string longest = LongestDestination(dst, plan);
                if (longest.Length > MaxPath)
                {
                    result.PathTooLong = true;
                    result.LongestLength = longest.Length;
                    result.LongestPath = longest;
                    result.Error = "path too long (" + longest.Length + ")";
                    return result;
                }
                Directory.CreateDirectory(dst);
                SweepPartFiles(dst);
                // before anything is written: a full disk otherwise shows up minutes later, after 3 retries x 2 s on the
                // first file that would not fit, with a half-copied game left behind (v0.3 review)
                long free = FreeSpaceOf != null ? FreeSpaceOf(dst) : -1;
                if (free >= 0 && plan.Bytes + FreeSpaceMarginBytes > free)
                {
                    result.NotEnoughSpace = true;
                    result.NeedBytes = plan.Bytes + FreeSpaceMarginBytes;
                    result.FreeBytes = free;
                    result.Error = "not enough space";
                    return result;
                }
                foreach (var f in plan.Folders) Directory.CreateDirectory(Path.Combine(dst, f));
                long done = 0;
                Progress(0, plan.Bytes);
                foreach (var rel in plan.Files)
                {
                    if (Cancelled()) { result.Error = "cancelled"; return result; }
                    string from = Path.Combine(src, rel), to = Path.Combine(dst, rel);
                    string why = CopyOne(from, to);
                    if (why != null)
                    {
                        result.Failed++;
                        result.Error = rel + ": " + why;
                        return result;
                    }
                    result.Copied++;
                    try { done += new FileInfo(to).Length; } catch (Exception) { }
                    result.Bytes = done;
                    Progress(done, plan.Bytes);
                }
                // the launcher's second, list-only robocopy pass: nothing may be left to copy
                var check = Plan(src, dst);
                if (check.Files.Count > 0) { result.Error = "incomplete (" + check.Files.Count + " file(s) still different)"; return result; }
                result.Same = CountFiles(src) - result.Copied;
                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.GetBaseException().Message;
                return result;
            }
        }

        static int CountFiles(string src)
        {
            int n = 0;
            var stack = new Stack<string>();
            stack.Push(src);
            while (stack.Count > 0)
            {
                var dir = new DirectoryInfo(stack.Pop());
                foreach (var sub in dir.GetDirectories()) if (!IsSkippedFolder(sub.Name)) stack.Push(sub.FullName);
                foreach (var f in dir.GetFiles()) if (!IsSkippedFile(f.Name)) n++;
            }
            return n;
        }

        /// <summary>One file with robocopy's retries; null when it is there, else why not.</summary>
        string CopyOne(string from, string to)
        {
            string last = null;
            for (int attempt = 0; attempt <= Retries; attempt++)
            {
                if (attempt > 0)
                {
                    Log("copy: trying again (" + attempt + "/" + Retries + "): " + Path.GetFileName(from) + " - " + last);
                    Thread.Sleep(RetryWaitMs);
                }
                try
                {
                    BeforeCopyFile(from);
                    var fi = new FileInfo(from);
                    string tmp = to + ".part";
                    using (var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                        input.CopyTo(output, 1 << 20);
                    File.SetLastWriteTimeUtc(tmp, fi.LastWriteTimeUtc);
                    if (File.Exists(to)) File.Delete(to);
                    File.Move(tmp, to);
                    return null;
                }
                catch (Exception ex)
                {
                    last = ex.GetBaseException().Message;
                    try { if (File.Exists(to + ".part")) File.Delete(to + ".part"); } catch (Exception) { }
                }
            }
            return last;
        }
    }
}
