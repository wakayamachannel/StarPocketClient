// aegis\definitions.txt - cheat tool process names, DLL names cheats load through, DLL name keywords (aegis\Aegis.ps1
// class Defs, v0.5.5 lines 376-593). Updated without a release: the tray fetches the newest from GitHub (raw
// main/aegis/definitions.txt and its .sig) once at start into %LOCALAPPDATA%\PocketRoles\Aegis; the newer of the cached
// and the bundled file is used. Only a file whose signature verifies is used or saved; a download is saved only when it is
// newer than the file in use, or its very same bytes (rollback guard). With no verified file the built-in list is used.
//
// The ps1 keeps these as statics of one process (the tray, or a -PreLaunch / -ScanOnly process). The app keeps one
// instance per use instead (PORT-MAP 3.10): the tray's (loaded at start, fetched once, used by "scan again") and a fresh
// one for every pre-launch scan and scan-only - the same results as the ps1's separate processes.
// The rules are ported line for line; only the network call is a parameter (the self-test never downloads).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Starpocket.Client.Aegis
{
    internal sealed class DefinitionsStore
    {
        public const string Url = "https://raw.githubusercontent.com/wakayamachannel/PocketRoles/main/aegis/definitions.txt";
        public const string UserAgent = "Aegis/1.0 (+https://github.com/wakayamachannel/PocketRoles)";
        public const int MaxChars = 64 * 1024;
        public const int TimeoutMs = 8000;

        // never flagged, whatever a definitions file says: processes a host always runs, and the game's own DLLs
        public static readonly string[] KeepProcesses = { "amongus", "steam", "steamwebhelper", "steamservice", "powershell", "explorer", "svchost", "system", "discord", "valorant", "valorantwin64shipping", "riotclientservices", "riotclientux", "vgc", "vgtray", "mumuplayer", "mumunxmain", "mumunxdevice", "chrome", "msedge", "obs64", "claude" };
        public static readonly string[] KeepDlls = { "winhttp.dll", "gameassembly.dll", "unityplayer.dll", "baselib.dll", "steam_api.dll", "steam_api64.dll", "d3dcompiler_47.dll" };

        // the built-in list (version 0), used when no file with a valid signature is found
        public static readonly string[] BuiltInTools = { "cheatengine*", "artmoney*", "wemod", "extremeinjector*", "xenos", "xenos64", "ghinjector*", "squalr", "speedhack*", "gameguardian", "sickomenu*", "amongusmenu*", "reclass*" };
        public static readonly string[] BuiltInDlls = { "version.dll", "dxgi.dll", "d3d11.dll", "dinput8.dll", "winmm.dll", "dsound.dll", "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll", "opengl32.dll" };
        public static readonly string[] BuiltInDllWords = { "menu", "cheat", "inject" };

        readonly string bundledDir, stateDir;
        readonly DefinitionKey[] trusted;
        readonly string[] revoked;
        string bundled, cached;
        /// <summary>The canonical bytes of the file in use (null: the built-in list); an equal-version download must match them.</summary>
        byte[] inUse;

        public int Version { get; private set; }
        /// <summary>0: a signed file is in use; 1: no definitions file at all (built-in list); 2: no file with a valid signature (built-in list).</summary>
        public int SigState { get; private set; } = 1;
        public List<string> Tools { get; private set; } = new List<string>();
        public List<string> Dlls { get; private set; } = new List<string>();
        public List<string> DllWords { get; private set; } = new List<string>();

        internal sealed class Parsed
        {
            public int Version;
            public List<string> Tools, Dlls, DllWords;
            public byte[] Canon;
        }

        /// <param name="bundledDir">&lt;exe&gt;\aegis (the ps1's own folder); null or empty: no bundled file.</param>
        /// <param name="stateDir">%LOCALAPPDATA%\PocketRoles\Aegis (the cache).</param>
        /// <param name="trusted">The trusted keys (null: <see cref="TrustedKeys.Keys"/>; the self-test passes its own).</param>
        /// <param name="revoked">The revoked key ids (null: <see cref="TrustedKeys.RevokedIds"/>).</param>
        public DefinitionsStore(string bundledDir, string stateDir, DefinitionKey[] trusted = null, string[] revoked = null)
        {
            this.bundledDir = bundledDir;
            this.stateDir = stateDir;
            this.trusted = trusted ?? TrustedKeys.Keys;
            this.revoked = revoked ?? TrustedKeys.RevokedIds;
        }

        int Verify(byte[] canon, string sigText) => DefinitionsSignature.Verify(canon, sigText, trusted, revoked);

        /// <summary>Defs.Load: the verified file with the highest version (the bundled one first, so it wins a tie), else
        /// the built-in list.</summary>
        public DefinitionsStore Load()
        {
            bundled = string.IsNullOrEmpty(bundledDir) ? null : Path.Combine(bundledDir, "definitions.txt");
            cached = Path.Combine(stateDir, "definitions.txt");
            Parsed best = null;
            bool anyFile = false;
            foreach (var f in new[] { bundled, cached })
            {
                if (f == null || !File.Exists(f)) continue;
                anyFile = true;
                var p = ReadVerified(f);
                if (p != null && (best == null || p.Version > best.Version)) best = p;
            }
            if (best != null)
            {
                Version = best.Version; Tools = best.Tools; Dlls = best.Dlls; DllWords = best.DllWords; inUse = best.Canon; SigState = 0;
                return this;
            }
            // no file with a valid signature: the built-in list (v0)
            Version = 0; inUse = null; SigState = anyFile ? 2 : 1;
            Tools = new List<string>(BuiltInTools);
            Dlls = new List<string>(BuiltInDlls);
            DllWords = new List<string>(BuiltInDllWords);
            return this;
        }

        /// <summary>A definitions file and the .sig next to it: parsed only when the signature verifies (null otherwise).</summary>
        Parsed ReadVerified(string file)
        {
            try
            {
                var fi = new FileInfo(file);
                if (!fi.Exists || fi.Length > 4L * MaxChars) return null;
                byte[] canon = DefinitionsSignature.Canonical(File.ReadAllBytes(file));
                string sigText = null;
                var si = new FileInfo(file + ".sig");
                if (si.Exists && si.Length <= DefinitionsSignature.MaxSigChars) sigText = File.ReadAllText(si.FullName, Encoding.UTF8);
                if (Verify(canon, sigText) != DefinitionsSignature.Ok) return null;
                var p = ParseText(Encoding.UTF8.GetString(canon));
                if (p != null) p.Canon = canon;
                return p;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Defs.ParseText (version= as the mod reads it: the first "version" key, any case, spaces around '=').</summary>
        internal static Parsed ParseText(string text)
        {
            try
            {
                if (text == null || text.Length > MaxChars) return null;
                var tools = new List<string>(); var dlls = new List<string>(); var words = new List<string>();
                int version = 0; bool versionSeen = false; List<string> cur = null;
                foreach (var raw in text.Split('\n'))
                {
                    string line = raw.Trim().TrimStart((char)0xFEFF).Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq > 0 && !line.StartsWith("[") && string.Equals(line.Substring(0, eq).Trim(), "version", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!versionSeen)
                        {
                            versionSeen = true;
                            if (!int.TryParse(line.Substring(eq + 1).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out version)) version = 0;
                        }
                        continue;
                    }
                    if (line == "[tools]") { cur = tools; continue; }
                    if (line == "[dlls]") { cur = dlls; continue; }
                    if (line == "[dllwords]") { cur = words; continue; }
                    if (line.StartsWith("[")) { cur = null; continue; }
                    if (cur != null) cur.Add(line.ToLowerInvariant());
                    if (tools.Count + dlls.Count + words.Count > 500) return null;
                }
                if (version <= 0 || tools.Count == 0) return null;
                // a broken or hostile file must not stop every launch: short entries and the game's own files are refused
                tools.RemoveAll(t => t.TrimEnd('*').Length < 5 || HitsKeptProcess(t));
                dlls.RemoveAll(d => !d.EndsWith(".dll") || Array.IndexOf(KeepDlls, d) >= 0);
                words.RemoveAll(w => w.Length < 4 || HitsKeptDll(w));
                if (tools.Count == 0) return null;
                return new Parsed { Version = version, Tools = tools, Dlls = dlls, DllWords = words };
            }
            catch (Exception) { return null; }
        }

        static bool HitsKeptProcess(string entry)
        {
            foreach (var k in KeepProcesses) if (ToolMatch(k, entry)) return true;
            return false;
        }

        static bool HitsKeptDll(string word)
        {
            foreach (var k in KeepDlls) if (k.Contains(word)) return true;
            return false;
        }

        /// <summary>A process name (normalized) against one entry: "name*" matches the start, otherwise the whole name.</summary>
        public static bool ToolMatch(string key, string entry) => entry.EndsWith("*") ? key.StartsWith(entry.TrimEnd('*')) : key == entry;

        /// <summary>
        /// Defs.FetchAsync's body (the caller runs it on a background thread, once, when the tray starts): the newest file,
        /// then its .sig only when the file came, then <see cref="Store"/>. Any failure: nothing is saved and nothing is said.
        /// Returns what happened (for client.log only).
        /// </summary>
        public string Fetch(Func<string, int, byte[]> download)
        {
            try
            {
                byte[] data = download(Url, 4 * MaxChars);
                if (data == null) return "definitions download: too large";
                byte[] sig = download(Url + ".sig", DefinitionsSignature.MaxSigChars);
                if (sig == null) return "definitions download: signature too large";
                return Store(data, Encoding.UTF8.GetString(sig)) ? "definitions download: in the cache" : "definitions download: not saved (signature, format or not newer)";
            }
            catch (Exception ex) { return "definitions download: failed (" + ex.GetType().Name + ")"; }
        }

        /// <summary>The ps1's Download: a plain GET (8 s timeouts, the same User-Agent); null when longer than
        /// <paramref name="max"/> bytes. Throws on HTTP / network errors. The only network use of the app.</summary>
        public static byte[] Download(string url, int max)
        {
            // the ps1 adds TLS 1.2 (Windows PowerShell's default may lack it); an app built for .NET 4.8 already lets
            // Windows choose (SystemDefault, TLS 1.2 and newer): OR-ing Tls12 into that would limit it to TLS 1.2 only
            if (ServicePointManager.SecurityProtocol != SecurityProtocolType.SystemDefault)
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = TimeoutMs; req.ReadWriteTimeout = TimeoutMs; req.UserAgent = UserAgent;
            using (var resp = req.GetResponse())
            using (var s = resp.GetResponseStream())
                return ReadLimited(s, max);
        }

        /// <summary>The stream's bytes; null as soon as there are more than <paramref name="max"/>.</summary>
        public static byte[] ReadLimited(Stream s, int max)
        {
            using (var ms = new MemoryStream())
            {
                var buf = new byte[8192];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > max) return null;
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Defs.Store: a downloaded file and its .sig text, saved to the cache (the canonical bytes + the .sig) only when the
        /// signature verifies, the file is well-formed ([tools], at most 64 KB) and it is newer than the file in use, or its
        /// very same bytes (an older signed file, or a different signed file of the same version, is never saved, so the file
        /// in use stays). True when the cache holds this file afterwards. Call after <see cref="Load"/>. The file in use in
        /// memory does not change (the new one is used from the next load: the next pre-launch scan or the next start).
        /// </summary>
        public bool Store(byte[] data, string sigText)
        {
            if (cached == null || data == null) return false;
            byte[] canon = DefinitionsSignature.Canonical(data);
            if (Verify(canon, sigText) != DefinitionsSignature.Ok) return false;
            var p = ParseText(Encoding.UTF8.GetString(canon));
            if (p == null || p.Version < Version) return false;
            if (p.Version == Version && (inUse == null || !SameBytes(inUse, canon))) return false;
            string sigPath = cached + ".sig", tmp = cached + ".tmp", sigTmp = sigPath + ".tmp";
            try
            {
                // already cached (the same bytes with a signature that verifies): nothing to write
                if (File.Exists(cached) && File.Exists(sigPath) && SameBytes(File.ReadAllBytes(cached), canon)
                    && Verify(canon, File.ReadAllText(sigPath, Encoding.UTF8)) == DefinitionsSignature.Ok) return true;
            }
            catch (Exception) { }
            File.WriteAllText(sigTmp, sigText.Trim() + "\n", new UTF8Encoding(false));
            File.WriteAllBytes(tmp, canon);
            Replace(sigTmp, sigPath);
            Replace(tmp, cached);
            return true;
        }

        static void Replace(string tmp, string dest)
        {
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
