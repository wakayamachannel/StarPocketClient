// The 13 checks of the Aegis scan (aegis\Aegis.ps1 classes Check / Scanner and the step logic of Splash, v0.5.5 lines
// 260-268, 595-857, 906-946): real checks of this PC's mod setup, ported line for line (PORT-MAP 3.9).
// Read-only: files of the game copy, registry values any user can read, and the process NAME list; no handle to another
// process is opened. The reading of this PC (registry, process names) goes through IAegisSystem so the self-test decides
// on given values; files are read from the folders it is given (the self-test's fake folders).
// The ps1 ran the checks one by one on its UI thread with a short pause each for the animation; the app runs them on a
// worker thread without the pauses (PORT-MAP 3.6, 9.2 A-12). The decisions are the same.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Starpocket.Client.Core;

namespace Starpocket.Client.Aegis
{
    /// <summary>A registry key opened for reading.</summary>
    internal interface IRegKey : IDisposable
    {
        object GetValue(string name);
        int SubKeyCount { get; }
    }

    /// <summary>What the scan reads of this PC besides files.</summary>
    internal interface IAegisSystem
    {
        /// <summary>HKLM (64-bit view) key, or null when it is missing or cannot be opened (the ps1's Hklm()).</summary>
        IRegKey OpenHklm(string path);

        /// <summary>The names of the running processes ("" for a process whose name cannot be read). May throw.</summary>
        string[] ProcessNames();
    }

    /// <summary>The real PC: RegistryKey.OpenBaseKey(LocalMachine, Registry64) and Process.GetProcesses().</summary>
    internal sealed class RealAegisSystem : IAegisSystem
    {
        public static readonly RealAegisSystem Instance = new RealAegisSystem();

        sealed class Key : IRegKey
        {
            readonly RegistryKey k;
            public Key(RegistryKey k) { this.k = k; }
            public object GetValue(string name) => k.GetValue(name);
            public int SubKeyCount => k.SubKeyCount;
            public void Dispose() => k.Dispose();
        }

        public IRegKey OpenHklm(string path)
        {
            try
            {
                var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(path);
                return k == null ? null : new Key(k);
            }
            catch (Exception) { return null; }
        }

        public string[] ProcessNames()
        {
            var list = new List<string>();
            foreach (var p in Process.GetProcesses())
            {
                string n;
                try { n = p.ProcessName; } catch (Exception) { n = ""; }
                p.Dispose();
                list.Add(n);
            }
            return list.ToArray();
        }
    }

    /// <summary>One row of the scan (the ps1's Check). Detail and Fix are drawn in the language of the moment.</summary>
    internal sealed class AegisCheck
    {
        /// <summary>The text key of the title (engine, game, bep, mod, plug, inj, cfg, ban, sb, tpm, kern, vdb, tools).</summary>
        public string Key = "";
        public AegisTextRef Detail = AegisTextRef.Empty;
        /// <summary>How to fix a failed check (shown under the rows and in the launcher's message).</summary>
        public AegisTextRef Fix = AegisTextRef.Empty;
        /// <summary>0 waiting, 1 running, 2 ok, 3 warning, 4 serious (stops a launch).</summary>
        public int State;
        /// <summary>Cheat-related: an unknown plugin / injected DLL / cheat tool / test-signing / a changed mod.</summary>
        public bool SeriousOnFail;
        public Func<AegisCheck, bool> Run;

        public string Title(string lang) => AegisText.Get(lang, Key);
        public bool HasFix(string lang) => Fix != null && Fix.Render(lang).Length > 0;
    }

    internal sealed class ScanOutcome
    {
        public List<AegisCheck> Rows;
        public int Warnings, Serious;
        /// <summary>The caller stopped waiting (the pre-launch deadline) before the last row.</summary>
        public bool Cancelled;

        /// <summary>The keys of the red rows (mod, plug, inj, kern, tools). A block whose only red row is "mod" is the one
        /// thing the app can put right by itself: the play button becomes 「修復」 (PORT-MAP 13.3, error code R-41).</summary>
        public string[] RedKeys()
        {
            var keys = new List<string>();
            foreach (var c in Rows) if (c.State == 4) keys.Add(c.Key);
            return keys.ToArray();
        }

