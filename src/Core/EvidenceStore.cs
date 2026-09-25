// The Aegis evidence records on this PC: reading them, finding the log lines that back them, and deciding which ones a
// zip may hold. Ported from the launcher on branch evidence-90d (ps1 Read-EvidenceRecord, Find-EvidenceBacking,
// Save-EvidenceBackingFrom, Get-ReportEvidence, Get-EnforcedEvidenceIds, Get-EraseCutoffs, Select-PlayerEvidence).
//
// The rules that matter (owner decision 2026-09-23 "A" and "B"):
//  - the mod keeps a record 90 days from when it was written; the evidence of a ban still in force is kept until at
//    least 30 days after that ban ends. This app only READS records - the mod deletes them.
//  - logs still go after 30 days. Just before a log is deleted, the two lines that back a record still kept are copied
//    into evidence\<id>.log, so an older record can still be checked against a log that no longer exists. That file is
//    deleted with its record, and an erase request takes it at once.
//  - the report zip holds the records of the last 90 days (at most 200 and 5 MB, as before: a report zip is about
//    recent games and names other players). An appeal about an older record gets the one-player zip instead.
//  - a record an erase request emptied or covers, and one past its 90 days the mod has not deleted yet, never go into
//    a one-player zip - unless an entry of aegis-bans.json still names it (a ban in force).
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
    /// <summary>One AEG-*.json as the app reads it.</summary>
    internal sealed class EvidenceRecord
    {
        public string Id;
        public string Path;
        public string Text;
        public long Length;
        /// <summary>The record's own "time" (UTC), else the file's last write time.</summary>
        public DateTime TimeUtc;
        /// <summary>An erase request emptied it: its log lines went at once, so it never gets backing lines.</summary>
        public bool Blanked;
        public string Hash = "";      // the friend code's hash
        public string Puid = "";      // the PUID's hash
        public string Code = "";      // the player's erase code (/cmd id)
        /// <summary>The detection its backing lines must match; null when the record was blanked.</summary>
        public string Detection;
    }

    /// <summary>How a one-player export was asked for.</summary>
    internal sealed class ExportKey
    {
        /// <summary>id / code / friend.</summary>
        public string Kind;
        /// <summary>What was matched (an evidence id, an erase code or a friend code's HASH - never the friend code).</summary>
        public string Value;
    }

    internal sealed class EvidenceStore
    {
        public const int KeepDays = 90;          // the mod's AegisPrivacyCore.EvidenceKeepDays (texts only: the mod deletes)
        public const int ReportDays = 90;        // what a report zip reaches back over
        public const int ReportMax = 200;
        public const long ReportBytes = 5L * 1024 * 1024;
        const long RecordMaxBytes = 1024 * 1024;
        const long BackingMaxBytes = 64 * 1024;
        const long BanFileMaxBytes = 16L * 1024 * 1024;
        const long RulesCacheMaxBytes = 256 * 1024;
        /// <summary>AegisPrivacyCore.EraseGraceDays.</summary>
        const int EraseGraceDays = 2;

        static readonly Regex AegId = new Regex("AEG-[0-9A-Z]{5,6}", RegexOptions.CultureInvariant);

        public ModPaths Paths;
        public Func<DateTime> NowUtc = () => DateTime.UtcNow;
        public Action<string> LogQuiet = _ => { };
        public string UserProfile = Environment.GetEnvironmentVariable("USERPROFILE");

        public string EvidenceDir => GameFolders.Join(Paths.Modded, @"BepInEx\PocketRoles\evidence");
        string BanFile => GameFolders.Join(Paths.Modded, @"BepInEx\PocketRoles\aegis-bans.json");
        string RulesCache => GameFolders.Join(Paths.Modded, @"BepInEx\PocketRoles\aegis-rules-cache.txt");

        // ------------------------------------------------------------------ reading records
        /// <summary>One record, or null when the file is not one (ps1 Read-EvidenceRecord). Never throws.</summary>
        public EvidenceRecord Read(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length > RecordMaxBytes || !EvidenceText.RecordFileRe.IsMatch(fi.Name)) return null;
                string text = TextFiles.ReadShared(path);
                if (text.IndexOf("\"format\": \"PocketRoles.AegisEvidence\"", StringComparison.Ordinal) < 0 &&
                    !Regex.IsMatch(text, "\"format\"\\s*:\\s*\"PocketRoles\\.AegisEvidence\"")) return null;
                var o = Json.TryParseObject(text);
                if (o == null || Json.Str(o, "format") != "PocketRoles.AegisEvidence") return null;
                var p = Json.Obj(o, "player");
                var trail = new List<string>();
                object logs;
                if (o.TryGetValue("log", out logs))
                {
                    var arr = logs as object[];
                    if (arr != null) foreach (var l in arr) { var s = l as string; if (s != null) trail.Add(s); }
                }
                bool blanked = o.ContainsKey("erased") && Json.Str(o, "erased") != null;
                var r = new EvidenceRecord
                {
                    Id = fi.Name.Substring(0, fi.Name.Length - 5),
                    Path = fi.FullName,
                    Text = text,
                    Length = fi.Length,
                    Blanked = blanked,
                    TimeUtc = fi.LastWriteTimeUtc,
                    Hash = (Json.Str(p, "hash") ?? "").ToLowerInvariant(),
                    Puid = (Json.Str(p, "puidHash") ?? "").ToLowerInvariant(),
                    Code = EvidenceText.NormalizeEraseCode(Json.Str(p, "eraseCode")),
                    Detection = blanked ? null : EvidenceText.Detection(trail),
                };
                string time = Json.Str(o, "time");
                DateTime parsed;
                if (time != null && DateTime.TryParse(time, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed)) r.TimeUtc = parsed;
                return r;
            }
            catch (Exception ex) { LogQuiet("evidence " + path + ": " + ex.Message); return null; }
        }

        /// <summary>Every record in the folder (newest last). Never throws.</summary>
        public List<EvidenceRecord> ReadAll()
        {
            var list = new List<EvidenceRecord>();
            try
            {
                string dir = EvidenceDir;
                if (!GameFolders.PathExists(dir)) return list;
                foreach (var f in Directory.GetFiles(dir, "AEG-*.json"))
                {
                    var r = Read(f);
                    if (r != null) list.Add(r);
                }
            }
            catch (Exception ex) { LogQuiet("evidence: " + ex.Message); }
            list.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
            return list;
        }

        // ------------------------------------------------------------------ backing lines
        /// <summary>The first line of a backing file (ps1 Get-BackingHeader). The ban console's import takes only lines
        /// with an Aegis word, so this comment is ignored there. <paramref name="fromLiveLog"/>: the lines were copied
        /// from a log that is still here, so the author is not told the log was deleted when it was not.</summary>
        public static string BackingHeader(string id, string logName, bool fromLiveLog) => fromLiveLog
            ? "# PocketRoles: the lines of " + logName + " that back the evidence record " + id + " (copied from that log for this export; the log is still on the host PC)"
            : "# PocketRoles: the lines of " + logName + " that back the evidence record " + id + " (kept after that log was deleted at 30 days; deleted with the record)";

        /// <summary>The backing lines of the wanted records (id -&gt; detection) inside one log file or one zip of logs:
        /// id -&gt; (log name, lines). Never throws (ps1 Find-EvidenceBacking).</summary>
        public Dictionary<string, KeyValuePair<string, List<string>>> FindBacking(string path, IDictionary<string, string> wanted)
        {
            var res = new Dictionary<string, KeyValuePair<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
            if (wanted == null || wanted.Count == 0) return res;
            try
            {
                string name = System.IO.Path.GetFileName(path);
                if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var kv in EvidenceText.Backing(EvidenceText.ReadLines(path), wanted))
                        res[kv.Key] = new KeyValuePair<string, List<string>>(name, kv.Value);
                }
                else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using (var za = ZipFile.OpenRead(path))
                        foreach (var e in za.Entries)
                        {
                            if (!e.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) continue;
                            Dictionary<string, List<string>> found;
                            using (var sr = new StreamReader(e.Open(), Encoding.UTF8, true))
                                found = EvidenceText.Backing(EvidenceText.LinesOf(sr), wanted);
                            foreach (var kv in found)
                                if (!res.ContainsKey(kv.Key)) res[kv.Key] = new KeyValuePair<string, List<string>>(e.Name, kv.Value);
                        }
                }
            }
            catch (Exception ex) { LogQuiet("evidence backing " + path + ": " + ex.Message); }
            return res;
        }

        /// <summary>What the 30-day log deletion needs so the records still kept keep their two lines: the records that
        /// have no &lt;id&gt;.log yet and were not blanked (id -&gt; detection). Worked out once, on the first log that is
        /// due (ps1 New-BackingJob / Get-BackingWanted).</summary>
        public Dictionary<string, string> BackingWanted()
        {
            var w = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string dir = EvidenceDir;
                if (!GameFolders.PathExists(dir)) return w;
                foreach (var f in Directory.GetFiles(dir, "AEG-*.json"))
                {
                    if (!EvidenceText.RecordFileRe.IsMatch(System.IO.Path.GetFileName(f))) continue;
                    if (File.Exists(f.Substring(0, f.Length - 5) + ".log")) continue;
                    var r = Read(f);
                    if (r != null && r.Detection != null) w[r.Id] = r.Detection;
                }
            }
            catch (Exception ex) { LogQuiet("evidence backing: " + ex.Message); }
            return w;
        }

        /// <summary>evidence\&lt;id&gt;.log (header + lines, UTF-8 without a BOM) through a .tmp file.</summary>
        public void WriteBackingFile(string id, string logName, IEnumerable<string> lines)
        {
            string path = GameFolders.Join(EvidenceDir, id + ".log");
            string tmp = path + ".tmp";
            var all = new List<string> { BackingHeader(id, logName, false) };
            all.AddRange(lines);
            File.WriteAllText(tmp, string.Join("\r\n", all.ToArray()) + "\r\n", new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>A past log (or zip of logs) about to be deleted: the backing lines of the records written in it are
        /// kept first (ps1 Save-EvidenceBackingFrom). Returns how many were written. Never throws.</summary>
        public int SaveBackingFrom(string path, Dictionary<string, string> wanted)
        {
            int saved = 0;
            if (wanted == null || wanted.Count == 0) return 0;
            try
            {
                var found = FindBacking(path, wanted);
                foreach (var id in new List<string>(found.Keys))
                {
                    // the record is read again right here: an erase request may have emptied or deleted it since the
                    // folder was listed, and then its log lines must go at once instead of coming back here
                    var now = Read(GameFolders.Join(EvidenceDir, id + ".json"));
                    if (now == null || now.Detection == null) { wanted.Remove(id); continue; }
                    try { WriteBackingFile(id, found[id].Key, found[id].Value); wanted.Remove(id); saved++; }
                    catch (Exception ex) { LogQuiet("evidence backing " + id + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { LogQuiet("evidence backing: " + ex.Message); }
            return saved;
        }

        /// <summary>A record's own backing file, masked like a log (null when there is none, or when the record was
        /// blanked). ps1 Get-MaskedBacking.</summary>
        public string MaskedBacking(string id, bool blanked)
        {
            if (blanked) return null;
            try
            {
                string p = GameFolders.Join(EvidenceDir, id + ".log");
                if (GameFolders.PathExists(p) && new FileInfo(p).Length <= BackingMaxBytes)
                    return Mask.LogText(TextFiles.ReadShared(p), UserProfile);
            }
            catch (Exception ex) { LogQuiet("evidence backing " + id + ": " + ex.Message); }
            return null;
        }

        // ------------------------------------------------------------------ the report zip's records
        /// <summary>What goes into a zip: the file name, the masked text and its backing lines.</summary>
        internal sealed class ZipRecord
        {
            public string Name, Text, Backing;
        }

        /// <summary>The records for a report zip: written in the last 90 days, newest first, at most 200 and 5 MB
        /// (ps1 Get-ReportEvidence). Never throws.</summary>
        public List<ZipRecord> ForReport()
        {
            var outp = new List<ZipRecord>();
            try
            {
                string dir = EvidenceDir;
                if (!GameFolders.PathExists(dir)) return outp;
                var since = NowUtc().AddDays(-ReportDays);
                var files = new List<FileInfo>();
                foreach (var f in new DirectoryInfo(dir).GetFiles("AEG-*.json"))
                    if (EvidenceText.RecordFileRe.IsMatch(f.Name) && f.LastWriteTimeUtc >= since && f.Length <= RecordMaxBytes) files.Add(f);
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                long bytes = 0;
                foreach (var f in files)
                {
                    if (outp.Count >= ReportMax || bytes + f.Length > ReportBytes) break;
                    try
                    {
                        string text = TextFiles.ReadShared(f.FullName);
                        if (!Regex.IsMatch(text, "\"format\"\\s*:\\s*\"PocketRoles\\.AegisEvidence\"")) continue;
                        bool blanked = Regex.IsMatch(text, "\"erased\"\\s*:\\s*\"");
                        text = Mask.ConvertToReportEvidence(Mask.Secrets(text, UserProfile));
                        if (text == null) { LogQuiet("report evidence " + f.Name + ": left out (not in the expected shape)"); continue; }
                        string id = f.Name.Substring(0, f.Name.Length - 5);
                        outp.Add(new ZipRecord { Name = f.Name, Text = text, Backing = MaskedBacking(id, blanked) });
                        bytes += f.Length;
                    }
                    catch (Exception ex) { LogQuiet("report evidence " + f.Name + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { LogQuiet("report evidence: " + ex.Message); }
            return outp;
        }

        // ------------------------------------------------------------------ one player (the export)
        /// <summary>What the host typed: an evidence id, an erase code, or a friend code (kept only as its hash).
        /// null when it is none of them (ps1 Resolve-ExportKey).</summary>
        public static ExportKey ResolveKey(string who)
        {
            if (string.IsNullOrEmpty(who)) return null;
            string id = EvidenceText.EvidenceId(who);
            if (id.Length > 0) return new ExportKey { Kind = "id", Value = id };
            string code = EvidenceText.NormalizeEraseCode(who);
            if (code.Length > 0) return new ExportKey { Kind = "code", Value = code };
            string hash = EvidenceText.FriendCodeHash(who);
            if (hash.Length > 0) return new ExportKey { Kind = "friend", Value = hash };
            return null;
        }

        /// <summary>That player's records, oldest first: the ones the key names, then every record that shares any of
        /// their hashes or their erase code (ps1 Select-PlayerEvidence).</summary>
        public static List<EvidenceRecord> SelectPlayer(List<EvidenceRecord> records, ExportKey key)
        {
            var mine = new List<EvidenceRecord>();
            if (records == null || key == null) return mine;
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var seeds = new List<EvidenceRecord>();
            foreach (var r in records)
            {
                if (key.Kind == "id" && string.Equals(r.Id, key.Value, StringComparison.OrdinalIgnoreCase)) seeds.Add(r);
                else if (key.Kind == "code" && r.Code == key.Value) seeds.Add(r);
                else if (key.Kind == "friend" && (string.Equals(r.Hash, key.Value, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(r.Puid, key.Value, StringComparison.OrdinalIgnoreCase))) seeds.Add(r);
            }
            foreach (var s in seeds)
            {
                if (s.Hash.Length > 0) hashes.Add(s.Hash);
                if (s.Puid.Length > 0) hashes.Add(s.Puid);
                if (s.Code.Length > 0) codes.Add(s.Code);
            }
            foreach (var r in records)
                if ((r.Puid.Length > 0 && hashes.Contains(r.Puid)) || (r.Hash.Length > 0 && hashes.Contains(r.Hash)) || (r.Code.Length > 0 && codes.Contains(r.Code)))
                    mine.Add(r);
            mine.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
            return mine;
        }

        /// <summary>The evidence ids any entry of aegis-bans.json still names: a record one of them names may be the
        /// evidence of a ban that still applies and is kept whatever its age and whatever the erase list says
        /// (ps1 Get-EnforcedEvidenceIds). Never throws.</summary>
        public HashSet<string> EnforcedIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string p = BanFile;
                if (!GameFolders.PathExists(p) || new FileInfo(p).Length > BanFileMaxBytes) return ids;
                foreach (Match m in AegId.Matches(TextFiles.ReadShared(p))) ids.Add(m.Value);
            }
            catch (Exception ex) { LogQuiet("evidence export (ban file): " + ex.Message); }
            return ids;
        }

        /// <summary>The [erase] list of the cached definitions file: erase code -&gt; the cutoff (the request's date plus
        /// the grace days), plus the hashes of those players from aegis-bans.json for records with no code
        /// (ps1 Get-EraseCutoffs + Add-EraseHashes). The signature is NOT checked here on purpose: this list only LEAVES
        /// records OUT of a zip, so a wrong or old file can only make a zip smaller. Never throws.</summary>
        public Dictionary<string, DateTime> EraseCutoffs()
        {
            var outp = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            try
            {
                string p = RulesCache;
                if (!GameFolders.PathExists(p) || new FileInfo(p).Length > RulesCacheMaxBytes) return outp;
                string section = "";
                DateTime? header = null;
                foreach (var raw in File.ReadAllLines(p, Encoding.UTF8))
                {
                    string line = raw;
                    int h = line.IndexOf('#');
                    if (h >= 0) line = line.Substring(0, h);
                    try { line = line.Normalize(NormalizationForm.FormKC); } catch (ArgumentException) { }
                    line = line.Trim('﻿').Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        section = line.EndsWith("]", StringComparison.Ordinal) ? line.Substring(1, line.Length - 2).Trim().ToLowerInvariant() : "";
                        header = null;
                        continue;
                    }
                    if (section != "erase") continue;
                    var t = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (t.Length == 0) continue;
                    DateTime d;
                    if (t[0].StartsWith("@", StringComparison.Ordinal))
                    {
                        header = null;
                        if (t.Length == 1 && TryDate(t[0].Substring(1), out d)) header = d;
                        continue;
                    }
                    var sb = new StringBuilder();
                    int used = 0, letters = 0;
                    while (used < t.Length && letters < 16 && !HasDigit(t[used])) { sb.Append(t[used]); letters += t[used].Replace(" ", "").Replace("-", "").Length; used++; }
                    string code = EvidenceText.NormalizeEraseCode(sb.ToString());
                    if (code.Length == 0) continue;
                    DateTime? date = null;
                    if (used < t.Length) { if (TryDate(t[used], out d)) date = d; }
                    else if (header.HasValue) date = header;
                    if (!date.HasValue) continue;
                    var cut = date.Value.AddDays(EraseGraceDays);
                    DateTime had;
                    if (!outp.TryGetValue(code, out had) || had < cut) outp[code] = cut;
                }
                AddEraseHashes(outp);
            }
            catch (Exception ex) { LogQuiet("evidence export (erase list): " + ex.Message); }
            return outp;
        }

        static bool HasDigit(string s)
        {
            foreach (char c in s) if (c >= '0' && c <= '9') return true;
            return false;
        }

        static bool TryDate(string s, out DateTime d) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out d);

        void AddEraseHashes(Dictionary<string, DateTime> cutoffs)
        {
            try
            {
                if (cutoffs.Count == 0) return;
                string p = BanFile;
                if (!GameFolders.PathExists(p) || new FileInfo(p).Length > BanFileMaxBytes) return;
                var o = Json.TryParseObject(TextFiles.ReadShared(p));
                object bans;
                if (o == null || !o.TryGetValue("bans", out bans)) return;
                var arr = bans as object[];
                if (arr == null) return;
                foreach (var b in arr)
                {
                    var e = b as Dictionary<string, object>;
                    if (e == null) continue;
                    string code = EvidenceText.NormalizeEraseCode(Json.Str(e, "eraseCode"));
                    DateTime cut;
                    if (code.Length == 0 || !cutoffs.TryGetValue(code, out cut)) continue;
                    foreach (var h in new[] { Json.Str(e, "hash"), Json.Str(e, "puidHash") })
                    {
                        if (string.IsNullOrEmpty(h)) continue;
                        DateTime had;
                        if (!cutoffs.TryGetValue(h, out had) || had < cut) cutoffs[h] = cut;
                    }
                }
            }
            catch (Exception ex) { LogQuiet("evidence export (erase hashes): " + ex.Message); }
        }

        /// <summary>Why a record of that player is not in the export.</summary>
        internal sealed class LeftOut
        {
            public int Blanked, Erased, Old;
            public int Total => Blanked + Erased + Old;
        }

        /// <summary>Of one player's records, the ones this PC may still keep (ps1 Invoke-ExportOne's filter): what
        /// AegisPrivacyCore.EvidenceFate would no longer keep must not go into a zip either.</summary>
        public List<EvidenceRecord> KeepForExport(List<EvidenceRecord> all, LeftOut left)
        {
            var enforced = EnforcedIds();
            var cutoffs = EraseCutoffs();
            var now = NowUtc();
            var mine = new List<EvidenceRecord>();
            foreach (var r in all)
            {
                if (r.Blanked) { left.Blanked++; continue; }
                if (!enforced.Contains(r.Id))
                {
                    DateTime? cut = null;
                    foreach (var k in new[] { r.Code, r.Hash, r.Puid })
                    {
                        DateTime c;
                        if (!string.IsNullOrEmpty(k) && cutoffs.TryGetValue(k, out c) && (!cut.HasValue || c > cut.Value)) cut = c;
                    }
                    if (cut.HasValue && r.TimeUtc < cut.Value) { left.Erased++; continue; }
                    if ((now - r.TimeUtc).TotalDays >= KeepDays) { left.Old++; continue; }
                }
                mine.Add(r);
            }
            return mine;
        }

        /// <summary>The logs a backing line may still be in, newest first: the current log, the archive's session logs
        /// and day zips, and logs moved aside next to LogOutput.log (ps1 Get-SearchableLogs).</summary>
        public List<string> SearchableLogs()
        {
            var list = new List<string>();
            try
            {
                if (GameFolders.PathExists(Paths.LogPath)) list.Add(Paths.LogPath);
                var more = new List<FileInfo>();
                if (GameFolders.PathExists(Paths.LogArchiveDir))
                    foreach (var f in new DirectoryInfo(Paths.LogArchiveDir).GetFiles())
                        if (Regex.IsMatch(f.Name, @"^(LogOutput-.+\.log|logs-.+\.zip)$")) more.Add(f);
                string bep = GameFolders.Parent(Paths.LogPath);
                if (!string.IsNullOrEmpty(bep) && GameFolders.PathExists(bep))
                    more.AddRange(new DirectoryInfo(bep).GetFiles("LogOutput-*.log"));
                more.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                foreach (var f in more) list.Add(f.FullName);
            }
            catch (Exception ex) { LogQuiet("evidence export: " + ex.Message); }
            return list;
        }
    }
}
