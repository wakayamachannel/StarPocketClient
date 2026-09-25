// The game-log housekeeping of the launcher (PORT-MAP 3.7; ps1:1072-1434, v0.5.5):
//  - Save-GameLog: right before a game starts (and when the app opens), BepInEx\LogOutput.log is copied to
//    BepInEx\PocketRoles\logs\LogOutput-<yyyy-MM-dd_HHmmss>.log (named by its last write time).
//  - Remove-ExpiredLocalData: logs, day / month zips and Desktop report zips 30 days old are deleted (the logs hold
//    players' names; nothing for the host to do).
//  - Compress-OldGameLogs (v0.4, ps1:1285-1380): loose logs older than 7 days go into one zip per day. This only
//    saves room - nothing is kept longer or shorter for it: a day zip is deleted 30 days after its day, like the
//    loose logs it holds (ArchiveExpired), and no number of days is changed anywhere.
//  - Get-LogArchiveInfo / Format-Size (v0.4, ps1:1399-1417): how much room the logs folder takes, and the notice
//    once it passes 2 GB.
// Both run under the named mutex Local\PocketRolesLauncher.logs (the PowerShell launcher and the mod take the same one),
// waiting up to 15 s; an abandoned mutex counts as taken. Errors never escape: they go to client.log only (Log-Quiet).
// Call from a worker thread (the mutex is owned by the calling thread and released on it).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;

namespace Starpocket.Client.Core
{
    /// <summary>Get-LogArchiveInfo: what is in BepInEx\PocketRoles\logs right now.</summary>
    internal sealed class LogFolderInfo
    {
        public long Bytes;
        public int Logs, Zips;
        /// <summary>Over 2 GB: the size is shown in the danger colour and said once when the app opens.</summary>
        public bool Big => Bytes > GameLogs.BigFolder;
        public string Size => GameLogs.FormatSize(Bytes);
    }

    internal sealed class GameLogs
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>ps1: `$bytes -gt 2GB`. Only a notice - nothing is deleted for it.</summary>
        public const long BigFolder = 2L * 1024 * 1024 * 1024;
        /// <summary>$script:LogZipBudget: how much is zipped per app start; the rest waits for the next one.</summary>
        public long LogZipBudget = 256L * 1024 * 1024;
        /// <summary>$script:LogZipMaxFile: a log bigger than this stays loose (zipping it would take far too long).</summary>
        public long LogZipMaxFile = 512L * 1024 * 1024;

        public string LogPath;          // <Modded>\BepInEx\LogOutput.log
        public string LogArchiveDir;    // <Modded>\BepInEx\PocketRoles\logs
        public string Desktop;          // report zips (PocketRoles-report-<yyyyMMdd-HHmm>.zip)
        public string CacheDir;         // %TEMP%\PocketRolesLauncher (report-<yyyyMMdd-HHmm> work folders)
        public string MutexName = AppInfo.LogMutexName;
        public int LockWaitMs = 15000;
        public Func<bool> IsModdedGameRunning = () => false;
        public Func<DateTime> Now = () => DateTime.Now;
        public int ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
        /// <summary>Log: client.log (the launcher's log box and launcher.log).</summary>
        public Action<string> Log = _ => { };
        /// <summary>Log-Quiet: client.log only.</summary>
        public Action<string> LogQuiet = _ => { };
        public string Lang = "ja";

        public static string ArchiveName(DateTime t) => "LogOutput-" + t.ToString("yyyy-MM-dd_HHmmss", Ci) + ".log";

        static readonly Regex ArchiveNamePattern = new Regex(@"^LogOutput-(\d{4}-\d{2}-\d{2}_\d{6})\.log$", RegexOptions.IgnoreCase);
        static readonly Regex DayZip = new Regex(@"^logs-(\d{8})(-\d+)?\.zip$", RegexOptions.IgnoreCase);
        static readonly Regex MonthZip = new Regex(@"^logs-(\d{4}-\d{2})(-\d+)?\.zip$", RegexOptions.IgnoreCase);
        static readonly Regex ReportZip = new Regex(@"^PocketRoles-report-(\d{8}-\d{4})\.zip$");
        static readonly Regex ReportDir = new Regex(@"^report-(\d{8}-\d{4})$");
        // v0.3: the one-player evidence zip goes to the second and may carry -2 (two players exported in the same
        // second must not overwrite each other). It is deleted after 30 days like a report zip.
        static readonly Regex EvidenceZip = new Regex(@"^PocketRoles-evidence-(\d{8}-\d{4}(?:\d{2})?)(?:-\d+)?\.zip$");
        static readonly Regex EvidenceDir = new Regex(@"^evidence-(\d{8}-\d{4}(?:\d{2})?)$");