        /// <summary>The red rows as "&lt;title&gt;: &lt;detail&gt;" plus " → &lt;fix&gt;" (Entry.PreLaunch: the lines of prelaunch-result.txt).</summary>
        public string[] RedLines(string lang)
        {
            var lines = new List<string>();
            foreach (var c in Rows)
                if (c.State == 4)
                {
                    string fix = c.Fix != null ? c.Fix.Render(lang) : "";
                    lines.Add(c.Title(lang) + ": " + c.Detail.Render(lang) + (fix.Length > 0 ? " → " + fix : ""));
                }
            return lines.ToArray();
        }
    }

    internal static class AegisScanner
    {
        public static string SupportedGame => AppInfo.SupportedGameVersion;

        /// <summary>The 13 checks in the ps1's order (Scanner.Build).</summary>
        public static List<AegisCheck> Build(string gameDir, string stateDir, DefinitionsStore defs, IAegisSystem sys)
        {
            var list = new List<AegisCheck>();
            string bep = Path.Combine(gameDir, "BepInEx");
            list.Add(new AegisCheck { Key = "engine", Run = c =>
            {
                // v0.5.5: only a definitions file whose signature verifies is used (SigState)
                if (defs.SigState == 0) { c.Detail = AegisTextRef.Of("engine.ok", AppInfo.AegisRuleCount, defs.Version); return true; }
                if (defs.SigState == 2) { c.Detail = AegisTextRef.Of("engine.nosig"); return false; }
                c.Detail = AegisTextRef.Of("engine.nofile", AppInfo.AegisRuleCount); return true;
            } });
            list.Add(new AegisCheck { Key = "game", Run = c =>
            {
                if (!File.Exists(Path.Combine(gameDir, "Among Us.exe"))) { c.Detail = AegisTextRef.Of("game.none"); return false; }
                string v = GameVersion(gameDir);
                if (v == null) { c.Detail = AegisTextRef.Of("game.nover"); return true; }
                if (v == SupportedGame) { c.Detail = AegisTextRef.Of("game.ok", v); return true; }
                c.Detail = AegisTextRef.Of("game.other", v, SupportedGame); return false;
            } });
            list.Add(new AegisCheck { Key = "bep", Run = c =>
            {
                bool ok = File.Exists(Path.Combine(bep, "core", "BepInEx.Core.dll")) && File.Exists(Path.Combine(gameDir, "winhttp.dll"));
                c.Detail = AegisTextRef.Of(ok ? "bep.ok" : "bep.none"); return ok;
            } });
            list.Add(new AegisCheck { Key = "mod", SeriousOnFail = true, Run = c => ModIntegrity(c, Path.Combine(bep, "plugins", "PocketRoles.dll"), stateDir) });
            list.Add(new AegisCheck { Key = "plug", SeriousOnFail = true, Run = c =>
            {
                var others = new List<string>();
                string dir = Path.Combine(bep, "plugins");
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories))
                        if (!string.Equals(Path.GetFileName(f), "PocketRoles.dll", StringComparison.OrdinalIgnoreCase)) others.Add(Path.GetFileName(f));
                if (others.Count == 0) { c.Detail = AegisTextRef.Of("plug.ok"); return true; }
                c.Detail = AegisTextRef.Of("plug.warn", string.Join(", ", others.ToArray()));
                c.Fix = AegisTextRef.Of("plug.fix", string.Join(", ", others.ToArray())); return false;
            } });
            list.Add(new AegisCheck { Key = "inj", SeriousOnFail = true, Run = c => Injection(c, gameDir, defs) });
            list.Add(new AegisCheck { Key = "cfg", Run = c =>
            {
                string cfg = Path.Combine(bep, "config", "jp.pocketroles.mod.cfg");
                if (!File.Exists(cfg)) { c.Detail = AegisTextRef.Of("cfg.none"); return true; }
                var v = ReadSection(cfg, "AntiCheat");
                Func<string, bool> on = k => { string x; return !v.TryGetValue(k, out x) || x.Trim().ToLowerInvariant() != "false"; };
                Func<bool, AegisTextRef> onoff = b => AegisTextRef.Of(b ? "on" : "off");
                if (!on("Detect")) { c.Detail = AegisTextRef.Of("cfg.off"); return false; }
                c.Detail = AegisTextRef.Of("cfg.ok", onoff(true), onoff(on("AutoKick")), onoff(on("AnnounceKick")), onoff(on("Callout"))); return true;
            } });
            list.Add(new AegisCheck { Key = "ban", Run = c =>
            {
                int n = 0;
                string f = Path.Combine(bep, "PocketRoles", "Banlist.txt");
                if (File.Exists(f))
                    foreach (var line in File.ReadAllLines(f, Encoding.UTF8))
                    {
                        // the mod's rules: a trailing "// comment" is dropped; ";" / "#" start a comment line
                        string t = line;
                        int cm = t.IndexOf("//", StringComparison.Ordinal);
                        if (cm >= 0) t = t.Substring(0, cm);
                        t = t.Trim();
                        if (t.Length > 0 && t[0] != ';' && t[0] != '#') n++;
                    }
                c.Detail = AegisTextRef.Of("ban.ok", n); return true;
            } });
            list.Add(new AegisCheck { Key = "sb", Run = c => SecureBoot(c, sys) });
            list.Add(new AegisCheck { Key = "tpm", Run = c => Tpm(c, sys) });
            list.Add(new AegisCheck { Key = "kern", SeriousOnFail = true, Run = c => Kernel(c, sys) });
            list.Add(new AegisCheck { Key = "vdb", Run = c => DriverBlocklist(c, sys) });   // v0.5.5, warning only: never stops a launch
            list.Add(new AegisCheck { Key = "tools", SeriousOnFail = true, Run = c => CheatTools(c, defs, sys) });
            return list;
        }

        /// <summary>
        /// Runs the checks in order, like the ps1's Splash: a row is "running" (1) while it runs; a check that throws shows
        /// the exception's message, no fix, and never stops a launch (Aegis's own failure, 9.2 A-7); a failed row is 4 when
        /// it is cheat-related, else 3. <paramref name="beforeStep"/> gets the 1-based number of the row about to run;
        /// <paramref name="setState"/> stores a row's state (the caller may lock around it). Stops between rows when
        /// <paramref name="cancel"/> is set.
        /// </summary>
        public static ScanOutcome Run(List<AegisCheck> checks, Action<int> beforeStep, Action<AegisCheck, int> setState, CancellationToken cancel)
        {
            var o = new ScanOutcome { Rows = checks };
            setState = setState ?? ((c, s) => c.State = s);
            for (int i = 0; i < checks.Count; i++)
            {
                if (cancel.IsCancellationRequested) { o.Cancelled = true; return o; }
                var c = checks[i];
                setState(c, 1);
                beforeStep?.Invoke(i + 1);
                bool ok;
                bool crashed = false;
                try { ok = c.Run(c); }
                catch (Exception ex) { c.Detail = AegisTextRef.Plain(ex.Message); c.Fix = AegisTextRef.Empty; ok = false; crashed = true; }
                if (crashed) c.SeriousOnFail = false;   // Aegis's own failure never stops a launch
                setState(c, ok ? 2 : c.SeriousOnFail ? 4 : 3);
                if (!ok) { o.Warnings++; if (c.SeriousOnFail) o.Serious++; }
            }
            return o;
        }

        // ---- this PC (read-only: registry values any user can read, the process NAME list; no handles to other processes)

        static bool SecureBoot(AegisCheck c, IAegisSystem sys)
        {
            using (var k = sys.OpenHklm(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
            {
                if (k == null) { c.Detail = AegisTextRef.Of("sb.unknown"); return true; }
                object v = k.GetValue("UEFISecureBootEnabled");
                bool on = v is int && (int)v == 1;
                c.Detail = AegisTextRef.Of(on ? "sb.on" : "sb.off"); return on;
            }
        }

        static bool Tpm(AegisCheck c, IAegisSystem sys)
        {
            // every TPM 2.0 (firmware or discrete) is the ACPI device MSFT0101
            using (var k = sys.OpenHklm(@"SYSTEM\CurrentControlSet\Enum\ACPI\MSFT0101"))
            {
                bool ok = k != null && k.SubKeyCount > 0;
                c.Detail = AegisTextRef.Of(ok ? "tpm.ok" : "tpm.none"); return ok;
            }
        }

        static bool Kernel(AegisCheck c, IAegisSystem sys)
        {
            string opts = "";
            using (var k = sys.OpenHklm(@"SYSTEM\CurrentControlSet\Control")) { if (k != null) opts = (k.GetValue("SystemStartOptions") as string) ?? ""; }
            var bad = new List<string>();
            foreach (var tok in opts.ToUpperInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (tok == "TESTSIGNING") bad.Add("TESTSIGNING");
                else if (tok == "DEBUG" || tok.StartsWith("DEBUGPORT")) { if (!bad.Contains("DEBUG")) bad.Add("DEBUG"); }
                else if (tok == "DISABLE_INTEGRITY_CHECKS") bad.Add("NOINTEGRITYCHECKS");
            }
            if (bad.Count > 0)
            {
                string fix = bad.Contains("TESTSIGNING") ? "testsigning" : bad.Contains("DEBUG") ? "debug" : "nointegritychecks";
                c.Detail = AegisTextRef.Of("kern.warn", string.Join(" / ", bad.ToArray()));
                c.Fix = AegisTextRef.Of("kern.fix", fix); return false;
            }
            c.Detail = AegisTextRef.Of("kern.ok", Hvci(sys) ? (object)AegisTextRef.Of("kern.hvci") : ""); return true;
        }

        // memory integrity (HVCI) is on
        static bool Hvci(IAegisSystem sys)
        {
            using (var k = sys.OpenHklm(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"))
            {
                if (k == null) return false;
                object v = k.GetValue("Enabled");
                return v is int && (int)v == 1;
            }
        }

        // v0.5.5: Microsoft's vulnerable driver blocklist, which keeps known vulnerable signed drivers (the kind cheats load
        // to reach kernel memory) from loading. A warning only, never stops a launch. The toggle writes CI\Config
        // VulnerableDriverBlocklistEnable; Windows 11 22H2 (build 22621) and later have it on by default with no value yet,
        // and memory integrity or Smart App Control enforce it whatever the toggle says.
        static bool DriverBlocklist(AegisCheck c, IAegisSystem sys)
        {
            object v = null;
            using (var k = sys.OpenHklm(@"SYSTEM\CurrentControlSet\Control\CI\Config")) { if (k != null) v = k.GetValue("VulnerableDriverBlocklistEnable"); }
            if (v is int && (int)v == 1) { c.Detail = AegisTextRef.Of("vdb.on"); return true; }
            if (Hvci(sys)) { c.Detail = AegisTextRef.Of("vdb.hvci"); return true; }
            using (var k = sys.OpenHklm(@"SYSTEM\CurrentControlSet\Control\CI\Policy"))
            {
                object s = k != null ? k.GetValue("VerifiedAndReputablePolicyState") : null;   // Smart App Control: 1 = on
                if (s is int && (int)s == 1) { c.Detail = AegisTextRef.Of("vdb.on"); return true; }
            }
            if (v == null && WindowsBuild(sys) >= 22621) { c.Detail = AegisTextRef.Of("vdb.default"); return true; }
            c.Detail = AegisTextRef.Of("vdb.off"); return false;
        }

        // the real build number (Environment.OSVersion reports 6.2 to programs without a manifest)
        static int WindowsBuild(IAegisSystem sys)
        {
            using (var k = sys.OpenHklm(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                int b;
                return k != null && int.TryParse((k.GetValue("CurrentBuildNumber") as string) ?? "", out b) ? b : 0;
            }
        }

        /// <summary>Running processes whose NAME matches a definitions entry (Scanner.FindTools; no handle to any process
        /// is opened). The name is compared without spaces, '-' and '_', in lower case; each name once.</summary>
        public static List<string> FindTools(DefinitionsStore defs, IAegisSystem sys)
        {
            var found = new List<string>();
            foreach (var n in sys.ProcessNames())
            {
                string key = (n ?? "").Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
                foreach (var t in defs.Tools)
                    if (DefinitionsStore.ToolMatch(key, t) && !found.Contains(n)) { found.Add(n); break; }
            }
            return found;
        }

        static bool CheatTools(AegisCheck c, DefinitionsStore defs, IAegisSystem sys)
        {
            var found = FindTools(defs, sys);
            if (found.Count == 0) { c.Detail = AegisTextRef.Of("tools.ok"); return true; }
            c.Detail = AegisTextRef.Of("tools.warn", string.Join(", ", found.ToArray()));
            c.Fix = AegisTextRef.Of("tools.fix", string.Join(", ", found.ToArray())); return false;
        }

        // proxy DLLs next to Among Us.exe: BepInEx uses winhttp.dll; menus like AmongUsMenu / SickoMenu load through version.dll and the like
        static bool Injection(AegisCheck c, string gameDir, DefinitionsStore defs)
        {
            var found = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(gameDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    string n = Path.GetFileName(f).ToLowerInvariant();
                    bool hit = defs.Dlls.Contains(n);
                    foreach (var w in defs.DllWords) if (!hit && n.Contains(w)) hit = true;
                    if (hit) found.Add(Path.GetFileName(f));
                }
            }
            catch (Exception) { }
            if (found.Count == 0) { c.Detail = AegisTextRef.Of("inj.ok"); return true; }
            c.Detail = AegisTextRef.Of("inj.warn", string.Join(", ", found.ToArray()));
            c.Fix = AegisTextRef.Of("inj.fix", string.Join(", ", found.ToArray())); return false;
        }

        /// <summary>Scanner.GameVersion: globalgamemanagers read as Latin-1, the first version not starting with 2022.</summary>
        static string GameVersion(string dir)
        {
            try
            {
                string f = Path.Combine(dir, "Among Us_Data", "globalgamemanagers");
                if (!File.Exists(f)) return null;
                return Core.GameVersion.FromText(Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(f)));
            }
            catch (Exception) { }
            return null;
        }

        static bool ModIntegrity(AegisCheck c, string dll, string stateDir)
        {
            if (!File.Exists(dll)) { c.Detail = AegisTextRef.Of("mod.none"); c.SeriousOnFail = false; return false; }   // not installed yet: the launcher's job
            string ver = "?";
            try { var fv = FileVersionInfo.GetVersionInfo(dll); ver = (fv.ProductVersion ?? fv.FileVersion ?? "?").Split('+')[0]; } catch (Exception) { }
            string sha;
            using (var s = File.Open(dll, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var h = SHA256.Create()) sha = BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
            string state = Path.Combine(stateDir, "mod-fingerprint.txt");
            string prevSha = null, prevVer = null;
            try { if (File.Exists(state)) { var p = File.ReadAllText(state).Trim().Split('|'); if (p.Length >= 2) { prevSha = p[0]; prevVer = p[1]; } } } catch (Exception) { }
            // never overwritten on a mismatch (review 2026-09-21: rewriting it let a tampered DLL pass the next scan);
            // the launcher writes it after its own install / update / build
            if (prevSha == null) { Save(state, sha, ver); c.Detail = AegisTextRef.Of("mod.first", ver); return true; }
            if (prevSha == sha) { c.Detail = AegisTextRef.Of("mod.same", ver); return true; }
            if (prevVer != ver) { Save(state, sha, ver); c.Detail = AegisTextRef.Of("mod.update", ver, prevVer); return true; }
            c.Detail = AegisTextRef.Of("mod.changed", ver);
            c.Fix = AegisTextRef.Of("mod.fix"); return false;
        }

        static void Save(string state, string sha, string ver)
        {
            try { File.WriteAllText(state, sha + "|" + ver); } catch (Exception) { }
        }

        static Dictionary<string, string> ReadSection(string file, string section)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool inside = false;
            foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) { inside = string.Equals(line.Substring(1, line.Length - 2), section, StringComparison.OrdinalIgnoreCase); continue; }
                if (!inside || line.StartsWith("#") || line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq > 0) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return d;
        }
    }
}
