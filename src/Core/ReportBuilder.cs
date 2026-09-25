// The report zip and the one-player evidence zip (ps1:1442-1704 Invoke-Report, and Invoke-ExportOne on the launcher's
// evidence-90d branch). The same files, the same masking, the same names, the same 30-day life.
//
// What a report zip holds: the current game log, launcher-state.json, the launcher's own log, the 3 most recent past
// session logs (each at most 8 MB: head + tail), the Aegis evidence records of the last 90 days (at most 200 / 5 MB,
// each with its backing log lines when its own log is gone) and system.txt.
// What it never holds: the mod's .cfg files (a Discord webhook URL lives there), report-mail.json, any key or password
// - left out by name AND by folder, not by hoping the masking catches them.
// The one-player zip holds ONLY that player's records (the friend code and its hash never go in, only the PUID's hash
// and their erase code) and is for the author alone.
//
// Nothing here is sent anywhere: both zips are written to the Desktop and the person attaches them to an e-mail
// themselves (SignPath: no data leaves the PC without the viewer doing it).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Starpocket.Client.Core
{
    internal sealed class ReportResult
    {
        public bool Ok;
        public string Zip;         // the full path of the zip
        public string Name;        // its file name
        public string Error;
        /// <summary>How many evidence records went in, and how many of those came with backing lines.</summary>
        public int Records, WithBacking;
        /// <summary>The first few ids, so the host can see whose zip it is before attaching it (one-player export).</summary>
        public string[] Ids = new string[0];
        public string SearchedBy;
    }

    internal sealed class ReportBuilder
    {
        /// <summary>Per past log in a report zip.</summary>
        public const long PastLogMax = 8L * 1024 * 1024;
        static readonly Regex SecretName = new Regex(@"deepl-key|report-mail|password|\.key$|\.cfg$", RegexOptions.IgnoreCase);

        public ModPaths Paths;
        public string Desktop;
        public string CacheDir;
        public string StatePath;
        public string ClientLogPath;
        public string SteamDir;
        public bool DevMode;
        public Func<string> Lang = () => "ja";
        public Action<string> Log = _ => { };
        public Action<string> LogQuiet = _ => { };
        public Func<DateTime> Now = () => DateTime.Now;
        public string UserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        public EvidenceStore Evidence;

        string T(string key, params object[] args) => S.T(Lang(), key, args);

        EvidenceStore Store => Evidence ?? (Evidence = new EvidenceStore { Paths = Paths, LogQuiet = LogQuiet, UserProfile = UserProfile, NowUtc = () => Now().ToUniversalTime() });

        /// <summary>The launcher's Format-Size.</summary>
        public static string FormatSize(long b)
        {
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
            if (b >= 10L << 20 || b == 0) return (b / (double)(1 << 20)).ToString("0", CultureInfo.InvariantCulture) + " MB";
            if (b < 1 << 20) return ((long)Math.Ceiling(b / 1024.0)).ToString(CultureInfo.InvariantCulture) + " KB";
            return (b / (double)(1 << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        // ------------------------------------------------------------------ the report zip
        public ReportResult MakeReport()
        {
            try
            {
                Log(T("rp_creating"));
                string stamp = Now().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
                Directory.CreateDirectory(Desktop);
                string zip = Path.Combine(Desktop, "PocketRoles-report-" + stamp + ".zip");
                string tmp = Path.Combine(CacheDir, "report-" + stamp);
                RemoveDir(tmp);
                Directory.CreateDirectory(tmp);

                var items = new List<KeyValuePair<string, long>>();   // path -> cap (0 = whole file)
                foreach (var f in new[] { Paths.LogPath, StatePath, ClientLogPath })
                    if (!string.IsNullOrEmpty(f)) items.Add(new KeyValuePair<string, long>(f, 0));
                foreach (var f in RecentArchivedLogs(3)) items.Add(new KeyValuePair<string, long>(f, PastLogMax));

                foreach (var it in items) CopyMasked(it.Key, tmp, it.Value);

                var ev = Store.ForReport();
                int withBacking = 0;
                if (ev.Count > 0)
                {
                    string evDir = Path.Combine(tmp, "evidence");
                    Directory.CreateDirectory(evDir);
                    var utf8 = new UTF8Encoding(false);
                    foreach (var e in ev)
                    {
                        try
                        {
                            File.WriteAllText(Path.Combine(evDir, e.Name), e.Text, utf8);
                            if (e.Backing != null)
                            {
                                File.WriteAllText(Path.Combine(evDir, e.Name.Substring(0, e.Name.Length - 5) + ".log"), e.Backing, utf8);
                                withBacking++;
                            }
                        }
                        catch (Exception ex) { LogQuiet("report evidence " + e.Name + ": " + ex.Message); }
                    }
                }
                string summary = SystemSummary() + "\r\n" +
                    "aegis evidence  : " + ev.Count + " record(s) in evidence/ (last " + EvidenceStore.ReportDays + " days, at most " +
                    EvidenceStore.ReportMax + " / " + FormatSize(EvidenceStore.ReportBytes) + "; " + withBacking +
                    " with the backing lines of a deleted log, <id>.log)";
                File.WriteAllText(Path.Combine(tmp, "system.txt"), summary, Encoding.UTF8);

                try { if (File.Exists(zip)) File.Delete(zip); } catch (Exception) { }
                WriteZip(tmp, zip);
                RemoveDir(tmp);
                try { LogQuiet("report zip: " + FormatSize(new FileInfo(zip).Length)); } catch (Exception) { }
                Log(T("rp_done", zip));
                Log(T("rp_autodelete"));
                return new ReportResult { Ok = true, Zip = zip, Name = Path.GetFileName(zip), Records = ev.Count, WithBacking = withBacking };
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return new ReportResult { Error = e };
            }
        }

        /// <summary>One file into the work folder, masked. A file that is a secret by name or by folder is never copied,
        /// whatever the masking would do to it.</summary>
        void CopyMasked(string path, string tmp, long cap)
        {
            string leaf = Path.GetFileName(path);
            if (SecretName.IsMatch(leaf) || path.IndexOf(@"\BepInEx\config\", StringComparison.OrdinalIgnoreCase) >= 0) return;
            if (!GameFolders.PathExists(path)) return;
            try
            {
                string text = cap > 0 ? TextFiles.ReadCapped(path, cap, FormatSize) : TextFiles.ReadShared(path);
                File.WriteAllText(Path.Combine(tmp, leaf), Mask.LogText(text, UserProfile), Encoding.UTF8);
            }
            catch (Exception ex) { Log(T("err", ex.Message)); }
        }

        /// <summary>The newest PAST session logs (the launcher's Get-RecentArchivedLogs, evidence-90d ps1:1454-1468).
        /// Two things it leaves out, and the reason for both: the archive whose name is the one Save-GameLog gives the
        /// CURRENT LogOutput.log - it is the same session, and the zip already carries LogOutput.log itself, so it would
        /// be in there twice (up to 8 MB of the same text) and one real past session would be pushed out - and a name
        /// the time cannot be read from. Sorted by name, newest first, like the launcher.</summary>
        public List<string> RecentArchivedLogs(int count)
        {
            var list = new List<string>();
            try
            {
                string current = null;
                try
                {
                    var live = new FileInfo(Paths.LogPath);
                    if (live.Exists) current = GameLogs.ArchiveName(live.LastWriteTime);
                }
                catch (Exception) { }
                if (!GameFolders.PathExists(Paths.LogArchiveDir)) return list;
                var names = new List<string>();
                foreach (var f in Directory.GetFiles(Paths.LogArchiveDir, "LogOutput-*.log"))
                {
                    string n = Path.GetFileName(f);
                    if (string.Equals(n, current, StringComparison.OrdinalIgnoreCase)) continue;
                    if (GameLogs.ArchiveTime(n) == null) continue;
                    names.Add(n);
                }
                names.Sort((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase));
                for (int i = 0; i < names.Count && i < count; i++) list.Add(Path.Combine(Paths.LogArchiveDir, names[i]));
            }
            catch (Exception ex) { LogQuiet("report logs: " + ex.Message); }
            return list;
        }

        /// <summary>system.txt (ps1 Get-SystemSummary): what this PC is and what is installed, with the home folder masked.</summary>
        public string SystemSummary()
        {
            var L = new List<string>();
            string exe = Paths.GameExe;
            string core = GameFolders.Join(Paths.Modded, @"BepInEx\core\BepInEx.Core.dll");
            var info = InstallInfo.Read(Paths);
            L.Add("PocketRoles report " + Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            L.Add("client          : " + AppInfo.Name + " " + AppInfo.Version + " (" + (DevMode ? "developer" : "friend") + ", lang " + Lang() + ")");
            L.Add("OS              : " + WindowsName() + " / " + Environment.OSVersion.VersionString + " / " + (Environment.Is64BitOperatingSystem ? "x64" : "x86"));
            L.Add("culture         : " + CultureInfo.CurrentCulture.Name + " / UI " + CultureInfo.CurrentUICulture.Name);
            L.Add("game dir        : " + Mask.Home(Paths.Modded, UserProfile));
            L.Add("game version    : " + (GameVersion.Read(Paths.Modded) ?? "") + "  (exe FileVersion " + (GameVersion.FileVersion(exe) ?? "") + ")");
            L.Add("PocketRoles.dll : " + DllLine());
            L.Add("BepInEx core    : " + (GameFolders.PathExists(core) ? (GameVersion.ProductVersion(core) ?? "") : "missing"));
            L.Add("interop         : " + (info.Interop ? "present" : "missing"));
            L.Add("plugins         : " + string.Join(", ", NamesIn(GameFolders.Join(Paths.Modded, @"BepInEx\plugins"), "*.dll")));
            L.Add("steam dir       : " + Mask.Home(SteamDir ?? "", UserProfile) + "  version " + (GameVersion.Read(SteamDir) ?? ""));
            L.Add("steam running   : " + Processes.SteamRunning());
            L.Add("game running    : " + Processes.GameRunning());
            L.Add("state           : " + Mask.Home(StatePath ?? "", UserProfile));
            L.Add("past logs       : " + ArchiveLine());
            return string.Join("\r\n", L.ToArray());
        }

        string DllLine()
        {
            try
            {
                if (!GameFolders.PathExists(Paths.DllPath)) return "missing";
                var fi = new FileInfo(Paths.DllPath);
                return (GameVersion.DllVersionString(Paths.DllPath) ?? "") + "  FileVersion " + (GameVersion.FileVersion(Paths.DllPath) ?? "") +
                       "  " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + fi.Length + " bytes";
            }
            catch (Exception) { return "missing"; }
        }

        string ArchiveLine()
        {
            long bytes = 0;
            int logs = 0, zips = 0;
            try
            {
                if (GameFolders.PathExists(Paths.LogArchiveDir))
                    foreach (var f in new DirectoryInfo(Paths.LogArchiveDir).GetFiles())
                    {
                        bytes += f.Length;
                        if (f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) zips++; else logs++;
                    }
            }
            catch (Exception) { }
            return FormatSize(bytes) + "  (" + logs + " logs, " + zips + " zips)  " + Mask.Home(Paths.LogArchiveDir, UserProfile);
        }

        static IEnumerable<string> NamesIn(string dir, string pattern)
        {
            var names = new List<string>();
            try { if (GameFolders.PathExists(dir)) foreach (var f in Directory.GetFiles(dir, pattern)) names.Add(Path.GetFileName(f)); }
            catch (Exception) { }
            return names;
        }

        static string WindowsName()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k == null) return "";
                    return string.Join(" ", new[] { k.GetValue("ProductName") as string, k.GetValue("DisplayVersion") as string,
                        "build " + k.GetValue("CurrentBuild") + "." + k.GetValue("UBR") });
                }
            }
            catch (Exception) { return ""; }
        }

        // ------------------------------------------------------------------ one player's evidence
        /// <summary>The one-player export (evidence-90d 「B」). <paramref name="who"/> is an erase code, a friend code or
        /// an evidence id as the host typed it; the friend code itself never reaches the zip.</summary>
        public ReportResult ExportOne(string who)
        {
            try
            {
                var key = EvidenceStore.ResolveKey(who);
                if (key == null) { Log(T("ex_bad")); return new ReportResult { Error = T("ex_bad") }; }
                Log(T("ex_creating"));
                var all = EvidenceStore.SelectPlayer(Store.ReadAll(), key);
                if (all.Count == 0) { Log(T("ex_none")); return new ReportResult { Error = T("ex_none") }; }
                var left = new EvidenceStore.LeftOut();
                var mine = Store.KeepForExport(all, left);
                if (left.Blanked > 0) LogQuiet("evidence export: " + left.Blanked + " record(s) left out (blanked)");
                if (left.Erased > 0) LogQuiet("evidence export: " + left.Erased + " record(s) left out (erased)");
                if (left.Old > 0) LogQuiet("evidence export: " + left.Old + " record(s) left out (old)");
                if (mine.Count == 0) { Log(T("ex_none")); return new ReportResult { Error = T("ex_none") }; }

                // the backing lines: the record's own <id>.log, else searched in the logs that are still here
                var backing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in mine)
                {
                    string b = Store.MaskedBacking(r.Id, r.Blanked);
                    if (b != null) backing[r.Id] = b;
                    else if (r.Detection != null) wanted[r.Id] = r.Detection;
                }
                if (wanted.Count > 0)
                    foreach (var lp in Store.SearchableLogs())
                    {
                        if (wanted.Count == 0) break;
                        var found = Store.FindBacking(lp, wanted);
                        foreach (var id in new List<string>(found.Keys))
                        {
                            var lines = new List<string> { EvidenceStore.BackingHeader(id, found[id].Key, true) };
                            lines.AddRange(found[id].Value);
                            backing[id] = Mask.LogText(string.Join("\r\n", lines.ToArray()) + "\r\n", UserProfile);
                            wanted.Remove(id);
                        }
                    }

                // to the second, and an existing file is never replaced: two players exported in the same minute must
                // not overwrite each other
                string stamp = Now().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                Directory.CreateDirectory(Desktop);
                string zip = Path.Combine(Desktop, "PocketRoles-evidence-" + stamp + ".zip");
                for (int i = 2; File.Exists(zip) && i < 100; i++) zip = Path.Combine(Desktop, "PocketRoles-evidence-" + stamp + "-" + i + ".zip");
                if (File.Exists(zip))
                {
                    string e = T("err", "too many zips named PocketRoles-evidence-" + stamp);
                    Log(e);
                    return new ReportResult { Error = e };
                }

                string tmp = Path.Combine(CacheDir, "evidence-" + stamp);
                RemoveDir(tmp);
                string evDir = Path.Combine(tmp, "evidence");
                Directory.CreateDirectory(evDir);
                var utf8 = new UTF8Encoding(false);
                int n = 0, withLog = 0;
                var ids = new List<string>();
                foreach (var r in mine)
                {
                    string text = Mask.ConvertToReportEvidence(Mask.Secrets(r.Text, UserProfile));
                    if (text == null) { LogQuiet("evidence export " + r.Id + ": left out (not in the expected shape)"); continue; }
                    File.WriteAllText(Path.Combine(evDir, r.Id + ".json"), text, utf8);
                    n++;
                    ids.Add(r.Id);
                    string b;
                    if (backing.TryGetValue(r.Id, out b)) { File.WriteAllText(Path.Combine(evDir, r.Id + ".log"), b, utf8); withLog++; }
                }
                if (n == 0) { RemoveDir(tmp); Log(T("ex_none")); return new ReportResult { Error = T("ex_none") }; }

                string by = key.Kind == "id" ? "evidence id " + key.Value
                    : key.Kind == "code" ? "erase code (the player's /cmd id code)"
                    : "friend code (neither the friend code nor its hash is in this zip)";
                var L = new List<string>
                {
                    "PocketRoles one-player evidence " + Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    "!! for the author only: send this zip to " + AppInfo.MailHost + " and to nobody else; do not pass it on or post it (play rules, article 7(1)).",
                    "!! it holds this player's erase code (/cmd id) and the hash of their PUID, which the public restriction list uses.",
                    "!! " + S.T("en", "ex_warn", AppInfo.MailHost),
                    "searched by     : " + by,
                    "records         : " + n + " (what this PC still keeps of that player: evidence records are kept " + EvidenceStore.KeepDays +
                        " days, those of a ban in force until at least 30 days after it ends; records an erase request emptied or covers are not in this zip)",
                    "backing lines   : " + withLog + " of " + n + " in evidence\\<id>.log (the log lines that back the record; the others' logs are gone or never had the record)",
                    "identity        : \"player.hash\" is the PUID's hash (the friend code's hash is replaced, as in the report zip); \"player.eraseCode\" is the player's /cmd id code. The code ties an appeal to these records; it does not prove who sent it (hosts, and anyone who saw a /cmd id answer in an unregistered room, know other players' codes).",
                    "other players   : no other player's records and no other lines of the logs. A detection line of THIS player's records can name another player of that game (the player killed, reported or voted) or the moderator who banned them.",
                };
                File.WriteAllText(Path.Combine(tmp, "export.txt"), string.Join("\r\n", L.ToArray()), Encoding.UTF8);
                File.WriteAllText(Path.Combine(tmp, "system.txt"),
                    SystemSummary() + "\r\n" + "aegis evidence  : " + n + " record(s) of one player in evidence/ (one-player export)", Encoding.UTF8);
                if (GameFolders.PathExists(StatePath))
                {
                    try { File.WriteAllText(Path.Combine(tmp, AppInfo.StateFileName), Mask.LogText(TextFiles.ReadShared(StatePath), UserProfile), Encoding.UTF8); }
                    catch (Exception) { }
                }
                WriteZip(tmp, zip);
                RemoveDir(tmp);

                string shown = string.Join(" ", ids.GetRange(0, Math.Min(3, ids.Count)).ToArray()) + (ids.Count > 3 ? " …" : "");
                Log(T("ex_done", n, zip) + " [" + shown + "]");
                if (withLog < n) Log(T("ex_nolog", n - withLog));
                Log(T("ex_warn", AppInfo.MailHost));
                Log(T("rp_autodelete"));
                // one line in the app's log: that an export was made and how it was searched, never the key itself
                LogQuiet("evidence export: " + n + " record(s), " + withLog + " with backing lines, searched by " + key.Kind +
                         " (" + Path.GetFileName(zip) + ", ids " + string.Join(" ", ids.GetRange(0, Math.Min(5, ids.Count)).ToArray()) + (ids.Count > 5 ? " …" : "") + ")");
                return new ReportResult
                {
                    Ok = true, Zip = zip, Name = Path.GetFileName(zip), Records = n, WithBacking = withLog,
                    Ids = ids.ToArray(), SearchedBy = key.Kind,
                };
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return new ReportResult { Error = e };
            }
        }

        // ------------------------------------------------------------------ the zip itself
        /// <summary>The files of <paramref name="dir"/> at the root and the files of each subfolder under
        /// "&lt;subfolder&gt;/" (ps1 New-ReportZip). ZipFile.CreateFromDirectory on .NET Framework writes "\" into
        /// subfolder entry names, which some unpackers show as one long file name.</summary>
        public static void WriteZip(string dir, string zip)
        {
            using (var fs = new FileStream(zip, FileMode.CreateNew))
            using (var za = new ZipArchive(fs, ZipArchiveMode.Create, false))
            {
                var root = new DirectoryInfo(dir);
                var files = new List<FileInfo>(root.GetFiles());
                files.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var f in files) za.CreateEntryFromFile(f.FullName, f.Name, CompressionLevel.Optimal);
                var dirs = new List<DirectoryInfo>(root.GetDirectories());
                dirs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var d in dirs)
                {
                    var sub = new List<FileInfo>(d.GetFiles());
                    sub.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                    foreach (var f in sub) za.CreateEntryFromFile(f.FullName, d.Name + "/" + f.Name, CompressionLevel.Optimal);
                }
            }
        }

        static void RemoveDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception) { }
        }
    }
}