        /// <summary>The time in a zip's name, by the minute or by the second; null when the name is not one of ours.</summary>
        static bool ZipStamp(Match m, out DateTime t)
        {
            t = default(DateTime);
            if (!m.Success) return false;
            string s = m.Groups[1].Value;
            return DateTime.TryParseExact(s, s.Length == 15 ? "yyyyMMdd-HHmmss" : "yyyyMMdd-HHmm", Ci, DateTimeStyles.None, out t);
        }

        /// <summary>evidence-90d: the two log lines that back a record still kept are saved into evidence\&lt;id&gt;.log
        /// before the log holding them is deleted. null when the caller does not want that (the self-tests of the plain
        /// log rules).</summary>
        public EvidenceStore Evidence;
        Dictionary<string, string> backingWanted;
        bool backingAsked;
        /// <summary>How many backing files the last RemoveExpired wrote.</summary>
        public int BackingSaved { get; private set; }

        /// <summary>Called with every log file (or zip of logs) that is about to be deleted.</summary>
        void KeepBacking(string path)
        {
            if (Evidence == null) return;
            try
            {
                // worked out once, on the first log that is due (most runs delete nothing)
                if (!backingAsked) { backingAsked = true; backingWanted = Evidence.BackingWanted(); }
                if (backingWanted == null || backingWanted.Count == 0) return;
                BackingSaved += Evidence.SaveBackingFrom(path, backingWanted);
            }
            catch (Exception ex) { LogQuiet("evidence backing: " + ex.Message); }
        }

