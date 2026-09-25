// Zip handling for the install (the launcher's Test-ZipHas / Expand-ZipOver, ps1:707-741), in-process with
// System.IO.Compression - no shell, no tar.exe, no Expand-Archive.
//   Has:       does the zip hold this entry (\ read as /, case ignored)
//   ExpandOver: unpack over a folder, never deleting anything, skipping entries whose path starts with one of `skip`
//              (the install skips BepInEx\config\ so the viewer's settings are kept) and refusing an entry that would
//              land outside the destination (a "zip slip" name like ..\..\x). There are two: one that opens the file
//              itself, and one that takes an archive already open (VerifiedZip, for the pinned BepInEx zip).
// Each file is written to <file>.part and only then moved into place (like FileCopy.CopyOne): a kill, a crash, a full
// disk or a locked file part-way through can leave the destination holding old files and new files, but never a file
// that is half old and half new (v0.3 review: a truncated BepInEx DLL stopped the game from starting at all, and the
// 修復 button could not put it right because the version checks said "already there").
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.IO.Compression;

namespace Starpocket.Client.Core
{
    internal static class ZipFiles
    {
        public static bool Has(string zipPath, string entry)
        {
            if (!File.Exists(zipPath)) return false;
            try
            {
                using (var za = ZipFile.OpenRead(zipPath))
                    foreach (var e in za.Entries)
                        if (string.Equals(e.FullName.Replace('\\', '/'), entry, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Unpacks over <paramref name="dest"/> and returns how many files were written. Throws on a broken zip.</summary>
        public static int ExpandOver(string zipPath, string dest, string[] skip, Action<int> progress = null)
        {
            using (var za = ZipFile.OpenRead(zipPath))
                return ExpandOver(za, dest, skip, progress);
        }

        /// <summary>The same, from an archive that is already open. This is the one the pinned BepInEx zip goes through
        /// (<see cref="VerifiedZip"/>): the fingerprint, the entry check and the unpacking then all read a single handle
        /// that was never let go, so nothing can swap the file between "we checked it" and "we unpacked it".</summary>
        public static int ExpandOver(ZipArchive za, string dest, string[] skip, Action<int> progress = null)
        {
            Directory.CreateDirectory(dest);
            string destFull = Path.GetFullPath(dest);
            if (!destFull.EndsWith("\\", StringComparison.Ordinal)) destFull += "\\";
            int count = 0;
            {
                foreach (var e in za.Entries)
                {
                    string rel = e.FullName.Replace('/', '\\');
                    if (rel.EndsWith("\\", StringComparison.Ordinal) || string.IsNullOrEmpty(e.Name)) continue;
                    bool skipped = false;
                    if (skip != null)
                        foreach (var s in skip)
                            if (!string.IsNullOrEmpty(s) && rel.StartsWith(s, StringComparison.OrdinalIgnoreCase)) { skipped = true; break; }
                    if (skipped) continue;
                    string full;
                    try { full = Path.GetFullPath(Path.Combine(destFull, rel)); }
                    catch (Exception) { continue; }
                    if (!full.StartsWith(destFull, StringComparison.OrdinalIgnoreCase)) continue;
                    string dir = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    string tmp = full + ".part";
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                    try
                    {
                        e.ExtractToFile(tmp, true);
                        if (File.Exists(full)) File.Delete(full);
                        File.Move(tmp, full);
                    }
                    catch (Exception)
                    {
                        try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                        throw;
                    }
                    count++;
                    progress?.Invoke(count);
                }
            }
            return count;
        }
    }
}
