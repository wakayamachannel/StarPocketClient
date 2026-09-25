// v0.4 part 1: the three things only the PowerShell launcher could still do (PORT-MAP 14.5).
//   - the logs folder's size and the 2 GB notice (Get-LogArchiveInfo / Format-Size / Update-LogSizeLabel)
//   - loose logs older than 7 days into one zip per day (Compress-OldGameLogs / Add-LogsToDayZip / Move-GameLogAside)
//   - the app's own log page (showLog) and developer mode's 「再ビルド」「更新」 (Invoke-Build / Invoke-Update)
// Everything is played inside the self-test folder: no compiler is started, no game is started, no Steam is asked, and
// the one place that could start a program (ShellOpen) is checked through its allow-list and its recorder only.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Starpocket.Client.Core;

namespace Starpocket.Client.SelfTest
{
    internal static class DevLogSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            SizeTests(r);
            DayZipTests(r);
            AsideTests(r);
            LogPageTests(r);
            BuildEntryTests(r);
            RebuildTests(r);
            DevUpdateTests(r);
            OriginTests(r);
            DevStringTests(r);
            r.Section("");
        }

        const long KB = 1024, MB = 1024 * KB, GB = 1024 * MB;

        static GameLogs NewLogs(string g, List<string> log)
        {
            return new GameLogs
            {
                LogPath = Path.Combine(g, @"BepInEx\LogOutput.log"),
                LogArchiveDir = Path.Combine(g, @"BepInEx\PocketRoles\logs"),
                Desktop = Path.Combine(g, "Desktop"),
                CacheDir = Path.Combine(g, "Cache"),
                MutexName = @"Local\StarPocketClient.SelfTest." + Guid.NewGuid().ToString("N"),
                LockWaitMs = 2000,
                IsModdedGameRunning = () => false,
                Log = s => log.Add(s),
                LogQuiet = s => log.Add("quiet: " + s),
                Lang = "en",
            };
        }

        /// <summary>A loose archived log of that age, <paramref name="bytes"/> long.</summary>
        static string Loose(GameLogs gl, DateTime when, int bytes = 8)
        {
            string p = Path.Combine(gl.LogArchiveDir, GameLogs.ArchiveName(when));
            Directory.CreateDirectory(gl.LogArchiveDir);
            File.WriteAllBytes(p, new byte[bytes]);
            return p;
        }

        // ------------------------------------------------------------------ the logs folder's size (ps1:1399-1434)
        static void SizeTests(SelfTestRunner r)
        {
            r.Section("logs folder size");
            // Format-Size, value for value as the launcher writes it
            r.Equal("nothing at all", "0 MB", GameLogs.FormatSize(0));
            r.Equal("one byte rounds up to a whole KB", "1 KB", GameLogs.FormatSize(1));
            r.Equal("exactly 1 KB", "1 KB", GameLogs.FormatSize(KB));
            r.Equal("just over 1 KB", "2 KB", GameLogs.FormatSize(KB + 1));
            r.Equal("just under 1 MB stays in KB", "1024 KB", GameLogs.FormatSize(MB - 1));
            r.Equal("1 MB", "1.0 MB", GameLogs.FormatSize(MB));
            r.Equal("under 10 MB keeps one decimal", "9.5 MB", GameLogs.FormatSize((long)(9.5 * MB)));
            r.Equal("from 10 MB the decimal goes", "10 MB", GameLogs.FormatSize(10 * MB));
            r.Equal("123 MB", "123 MB", GameLogs.FormatSize(123 * MB));
            r.Equal("1 GB", "1.0 GB", GameLogs.FormatSize(GB));
            r.Equal("2.5 GB", "2.5 GB", GameLogs.FormatSize((long)(2.5 * GB)));
            r.Check("the same words whatever the PC's number format", GameLogs.FormatSize(GB).Contains("."));

            r.Test("what is in the folder", () =>
            {
                string g = r.NewDir("logsize");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                var none = gl.ArchiveInfo();
                r.Check("no folder yet: 0, and no folder is made for a look", none.Bytes == 0 && none.Logs == 0 && none.Zips == 0 && !Directory.Exists(gl.LogArchiveDir));
                r.Check("... and that reads as 0 MB, not as a warning", none.Size == "0 MB" && !none.Big);
                var now = DateTime.Now;
                Loose(gl, now.AddDays(-1), 1000);
                Loose(gl, now.AddDays(-2), 2000);
                File.WriteAllBytes(Path.Combine(gl.LogArchiveDir, "logs-20260101.zip"), new byte[500]);
                File.WriteAllBytes(Path.Combine(gl.LogArchiveDir, "readme.txt"), new byte[7]);
                Directory.CreateDirectory(Path.Combine(gl.LogArchiveDir, "old"));
                File.WriteAllBytes(Path.Combine(gl.LogArchiveDir, @"old\logs-20250101.zip"), new byte[11]);
                var i = gl.ArchiveInfo();
                r.Equal("every byte counts, sub-folders too", 1000 + 2000 + 500 + 7 + 11L, i.Bytes);
                r.Equal("loose logs counted", 2, i.Logs);
                r.Equal("zips counted, wherever they are", 2, i.Zips);
                r.Check("a file that is neither is only bytes", i.Logs + i.Zips == 4);
                r.Check("a folder of a few KB is not big", !i.Big);
            });

            r.Test("the 2 GB line", () =>
            {
                r.Check("exactly 2 GB is not over 2 GB", !new LogFolderInfo { Bytes = GameLogs.BigFolder }.Big);
                r.Check("one byte more is", new LogFolderInfo { Bytes = GameLogs.BigFolder + 1 }.Big);
                foreach (var l in Lang.Codes)
                {
                    r.Check("「ログ: 123 MB」 in " + l, S.T(l, "lg_size", "123 MB").Contains("123 MB"), S.T(l, "lg_size", "123 MB"));
                    r.Check("the 2 GB notice names the size in " + l, S.T(l, "lg_big", "2.1 GB").Contains("2.1 GB"), S.T(l, "lg_big", "2.1 GB"));
                    r.Check("the plain tooltip says nothing about 2 GB in " + l, S.T(l, "lg_tip").Length > 20 && !S.T(l, "lg_tip").Contains("2 GB"));
                }
            });
        }

        // ------------------------------------------------------------------ day zips (ps1:1285-1380)
        static void DayZipTests(SelfTestRunner r)
        {
            r.Section("day zips");
            r.Test("nothing to do", () =>
            {
                string g = r.NewDir("zipnone");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                r.Equal("no logs folder at all: 0", 0, gl.CompressOldLogsNow());
                r.Check("... and no folder is made for it", !Directory.Exists(gl.LogArchiveDir));
                Directory.CreateDirectory(gl.LogArchiveDir);
                r.Equal("an empty folder: 0", 0, gl.CompressOldLogsNow());
                r.Check("... and no zip appears", Directory.GetFiles(gl.LogArchiveDir).Length == 0);
                var now = DateTime.Now;
                string young = Loose(gl, now.AddDays(-6).AddHours(-23));
                r.Equal("a log 6 days old stays loose", 0, gl.CompressOldLogsNow());
                r.Check("... it is still there and there is no zip", File.Exists(young) && Directory.GetFiles(gl.LogArchiveDir, "*.zip").Length == 0);
                r.Check("... and nothing was said to the viewer", !log.Any(l => l.Contains("daily zips")), string.Join(" / ", log));
            });

            r.Test("one zip per day", () =>
            {
                string g = r.NewDir("zipdays");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                gl.Now = () => now;
                string a1 = Loose(gl, new DateTime(2026, 9, 10, 9, 0, 0), 100);
                string a2 = Loose(gl, new DateTime(2026, 9, 10, 21, 30, 0), 100);
                string b1 = Loose(gl, new DateTime(2026, 9, 11, 8, 0, 0), 100);
                string keep = Loose(gl, new DateTime(2026, 9, 20, 8, 0, 0), 100);   // 3 days old
                r.Equal("three logs of two days move", 3, gl.CompressOldLogsNow());
                string z1 = Path.Combine(gl.LogArchiveDir, "logs-20260910.zip");
                string z2 = Path.Combine(gl.LogArchiveDir, "logs-20260911.zip");
                r.Check("one zip per day, named by the day", File.Exists(z1) && File.Exists(z2));
                r.Check("the loose logs are gone", !File.Exists(a1) && !File.Exists(a2) && !File.Exists(b1));
                r.Check("the log 3 days old is untouched", File.Exists(keep));
                using (var za = ZipFile.OpenRead(z1))
                {
                    r.Equal("both of that day are in it", 2, za.Entries.Count);
                    r.Check("under their own names, with no folder in front",
                        za.Entries.All(e => e.FullName == Path.GetFileName(e.FullName) && e.FullName.StartsWith("LogOutput-2026-09-10_", StringComparison.Ordinal)),
                        string.Join(",", za.Entries.Select(e => e.FullName).ToArray()));
                    r.Check("and they still hold what they held", za.Entries.All(e => e.Length == 100));
                }
                r.Check("lg_zipped said once, with the count", log.Contains("Moved 3 log(s) older than 7 days into daily zips"), string.Join(" / ", log));
                r.Check("no .tmp left behind", Directory.GetFiles(gl.LogArchiveDir, "*.tmp").Length == 0);
                // a zip of a day that is gone 30 days after that day, exactly as the loose logs were: nothing is kept
                // for longer or shorter because it was zipped
                r.Check("a day zip made now is not due", !GameLogs.ArchiveExpired("logs-20260910.zip", now, now));
                r.Check("... and is due 30 days after its day", GameLogs.ArchiveExpired("logs-20260910.zip", now, new DateTime(2026, 10, 11, 12, 0, 0)));
            });

            r.Test("a zip of that day is already there", () =>
            {
                string g = r.NewDir("ziptaken");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                gl.Now = () => now;
                string first = Path.Combine(gl.LogArchiveDir, "logs-20260910.zip");
                Directory.CreateDirectory(gl.LogArchiveDir);
                using (var za = ZipFile.Open(first, ZipArchiveMode.Create)) za.CreateEntry("LogOutput-2026-09-10_000000.log");
                long was = new FileInfo(first).Length;
                string a1 = Loose(gl, new DateTime(2026, 9, 10, 9, 0, 0), 100);
                r.Equal("the log still moves", 1, gl.CompressOldLogsNow());
                string second = Path.Combine(gl.LogArchiveDir, "logs-20260910-2.zip");
                r.Check("into a new -2 zip", File.Exists(second) && !File.Exists(a1));
                r.Check("the zip that was there is not touched at all", new FileInfo(first).Length == was);
                using (var za = ZipFile.OpenRead(first)) r.Equal("... and still holds only what it held", 1, za.Entries.Count);
                // and a third time
                string a2 = Loose(gl, new DateTime(2026, 9, 10, 10, 0, 0), 100);
                gl.CompressOldLogsNow();
                r.Check("and a third goes to -3", File.Exists(Path.Combine(gl.LogArchiveDir, "logs-20260910-3.zip")) && !File.Exists(a2));
            });

            r.Test("a log that cannot be read is kept, and the rest still go", () =>
            {
                string g = r.NewDir("ziplocked");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                gl.Now = () => now;
                string locked = Loose(gl, new DateTime(2026, 9, 10, 9, 0, 0), 100);
                string free = Loose(gl, new DateTime(2026, 9, 10, 10, 0, 0), 100);
                using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    r.Equal("only the one that could be read moved", 1, gl.CompressOldLogsNow());
                    r.Check("the locked log is still on disk", File.Exists(locked));
                    r.Check("the other one is in the zip and gone from disk", !File.Exists(free) && File.Exists(Path.Combine(gl.LogArchiveDir, "logs-20260910.zip")));
                }
                r.Check("and the reason was written down quietly", log.Any(l => l.StartsWith("quiet: log zip: ", StringComparison.Ordinal) && l.Contains(locked)), string.Join(" / ", log));
            });

            r.Test("the zip cannot be written (a full disk)", () =>
            {
                string g = r.NewDir("zipfull");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                gl.Now = () => now;
                string a1 = Loose(gl, new DateTime(2026, 9, 10, 9, 0, 0), 100);
                // the name the new zip would be streamed into is a folder: creating the file can only fail, the way it
                // fails when the disk is full or the folder is read-only
                string tmp = Path.Combine(gl.LogArchiveDir, "logs-20260910." + Process.GetCurrentProcess().Id.ToString() + ".tmp");
                Directory.CreateDirectory(tmp);
                r.Equal("nothing is reported as moved", 0, gl.CompressOldLogsNow());
                r.Check("the log is still there - a log is never the price of a failed zip", File.Exists(a1));
                r.Check("no zip was left half-made", Directory.GetFiles(gl.LogArchiveDir, "*.zip").Length == 0);
                r.Check("and the reason is in the log", log.Any(l => l.Contains("log zip 20260910: ")), string.Join(" / ", log));
                Directory.Delete(tmp);
            });

            r.Test("leftovers of a launcher that died half-way", () =>
            {
                string g = r.NewDir("ziptmp");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                gl.Now = () => now;
                Directory.CreateDirectory(gl.LogArchiveDir);
                string dead = SelfTestRunner.Touch(Path.Combine(gl.LogArchiveDir, "logs-20260910.4242.tmp"), "half a zip");
                string deadMonth = SelfTestRunner.Touch(Path.Combine(gl.LogArchiveDir, "logs-2026-09.4242.tmp"), "half a zip");
                string notOurs = SelfTestRunner.Touch(Path.Combine(gl.LogArchiveDir, "logs-notes.tmp"), "somebody else's");
                string alsoNot = SelfTestRunner.Touch(Path.Combine(gl.LogArchiveDir, "logs-20260910.tmp"), "no process number");
                gl.CompressOldLogsNow();
                r.Check("a half-written zip of a dead run goes", !File.Exists(dead) && !File.Exists(deadMonth));
                r.Check("a .tmp that is not one of ours stays", File.Exists(notOurs) && File.Exists(alsoNot));
            });

            r.Test("how much is done per start", () =>
            {
                string g = r.NewDir("zipbudget");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                gl.Now = () => now;
                gl.LogZipBudget = 250;
                gl.LogZipMaxFile = 400;
                string huge = Loose(gl, new DateTime(2026, 9, 1, 9, 0, 0), 500);    // over the per-file limit
                string one = Loose(gl, new DateTime(2026, 9, 2, 9, 0, 0), 200);
                string two = Loose(gl, new DateTime(2026, 9, 3, 9, 0, 0), 200);
                r.Equal("the budget stops after the first", 1, gl.CompressOldLogsNow());
                r.Check("the one that fits is zipped", !File.Exists(one) && File.Exists(Path.Combine(gl.LogArchiveDir, "logs-20260902.zip")));
                r.Check("the next one waits for the next start", File.Exists(two));
                r.Check("a log over the per-file limit is never zipped", File.Exists(huge));
                r.Check("both reasons are written down quietly",
                    log.Any(l => l.Contains("left loose (over")) && log.Any(l => l.Contains("wait for the next start")), string.Join(" / ", log));
                r.Equal("the next start takes the next one", 1, gl.CompressOldLogsNow());
                r.Check("... and it is gone from disk", !File.Exists(two));
                r.Check("the too-big one is still never taken", File.Exists(huge));
            });

            r.Test("the first log always fits", () =>
            {
                string g = r.NewDir("zipfirst");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                gl.Now = () => new DateTime(2026, 9, 23, 12, 0, 0);
                gl.LogZipBudget = 10;
                string big = Loose(gl, new DateTime(2026, 9, 1, 9, 0, 0), 300);
                r.Equal("one over the budget still goes, or the folder could never shrink", 1, gl.CompressOldLogsNow());
                r.Check("... and it is in a zip now", !File.Exists(big) && File.Exists(Path.Combine(gl.LogArchiveDir, "logs-20260901.zip")));
            });

            r.Test("another launcher window is busy with the logs", () =>
            {
                string g = r.NewDir("ziplock");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                gl.Now = () => new DateTime(2026, 9, 23, 12, 0, 0);
                string a1 = Loose(gl, new DateTime(2026, 9, 10, 9, 0, 0), 100);
                using (var held = new Mutex(false, gl.MutexName))
                {
                    var holder = new Thread(() => { held.WaitOne(); Thread.Sleep(600); held.ReleaseMutex(); });
                    holder.Start();
                    Thread.Sleep(100);
                    int moved = gl.CompressOldLogsNow();
                    holder.Join();
                    r.Equal("it is skipped rather than waited for", 0, moved);
                    r.Check("nothing was zipped and nothing was lost", File.Exists(a1) && Directory.GetFiles(gl.LogArchiveDir, "*.zip").Length == 0);
                    r.Check("and it says why, quietly", log.Any(l => l.Contains("log zip: skipped")), string.Join(" / ", log));
                }
            });

            r.Test("once per app start", () =>
            {
                string g = r.NewDir("ziponce");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                gl.Now = () => new DateTime(2026, 9, 23, 12, 0, 0);
                GameLogs.ResetZipOnce();
                Loose(gl, new DateTime(2026, 9, 1, 9, 0, 0), 100);
                r.Equal("the first time it runs", 1, gl.CompressOldLogs());
                Loose(gl, new DateTime(2026, 9, 2, 9, 0, 0), 100);
                r.Equal("the second time it does nothing", 0, gl.CompressOldLogs());
                r.Check("... and that log is still loose", File.Exists(Path.Combine(gl.LogArchiveDir, GameLogs.ArchiveName(new DateTime(2026, 9, 2, 9, 0, 0)))));
                GameLogs.ResetZipOnce();
            });
        }

        // ------------------------------------------------------------------ Move-GameLogAside (ps1:1265-1283)
        static void AsideTests(SelfTestRunner r)
        {
            r.Section("log moved aside");
            r.Test("before the game overwrites it", () =>
            {
                string g = r.NewDir("aside");
                var log = new List<string>();
                var gl = NewLogs(g, log);
                gl.MoveGameLogAside();
                r.Check("no log: nothing happens, and no folder is made", !Directory.Exists(gl.LogArchiveDir));
                var t = new DateTime(2026, 9, 20, 21, 4, 5);
                SelfTestRunner.Touch(gl.LogPath, "one");
                File.SetLastWriteTime(gl.LogPath, t);
                gl.MoveGameLogAside();
                string inArchive = Path.Combine(gl.LogArchiveDir, GameLogs.ArchiveName(t));
                r.Check("it is MOVED into the logs folder, never deleted", File.Exists(inArchive) && !File.Exists(gl.LogPath));
                r.Equal("with everything it held", "one", File.ReadAllText(inArchive));

                // the name in the logs folder is taken: it is renamed where it lies instead
                SelfTestRunner.Touch(gl.LogPath, "two");
                File.SetLastWriteTime(gl.LogPath, t);
                gl.MoveGameLogAside();
                string beside = Path.Combine(Path.GetDirectoryName(gl.LogPath), GameLogs.ArchiveName(t));
                r.Check("the second one is put beside itself", File.Exists(beside) && !File.Exists(gl.LogPath));
                r.Equal("the first is still the first", "one", File.ReadAllText(inArchive));

                // both names are taken: it stays where it is and says so
                SelfTestRunner.Touch(gl.LogPath, "three");
                File.SetLastWriteTime(gl.LogPath, t);
                gl.MoveGameLogAside();
                r.Check("the third is left alone rather than lost", File.Exists(gl.LogPath) && File.ReadAllText(gl.LogPath) == "three");
                r.Check("and it says so quietly", log.Any(l => l.Contains("left in place")), string.Join(" / ", log));
            });
        }

        // ------------------------------------------------------------------ the log page (showLog)
        static void LogPageTests(SelfTestRunner r)
        {
            r.Section("log page");
            r.Test("what it shows", () =>
            {
                string d = r.NewDir("logpage");
                string p = Path.Combine(d, "client.log");
                var empty = new ClientLog(p).Tail();
                r.Check("no log yet: no lines, nothing left out", empty.Lines.Length == 0 && !empty.Truncated && empty.Bytes == 0);
                r.Check("a log that writes nowhere is the same", ClientLog.None.Tail().Lines.Length == 0);

                var cl = new ClientLog(p);
                cl.Write("first");
                cl.Write("second");
                cl.Write("third");
                var t = cl.Tail();
                r.Equal("every line is there", 3, t.Lines.Length);
                r.Check("newest last, as the launcher's log box had it", t.Lines[2].EndsWith("third", StringComparison.Ordinal), string.Join(" | ", t.Lines));
                r.Check("with the time the launcher writes", System.Text.RegularExpressions.Regex.IsMatch(t.Lines[0], @"^\[\d\d:\d\d:\d\d\] first$"), t.Lines[0]);
                r.Check("nothing was left out", !t.Truncated);
                r.Check("no stray mark from the file's first bytes", t.Lines[0][0] == '[', ((int)t.Lines[0][0]).ToString());

                var few = cl.Tail(2, 256 * 1024);
                r.Equal("a page shows the newest N", 2, few.Lines.Length);
                r.Check("... which are the last ones", few.Lines[1].EndsWith("third", StringComparison.Ordinal) && few.Lines[0].EndsWith("second", StringComparison.Ordinal));
                r.Check("... and says the older ones were left out", few.Truncated);
            });

            r.Test("a log too big to show", () =>
            {
                string d = r.NewDir("logbig");
                string p = Path.Combine(d, "client.log");
                var sb = new StringBuilder();
                for (int i = 0; i < 4000; i++) sb.Append("[12:00:00] line ").Append(i).Append("\r\n");
                File.WriteAllText(p, sb.ToString(), Encoding.UTF8);
                var t = new ClientLog(p).Tail(400, 4096);
                r.Check("only the end of the file is read", t.Lines.Length > 0 && t.Lines.Length <= 400);
                r.Check("and it is the end", t.Lines[t.Lines.Length - 1] == "[12:00:00] line 3999", t.Lines[t.Lines.Length - 1]);
                r.Check("the half line the read started in the middle of is dropped",
                    t.Lines.All(l => l.StartsWith("[12:00:00] line ", StringComparison.Ordinal)), t.Lines[0]);
                r.Check("it says older lines were left out", t.Truncated);
                r.Equal("and how long the file really is", new FileInfo(p).Length, t.Bytes);
            });

            r.Test("read while the app is still writing", () =>
            {
                string d = r.NewDir("logopen");
                string p = Path.Combine(d, "client.log");
                var cl = new ClientLog(p);
                cl.Write("before");
                using (new FileStream(p, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                {
                    var t = cl.Tail();
                    r.Check("a log another writer holds open can still be read", t.Lines.Length == 1, t.Lines.Length.ToString());
                }
            });

            r.Test("the words of the page", () =>
            {
                foreach (var l in Lang.Codes)
                {
                    r.Check("a title in " + l, S.T(l, "lg_log_title").Length > 1 && S.T(l, "lg_log_title") != "lg_log_title");
                    r.Check("what it is, in " + l, S.T(l, "lg_log_sub").Length > 20 && S.T(l, "lg_log_sub") != "lg_log_sub");
                    r.Check("an empty log says so in " + l, S.T(l, "lg_log_none").Length > 2 && S.T(l, "lg_log_none") != "lg_log_none");
                    r.Check("and \"older lines left out\" in " + l, S.T(l, "lg_log_more").Length > 10 && S.T(l, "lg_log_more") != "lg_log_more");
                }
            });

            r.Test("the Windows account name never reaches the page", () =>
            {
                string home = @"C:\Users\someone";
                r.Equal("in a path", @"%USERPROFILE%\AppData\Local\StarPocket\Client\client.log",
                    Mask.Home(home + @"\AppData\Local\StarPocket\Client\client.log", home));
                r.Equal("and in a line", "[12:00:00] shortcut: %USERPROFILE%\\Desktop\\a.lnk",
                    Mask.Home(@"[12:00:00] shortcut: C:\Users\someone\Desktop\a.lnk", home));
                r.Equal("whatever the capitals", "%USERPROFILE%\\x", Mask.Home(@"c:\users\SOMEONE\x", home));
            });
        }

        // ------------------------------------------------------------------ the one entry that may start a compiler
        static void BuildEntryTests(SelfTestRunner r)
        {
            r.Section("the compiler entry");
            r.Check("the SDK's own dotnet.exe", ShellOpen.Allowed(OpenKind.Build, @"C:\Users\x\.dotnet\dotnet.exe"));
            r.Check("any capitals of that name", ShellOpen.Allowed(OpenKind.Build, @"C:\Program Files\dotnet\DOTNET.EXE"));
            r.Check("a relative path is refused", !ShellOpen.Allowed(OpenKind.Build, @".dotnet\dotnet.exe"));
            r.Check("a bare name is refused", !ShellOpen.Allowed(OpenKind.Build, "dotnet"));
            r.Check("a wrapper of that name is refused", !ShellOpen.Allowed(OpenKind.Build, @"C:\x\dotnet.cmd") && !ShellOpen.Allowed(OpenKind.Build, @"C:\x\dotnet.bat"));
            r.Check("another program is refused", !ShellOpen.Allowed(OpenKind.Build, @"C:\Windows\System32\anything.exe"));
            r.Check("nothing at all is refused", !ShellOpen.Allowed(OpenKind.Build, null) && !ShellOpen.Allowed(OpenKind.Build, ""));
            r.Check("and the game entry does not take a compiler", !ShellOpen.Allowed(OpenKind.Game, @"C:\x\dotnet.exe"));

            r.Test("what it would start", () =>
            {
                var seen = new List<ProcessStartInfo>();
                ShellOpen.Recorder = (kind, psi) => { if (kind == OpenKind.Build) seen.Add(psi); };
                try
                {
                    var refused = ShellOpen.RunBuild(@"C:\Windows\System32\cmd_of_some_kind.exe", @"C:\src");
                    r.Check("a program that is not dotnet.exe is refused, and nothing is started", refused.Error != null && seen.Count == 0, refused.Error);
                    var ok = ShellOpen.RunBuild(@"C:\Users\x\.dotnet\dotnet.exe", @"C:\src");
                    r.Check("the SDK is handed over", ok.Error == null && seen.Count == 1);
                    if (seen.Count != 1) return;
                    var psi = seen[0];
                    r.Equal("with the three words this app may ever pass", "build -c Release", psi.Arguments);
                    r.Check("no shell: UseShellExecute is off", !psi.UseShellExecute);
                    r.Check("no console window opens", psi.CreateNoWindow);
                    r.Check("what it prints comes back through pipes", psi.RedirectStandardOutput && psi.RedirectStandardError);
                    r.Equal("in the folder with the project", @"C:\src", psi.WorkingDirectory);
                    r.Equal("DOTNET_ROOT is set on the CHILD", @"C:\Users\x\.dotnet", psi.EnvironmentVariables["DOTNET_ROOT"]);
                    r.Check("... and the SDK is first on the child's PATH", (psi.EnvironmentVariables["PATH"] ?? "").StartsWith(@"C:\Users\x\.dotnet;", StringComparison.Ordinal));
                    r.Check("... while this app's own environment is untouched",
                        Environment.GetEnvironmentVariable("DOTNET_ROOT") == null || Environment.GetEnvironmentVariable("DOTNET_ROOT") != @"C:\Users\x\.dotnet");
                }
                finally { ShellOpen.Recorder = null; }
            });

            r.Check("the compiler is looked for where the launcher looks", DevBuild.DefaultDotnet().EndsWith(@".dotnet\dotnet.exe", StringComparison.OrdinalIgnoreCase), DevBuild.DefaultDotnet());
            r.Check("and its output goes to the launcher's own file", DevBuild.DefaultBuildLog().EndsWith(@"\pocketroles-build.log", StringComparison.OrdinalIgnoreCase), DevBuild.DefaultBuildLog());
        }

        // ------------------------------------------------------------------ 「再ビルド」 (Invoke-Build)
        sealed class Fake
        {
            public readonly List<string> Log = new List<string>();
            public readonly List<string> Calls = new List<string>();
            public BuildRun Run = new BuildRun { ExitCode = 0 };
            public bool WriteDllOnBuild = true;
            public string Dll;
            public DevBuild Dev;
            public LauncherStateFile State;
            public DateTime Clock = new DateTime(2026, 9, 23, 12, 0, 0);
        }

        static Fake NewFake(SelfTestRunner r, string name, bool devUpdate = false)
        {
            string root = r.NewDir(name);
            string modded = Path.Combine(root, "Among Us PocketRoles");
            string src = Path.Combine(root, "src");
            string state = Path.Combine(src, "launcher-state.json");
            Directory.CreateDirectory(src);
            var f = new Fake();
            var paths = ModPaths.For(modded);
            f.Dll = paths.DllPath;
            f.State = LauncherStateFile.Load(state);
            f.State.Log = s => f.Log.Add(s);
            var installer = new Installer
            {
                Paths = paths,
                Src = src,
                AegisStateDir = Path.Combine(root, "AegisState"),
                OriginDir = Path.Combine(root, "data"),
                State = f.State,
                Lang = () => "en",
                Log = s => f.Log.Add(s),
                GameRunning = () => false,
            };
            f.Dev = new DevBuild
            {
                Paths = paths,
                Src = src,
                AegisStateDir = installer.AegisStateDir,
                OriginDir = installer.OriginDir,
                State = f.State,
                Lang = () => "en",
                Log = s => f.Log.Add(s),
                DotnetPath = SelfTestRunner.Touch(Path.Combine(root, @"dot\dotnet.exe"), "not really a compiler"),
                BuildLogPath = Path.Combine(root, "pocketroles-build.log"),
                Installer = installer,
                GameRunning = () => false,
                SteamRunning = () => true,
                SteamDir = () => null,
                Now = () => f.Clock,
                Sleep = ms => { f.Clock = f.Clock.AddMilliseconds(ms); },
                InteropPollMs = 500,
                InteropGraceMs = 5000,
                NewGameLogs = () => null,
                RunBuild = (exe, dir) =>
                {
                    f.Calls.Add("build " + Path.GetFileName(exe) + " in " + Path.GetFileName(dir));
                    if (f.WriteDllOnBuild) SelfTestRunner.Touch(paths.DllPath, "the mod");
                    return f.Run;
                },
            };
            if (devUpdate) SelfTestRunner.Touch(Path.Combine(modded, "Among Us.exe"), "game");
            return f;
        }

        static void RebuildTests(SelfTestRunner r)
        {
            r.Section("rebuild");
            r.Test("no .NET SDK", () =>
            {
                var f = NewFake(r, "build-nosdk");
                f.Dev.DotnetPath = @"C:\nowhere\dotnet.exe";
                var o = f.Dev.Rebuild();
                r.Check("it refuses instead of trying", !o.Ok && f.Calls.Count == 0);
                r.Check("and names the file it looked for", f.Log.Any(l => l.Contains(@"C:\nowhere\dotnet.exe")), string.Join(" / ", f.Log));
            });

            r.Test("it works", () =>
            {
                var f = NewFake(r, "build-ok");
                SelfTestRunner.Touch(Path.Combine(f.Dev.Paths.Modded, @"Among Us_Data\globalgamemanagers"), "x 2026.8.18 x");
                f.Run = new BuildRun { ExitCode = 0, Output = "Build succeeded.\r\n" };
                var o = f.Dev.Rebuild();
                r.Check("the answer is yes", o.Ok, o.Error);
                r.Equal("the compiler was called once, in the project's folder", "build dotnet.exe in src", string.Join(" | ", f.Calls));
                r.Check("it says so in the launcher's words", f.Log.Any(l => l.Contains("BepInEx\\plugins")), string.Join(" / ", f.Log));
                r.Check("Aegis is told which DLL this is", File.Exists(Path.Combine(f.Dev.AegisStateDir, "mod-fingerprint.txt")));
                var origin = ModOriginFile.Read(f.Dev.OriginDir, f.Dll);
                r.Check("and it is written down as the author's own build (v1.1)", origin != null && origin.Kind == ModOrigin.Dev && origin.At == "2026-09-23 12:00:00", origin == null ? "null" : origin.Kind + "|" + origin.At);
                var again = LauncherStateFile.Load(f.State.Path);
                r.Equal("and the game version it was built for is written down", "2026.8.18", again.LastBuiltGameVersion);
                r.Check("what the compiler printed is kept for the author", File.Exists(f.Dev.BuildLogPath) && File.ReadAllText(f.Dev.BuildLogPath).Contains("Build succeeded"));
            });

            r.Test("it fails", () =>
            {
                var f = NewFake(r, "build-bad");
                f.WriteDllOnBuild = false;
                f.Run = new BuildRun
                {
                    ExitCode = 1,
                    Output = "a.cs(1,1): error CS1002: ; expected\r\n"
                        + "a.cs(1,1): error CS1002: ; expected\r\n"
                        + "b.cs(2,2): エラー CS0103: 名前がありません\r\n"
                        + "MSBuild : error MSB4018: boom\r\n"
                        + "  0 Warning(s)\r\n",
                };
                var o = f.Dev.Rebuild();
                r.Check("the answer is no", !o.Ok);
                r.Check("with the exit code", f.Log.Any(l => l.Contains("exit 1")), string.Join(" / ", f.Log));
                r.Check("the error lines are shown", f.Log.Any(l => l.Contains("error CS1002")) && f.Log.Any(l => l.Contains("エラー CS0103")) && f.Log.Any(l => l.Contains("error MSB4018")));
                r.Equal("the same error is not repeated", 1, f.Log.Count(l => l.Contains("error CS1002")));
                r.Check("a line that is not an error is not shown", !f.Log.Any(l => l.Contains("0 Warning(s)")));
                r.Check("and it says what to do next", f.Log.Any(l => l.Contains("Claude")), string.Join(" / ", f.Log));
                r.Check("nothing was written into launcher-state.json", LauncherStateFile.Load(f.State.Path).LastBuiltGameVersion == null);
                r.Check("and Aegis was not told about a DLL that is not there", !File.Exists(Path.Combine(f.Dev.AegisStateDir, "mod-fingerprint.txt")));
            });

            r.Test("the compiler said 0 but there is no DLL", () =>
            {
                var f = NewFake(r, "build-nodll");
                f.WriteDllOnBuild = false;
                f.Run = new BuildRun { ExitCode = 0, Output = "" };
                r.Check("that is a failure too, as in the launcher", !f.Dev.Rebuild().Ok);
                r.Check("and nothing was recorded as built", LauncherStateFile.Load(f.State.Path).LastBuiltGameVersion == null);
            });

            r.Test("the compiler could not be started at all", () =>
            {
                var f = NewFake(r, "build-nostart");
                f.WriteDllOnBuild = false;
                f.Run = new BuildRun { ExitCode = -1, Error = "the file is not a program" };
                r.Check("it is a failure with a reason", !f.Dev.Rebuild().Ok && f.Log.Any(l => l.Contains("not a program")), string.Join(" / ", f.Log));
            });

            // v0.4 review: ShellOpen waited for the compiler with no time limit at all, so a compiler that stopped
            // making progress held the window for ever. Now it is closed and the reason is said in words.
            r.Test("the compiler was still going after its 20 minutes", () =>
            {
                var f = NewFake(r, "build-timeout");
                f.WriteDllOnBuild = false;
                f.Run = new BuildRun { ExitCode = -1, TimedOut = true, Output = "" };
                var o = f.Dev.Rebuild();
                r.Check("it is a failure", !o.Ok);
                r.Check("the reason given is the time, not a compiler error", (o.Error ?? "").Contains("20 minutes"), o.Error);
                r.Check("and the log says the same", f.Log.Any(l => l.Contains("20 minutes")), string.Join(" / ", f.Log));
                r.Check("nothing was recorded as built", LauncherStateFile.Load(f.State.Path).LastBuiltGameVersion == null);
                r.Equal("20 minutes is what the app waits", 20, ShellOpen.BuildTimeoutMs / 60000);
                foreach (var lang in new[] { "ja", "zh-CN", "en" })
                    r.Check("the words are there in " + lang, !string.IsNullOrEmpty(S.T(lang, "dev_build_timeout", 20)) && S.T(lang, "dev_build_timeout", 20).Contains("20"));
            });

            r.Test("which lines are shown", () =>
            {
                string many = string.Join("\r\n", Enumerable.Range(0, 20).Select(i => "x.cs(1,1): error CS100" + i + ": no").ToArray());
                r.Equal("at most eight", 8, DevBuild.ErrorLines(many, 8).Count);
                r.Equal("nothing at all is no lines", 0, DevBuild.ErrorLines(null, 8).Count);
                r.Equal("a build with no errors is no lines", 0, DevBuild.ErrorLines("Build succeeded.\r\n", 8).Count);
                r.Equal("all four spellings are caught", 4, DevBuild.ErrorLines("a error CS1\r\nb error MSB2\r\nc エラー CS3\r\nd エラー MSB4\r\n", 8).Count);
            });
        }

        // ------------------------------------------------------------------ 「更新」 (Invoke-Update)
        sealed class FakeRun : IGameRun
        {
            public bool Exited;
            public bool Stopped, Disposed;
            /// <summary>Called on every look, so a test can let the game write its files part-way through the wait.</summary>
            public Action OnPoll;
            public int Polls;
            public bool HasExited { get { Polls++; if (OnPoll != null) OnPoll(); return Exited; } }
            public void Stop() { Stopped = true; Exited = true; }
            public void Dispose() { Disposed = true; }
        }

        static void DevUpdateTests(SelfTestRunner r)
        {
            r.Section("developer update");
            r.Test("what it refuses", () =>
            {
                var f = NewFake(r, "up-refuse", true);
                f.Dev.GameRunning = () => true;
                r.Check("not while Among Us runs", !f.Dev.DevUpdate().Ok);
                f.Dev.GameRunning = () => false;
                var noSteam = f.Dev.DevUpdate();
                r.Check("not without Steam's own copy", !noSteam.Ok && noSteam.NeedGame);
                string steam = r.NewDir("up-refuse-steam");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"), "game");
                f.Dev.SteamDir = () => steam;
                f.Dev.SteamRunning = () => false;
                var noClient = f.Dev.DevUpdate();
                r.Check("and not while Steam itself is closed (the interop run needs it)", !noClient.Ok && !noClient.NeedGame);
                r.Check("... and it says why", f.Log.Any(l => l.Contains("Start Steam before updating")), string.Join(" / ", f.Log));
                r.Check("nothing was compiled for any of them", f.Calls.Count == 0);
            });

            r.Test("all four steps", () =>
            {
                var f = NewFake(r, "up-ok", true);
                string steam = r.NewDir("up-ok-steam");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"), "game");
                SelfTestRunner.Touch(Path.Combine(steam, @"Among Us_Data\globalgamemanagers"), "x 2026.8.18 x");
                f.Dev.SteamDir = () => steam;
                // the old interop and cache, which [2/4] must throw away
                SelfTestRunner.Touch(Path.Combine(f.Dev.Paths.Modded, @"BepInEx\interop\Assembly-CSharp.dll"), "old");
                SelfTestRunner.Touch(Path.Combine(f.Dev.Paths.Modded, @"BepInEx\cache\x.dat"), "old");
                string interop = Path.Combine(f.Dev.Paths.Modded, @"BepInEx\interop\Assembly-CSharp.dll");
                var run = new FakeRun();
                int look = 0;
                run.OnPoll = () =>
                {
                    // the third time this app looks, BepInEx has written the interop and said so in the log
                    if (++look != 3) return;
                    SelfTestRunner.Touch(interop, "new");
                    SelfTestRunner.Touch(f.Dev.Paths.LogPath, "[Info] Chainloader startup complete\r\n");
                };
                f.Dev.StartInterop = () => run;
                var o = f.Dev.DevUpdate();
                r.Check("it ends well", o.Ok, o.Error);
                r.Check("the old interop was thrown away first", !Directory.Exists(Path.Combine(f.Dev.Paths.Modded, @"BepInEx\cache")));
                r.Check("the game copy has Steam's files now", File.Exists(Path.Combine(f.Dev.Paths.Modded, @"Among Us_Data\globalgamemanagers")));
                r.Check("the interop is there", File.Exists(interop));
                r.Check("the game this app started was closed again", run.Stopped && run.Disposed);
                r.Equal("and the mod was rebuilt once", 1, f.Calls.Count);
                r.Check("the four steps were said in order",
                    f.Log.Any(l => l.StartsWith("[1/4]", StringComparison.Ordinal)) && f.Log.Any(l => l.StartsWith("[2/4]", StringComparison.Ordinal))
                    && f.Log.Any(l => l.StartsWith("[3/4]", StringComparison.Ordinal)) && f.Log.Any(l => l.StartsWith("[4/4]", StringComparison.Ordinal)),
                    string.Join(" / ", f.Log));
            });

            r.Test("the interop never comes", () =>
            {
                var f = NewFake(r, "up-nointerop", true);
                string steam = r.NewDir("up-nointerop-steam");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"), "game");
                f.Dev.SteamDir = () => steam;
                var run = new FakeRun();
                f.Dev.StartInterop = () => run;
                f.Dev.InteropTimeoutSec = 5;   // the fake clock moves 500 ms per look
                var o = f.Dev.DevUpdate();
                r.Check("it stops rather than build against nothing", !o.Ok && f.Calls.Count == 0);
                r.Check("it says which file to look at", f.Log.Any(l => l.Contains("LogOutput.log")), string.Join(" / ", f.Log));
                r.Check("the game it started is closed even so", run.Stopped && run.Disposed);
                r.Check("it really waited the whole time", run.Polls >= 9, run.Polls.ToString());
            });

            r.Test("the game closes itself", () =>
            {
                var f = NewFake(r, "up-exits", true);
                string steam = r.NewDir("up-exits-steam");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"), "game");
                f.Dev.SteamDir = () => steam;
                string interop = Path.Combine(f.Dev.Paths.Modded, @"BepInEx\interop\Assembly-CSharp.dll");
                var run = new FakeRun();
                run.OnPoll = () => { SelfTestRunner.Touch(interop, "new"); run.Exited = true; };
                f.Dev.StartInterop = () => run;
                var o = f.Dev.DevUpdate();
                r.Check("a game that ends by itself with the interop written is fine", o.Ok, o.Error);
                r.Check("and nothing was killed", !run.Stopped);

                var f2 = NewFake(r, "up-exits-bad", true);
                f2.Dev.SteamDir = () => steam;
                var run2 = new FakeRun { Exited = true };
                f2.Dev.StartInterop = () => run2;
                r.Check("a game that ends with no interop is a failure", !f2.Dev.DevUpdate().Ok);
            });

            r.Test("the previous game's log is kept first", () =>
            {
                var f = NewFake(r, "up-log", true);
                string steam = r.NewDir("up-log-steam");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"), "game");
                f.Dev.SteamDir = () => steam;
                var logs = NewLogs(Path.GetDirectoryName(Path.GetDirectoryName(f.Dev.Paths.LogPath)), f.Log);
                logs.LogPath = f.Dev.Paths.LogPath;
                logs.LogArchiveDir = f.Dev.Paths.LogArchiveDir;
                f.Dev.NewGameLogs = () => logs;
                var t = new DateTime(2026, 9, 20, 21, 4, 5);
                SelfTestRunner.Touch(f.Dev.Paths.LogPath, "last game");
                File.SetLastWriteTime(f.Dev.Paths.LogPath, t);
                string interop = Path.Combine(f.Dev.Paths.Modded, @"BepInEx\interop\Assembly-CSharp.dll");
                var run = new FakeRun();
                run.OnPoll = () => { SelfTestRunner.Touch(interop, "new"); run.Exited = true; };
                f.Dev.StartInterop = () => run;
                f.Dev.DevUpdate();
                string kept = Path.Combine(f.Dev.Paths.LogArchiveDir, GameLogs.ArchiveName(t));
                r.Check("it is in the logs folder", File.Exists(kept) && File.ReadAllText(kept) == "last game");
                r.Check("and the game starts from an empty one", !File.Exists(f.Dev.Paths.LogPath) || File.ReadAllText(f.Dev.Paths.LogPath).Length == 0);
            });

            r.Test("the last 30 lines of a log the game is writing", () =>
            {
                string d = r.NewDir("tail");
                string p = Path.Combine(d, "LogOutput.log");
                var sb = new StringBuilder();
                for (int i = 0; i < 100; i++) sb.Append("line ").Append(i).Append("\r\n");
                sb.Append("[Info   :BepInEx] Chainloader startup complete\r\n");
                File.WriteAllText(p, sb.ToString(), new UTF8Encoding(false));
                using (new FileStream(p, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                {
                    string tail = DevBuild.Tail(p, 30);
                    r.Check("the line that says it is ready is found", tail.Contains("Chainloader startup complete"));
                    r.Check("and only the end is read", !tail.Contains("line 0\n") && tail.Contains("line 99"));
                }
                r.Equal("a log that is not there reads as nothing", "", DevBuild.Tail(Path.Combine(d, "no.log"), 30));
            });
        }

        // ------------------------------------------------------------------ which DLL is in the copy (v1.1)
        // On the author's PC the released mod and the locally built one land in the SAME game copy, so the app writes
        // down which one it put there (mod-origin.txt) and judges the DLL on disk against that note by its hash - a DLL
        // the old launcher rebuilt afterwards is "unknown", never "the release this app installed" (src\Core\ModOrigin.cs).
        static void OriginTests(SelfTestRunner r)
        {
            r.Section("which DLL");
            r.Test("mod-origin.txt", () =>
            {
                string root = r.NewDir("origin");
                string data = Path.Combine(root, "data");
                string dll = Path.Combine(root, @"Among Us PocketRoles\BepInEx\plugins\PocketRoles.dll");
                var when = new DateTime(2026, 9, 24, 1, 2, 3);
                r.Check("no DLL: nothing to say (null)", ModOriginFile.Read(data, dll) == null);
                SelfTestRunner.Touch(dll, "release bytes");
                var o = ModOriginFile.Read(data, dll);
                r.Check("a DLL nobody wrote down: unknown", o != null && o.Kind == ModOrigin.Unknown && o.At == null);
                ModOriginFile.Record(data, dll, ModOrigin.Release, "0.5.4", when, null);
                o = ModOriginFile.Read(data, dll);
                r.Check("the release this app installed", o.Kind == ModOrigin.Release && o.Version == "0.5.4" && o.At == "2026-09-24 01:02:03", o.Kind + "|" + o.Version + "|" + o.At);
                File.WriteAllText(dll, "the old launcher built this");
                o = ModOriginFile.Read(data, dll);
                r.Check("the DLL changed under it (the old launcher's build): unknown, never 'release'", o.Kind == ModOrigin.Unknown && o.At == null);
                ModOriginFile.Record(data, dll, ModOrigin.Dev, "0.5.5", when.AddHours(1), null);
                o = ModOriginFile.Read(data, dll);
                r.Check("the author's own build", o.Kind == ModOrigin.Dev && o.At == "2026-09-24 02:02:03");
                File.WriteAllText(Path.Combine(data, ModOriginFile.FileName), "garbage");
                r.Check("a broken note reads as unknown", ModOriginFile.Read(data, dll).Kind == ModOrigin.Unknown);
                r.Check("no data folder (the self-test's fakes): unknown", ModOriginFile.Read(null, dll).Kind == ModOrigin.Unknown);
                ModOriginFile.Record(null, dll, ModOrigin.Dev, "1", when, null);
                r.Check("... and nothing is written anywhere", !File.Exists(Path.Combine(root, ModOriginFile.FileName)) && Directory.GetFiles(root).Length == 0);
                foreach (var l in Lang.Codes)
                {
                    string dev = new ModOrigin { Kind = ModOrigin.Dev, At = "2026-09-24 02:02:03" }.Text(l);
                    string rel = new ModOrigin { Kind = ModOrigin.Release, Version = "0.5.4", At = "2026-09-23 16:38:11" }.Text(l);
                    string unk = new ModOrigin { Kind = ModOrigin.Unknown }.Text(l);
                    r.Check("the three answers have their own words in " + l, dev.Contains("2026-09-24 02:02:03") && rel.Contains("0.5.4") && rel.Contains("2026-09-23 16:38:11") && unk.Length > 5 && dev != rel && rel != unk && !unk.StartsWith("origin_", StringComparison.Ordinal));
                }
            });
        }

        // ------------------------------------------------------------------ the words
        static void DevStringTests(SelfTestRunner r)
        {
            r.Section("developer words");
            string[] keys = { "sdk_missing", "dev_only", "dev_building", "dev_build_done", "dev_build_ok", "dev_build_failed",
                "dev_build_timeout", "dev_build_failed_exit", "dev_build_ask", "dev_need_steam", "dev_up_start", "dev_up_1", "dev_up_2",
                "dev_up_3", "dev_up_4", "dev_interop_failed", "dev_interop_ok", "dev_up_done",
                "lg_zipped", "lg_size", "lg_tip", "lg_big", "lg_log_title", "lg_log_sub", "lg_log_none", "lg_log_more",
                // v1.1: the developer switch and which DLL is in the copy
                "dev_nofolder", "dev_switch_on", "dev_switch_off", "origin_dev", "origin_release", "origin_unknown" };
            foreach (var l in Lang.Codes)
            {
                var missing = keys.Where(k => S.T(l, k) == k).ToArray();
                r.Check("all " + keys.Length + " new words are in " + l, missing.Length == 0, string.Join(",", missing));
            }
            // the ps1 wrote the developer lines in Japanese only; these are the same words
            r.Equal("sdk_missing (ja) is the launcher's", ".NET SDK が見つかりません: C:\\x", S.T("ja", "sdk_missing", @"C:\x"));
            r.Equal("dev_up_start (ja) is the launcher's", "=== 更新開始 ===", S.T("ja", "dev_up_start"));
            r.Equal("dev_building (ja) is the launcher's", "PocketRoles をビルドしています (1〜2 分)...", S.T("ja", "dev_building"));
            r.Equal("the exit code is filled in", "Build failed (exit 1). The game's API may have changed.", S.T("en", "dev_build_failed_exit", 1));
            r.Check("the 「開発モードだけ」 line exists in all three", Lang.Codes.All(l => S.T(l, "dev_only").Length > 10));
        }
    }
}