        /// <summary>Get-LogArchiveTime: the time in LogOutput-&lt;time&gt;.log; null for any other name.</summary>
        public static DateTime? ArchiveTime(string name)
        {
            var m = ArchiveNamePattern.Match(name ?? "");
            DateTime t;
            if (m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd_HHmmss", Ci, DateTimeStyles.None, out t)) return t;
            return null;
        }

        /// <summary>Test-LogArchiveExpired (the mod's AegisPrivacyCore.LogArchiveExpired uses the same rules).</summary>
        public static bool ArchiveExpired(string name, DateTime lastWrite, DateTime now)
        {
            var lt = ArchiveTime(name);
            if (lt.HasValue) return (now - lt.Value).TotalDays >= 30;
            if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) return (now - lastWrite).TotalDays >= 1;
            DateTime t;
            var m = DayZip.Match(name);
            // year 9999 (a hand-made name): the end of that day / month is past DateTime.MaxValue, never due
            if (m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMdd", Ci, DateTimeStyles.None, out t)) return t.Year < 9999 && (now - t.AddDays(1)).TotalDays >= 30;
            m = MonthZip.Match(name);
            if (m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM", Ci, DateTimeStyles.None, out t)) return t.Year < 9999 && (now - t.AddMonths(1)).TotalDays >= 30;
            return false;
        }

        /// <summary>Test-ZipHas: the zip holds an entry of that name (\ read as /, case ignored).</summary>
        public static bool ZipHas(string zip, string entry)
        {
            if (!File.Exists(zip)) return false;
            try
            {
                using (var za = ZipFile.OpenRead(zip))
                    foreach (var e in za.Entries)
                        if (string.Equals(e.FullName.Replace('\\', '/'), entry, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Test-LogInZips: in one of the zips of its day (logs-yyyyMMdd*.zip) or month (logs-yyyy-MM*.zip).</summary>
        public bool InZips(DateTime t, string name)
        {
            try
            {
                if (!Directory.Exists(LogArchiveDir)) return false;
                foreach (var stem in new[] { t.ToString("yyyyMMdd", Ci), t.ToString("yyyy-MM", Ci) })
                    foreach (var z in Directory.GetFiles(LogArchiveDir, "logs-" + stem + "*.zip"))
                        if (ZipHas(z, name)) return true;
            }
            catch (Exception) { }
            return false;
        }

        string T(string key, params object[] args) => S.T(Lang, key, args);

        // ---- the log lock (Enter-LogLock / Exit-LogLock)
        Mutex EnterLock() => EnterLock(LockWaitMs);

        Mutex EnterLock(int waitMs)
        {
            Mutex m = null;
            try
            {
                m = new Mutex(false, MutexName);
                bool got;
                try { got = m.WaitOne(waitMs); }
                catch (AbandonedMutexException) { got = true; }   // its owner died: ours now
                if (got) return m;
                m.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                LogQuiet("log lock: " + ex.GetBaseException().Message);
                if (m != null) m.Dispose();
                return null;
            }
        }

        static void ExitLock(Mutex m)
        {
            if (m == null) return;
            try { m.ReleaseMutex(); } catch (Exception) { }
            m.Dispose();
        }

        /// <summary>Remove-ExpiredLocalData. Returns the number of files deleted (the report work folders are not counted).</summary>
        public int RemoveExpired()
        {
            Mutex lk = null;
            int n = 0;
            try
            {
                lk = EnterLock();
                if (lk == null) { LogQuiet("expiry: skipped (another launcher window is busy with the logs)"); return 0; }
                DateTime now = Now();
                var src = new FileInfo(LogPath);
                BackingSaved = 0;
                backingAsked = false;
                if (src.Exists && (now - src.LastWriteTime).TotalDays >= 30 && !IsModdedGameRunning())
                {
                    KeepBacking(src.FullName);
                    try { File.Delete(src.FullName); n++; } catch (Exception ex) { LogQuiet("expiry: " + src.Name + ": " + ex.GetBaseException().Message); }
                }
                string bep = src.DirectoryName;
                if (!string.IsNullOrEmpty(bep) && Directory.Exists(bep))
                {
                    foreach (var f in Directory.GetFiles(bep, "LogOutput-*.log"))
                    {
                        var lt = ArchiveTime(Path.GetFileName(f));
                        if (lt.HasValue && (now - lt.Value).TotalDays >= 30)
                        {
                            KeepBacking(f);
                            try { File.Delete(f); n++; } catch (Exception ex) { LogQuiet("expiry: " + f + ": " + ex.GetBaseException().Message); }
                        }
                    }
                }
                if (Directory.Exists(LogArchiveDir))
                {
                    foreach (var fi in new DirectoryInfo(LogArchiveDir).GetFiles())
                    {
                        // per file: one odd name (a hand-made logs-99991231.zip) must not stop the rest of the expiry
                        try
                        {
                            if (!ArchiveExpired(fi.Name, fi.LastWriteTime, now)) continue;
                            KeepBacking(fi.FullName);
                            fi.Delete();
                            n++;
                        }
                        catch (Exception ex) { LogQuiet("expiry: " + fi.Name + ": " + ex.GetBaseException().Message); }
                    }
                }
                if (!string.IsNullOrEmpty(Desktop) && Directory.Exists(Desktop))
                {
                    foreach (var f in Directory.GetFiles(Desktop, "PocketRoles-report-*.zip"))
                    {
                        DateTime t;
                        if (ZipStamp(ReportZip.Match(Path.GetFileName(f)), out t) && (now - t).TotalDays >= 30)
                        {
                            try { File.Delete(f); n++; } catch (Exception ex) { LogQuiet("expiry: " + f + ": " + ex.GetBaseException().Message); }
                        }
                    }
                    // v0.3: the one-player evidence zips, by the time in their name and never by anything else
                    foreach (var f in Directory.GetFiles(Desktop, "PocketRoles-evidence-*.zip"))
                    {
                        DateTime t;
                        if (ZipStamp(EvidenceZip.Match(Path.GetFileName(f)), out t) && (now - t).TotalDays >= 30)
                        {
                            try { File.Delete(f); n++; } catch (Exception ex) { LogQuiet("expiry: " + f + ": " + ex.GetBaseException().Message); }
                        }
                    }
                }
                if (!string.IsNullOrEmpty(CacheDir) && Directory.Exists(CacheDir))
                {
                    foreach (var pattern in new[] { "report-*", "evidence-*" })
                        foreach (var d in Directory.GetDirectories(CacheDir, pattern))
                        {
                            string leaf = Path.GetFileName(d);
                            DateTime t;
                            if ((ZipStamp(ReportDir.Match(leaf), out t) || ZipStamp(EvidenceDir.Match(leaf), out t)) && (now - t).TotalDays >= 1)
                            {
                                try { Directory.Delete(d, true); } catch (Exception ex) { LogQuiet("expiry: " + d + ": " + ex.GetBaseException().Message); }
                            }
                        }
                }
                if (n > 0) Log(T("lg_expired", n));
            }
            catch (Exception ex) { LogQuiet("expiry: " + ex.Message); }
            finally { ExitLock(lk); }
            return n;
        }

        /// <summary>Save-GameLog. true when the log is safe: nothing to keep, already archived (loose or in a zip) or copied now.</summary>
        public bool SaveGameLog()
        {
            string tmp = null;
            Mutex lk = null;
            try
            {
                var src = new FileInfo(LogPath);
                if (!src.Exists || src.Length <= 0) return true;
                if (IsModdedGameRunning()) { LogQuiet("log archive: skipped (Among Us is running)"); return false; }
                lk = EnterLock();
                if (lk == null) { LogQuiet("log archive: skipped (another launcher window is busy with the logs)"); return false; }
                DateTime t = src.LastWriteTime;
                string name = ArchiveName(t);
                string dest = Path.Combine(LogArchiveDir, name);
                if (File.Exists(dest)) return true;
                // already moved into a day zip (the launcher was opened again 7+ days later without playing)
                if (InZips(t, name)) return true;
                Directory.CreateDirectory(LogArchiveDir);
                // FileShare.Read: fails while the game (BepInEx) still has the log open for writing
                FileStream input;
                try { input = new FileStream(src.FullName, FileMode.Open, FileAccess.Read, FileShare.Read); }
                catch (Exception ex) { LogQuiet("log archive: skipped (LogOutput.log is locked: " + ex.GetBaseException().Message + ")"); return false; }
                tmp = dest + "." + ProcessId.ToString(Ci) + ".part";   // this process's own copy (another window never touches it)
                using (input)
                using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);
                File.SetLastWriteTime(tmp, t);
                File.Move(tmp, dest);
                tmp = null;
                Log(T("lg_saved", name));
                return true;
            }
            catch (Exception ex)
            {
                LogQuiet("log archive: " + ex.Message);
                if (tmp != null) { try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { } }
                return false;
            }
            finally { ExitLock(lk); }
        }

        /// <summary>Move-GameLogAside (ps1:1265-1283): the developer update is about to let the game overwrite
        /// LogOutput.log, and Save-GameLog could not copy it. The file is MOVED, never deleted: into the logs folder
        /// under its archive name, else renamed next to itself. When neither works it is left alone. Never throws.</summary>
        public void MoveGameLogAside()
        {
            try
            {
                var src = new FileInfo(LogPath);
                if (!src.Exists) return;
                string name = ArchiveName(src.LastWriteTime);
                foreach (var dir in new[] { LogArchiveDir, src.DirectoryName })
                {
                    try
                    {
                        if (string.IsNullOrEmpty(dir)) continue;
                        Directory.CreateDirectory(dir);
                        string dest = Path.Combine(dir, name);
                        if (File.Exists(dest)) continue;
                        File.Move(src.FullName, dest);
                        LogQuiet("log archive: LogOutput.log moved aside to " + dest);
                        return;
                    }
                    catch (Exception ex) { LogQuiet("log archive: LogOutput.log not moved to " + dir + ": " + ex.GetBaseException().Message); }
                }
                LogQuiet("log archive: LogOutput.log left in place (the game start overwrites it)");
            }
            catch (Exception ex) { LogQuiet("log archive: " + ex.Message); }
        }

        // ------------------------------------------------------------------ the logs folder's size (ps1:1399-1417)
        /// <summary>Format-Size: the launcher's own wording, so 「ログ: 123 MB」 reads the same in both programs.</summary>
        public static string FormatSize(long b)
        {
            const long KB = 1024, MB = 1024 * KB, GB = 1024 * MB;
            if (b >= GB) return ((double)b / GB).ToString("0.0", Ci) + " GB";
            if (b >= 10 * MB || b == 0) return ((double)b / MB).ToString("0", Ci) + " MB";
            if (b < MB) return ((long)Math.Ceiling((double)b / KB)).ToString(Ci) + " KB";
            return ((double)b / MB).ToString("0.0", Ci) + " MB";
        }

        /// <summary>Get-LogArchiveInfo: total size, loose logs and zips of the logs folder (sub-folders counted too).
        /// A folder that is not there, or cannot be read, is simply 0 - this only draws a line of text.</summary>
        public LogFolderInfo ArchiveInfo()
        {
            var i = new LogFolderInfo();
            try
            {
                if (!Directory.Exists(LogArchiveDir)) return i;
                foreach (var fi in new DirectoryInfo(LogArchiveDir).GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        i.Bytes += fi.Length;
                        if (fi.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) i.Zips++;
                        else if (ArchiveTime(fi.Name).HasValue) i.Logs++;
                    }
                    catch (Exception) { }   // a file that vanished between the listing and the read
                }
            }
            catch (Exception) { }
            return i;
        }

        // ------------------------------------------------------------------ Compress-OldGameLogs (ps1:1285-1380)
        static readonly Regex LeftoverTmp = new Regex(@"^logs-(\d{8}|\d{4}-\d{2})\.\d+\.tmp$", RegexOptions.IgnoreCase);

        /// <summary>$script:LogZipDone: the roll-up happens at most once while the app runs.</summary>
        static int zipDone;

        /// <summary>The self-test starts each of its cases from a fresh app.</summary>
        internal static void ResetZipOnce() { Interlocked.Exchange(ref zipDone, 0); }

        /// <summary>Compress-OldGameLogs, once per app start. Returns how many loose logs went into a zip.</summary>
        public int CompressOldLogs()
        {
            if (Interlocked.Exchange(ref zipDone, 1) != 0) return 0;
            return CompressOldLogsNow();
        }

        /// <summary>Loose archived logs older than 7 days into one zip per day, oldest first, up to
        /// <see cref="LogZipBudget"/> per app start (a log over <see cref="LogZipMaxFile"/> stays loose). Skipped while
        /// another launcher window holds the log lock - it waits 0 ms on purpose, because this is only housekeeping and
        /// must never make the app wait. Never throws.</summary>
        public int CompressOldLogsNow()
        {
            Mutex lk = null;
            int moved = 0;
            try
            {
                if (!Directory.Exists(LogArchiveDir)) return 0;
                lk = EnterLock(0);
                if (lk == null) { LogQuiet("log zip: skipped (another launcher window is busy with the logs)"); return 0; }
                // temp zips of a launcher that died half-way (none is being written while the lock is held): never the
                // only copy of a log, because a loose log is removed only after its zip has been renamed into place
                foreach (var f in Directory.GetFiles(LogArchiveDir, "logs-*.tmp"))
                    if (LeftoverTmp.IsMatch(Path.GetFileName(f))) Delete(f);
                DateTime limit = Now().AddDays(-7);
                var old = new List<string>();
                foreach (var f in Directory.GetFiles(LogArchiveDir, "LogOutput-*.log"))
                {
                    var t = ArchiveTime(Path.GetFileName(f));
                    if (!t.HasValue || t.Value >= limit) continue;
                    old.Add(f);
                }
                old.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
                var byDay = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
                long used = 0;
                int taken = 0, left = 0;
                foreach (var f in old)
                {
                    long len;
                    try { len = new FileInfo(f).Length; } catch (Exception) { continue; }
                    if (len > LogZipMaxFile) { LogQuiet("log zip: left loose (over " + FormatSize(LogZipMaxFile) + "): " + f); continue; }
                    // the first log always goes in, however big it is: otherwise a folder of large logs would never shrink
                    if (taken > 0 && used + len > LogZipBudget) { left++; continue; }
                    used += len;
                    taken++;
                    string day = ArchiveTime(Path.GetFileName(f)).Value.ToString("yyyyMMdd", Ci);
                    List<string> list;
                    if (!byDay.TryGetValue(day, out list)) byDay[day] = list = new List<string>();
                    list.Add(f);
                }
                foreach (var kv in byDay) moved += AddLogsToDayZip(kv.Key, kv.Value);
                if (moved > 0) Log(T("lg_zipped", moved));
                if (left > 0) LogQuiet("log zip: " + left.ToString(Ci) + " more log(s) wait for the next start (" + FormatSize(LogZipBudget) + " per start)");
            }
            catch (Exception ex) { LogQuiet("log zip: " + ex.Message); }
            finally { ExitLock(lk); }
            return moved;
        }

        void Delete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }

        sealed class Added
        {
            public string File, Entry;
            public long Length;
        }

        /// <summary>Add-LogsToDayZip: the logs of ONE day into a NEW zip. An existing zip is never opened for writing
        /// (rewriting one held the whole archive in memory, and a failed replace could leave no zip at all): the new one
        /// is streamed into this process's own .tmp, read back, and then renamed to the first free
        /// logs-&lt;yyyyMMdd&gt;.zip / -2 / -3 ... (File.Move never overwrites). A loose log is deleted only after that
        /// rename, and only when its entry in the zip has the length it had on disk. Returns how many were moved.</summary>
        internal int AddLogsToDayZip(string day, List<string> files)
        {
            string tmp = Path.Combine(LogArchiveDir, "logs-" + day + "." + ProcessId.ToString(Ci) + ".tmp");
            try
            {
                Delete(tmp);   // a dead process with the same id: never the only copy of a log
                var added = new List<Added>();
                using (var za = ZipFile.Open(tmp, ZipArchiveMode.Create))
                    foreach (var f in files)
                    {
                        try
                        {
                            long len = new FileInfo(f).Length;
                            string entry = Path.GetFileName(f);   // all from one folder: the names are unique
                            za.CreateEntryFromFile(f, entry, CompressionLevel.Optimal);
                            added.Add(new Added { File = f, Entry = entry, Length = len });
                        }
                        catch (Exception ex) { LogQuiet("log zip: " + f + ": " + ex.GetBaseException().Message); }
                    }
                var ok = new List<Added>();
                using (var zr = ZipFile.OpenRead(tmp))
                    foreach (var a in added)
                    {
                        var e = zr.GetEntry(a.Entry);
                        if (e != null && e.Length == a.Length) ok.Add(a);
                        else LogQuiet("log zip: entry check failed, kept " + a.File);
                    }
                if (ok.Count == 0) { Delete(tmp); return 0; }
                string zipPath = null;
                for (int n = 1; n <= 999; n++)
                {
                    string cand = Path.Combine(LogArchiveDir, "logs-" + day + (n > 1 ? "-" + n.ToString(Ci) : "") + ".zip");
                    if (File.Exists(cand)) continue;
                    try { File.Move(tmp, cand); zipPath = cand; break; }
                    catch (Exception) { if (!File.Exists(cand)) throw; }   // taken in the meantime: the next name
                }
                if (zipPath == null) { LogQuiet("log zip " + day + ": no free zip name"); Delete(tmp); return 0; }
                int moved = 0;
                foreach (var a in ok)
                {
                    try { File.Delete(a.File); moved++; }
                    catch (Exception ex) { LogQuiet("log zip: could not remove " + a.File + ": " + ex.Message); }
                }
                LogQuiet("log zip: " + moved.ToString(Ci) + " log(s) moved into " + Path.GetFileName(zipPath));
                return moved;
            }
            catch (Exception ex)
            {
                LogQuiet("log zip " + day + ": " + ex.GetBaseException().Message);
                Delete(tmp);   // only this process's new zip: the loose logs are all still there
                return 0;
            }
        }
    }
}
