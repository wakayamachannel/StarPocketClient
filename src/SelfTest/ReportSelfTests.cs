// v0.3: the report zip, the Aegis evidence records, the shortcut button and the uninstall.
// Everything runs on fake folders inside <dir>\work and on a registry root of its own: no real game folder, no real
// Steam, no real Run key, nothing on the real Desktop, no network.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.Win32;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client.SelfTest
{
    internal static class ReportSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            MaskTests(r);
            EvidenceTextTests(r);
            EvidenceStoreTests(r);
            ReportZipTests(r);
            ExportOneTests(r);
            EvidenceExpiryTests(r);
            ShortcutTests(r);
            UninstallTests(r);
            r.Section("");
        }

        // a record exactly as the mod writes one
        static string Record(string id, string hash, string puid, string code, string time, string detection, bool erased = false)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"format\": \"PocketRoles.AegisEvidence\",\n  \"id\": \"").Append(id).Append("\",\n");
            sb.Append("  \"time\": \"").Append(time).Append("\",\n");
            if (erased) sb.Append("  \"erased\": \"2026-09-01\",\n");
            sb.Append("  \"player\": { \"name\": \"Alice\", \"hash\": \"").Append(hash).Append("\", \"puidHash\": \"").Append(puid)
              .Append("\", \"eraseCode\": \"").Append(code).Append("\" },\n");
            sb.Append("  \"room\": \"ABCDEF\",\n  \"log\": [\"12:00:00Z ").Append(detection).Append("\"]\n}");
            return sb.ToString();
        }

        static string Hex(char c) => new string(c, 64);

        // ------------------------------------------------------------------ masking
        static void MaskTests(SelfTestRunner r)
        {
            r.Section("report: masking");
            r.Test("what never leaves this PC", () =>
            {
                const string home = @"C:\Users\someone";
                r.Equal("the home folder", @"%USERPROFILE%\Desktop", Mask.Home(@"C:\Users\someone\Desktop", home));
                r.Equal("... whatever its case", "%USERPROFILE%", Mask.Home(@"c:\users\SOMEONE", home));
                // 2026-09-26: a path can reach a log in its 8.3 short form (%TEMP% is one way), and only the long form
                // was masked - so the first six letters of the account name rode along in the report zip. This runs
                // against the REAL home folder of whoever runs the self-test, because the short name is Windows's to
                // decide: on a volume with short names switched off there is nothing to mask and nothing to test.
                string realHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string shortHome = Native.TryGetShortPath(realHome);
                if (string.IsNullOrEmpty(shortHome))
                    r.Info("this volume has 8.3 short names off: nothing is ever written in the short form");
                else
                {
                    r.Equal("the home folder in its 8.3 short form", @"%USERPROFILE%\AppData",
                            Mask.Home(shortHome + @"\AppData", realHome));
                    r.Equal("... and the long form still goes", @"%USERPROFILE%\Desktop",
                            Mask.Home(realHome + @"\Desktop", realHome));
                    r.Check("the short form really is different", !string.Equals(shortHome, realHome, StringComparison.OrdinalIgnoreCase));
                }
                // The shape of a translation API key, built here from repeated digits so that no line of this file
                // ever looks like a real key to a reader or to a secret scanner. It is not one, and it never was.
                const string fakeKey = "11111111-2222-3333-4444-555555555555";
                r.Equal("an API key", "key=<api-key-masked>", Mask.Secrets("key=" + fakeKey, home));
                r.Equal("... with :fx", "<api-key-masked>", Mask.Secrets(fakeKey + ":fx", home));
                r.Equal("a Discord webhook", "<discord-webhook-masked>", Mask.Secrets("https://discord.com/api/webhooks/123/abcDEF", home));
                r.Equal("... discordapp.com too", "<discord-webhook-masked>", Mask.Secrets("https://discordapp.com/api/v10/webhooks/1/x", home));
                r.Equal("a PUID", "puid=<puid-masked>", Mask.Identities("puid=0123456789abcdef0123456789abcdef"));
                r.Equal("a friend code", "<friend-code-masked> joined", Mask.Identities("bravecat#1234 joined"));
                r.Equal("... written with the wide #", "<friend-code-masked>", Mask.Identities("bravecat\uFF031234"));
                // the expression is the launcher's character for character. A wide \uFF03 is not in its lookbehind, so a
                // friend code straight after one is still masked (v0.3 review: the port had one extra character here
                // and masked LESS than the launcher, letting a friend code into a report zip).
                r.Equal("... straight after a wide #", "\u540D\u524D\uFF03<friend-code-masked>", Mask.Identities("\u540D\u524D\uFF03Taro#1234"));
                r.Equal("... and after an ordinary # it is still left alone, like the launcher", "x#bravecat#1234", Mask.Identities("x#bravecat#1234"));
                r.Equal("an erase code", "id: <erase-code-masked>", Mask.Identities("id: BDFG HJKM NPRS TVXZ"));
                r.Equal("the mod's short hash", "hash ********\u2026", Mask.Identities("hash 1a2b3c4d\u2026"));
                r.Equal("a 32-hex word inside another word is left alone", "x0123456789abcdef0123456789abcdefy", Mask.Identities("x0123456789abcdef0123456789abcdefy"));
                r.Equal("ordinary text is untouched", "Alice was voted out", Mask.Identities("Alice was voted out"));
            });
            r.Test("an evidence record keeps what the ban console needs", () =>
            {
                string text = Record("AEG-AAAA1", Hex('a'), Hex('b'), "BDFGHJKMNPRSTVXZ", "2026-09-20T12:00:00Z", "CheatDetector: speed");
                string outp = Mask.ConvertToReportEvidence(text);
                r.Check("the friend code's hash is replaced by the PUID's", outp != null && outp.IndexOf(Hex('a'), StringComparison.Ordinal) < 0 && outp.IndexOf("\"hash\": \"" + Hex('b') + "\"", StringComparison.Ordinal) >= 0, outp);
                r.Check("the PUID's hash and the erase code stay whole (the console matches on them)",
                    outp.IndexOf("\"puidHash\": \"" + Hex('b') + "\"", StringComparison.Ordinal) >= 0 && outp.IndexOf("BDFGHJKMNPRSTVXZ", StringComparison.Ordinal) >= 0);
                r.Check("a file that is not the shape the mod writes is left out", Mask.ConvertToReportEvidence("{\"hash\":\"\",\"hash\":\"\"}") == null);
                r.Check("nothing is not a record either", Mask.ConvertToReportEvidence("") == null);
            });
        }

        // ------------------------------------------------------------------ the text rules shared with the mod
        static void EvidenceTextTests(SelfTestRunner r)
        {
            r.Section("report: evidence text");
            r.Test("the rules the ban console also uses", () =>
            {
                r.Equal("identities become # so two copies of a line match", "a # b", EvidenceText.Norm("a 0123456789abcdef0123456789abcdef b"));
                r.Equal("a masked one matches the same way", "a # b", EvidenceText.Norm("a <puid-masked> b"));
                var trail = new List<string> { "11:59:00Z Chat: hello", "12:00:00Z CheatDetector: speed 12.5 for bravecat#1234\u2026" };
                r.Equal("the detection is the last CheatDetector line, normalized", "CheatDetector: speed 12.5 for #", EvidenceText.Detection(trail));
                r.Equal("no CheatDetector line: nothing", "", EvidenceText.Detection(new List<string> { "12:00:00Z Chat: hi" }));
                r.Equal("nothing at all", "", EvidenceText.Detection(null));

                var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["AEG-AAAA1"] = "CheatDetector: speed" };
                var lines = new[]
                {
                    "12:00:00Z CheatDetector: speed for bravecat#1234",
                    "12:00:01Z Chat: something else",
                    "12:00:02Z AegisEvidence: AEG-AAAA1 written (1 line)",
                };
                var found = EvidenceText.Backing(lines, wanted);
                r.Check("two lines back the record: the detection and the one that wrote it",
                    found.ContainsKey("AEG-AAAA1") && found["AEG-AAAA1"].Count == 2 && found["AEG-AAAA1"][0] == lines[0] && found["AEG-AAAA1"][1] == lines[2],
                    found.ContainsKey("AEG-AAAA1") ? string.Join(" | ", found["AEG-AAAA1"].ToArray()) : "none");
                r.Check("a record nobody asked for is not collected", EvidenceText.Backing(lines, new Dictionary<string, string>()).Count == 0);
                r.Check("a log without the written line gives nothing", EvidenceText.Backing(new[] { lines[0] }, wanted).Count == 0);
            });
            r.Test("what a person may type", () =>
            {
                // the 16th letter is a check letter: these are worked out, not guessed
                string code = MakeCode("BDFGHJKMNPRSTVX");
                r.Equal("an erase code", code, EvidenceText.NormalizeEraseCode(code));
                r.Equal("... in groups of four", code, EvidenceText.NormalizeEraseCode(code.Substring(0, 4) + " " + code.Substring(4, 4) + "-" + code.Substring(8, 4) + "\u30FC" + code.Substring(12)));
                r.Equal("... in lower case and full width", code, EvidenceText.NormalizeEraseCode(ToWide(code.ToLowerInvariant())));
                r.Equal("one letter wrong: not a code (the check letter)", "", EvidenceText.NormalizeEraseCode(code.Substring(0, 15) + (code[15] == 'B' ? 'D' : 'B')));
                r.Equal("a letter that is not in the alphabet", "", EvidenceText.NormalizeEraseCode("AAAAAAAAAAAAAAAA"));
                r.Equal("too short", "", EvidenceText.NormalizeEraseCode("BDFG"));
                r.Equal("an evidence id", "AEG-7F3K2", EvidenceText.EvidenceId(" aeg-7f3k2 "));
                r.Equal("not an evidence id", "", EvidenceText.EvidenceId("AEG-7F3K2X9"));
                string h = EvidenceText.FriendCodeHash("bravecat#1234");
                r.Check("a friend code hashes to 64 hex digits", h.Length == 64, h);
                r.Equal("... the same whatever its case", h, EvidenceText.FriendCodeHash("BraveCat#1234"));
                r.Equal("something that is not a friend code", "", EvidenceText.FriendCodeHash("bravecat"));
            });
        }

        /// <summary>The 16th letter of an erase code is worked out from the first 15 (the mod's check letter).</summary>
        static string MakeCode(string first15)
        {
            const string alphabet = "BDFGHJKMNPRSTVXZ";
            foreach (char c in alphabet)
            {
                string t = first15 + c;
                if (EvidenceText.NormalizeEraseCode(t) == t) return t;
            }
            throw new InvalidOperationException("no check letter");
        }

        static string ToWide(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(c >= '!' && c <= '~' ? (char)(c - '!' + '\uFF01') : c);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the records on disk
        static ModPaths FakeGame(SelfTestRunner r, string name)
        {
            string modded = Path.Combine(r.NewDir(name), "Among Us PocketRoles");
            Directory.CreateDirectory(Path.Combine(modded, @"BepInEx\PocketRoles\evidence"));
            Directory.CreateDirectory(Path.Combine(modded, @"BepInEx\PocketRoles\logs"));
            return ModPaths.For(modded);
        }

        static EvidenceStore StoreFor(ModPaths p, DateTime now) =>
            new EvidenceStore { Paths = p, NowUtc = () => now, UserProfile = @"C:\Users\someone" };

        static void EvidenceStoreTests(SelfTestRunner r)
        {
            r.Section("report: evidence records");
            r.Test("90 days, and what a report zip may hold", () =>
            {
                var p = FakeGame(r, "ev");
                string dir = Path.Combine(p.Modded, @"BepInEx\PocketRoles\evidence");
                var now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
                void Write(string id, int daysAgo, string hash, string puid, string code = "", bool erased = false)
                {
                    string f = Path.Combine(dir, id + ".json");
                    File.WriteAllText(f, Record(id, hash, puid, code, now.AddDays(-daysAgo).ToString("yyyy-MM-ddTHH:mm:ssZ"), "CheatDetector: speed", erased), new UTF8Encoding(false));
                    File.SetLastWriteTimeUtc(f, now.AddDays(-daysAgo));
                }
                Write("AEG-NEW01", 1, Hex('a'), Hex('b'));
                Write("AEG-OLD89", 89, Hex('c'), Hex('d'));
                Write("AEG-OLD91", 91, Hex('e'), Hex('f'));
                File.WriteAllText(Path.Combine(dir, "notes.txt"), "not a record");
                File.WriteAllText(Path.Combine(dir, "AEG-BAD.json"), "{}");

                var store = StoreFor(p, now);
                var forReport = store.ForReport();
                var names = new List<string>();
                foreach (var e in forReport) names.Add(e.Name);
                names.Sort(StringComparer.Ordinal);
                r.Equal("the last 90 days only, newest first", "AEG-NEW01.json,AEG-OLD89.json", string.Join(",", names.ToArray()));
                r.Check("a record of 91 days ago is not in it (the mod deletes it at 90)", !names.Contains("AEG-OLD91.json"));
                r.Check("a file that is not a record is never taken", !names.Contains("notes.txt") && !names.Contains("AEG-BAD.json"));
                r.Check("the friend code's hash is gone from what goes in the zip", forReport[0].Text.IndexOf(Hex('a'), StringComparison.Ordinal) < 0);
                r.Equal("the caps are the launcher's", "90/200/5.0 MB", EvidenceStore.ReportDays + "/" + EvidenceStore.ReportMax + "/" + ReportBuilder.FormatSize(EvidenceStore.ReportBytes));

                var one = store.Read(Path.Combine(dir, "AEG-NEW01.json"));
                r.Check("a record is read with its hashes and its detection",
                    one != null && one.Id == "AEG-NEW01" && one.Puid == Hex('b') && one.Detection == "CheatDetector: speed", one == null ? "null" : one.Detection);
                r.Check("a file that is not a record reads as nothing", store.Read(Path.Combine(dir, "notes.txt")) == null);
            });

            r.Test("the two lines are kept before the log that holds them goes", () =>
            {
                var p = FakeGame(r, "backing");
                string dir = Path.Combine(p.Modded, @"BepInEx\PocketRoles\evidence");
                var now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
                File.WriteAllText(Path.Combine(dir, "AEG-AAAA1.json"),
                    Record("AEG-AAAA1", Hex('a'), Hex('b'), "", now.AddDays(-40).ToString("yyyy-MM-ddTHH:mm:ssZ"), "CheatDetector: speed"));
                string log = Path.Combine(p.LogArchiveDir, "LogOutput-2026-08-14_120000.log");
                File.WriteAllText(log, string.Join("\r\n", new[]
                {
                    "12:00:00Z CheatDetector: speed for bravecat#1234",
                    "12:00:02Z AegisEvidence: AEG-AAAA1 written (1 line)",
                }));
                var store = StoreFor(p, now);
                var wanted = store.BackingWanted();
                r.Check("the record with no <id>.log yet is wanted", wanted.ContainsKey("AEG-AAAA1"), string.Join(",", new List<string>(wanted.Keys).ToArray()));
                r.Equal("one backing file is written", 1, store.SaveBackingFrom(log, wanted));
                string kept = Path.Combine(dir, "AEG-AAAA1.log");
                r.Check("... next to the record", File.Exists(kept));
                string text = File.ReadAllText(kept);
                r.Check("it says the log was deleted at 30 days, and holds both lines",
                    text.IndexOf("kept after that log was deleted at 30 days", StringComparison.Ordinal) > 0 &&
                    text.IndexOf("CheatDetector: speed", StringComparison.Ordinal) > 0 &&
                    text.IndexOf("AegisEvidence: AEG-AAAA1 written", StringComparison.Ordinal) > 0, text);
                r.Check("the record is no longer wanted", !wanted.ContainsKey("AEG-AAAA1"));
                r.Check("and a second pass writes nothing", store.BackingWanted().Count == 0);
                string masked = store.MaskedBacking("AEG-AAAA1", false);
                r.Check("reading it back for a zip masks the friend code in it", masked != null && masked.IndexOf("bravecat#1234", StringComparison.Ordinal) < 0 && masked.IndexOf("<friend-code-masked>", StringComparison.Ordinal) > 0, masked);
                r.Check("a record an erase request emptied never gets one", store.MaskedBacking("AEG-AAAA1", true) == null);
            });

            r.Test("whose records may go into a one-player zip", () =>
            {
                var p = FakeGame(r, "keep");
                string dir = Path.Combine(p.Modded, @"BepInEx\PocketRoles\evidence");
                var now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
                string code = MakeCode("BDFGHJKMNPRSTVX");
                void Write(string id, int daysAgo, bool erased = false, string c = "")
                {
                    File.WriteAllText(Path.Combine(dir, id + ".json"),
                        Record(id, Hex('a'), Hex('b'), c, now.AddDays(-daysAgo).ToString("yyyy-MM-ddTHH:mm:ssZ"), "CheatDetector: speed", erased));
                }
                Write("AEG-KEEP1", 10, false, code);
                Write("AEG-BLNK1", 11, true, code);
                Write("AEG-OLD95", 95, false, code);
                Write("AEG-BAN99", 120, false, code);   // named by a ban still in force: kept whatever its age
                File.WriteAllText(Path.Combine(p.Modded, @"BepInEx\PocketRoles\aegis-bans.json"),
                    "{\"bans\":[{\"eraseCode\":\"\",\"hash\":\"" + Hex('a') + "\",\"evidence\":\"AEG-BAN99\"}]}");

                var store = StoreFor(p, now);
                var all = EvidenceStore.SelectPlayer(store.ReadAll(), new ExportKey { Kind = "code", Value = code });
                r.Equal("every record of that player is found", 4, all.Count);
                var left = new EvidenceStore.LeftOut();
                var mine = store.KeepForExport(all, left);
                var ids = new List<string>();
                foreach (var m in mine) ids.Add(m.Id);
                ids.Sort(StringComparer.Ordinal);
                r.Equal("only what this PC may still keep", "AEG-BAN99,AEG-KEEP1", string.Join(",", ids.ToArray()));
                r.Equal("the emptied one is left out", 1, left.Blanked);
                r.Equal("the one past 90 days is left out", 1, left.Old);
                r.Check("the ban file keeps the evidence of a ban in force", ids.Contains("AEG-BAN99"));
                r.Check("the ids of a ban file are read even from a file we cannot parse", store.EnforcedIds().Contains("AEG-BAN99"));

                // the erase list of the cached rules: a request leaves that player's older records out
                File.WriteAllText(Path.Combine(p.Modded, @"BepInEx\PocketRoles\aegis-rules-cache.txt"),
                    "[erase]\r\n" + code + " 2026-09-20\r\n");
                var cutoffs = store.EraseCutoffs();
                r.Check("the erase list is read (the date plus the grace days)", cutoffs.ContainsKey(code) && cutoffs[code] == new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc), cutoffs.ContainsKey(code) ? cutoffs[code].ToString("o") : "none");
                left = new EvidenceStore.LeftOut();
                mine = store.KeepForExport(all, left);
                ids.Clear();
                foreach (var m in mine) ids.Add(m.Id);
                r.Equal("a record from before the request is left out; the ban in force stays", "AEG-BAN99", string.Join(",", ids.ToArray()));
                // the erase list is checked before the 90 days, so the old one is counted as erased too (as the launcher does)
                r.Equal("... and both of that player's older records count as erased", 2, left.Erased);
            });

            r.Test("how a person is looked up", () =>
            {
                string code = MakeCode("BDFGHJKMNPRSTVX");
                r.Equal("an evidence id", "id", EvidenceStore.ResolveKey("aeg-7f3k2").Kind);
                r.Equal("an erase code", "code", EvidenceStore.ResolveKey(code).Kind);
                var friend = EvidenceStore.ResolveKey("bravecat#1234");
                r.Equal("a friend code", "friend", friend.Kind);
                r.Check("... kept only as its hash", friend.Value.Length == 64 && friend.Value.IndexOf("bravecat", StringComparison.OrdinalIgnoreCase) < 0);
                r.Check("anything else is not a key", EvidenceStore.ResolveKey("who?") == null && EvidenceStore.ResolveKey("") == null);
            });
        }

        // ------------------------------------------------------------------ the report zip
        static ReportBuilder BuilderFor(SelfTestRunner r, ModPaths p, string name, DateTime now)
        {
            string home = r.NewDir(name + "-home");
            string desktop = Path.Combine(home, "Desktop");
            string cache = Path.Combine(home, "Temp", "PocketRolesLauncher");
            Directory.CreateDirectory(desktop);
            Directory.CreateDirectory(cache);
            return new ReportBuilder
            {
                Paths = p,
                Desktop = desktop,
                CacheDir = cache,
                StatePath = SelfTestRunner.Touch(Path.Combine(home, AppInfo.StateFileName), "{\"steamDir\":\"D:\\\\Steam\"}"),
                ClientLogPath = SelfTestRunner.Touch(Path.Combine(home, "client.log"), "[12:00:00] started\r\n"),
                SteamDir = null,
                Lang = () => "ja",
                Now = () => now,
                UserProfile = home,
                Evidence = StoreFor(p, now.ToUniversalTime()),
            };
        }

        static List<string> ZipEntries(string zip)
        {
            var names = new List<string>();
            using (var za = ZipFile.OpenRead(zip)) foreach (var e in za.Entries) names.Add(e.FullName);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        static string ZipText(string zip, string entry)
        {
            using (var za = ZipFile.OpenRead(zip))
            {
                var e = za.GetEntry(entry);
                if (e == null) return null;
                using (var sr = new StreamReader(e.Open(), Encoding.UTF8, true)) return sr.ReadToEnd();
            }
        }

        static void ReportZipTests(SelfTestRunner r)
        {
            r.Section("report zip");
            r.Test("what goes in, and what never does", () =>
            {
                var p = FakeGame(r, "rep");
                var now = new DateTime(2026, 9, 23, 13, 45, 0, DateTimeKind.Local);
                var b = BuilderFor(r, p, "rep", now);
                Directory.CreateDirectory(Path.Combine(p.Modded, @"BepInEx\config"));
                File.WriteAllText(p.LogPath, "12:00:00Z /ban 0123456789abcdef0123456789abcdef\r\n12:00:01Z bravecat#1234 joined\r\n");
                File.WriteAllText(p.CfgPath, "webhook = https://discord.com/api/webhooks/1/secret\r\n");
                File.WriteAllText(Path.Combine(p.LogArchiveDir, "LogOutput-2026-09-20_120000.log"), "old session\r\n");
                File.WriteAllText(Path.Combine(p.Modded, @"BepInEx\PocketRoles\evidence\AEG-AAAA1.json"),
                    Record("AEG-AAAA1", Hex('a'), Hex('b'), "", now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), "CheatDetector: speed"));

                var res = b.MakeReport();
                r.Check("a zip is made", res.Ok && File.Exists(res.Zip), res.Error);
                r.Equal("named by the minute", "PocketRoles-report-20260923-1345.zip", res.Name);
                var names = ZipEntries(res.Zip);
                r.Check("the current log, the state, the app's log, the past session and system.txt are in it",
                    names.Contains("LogOutput.log") && names.Contains(AppInfo.StateFileName) && names.Contains("client.log") &&
                    names.Contains("LogOutput-2026-09-20_120000.log") && names.Contains("system.txt"), string.Join(",", names.ToArray()));
                r.Check("the mod's config is NOT (the Discord webhook lives there)", !names.Contains(Path.GetFileName(p.CfgPath)));
                r.Check("the evidence record is under evidence/ with a forward slash", names.Contains("evidence/AEG-AAAA1.json"), string.Join(",", names.ToArray()));
                string log = ZipText(res.Zip, "LogOutput.log");
                r.Check("the PUID and the friend code in the log are masked",
                    log.IndexOf("<puid-masked>", StringComparison.Ordinal) > 0 && log.IndexOf("<friend-code-masked>", StringComparison.Ordinal) > 0 &&
                    log.IndexOf("bravecat#1234", StringComparison.Ordinal) < 0, log);
                string sys = ZipText(res.Zip, "system.txt");
                r.Check("system.txt names the app and the 90 days, with the home folder masked",
                    sys.IndexOf(AppInfo.Name, StringComparison.Ordinal) >= 0 && sys.IndexOf("last 90 days", StringComparison.Ordinal) > 0 &&
                    sys.IndexOf(b.UserProfile, StringComparison.OrdinalIgnoreCase) < 0, sys);
                r.Equal("one record went in", 1, res.Records);
                string ev = ZipText(res.Zip, "evidence/AEG-AAAA1.json");
                r.Check("the record's friend-code hash is replaced by the PUID's", ev.IndexOf(Hex('a'), StringComparison.Ordinal) < 0 && ev.IndexOf(Hex('b'), StringComparison.Ordinal) > 0);
            });

            r.Test("a huge past log keeps its start and its end", () =>
            {
                string dir = r.NewDir("capped");
                string big = Path.Combine(dir, "big.log");
                var sb = new StringBuilder();
                for (int i = 0; i < 40000; i++) sb.Append("line ").Append(i).Append("\r\n");
                File.WriteAllText(big, sb.ToString());
                long len = new FileInfo(big).Length;
                string text = TextFiles.ReadCapped(big, 64 * 1024, ReportBuilder.FormatSize);
                r.Check("it is cut down", text.Length < len, text.Length + " of " + len);
                r.Check("the first lines are there", text.StartsWith("line 0\r\n", StringComparison.Ordinal));
                r.Check("the last lines are there", text.EndsWith("line 39999\r\n", StringComparison.Ordinal), text.Substring(Math.Max(0, text.Length - 40)));
                r.Check("and it says what was left out", text.IndexOf("left out of the report", StringComparison.Ordinal) > 0);
                string small = SelfTestRunner.Touch(Path.Combine(dir, "small.log"), "short\r\n");
                r.Equal("a small file is not cut at all", "short\r\n", TextFiles.ReadCapped(small, 64 * 1024, ReportBuilder.FormatSize));
            });
        }

        // ------------------------------------------------------------------ one player's evidence
        static void ExportOneTests(SelfTestRunner r)
        {
            r.Section("one player's evidence");
            r.Test("only that player, and only what this PC may keep", () =>
            {
                var p = FakeGame(r, "exp");
                var now = new DateTime(2026, 9, 23, 13, 45, 30, DateTimeKind.Local);
                var b = BuilderFor(r, p, "exp", now);
                string dir = Path.Combine(p.Modded, @"BepInEx\PocketRoles\evidence");
                string code = MakeCode("BDFGHJKMNPRSTVX");
                string utc = now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                File.WriteAllText(Path.Combine(dir, "AEG-MINE1.json"), Record("AEG-MINE1", Hex('a'), Hex('b'), code, utc, "CheatDetector: speed"));
                File.WriteAllText(Path.Combine(dir, "AEG-MINE2.json"), Record("AEG-MINE2", Hex('a'), Hex('b'), code, utc, "CheatDetector: vision"));
                File.WriteAllText(Path.Combine(dir, "AEG-OTHER.json"), Record("AEG-OTHER", Hex('c'), Hex('d'), "", utc, "CheatDetector: speed"));
                File.WriteAllText(p.LogPath, string.Join("\r\n", new[]
                {
                    "12:00:00Z CheatDetector: speed for bravecat#1234",
                    "12:00:02Z AegisEvidence: AEG-MINE1 written (1 line)",
                }));

                var res = b.ExportOne(code);
                r.Check("a zip is made", res.Ok && File.Exists(res.Zip), res.Error);
                r.Check("named by the second", res.Name.StartsWith("PocketRoles-evidence-20260923-134530", StringComparison.Ordinal), res.Name);
                var names = ZipEntries(res.Zip);
                r.Check("both of that player's records are in it",
                    names.Contains("evidence/AEG-MINE1.json") && names.Contains("evidence/AEG-MINE2.json"), string.Join(",", names.ToArray()));
                r.Check("nobody else's is", !names.Contains("evidence/AEG-OTHER.json"));
                r.Check("export.txt, system.txt and the state are in it", names.Contains("export.txt") && names.Contains("system.txt") && names.Contains(AppInfo.StateFileName));
                r.Check("the backing lines found in the log that is still here", names.Contains("evidence/AEG-MINE1.log"));
                string backing = ZipText(res.Zip, "evidence/AEG-MINE1.log");
                r.Check("... and they say the log is still on this PC (not that it was deleted)",
                    backing.IndexOf("the log is still on the host PC", StringComparison.Ordinal) > 0 && backing.IndexOf("deleted at 30 days", StringComparison.Ordinal) < 0, backing);
                r.Check("the friend code in those lines is masked", backing.IndexOf("bravecat#1234", StringComparison.Ordinal) < 0);
                string json = ZipText(res.Zip, "evidence/AEG-MINE1.json");
                r.Check("the friend code's hash is not in the zip; the PUID's and the erase code are",
                    json.IndexOf(Hex('a'), StringComparison.Ordinal) < 0 && json.IndexOf(Hex('b'), StringComparison.Ordinal) > 0 && json.IndexOf(code, StringComparison.Ordinal) > 0);
                string export = ZipText(res.Zip, "export.txt");
                r.Check("export.txt says it is for the author only and names the 90 days",
                    export.IndexOf("for the author only", StringComparison.Ordinal) > 0 && export.IndexOf(AppInfo.MailHost, StringComparison.Ordinal) > 0 &&
                    export.IndexOf("kept 90 days", StringComparison.Ordinal) > 0, export);
                r.Check("it never says which friend code was typed", export.IndexOf("bravecat", StringComparison.OrdinalIgnoreCase) < 0);
                r.Equal("the answer carries the ids so the host sees whose zip it is", 2, res.Ids.Length);

                r.Check("a second export in the same second does not overwrite the first", b.ExportOne(code).Name.EndsWith("-2.zip", StringComparison.Ordinal));
                var bad = b.ExportOne("who?");
                r.Check("something that is not a code gives no zip", !bad.Ok && bad.Error == S.T("ja", "ex_bad"), bad.Error);
                var none = b.ExportOne("nobody#0000");
                r.Check("a friend code with no records gives no zip", !none.Ok && none.Error == S.T("ja", "ex_none"), none.Error);
            });
        }

        // ------------------------------------------------------------------ the 30 days of the zips
        static void EvidenceExpiryTests(SelfTestRunner r)
        {
            r.Section("report zips: 30 days");
            r.Test("report and one-player zips go by the time in their name", () =>
            {
                string root = r.NewDir("zipexp");
                string desktop = Path.Combine(root, "Desktop");
                var p = FakeGame(r, "zipexp-game");
                Directory.CreateDirectory(desktop);
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                string Name(string kind, int daysAgo, string format, string tail = "") =>
                    SelfTestRunner.Touch(Path.Combine(desktop, "PocketRoles-" + kind + "-" + now.AddDays(-daysAgo).ToString(format) + tail + ".zip"));
                string repOld = Name("report", 31, "yyyyMMdd-HHmm");
                string repNew = Name("report", 29, "yyyyMMdd-HHmm");
                string evOld = Name("evidence", 31, "yyyyMMdd-HHmm");
                string evNew = Name("evidence", 29, "yyyyMMdd-HHmm");
                string evSec = Name("evidence", 31, "yyyyMMdd-HHmmss");
                string evSec2 = Name("evidence", 31, "yyyyMMdd-HHmmss", "-2");
                string other = SelfTestRunner.Touch(Path.Combine(desktop, "holiday-photos.zip"));

                var gl = new GameLogs
                {
                    LogPath = p.LogPath, LogArchiveDir = p.LogArchiveDir, Desktop = desktop,
                    CacheDir = Path.Combine(root, "cache"), Now = () => now, MutexName = @"Local\StarPocket.SelfTest.logs",
                };
                gl.RemoveExpired();
                r.Check("a report zip of 31 days ago goes, one of 29 stays", !File.Exists(repOld) && File.Exists(repNew));
                r.Check("a one-player zip goes the same way", !File.Exists(evOld) && File.Exists(evNew));
                r.Check("... also when its name goes to the second, with or without -2", !File.Exists(evSec) && !File.Exists(evSec2));
                r.Check("somebody else's zip on the Desktop is never touched", File.Exists(other));
            });

            r.Test("a log about to go leaves its two lines behind first", () =>
            {
                var p = FakeGame(r, "expback");
                var now = new DateTime(2026, 9, 23, 12, 0, 0);
                string dir = Path.Combine(p.Modded, @"BepInEx\PocketRoles\evidence");
                File.WriteAllText(Path.Combine(dir, "AEG-AAAA1.json"),
                    Record("AEG-AAAA1", Hex('a'), Hex('b'), "", now.AddDays(-40).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), "CheatDetector: speed"));
                string old = Path.Combine(p.LogArchiveDir, "LogOutput-" + now.AddDays(-31).ToString("yyyy-MM-dd_HHmmss") + ".log");
                File.WriteAllText(old, "12:00:00Z CheatDetector: speed for x\r\n12:00:02Z AegisEvidence: AEG-AAAA1 written (1 line)\r\n");
                File.SetLastWriteTime(old, now.AddDays(-31));

                var gl = new GameLogs
                {
                    Evidence = StoreFor(p, now.ToUniversalTime()),
                    LogPath = p.LogPath, LogArchiveDir = p.LogArchiveDir, Desktop = Path.Combine(r.Root, "no-desktop"),
                    CacheDir = Path.Combine(r.Root, "no-cache"), Now = () => now, MutexName = @"Local\StarPocket.SelfTest.logs",
                };
                gl.RemoveExpired();
                r.Check("the log is gone", !File.Exists(old));
                r.Equal("one backing file was kept", 1, gl.BackingSaved);
                r.Check("... and it is there", File.Exists(Path.Combine(dir, "AEG-AAAA1.log")));
            });
        }

        // ------------------------------------------------------------------ shortcuts, start with Windows
        /// <summary>The self-test's own registry corner. The real Run key and the real Apps &amp; Features key are never
        /// opened by a test: AutoStart / AppsAndFeatures take their root from a seam, and it points here.</summary>
        const string TestKeyRoot = @"Software\StarPocketClient-SelfTest";

        static RegistryKey TestRoot(SelfTestRunner r, string name) => Registry.CurrentUser.CreateSubKey(TestKeyRoot + "\\" + name);

        /// <summary>Takes the whole corner away again, so a test run leaves nothing behind in the registry.</summary>
        static void DropTestKeys()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(TestKeyRoot, false); } catch (Exception) { }
        }

        static void ShortcutTests(SelfTestRunner r)
        {
            r.Section("shortcut / start with Windows");
            r.Test("a shortcut is written only when it is asked for", () =>
            {
                string root = r.NewDir("shortcut");
                string desktop = Path.Combine(root, "Desktop");
                string programs = Path.Combine(root, "Programs");
                Directory.CreateDirectory(desktop);
                string exe = SelfTestRunner.Touch(Path.Combine(root, "app", AppInfo.Name + ".exe"));

                r.Check("nothing is there to start with", !Shortcuts.Exists(ShortcutPlace.Desktop, desktop, programs));
                string lnk = Shortcuts.Create(ShortcutPlace.Desktop, exe, desktop, programs);
                r.Check("the button writes one", File.Exists(lnk) && lnk.EndsWith(AppInfo.Name + ".lnk", StringComparison.Ordinal), lnk);
                r.Equal("it points at this exe", exe, ShellLink.TargetPath(lnk));
                r.Check("and the app can tell it is ours", Shortcuts.PointsAtUs(lnk, exe) && !Shortcuts.PointsAtUs(lnk, @"C:\somewhere\else.exe"));
                string start = Shortcuts.Create(ShortcutPlace.StartMenu, exe, desktop, programs);
                r.Check("the Start menu one is written too", File.Exists(start), start);
                // on a real PC that folder is <the viewer's own Start menu>\Programs\StarPocket Games (never the
                // machine-wide one, which would need administrator)
                r.Equal("and its folder is the brand's", AppInfo.Company, Path.GetFileName(Shortcuts.StartMenuDir()));
                r.Check("removing takes the file and the empty folder", Shortcuts.Remove(ShortcutPlace.StartMenu, exe, desktop, programs) && !File.Exists(start));
                // a .lnk of the same name that is not ours
                ShellLink.Save(Shortcuts.PathFor(ShortcutPlace.Desktop, desktop, programs), SelfTestRunner.Touch(Path.Combine(root, "other.exe")), root, "other", null);
                r.Check("a shortcut that points somewhere else is never deleted", !Shortcuts.Remove(ShortcutPlace.Desktop, exe, desktop, programs) && Shortcuts.Exists(ShortcutPlace.Desktop, desktop, programs));
            });

            r.Test("start with Windows is one value, and only when it is turned on", () =>
            {
                var old = AutoStart.OpenUserRoot;
                using (var key = TestRoot(r, "autostart"))
                {
                    AutoStart.OpenUserRoot = () => Registry.CurrentUser.CreateSubKey(TestKeyRoot + @"\autostart");
                    try
                    {
                        r.Check("off to start with", !AutoStart.IsOn());
                        r.Check("turning it on writes the value", AutoStart.Set(true, @"C:\app\x.exe") && AutoStart.IsOn());
                        using (var run = key.OpenSubKey(AutoStart.RunKey))
                            r.Equal("... quoted, under the Run key, with our own name", "\"C:\\app\\x.exe\"", Convert.ToString(run.GetValue(AutoStart.ValueName)));
                        r.Check("turning it off takes it away", AutoStart.Set(false, null) && !AutoStart.IsOn());
                        r.Check("turning it off again is not an error", AutoStart.Set(false, null));
                        r.Equal("the page's switch value", true, Bridge.OnOff("on"));
                        r.Equal("... and off", false, Bridge.OnOff("false"));
                        r.Check("anything else is refused", !Bridge.OnOff("maybe").HasValue);
                    }
                    finally { AutoStart.OpenUserRoot = old; }
                }
                DropTestKeys();
            });

            r.Test("the Apps & Features entry", () =>
            {
                var old = AppsAndFeatures.OpenUserRoot;
                AppsAndFeatures.OpenUserRoot = () => Registry.CurrentUser.CreateSubKey(TestKeyRoot + @"\arp");
                try
                {
                    r.Check("there is none until something installs", AppsAndFeatures.Read() == null);
                    r.Check("the setup writes one", AppsAndFeatures.Register(@"C:\app\x.exe", @"C:\app"));
                    var v = AppsAndFeatures.Read();
                    r.Check("with the name, the publisher and an uninstall command that is our own exe",
                        v != null && v["DisplayName"] == AppInfo.Name && v["Publisher"] == AppInfo.Company &&
                        v["UninstallString"] == "\"C:\\app\\x.exe\" --uninstall", v == null ? "none" : v["UninstallString"]);
                    r.Check("the uninstall takes it away", AppsAndFeatures.Unregister() && AppsAndFeatures.Read() == null);
                }
                finally
                {
                    AppsAndFeatures.OpenUserRoot = old;
                    DropTestKeys();
                }
            });
        }

        // ------------------------------------------------------------------ the uninstall
        /// <summary>Uninstaller.Run reads and may delete the "start with Windows" value and the Apps &amp; Features
        /// entry, so every Run() in these tests works in the self-test's own corner of HKCU - never the real Run key.</summary>
        static void WithTestRegistry(Action body)
        {
            var a = AutoStart.OpenUserRoot;
            var b = AppsAndFeatures.OpenUserRoot;
            AutoStart.OpenUserRoot = () => Registry.CurrentUser.CreateSubKey(TestKeyRoot + @"\un-autostart");
            AppsAndFeatures.OpenUserRoot = () => Registry.CurrentUser.CreateSubKey(TestKeyRoot + @"\un-arp");
            try { body(); }
            finally { AutoStart.OpenUserRoot = a; AppsAndFeatures.OpenUserRoot = b; DropTestKeys(); }
        }

        static void UninstallTests(SelfTestRunner r)
        {
            r.Section("uninstall");
            r.Test("what it removes and what it must never touch", () =>
            {
                string root = r.NewDir("uninstall");
                string local = Path.Combine(root, "Local");
                string dataDir = Path.Combine(local, AppInfo.DataFolderRelative);
                string exeDir = Path.Combine(local, "Programs", "StarPocket Client");
                string exe = SelfTestRunner.Touch(Path.Combine(exeDir, AppInfo.Name + ".exe"));
                string desktop = Path.Combine(root, "Desktop");
                string programs = Path.Combine(root, "Programs");
                string steam = Path.Combine(root, "Steam", "steamapps", "common", "Among Us");
                var p = FakeGame(r, "uninstall-game");
                SelfTestRunner.Touch(p.GameExe);   // it is the game's copy, and since the v0.4 review that is asked
                foreach (var d in new[] { dataDir, desktop, steam }) Directory.CreateDirectory(d);
                SelfTestRunner.Touch(Path.Combine(dataDir, "settings.json"), "{}");
                SelfTestRunner.Touch(Path.Combine(local, @"PocketRoles\Aegis\events.log"), "shared with the old tray");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"));
                Shortcuts.Create(ShortcutPlace.Desktop, exe, desktop, programs);

                var removed = new List<string>();
                var un = new Uninstaller
                {
                    DataDir = dataDir, ExePath = exe, ExeDir = exeDir, LocalAppData = local,
                    Paths = p, SteamDir = steam, DesktopDir = desktop, StartMenuDir = programs,
                    DeleteFolder = d => removed.Add("dir " + d),
                    DeleteFile = f => removed.Add("file " + f),
                };

                var plan = un.Plan(false);
                r.Check("the app's own data goes", plan.Folders.Contains(dataDir));
                r.Check("the shortcut it made goes", plan.Files.Contains(Shortcuts.PathFor(ShortcutPlace.Desktop, desktop, programs)));
                r.Check("the mod copy is kept unless it is ticked", plan.ModCopy == null && plan.Kept.Contains(p.Modded));
                r.Check("... and ticking it names it", un.Plan(true).ModCopy == p.Modded);
                r.Check("Steam is named as kept, never as removed", plan.Kept.Contains(steam) && !plan.Folders.Contains(steam));
                r.Check("the Aegis data the old tray uses is named as kept", plan.Kept.Contains(Path.Combine(local, @"PocketRoles\Aegis")));
                r.Check("the program folder of a per-user install is ours to remove", plan.ProgramFolder == exeDir);

                UninstallResult res = null; WithTestRegistry(() => res = un.Run(false));
                r.Check("it runs without failures", res.Ok, res.Error);
                r.Check("the data folder and the shortcut were the things deleted",
                    removed.Contains("dir " + dataDir) && removed.Contains("file " + Shortcuts.PathFor(ShortcutPlace.Desktop, desktop, programs)), string.Join(" | ", removed.ToArray()));
                r.Check("the mod copy was not", !removed.Contains("dir " + p.Modded));
                r.Check("the viewer is told the app's own folder goes after it closes", res.RestartNeeded && res.Kept.Contains(exeDir) && res.LeftFolder == exeDir && !res.LeftFolderIsTheirs);

                // the guard itself: a path that is not ours is refused even if it somehow reached the plan
                r.Check("Steam is refused", !un.Allowed(Path.Combine(steam, "Among Us.exe"), true));
                r.Check("the shared Aegis data is refused", !un.Allowed(Path.Combine(local, @"PocketRoles\Aegis"), true));
                r.Check("... and the folder that holds it", !un.Allowed(Path.Combine(local, "PocketRoles"), true));
                r.Check("the viewer's Documents are refused", !un.Allowed(Path.Combine(root, "Documents"), true));
                r.Check("the mod copy only when it is asked for", !un.Allowed(p.Modded, false) && un.Allowed(p.Modded, true));
                r.Check("the app's own data always", un.Allowed(Path.Combine(dataDir, "settings.json"), false));
                r.Check("a folder that only looks like a prefix is not inside it", !Uninstaller.Inside(dataDir + "-other", dataDir));
                r.Check("the same folder written two ways is one folder", Uninstaller.Same(dataDir, dataDir + "\\") && Uninstaller.Same(dataDir + "\\x\\..", dataDir));

                // a copy the person unpacked themselves: kept as well, but said in different words (we cannot tell what
                // else of theirs is in that folder)
                var loose = new Uninstaller { DataDir = dataDir, ExePath = exe, ExeDir = Path.Combine(root, "MyTools"), LocalAppData = local, Paths = p, SteamDir = steam, DesktopDir = desktop, StartMenuDir = programs };
                var loosePlan = loose.Plan(false);
                r.Check("an unpacked copy's folder is kept, not deleted", loosePlan.ProgramFolder == Path.Combine(root, "MyTools") && loosePlan.ProgramFolderIsTheirs && loosePlan.Kept.Contains(Path.Combine(root, "MyTools")));
                r.Check("... and is refused by the guard", !loose.Allowed(Path.Combine(root, @"MyTools\notes.txt"), true));
                r.Check("... and it is named in the answer too (it used to be left with no word at all)",
                    loose.Plan(false).ProgramFolder != null);
            });

            // v0.4 review: where the mod copy is can be said by --game-dir or by POCKETROLES_GAMEDIR - the same
            // variable the PowerShell launcher reads, so a stale setting on somebody's PC could point this at a folder
            // of their own. The ticked box would then have handed that whole folder to DeleteTree.
            r.Test("a \"mod copy\" with no game in it is kept, not emptied", () =>
            {
                string root = r.NewDir("uninstall-notagame");
                string local = Path.Combine(root, "Local");
                string dataDir = Path.Combine(local, AppInfo.DataFolderRelative);
                string exeDir = Path.Combine(local, "Programs", "StarPocket Client");
                string exe = SelfTestRunner.Touch(Path.Combine(exeDir, AppInfo.Name + ".exe"));
                string theirs = Path.Combine(root, "My Work");         // what POCKETROLES_GAMEDIR is pointing at
                SelfTestRunner.Touch(Path.Combine(theirs, "notes.txt"), "a year of work");
                Directory.CreateDirectory(dataDir);

                var removed = new List<string>();
                var un = new Uninstaller
                {
                    DataDir = dataDir, ExePath = exe, ExeDir = exeDir, LocalAppData = local, Paths = ModPaths.For(theirs),
                    DesktopDir = Path.Combine(root, "Desktop"), StartMenuDir = Path.Combine(root, "Programs"),
                    DeleteFolder = d => removed.Add(d), DeleteFile = f => removed.Add(f),
                };
                r.Check("there is no Among Us.exe in it", !un.LooksLikeGameCopy());
                r.Check("so ticking the box does not make it removable", !un.Allowed(theirs, true));
                r.Check("... nor anything in it", !un.Allowed(Path.Combine(theirs, "notes.txt"), true));
                var plan = un.Plan(true);
                r.Check("the plan does not promise to remove it", plan.ModCopy == null);
                r.Check("... and names it as kept instead", plan.Kept.Contains(theirs), string.Join(" | ", plan.Kept.ToArray()));
                UninstallResult res = null; WithTestRegistry(() => res = un.Run(true));
                r.Check("nothing of theirs was touched", !removed.Contains(theirs) && !removed.Contains(Path.Combine(theirs, "notes.txt")), string.Join(" | ", removed.ToArray()));
                r.Check("and the uninstall itself still succeeded", res.Ok, res.Error);
                r.Check("their file is still there", File.Exists(Path.Combine(theirs, "notes.txt")));
                // put the game in it and the same folder becomes removable again
                SelfTestRunner.Touch(Path.Combine(theirs, "Among Us.exe"));
                r.Check("with a game in it, the box works as before", un.LooksLikeGameCopy() && un.Allowed(theirs, true));
            });

            // the OneDrive setup: the Desktop is redirected, so GameFolders.ResolveModded puts the mod's game copy at
            // %LOCALAPPDATA%\PocketRoles\Among Us PocketRoles - right beside the records the PowerShell tray keeps. The
            // checkbox has to be able to remove that copy and must still never touch those records (v0.3 review: the
            // guard was the whole PocketRoles tree, so the dialog promised 1 GB the uninstall then refused).
            r.Test("the mod copy inside %LOCALAPPDATA%\\PocketRoles (a Desktop in OneDrive)", () =>
            {
                string root = r.NewDir("uninstall-onedrive");
                string local = Path.Combine(root, "Local");
                string dataDir = Path.Combine(local, AppInfo.DataFolderRelative);
                string exeDir = Path.Combine(local, "Programs", "StarPocket Client");
                string exe = SelfTestRunner.Touch(Path.Combine(exeDir, AppInfo.Name + ".exe"));
                string modded = Path.Combine(local, "PocketRoles", GameFolders.CopyFolderName);
                var paths = ModPaths.For(modded);
                SelfTestRunner.Touch(Path.Combine(modded, "Among Us.exe"));
                SelfTestRunner.Touch(Path.Combine(local, @"PocketRoles\Aegis\events.log"), "the old tray's records");
                Directory.CreateDirectory(dataDir);

                var removed = new List<string>();
                var un = new Uninstaller
                {
                    DataDir = dataDir, ExePath = exe, ExeDir = exeDir, LocalAppData = local, Paths = paths,
                    DesktopDir = Path.Combine(root, "Desktop"), StartMenuDir = Path.Combine(root, "Programs"),
                    DeleteFolder = d => removed.Add(d), DeleteFile = f => removed.Add(f),
                };
                r.Check("ticking the box may remove it", un.Allowed(modded, true));
                r.Check("... and leaving it unticked may not", !un.Allowed(modded, false));
                r.Check("the tray's records are still out of bounds", !un.Allowed(Path.Combine(local, @"PocketRoles\Aegis"), true) &&
                    !un.Allowed(Path.Combine(local, @"PocketRoles\Aegis\events.log"), true));
                r.Check("the folder they share is never removed", !un.Allowed(Path.Combine(local, "PocketRoles"), true));
                r.Equal("the plan names it", modded, un.Plan(true).ModCopy);
                UninstallResult res = null; WithTestRegistry(() => res = un.Run(true));
                r.Check("and it really goes", res.Ok && removed.Contains(modded), string.Join(" | ", removed.ToArray()) + " / " + res.Error);
            });

            // the app's own data folder is the first thing to go, and when it will not go nothing else is touched:
            // otherwise the app is left unregistered with all its files still there and no way to press again
            r.Test("the data folder goes first, and a failure stops everything", () =>
            {
                string root = r.NewDir("uninstall-stop");
                string local = Path.Combine(root, "Local");
                string dataDir = Path.Combine(local, AppInfo.DataFolderRelative);
                string exeDir = Path.Combine(local, "Programs", "StarPocket Client");
                string exe = SelfTestRunner.Touch(Path.Combine(exeDir, AppInfo.Name + ".exe"));
                string desktop = Path.Combine(root, "Desktop"), programs = Path.Combine(root, "Programs");
                var p = FakeGame(r, "uninstall-stop-game");
                Directory.CreateDirectory(dataDir);
                Directory.CreateDirectory(desktop);
                Shortcuts.Create(ShortcutPlace.Desktop, exe, desktop, programs);

                var removed = new List<string>();
                var un = new Uninstaller
                {
                    DataDir = dataDir, ExePath = exe, ExeDir = exeDir, LocalAppData = local, Paths = p,
                    DesktopDir = desktop, StartMenuDir = programs,
                    DeleteFolder = d => { if (d == dataDir) throw new IOException("in use"); removed.Add(d); },
                    DeleteFile = f => removed.Add(f),
                };
                UninstallResult res = null; WithTestRegistry(() => res = un.Run(true));
                r.Check("it says so", !res.Ok && res.StoppedAtDataDir && res.Failed.Contains(dataDir));
                r.Check("nothing else was deleted", removed.Count == 0, string.Join(" | ", removed.ToArray()));
                r.Check("the shortcut is still there", File.Exists(Shortcuts.PathFor(ShortcutPlace.Desktop, desktop, programs)));
                r.Check("the mod copy was not touched either", !removed.Contains(p.Modded));

                // and the retries: WebView2 lets go of the profile folder a moment after the window is gone
                int tries = 0;
                var slow = new Uninstaller
                {
                    DataDir = dataDir, ExePath = exe, ExeDir = exeDir, LocalAppData = local, Paths = p,
                    DesktopDir = desktop, StartMenuDir = programs, DeleteAttempts = 4, Sleep = _ => { },
                    DeleteFolder = d => { if (d == dataDir && ++tries < 3) throw new IOException("still in use"); },
                    DeleteFile = f => { },
                };
                UninstallResult ok = null; WithTestRegistry(() => ok = slow.Run(false));
                r.Check("a folder that is free a moment later is removed", ok.Ok && tries == 3, "tries: " + tries + " " + ok.Error);
            });

            r.Test("DeleteTree", () =>
            {
                string root = r.NewDir("deletetree");
                string tree = Path.Combine(root, "tree");
                SelfTestRunner.Touch(Path.Combine(tree, @"a\b\c.txt"));
                string ro = SelfTestRunner.Touch(Path.Combine(tree, "read-only.txt"));
                File.SetAttributes(ro, FileAttributes.ReadOnly);
                Uninstaller.DeleteTree(tree);
                r.Check("the whole tree goes, read-only files too", !Directory.Exists(tree));
                Uninstaller.DeleteTree(tree);   // a folder that is not there is not an error
                r.Check("a folder that is already gone is not an error", !Directory.Exists(tree));
            });

            r.Test("a shortcut that points somewhere else is left alone", () =>
            {
                string root = r.NewDir("uninstall-lnk");
                string desktop = Path.Combine(root, "Desktop"), programs = Path.Combine(root, "Programs");
                string ours = SelfTestRunner.Touch(Path.Combine(root, "app", AppInfo.Name + ".exe"));
                string other = SelfTestRunner.Touch(Path.Combine(root, "other", AppInfo.Name + ".exe"));
                Directory.CreateDirectory(desktop);
                Shortcuts.Create(ShortcutPlace.Desktop, other, desktop, programs);
                string lnk = Shortcuts.PathFor(ShortcutPlace.Desktop, desktop, programs);
                r.Check("it is not ours", !Shortcuts.PointsAtUs(lnk, ours));
                r.Check("... and ours is", Shortcuts.PointsAtUs(lnk, other));
                var un = new Uninstaller { DataDir = Path.Combine(root, "data"), ExePath = ours, ExeDir = Path.Combine(root, "app"), LocalAppData = Path.Combine(root, "Local"), DesktopDir = desktop, StartMenuDir = programs };
                r.Check("so the plan leaves it out", !un.Plan(false).Files.Contains(lnk));
            });
        }
    }
}
