// Self-test of the Aegis port (PORT-MAP 8: 5 scan decisions, 6 signature, 7 definitions load / store, 8 the real keys,
// 9-10 log lines and reading, 12 events.log). Every expected value is what aegis\Aegis.ps1 (v0.5.5) decides for the same
// input, worked out from its code (Aegis.ps1 is never run: it opens windows). Nothing real is read: the registry and the
// process list are fakes (IAegisSystem), the game copy, %LOCALAPPDATA%\PocketRoles\Aegis and the bundled folder are fake
// folders inside the self-test folder, the signing keys are made on the spot (ephemeral CNG keys, nothing stored), the
// download is a fake (no network), and no window, toast, timer or mutex is made.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Starpocket.Client.Aegis;
using Starpocket.Client.Core;

namespace Starpocket.Client.SelfTest
{
    internal static class AegisSelfTests
    {
        const string SB = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
        const string TPM = @"SYSTEM\CurrentControlSet\Enum\ACPI\MSFT0101";
        const string CTRL = @"SYSTEM\CurrentControlSet\Control";
        const string HVCI = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
        const string CICFG = @"SYSTEM\CurrentControlSet\Control\CI\Config";
        const string CIPOL = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
        const string NT = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        static readonly DateTime Clock0 = new DateTime(2026, 9, 22, 21, 4, 12);

        public static void Run(SelfTestRunner r)
        {
            using (var keys = new TestKeys())
            {
                TextTests(r);
                SignatureTests(r, keys);
                RealKeyTests(r);
                ParseTests(r);
                LoadTests(r, keys);
                StoreTests(r, keys);
                FetchTests(r, keys);
                ScanTests(r, keys);
                PreLaunchTests(r, keys);
                WatcherTests(r);
                EventsLogTests(r);
                ToastTests(r);
            }
            r.Section("");
        }

        // ================================================================== fakes
        sealed class FakeKey : IRegKey
        {
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            public int SubKeys;
            public bool ThrowOnGet;
            public object GetValue(string name)
            {
                if (ThrowOnGet) throw new System.Security.SecurityException("Requested registry access is not allowed.");
                object v;
                return Values.TryGetValue(name, out v) ? v : null;
            }
            public int SubKeyCount => SubKeys;
            public void Dispose() { }
        }

        sealed class FakeSystem : IAegisSystem
        {
            public readonly Dictionary<string, FakeKey> Keys = new Dictionary<string, FakeKey>(StringComparer.OrdinalIgnoreCase);
            public string[] Names = new string[0];
            public bool ThrowOnProcesses;

            public IRegKey OpenHklm(string path) { FakeKey k; return Keys.TryGetValue(path, out k) ? k : null; }

            public string[] ProcessNames()
            {
                if (ThrowOnProcesses) throw new InvalidOperationException("the process list could not be read");
                return Names;
            }

            public FakeKey Key(string path)
            {
                FakeKey k;
                if (!Keys.TryGetValue(path, out k)) Keys[path] = k = new FakeKey();
                return k;
            }

            public FakeSystem Set(string path, string name, object value) { Key(path).Values[name] = value; return this; }

            /// <summary>A PC with nothing to warn about (Secure Boot, TPM, memory integrity, the blocklist, build 26200).</summary>
            public static FakeSystem Good()
            {
                var s = new FakeSystem();
                s.Set(SB, "UEFISecureBootEnabled", 1);
                s.Key(TPM).SubKeys = 1;
                s.Set(CTRL, "SystemStartOptions", " NOEXECUTE=OPTIN  FVEBOOT=2654208");
                s.Set(HVCI, "Enabled", 1);
                s.Set(CICFG, "VulnerableDriverBlocklistEnable", 1);
                s.Set(NT, "CurrentBuildNumber", "26200");
                s.Names = new[] { "explorer", "steam", "Among Us", "svchost", "" };
                return s;
            }
        }

        /// <summary>Signing keys made on the spot (RSA 3072, ephemeral CNG keys: nothing is written to the key store).</summary>
        sealed class TestKeys : IDisposable
        {
            public readonly TestSigner A = new TestSigner("a1a1a1a1a1a1a1a1");   // trusted
            public readonly TestSigner B = new TestSigner("b2b2b2b2b2b2b2b2");   // trusted, second admin
            public readonly TestSigner X = new TestSigner("c3c3c3c3c3c3c3c3");   // not trusted
            public readonly TestSigner R = new TestSigner("d4d4d4d4d4d4d4d4");   // listed but revoked
            public DefinitionKey[] Trusted => new[] { A.Key, B.Key, R.Key };
            public string[] Revoked => new[] { "D4D4D4D4D4D4D4D4", "cedca02cae60f103" };
            public DefinitionsStore Store(string bundled, string state) => new DefinitionsStore(bundled, state, Trusted, Revoked);
            public void Dispose() { A.Dispose(); B.Dispose(); X.Dispose(); R.Dispose(); }
        }

        sealed class TestSigner : IDisposable
        {
            readonly RSACng rsa = new RSACng(3072);
            public readonly string Id;
            public readonly DefinitionKey Key;

            public TestSigner(string id)
            {
                Id = id;
                var p = rsa.ExportParameters(false);
                Key = new DefinitionKey(id, "<RSAKeyValue><Modulus>" + Convert.ToBase64String(p.Modulus) + "</Modulus><Exponent>" + Convert.ToBase64String(p.Exponent) + "</Exponent></RSAKeyValue>");
            }

            public byte[] SignBytes(byte[] canonical) => rsa.SignData(canonical, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            /// <summary>A .sig as tools\sign-definitions.ps1 writes it: the base64 line, then keyid= (unless null).</summary>
            public string Sig(byte[] canonical, bool withId = true) => Convert.ToBase64String(SignBytes(canonical)) + "\n" + (withId ? "keyid=" + Id + "\n" : "");

            public void Dispose() => rsa.Dispose();
        }

        static string DefsText(int version, params string[] tools) =>
            "# test definitions\nversion=" + version + "\n\n[tools]\n" + string.Join("\n", tools) + "\n\n[dlls]\nversion.dll\nxinput1_3.dll\n\n[dllwords]\nmenu\nhack\n\n[rules]\nkillcooldown=1\n";

        /// <summary>definitions.txt (+ .sig) in <paramref name="dir"/>; signer null = no .sig.</summary>
        static byte[] WriteDefs(string dir, string text, TestSigner signer, bool withId = true, bool bom = false, bool crlf = false)
        {
            Directory.CreateDirectory(dir);
            string t = crlf ? text.Replace("\n", "\r\n") : text;
            byte[] bytes = Encoding.UTF8.GetBytes(t);
            if (bom) bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(bytes).ToArray();
            File.WriteAllBytes(Path.Combine(dir, "definitions.txt"), bytes);
            string sig = Path.Combine(dir, "definitions.txt.sig");
            if (signer != null) File.WriteAllText(sig, signer.Sig(DefinitionsSignature.Canonical(bytes), withId), Utf8NoBom);
            else if (File.Exists(sig)) File.Delete(sig);
            return bytes;
        }

        static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

        // ================================================================== texts
        static void TextTests(SelfTestRunner r)
        {
            r.Section("aegis texts");
            var all = AegisText.All.ToList();
            var bad = all.Where(kv => kv.Value.Length != 3 || kv.Value.Any(string.IsNullOrEmpty)).Select(kv => kv.Key).ToArray();
            r.Check("every key in ja / zh-CN / en", bad.Length == 0, string.Join(",", bad));
            string[] unofficial = { "内鬼", "通风管", "举报", "管道", "放逐" };   // terms-ok (the words to catch)
            var zhBad = all.Where(kv => unofficial.Any(u => kv.Value[1].Contains(u))).Select(kv => kv.Key).ToArray();
            r.Check("Chinese uses the official Among Us terms (PORT-MAP 9.3)", zhBad.Length == 0, string.Join(",", zhBad));
            r.Equal("r.TaskImpostor zh-CN", "伪装者完成任务", AegisText.Rule("zh-CN", "TaskImpostor"));
            r.Equal("r.VentRole zh-CN", "不能用通风口却用了", AegisText.Rule("zh-CN", "VentRole"));
            r.Equal("r.VentFar zh-CN", "远离通风口进入通风口", AegisText.Rule("zh-CN", "VentFar"));
            r.Equal("r.ReportForge zh-CN", "不可能的报告", AegisText.Rule("zh-CN", "ReportForge"));
            r.Equal("r.KillPhase zh-CN (驱逐, the official ExileTextNonConfirm)", "会议或驱逐画面中击杀", AegisText.Rule("zh-CN", "KillPhase"));
            r.Equal("ja and en rule texts unchanged", "インポスターのタスク完了|task done as an impostor", AegisText.Rule("ja", "TaskImpostor") + "|" + AegisText.Rule("en", "TaskImpostor"));
            r.Equal("a rule without a text shows its name (S.Rule)", "SomeNewRule", AegisText.Rule("ja", "SomeNewRule"));
            r.Equal("an unknown key shows the key (S.Get)", "no.such", AegisText.Get("en", "no.such"));
            r.Equal("\"zh\" is Chinese, anything else English (S.Idx)", "扫描完成 — 保护中|Scan complete — protected", AegisText.Get("zh", "done") + "|" + AegisText.Get("fr", "done"));
            r.Equal("23 rule texts (26 rules; the 3 callout rules are never shown)", 23, all.Count(kv => kv.Key.StartsWith("r.")));
            r.Equal("rule count 26 (engine.ok)", "検知ルール 26 件・定義ファイル v2・署名 OK", AegisText.Get("ja", "engine.ok", AppInfo.AegisRuleCount, 2));
            r.Equal("Aegis's mutex name (Aegis.ps1 checks the same one)", @"Local\wakayamachannel.Aegis.AntiCheat", AppInfo.AegisMutexName);
            var tr = AegisTextRef.Of("kern.ok", AegisTextRef.Of("kern.hvci"));
            r.Equal("a text inside a text follows the language (ja)", "テスト署名・デバッグモードなし・メモリ整合性 ON", tr.Render("ja"));
            r.Equal("a text inside a text follows the language (en)", "no test-signing / debug mode · memory integrity on", tr.Render("en"));
        }

        // ================================================================== 6. signature (Sig.Verify)
        static void SignatureTests(SelfTestRunner r, TestKeys k)
        {
            r.Section("signature");
            byte[] data = DefinitionsSignature.Canonical(Bytes(DefsText(3, "cheatengine*")));
            var tr = k.Trusted; var rv = k.Revoked;
            int V(string sig, byte[] d = null) => DefinitionsSignature.Verify(d ?? data, sig, tr, rv);
            const int Ok = DefinitionsSignature.Ok, Missing = DefinitionsSignature.Missing, Invalid = DefinitionsSignature.Invalid;

            r.Equal("trusted key with keyid= -> Ok", Ok, V(k.A.Sig(data)));
            r.Equal("trusted key without keyid= (each key tried) -> Ok", Ok, V(k.B.Sig(data, false)));
            r.Equal("keyid= in any case, CRLF and a BOM in the .sig", Ok, V((char)0xFEFF + "# comment\r\n\r\n" + Convert.ToBase64String(k.A.SignBytes(data)) + "\r\nKEYID= " + k.A.Id.ToUpperInvariant() + " \r\n"));
            r.Equal("untrusted key -> Invalid", Invalid, V(k.X.Sig(data)));
            r.Equal("untrusted key without keyid= -> Invalid", Invalid, V(k.X.Sig(data, false)));
            r.Equal("keyid= names another trusted key -> Invalid (only that key is tried)", Invalid, V(Convert.ToBase64String(k.A.SignBytes(data)) + "\nkeyid=" + k.B.Id + "\n"));
            r.Equal("keyid= of an unknown key -> Invalid", Invalid, V(Convert.ToBase64String(k.A.SignBytes(data)) + "\nkeyid=0000000000000000\n"));
            r.Equal("revoked keyid= -> Invalid", Invalid, V(k.R.Sig(data)));
            r.Equal("revoked key without keyid= -> Invalid (never used even when listed)", Invalid, V(k.R.Sig(data, false)));
            r.Equal("keyid= of the revoked Claude-app key -> Invalid", Invalid, V(Convert.ToBase64String(k.A.SignBytes(data)) + "\nkeyid=cedca02cae60f103\n"));
            r.Equal("no .sig -> Missing", Missing, V(null));
            r.Equal("blank .sig -> Missing", Missing, V(" \r\n\t"));
            r.Equal("only comments -> Invalid", Invalid, V("# nothing\n"));
            r.Equal("only keyid= -> Invalid", Invalid, V("keyid=" + k.A.Id + "\n"));
            string one = Convert.ToBase64String(k.A.SignBytes(data));
            r.Equal("two signature lines -> Invalid", Invalid, V(one + "\n" + one + "\n"));
            r.Equal("not base64 -> Invalid", Invalid, V("this is not base64!\n"));
            r.Equal("longer than 4096 characters -> Invalid", Invalid, V(one + "\n" + new string('#', 4097)));
            byte[] sb = k.A.SignBytes(data);
            r.Equal("signature one byte short -> Invalid", Invalid, V(Convert.ToBase64String(sb.Take(sb.Length - 1).ToArray())));
            r.Equal("signature one byte long -> Invalid", Invalid, V(Convert.ToBase64String(sb.Concat(new byte[] { 0 }).ToArray())));
            byte[] changed = (byte[])data.Clone(); changed[changed.Length - 2] ^= 1;
            r.Equal("one byte of the file changed -> Invalid", Invalid, V(k.A.Sig(data), changed));
            r.Equal("no file bytes -> Invalid", Invalid, DefinitionsSignature.Verify(null, k.A.Sig(data), tr, rv));
            r.Equal("no trusted list -> Invalid", Invalid, DefinitionsSignature.Verify(data, k.A.Sig(data), null, rv));
            r.Equal("a key XML with a bad base64 value is skipped", Ok, DefinitionsSignature.Verify(data, k.A.Sig(data, false), new[] { new DefinitionKey("x", "<RSAKeyValue><Modulus>!!</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>"), k.A.Key }, rv));
            // parity (PORT-MAP 9.2 A-14): FromXmlString throws XmlSyntaxException for malformed XML, which the ps1's catch
            // list does not name, so "never throws" does not hold there; only the built-in key list reaches it (not reachable)
            string thrown = "none";
            try { DefinitionsSignature.Verify(data, k.A.Sig(data, false), new[] { new DefinitionKey("x", "<RSAKeyValue>broken"), k.A.Key }, rv); }
            catch (Exception ex) { thrown = ex.GetType().Name; }
            r.Equal("a malformed key XML throws XmlSyntaxException, like the ps1 (kept)", "XmlSyntaxException", thrown);

            // Canonical: the signature is over the file with a BOM removed and CRLF -> LF (a lone CR kept)
            r.Equal("canonical: BOM removed, CRLF -> LF, lone CR kept", "61 0A 62 0D 63 0A", BitConverter.ToString(DefinitionsSignature.Canonical(new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x0D, 0x0A, 0x62, 0x0D, 0x63, 0x0A })).Replace("-", " "));
            r.Equal("canonical: BOM only at the start", "61 EF BB BF", BitConverter.ToString(DefinitionsSignature.Canonical(new byte[] { 0x61, 0xEF, 0xBB, 0xBF })).Replace("-", " "));
            r.Equal("canonical of null is empty", 0, DefinitionsSignature.Canonical(null).Length);
            byte[] lf = Bytes(DefsText(3, "cheatengine*"));
            byte[] crlfBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes(DefsText(3, "cheatengine*").Replace("\n", "\r\n"))).ToArray();
            r.Equal("the same file with CRLF and a BOM verifies with the LF signature", Ok, V(k.A.Sig(DefinitionsSignature.Canonical(lf)), DefinitionsSignature.Canonical(crlfBom)));
        }

        // ================================================================== 8. the real keys
        static void RealKeyTests(SelfTestRunner r)
        {
            r.Section("real keys");
            r.Equal("one trusted key", 1, TrustedKeys.Keys.Length);
            var key = TrustedKeys.Keys[0];
            byte[] n = Convert.FromBase64String(Regex.Match(key.Xml, "<Modulus>(.*?)</Modulus>").Groups[1].Value);
            byte[] e = Convert.FromBase64String(Regex.Match(key.Xml, "<Exponent>(.*?)</Exponent>").Groups[1].Value);
            r.Equal("RSA 3072", 3072, n.Length * 8 - (n[0] == 0 ? 8 : 0));
            r.Equal("exponent AQAB (65537)", "AQAB", Convert.ToBase64String(e));
            string sha;
            using (var h = SHA256.Create()) sha = BitConverter.ToString(h.ComputeHash(Spki(n, e))).Replace("-", "").ToLowerInvariant();
            r.Equal("SHA-256 of its SubjectPublicKeyInfo", TrustedKeys.OwnerKeySpkiSha256, sha);
            r.Equal("its id is the first 16 hex digits", sha.Substring(0, 16), key.Id);
            r.Equal("the id is 91400fdf0f5af4ca", "91400fdf0f5af4ca", key.Id);
            r.Check("cedca02cae60f103 (made inside the Claude app) is revoked", TrustedKeys.RevokedIds.Contains("cedca02cae60f103") && TrustedKeys.RevokedIds.Length == 1);
            r.Check("the key XML imports", DefinitionsSignature.Verify(new byte[] { 1 }, Convert.ToBase64String(new byte[384])) == DefinitionsSignature.Invalid);

            // aegis\definitions.txt and its .sig are NOT in the repository (the owner's decision: the list of names stays out
            // of the public repo, and the two files are attached to the release instead). A build from a fresh checkout - CI
            // above all - has none next to the exe, and the app then uses the built-in list (DefinitionsStore.Load, SigState
            // 1). So their absence is reported here, never failed; when they are present they are read and verified.
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string bundled = Path.Combine(exeDir, "aegis");
            string f = Path.Combine(bundled, "definitions.txt"), s = f + ".sig";
            if (!File.Exists(f) || !File.Exists(s))
            {
                r.Info("bundled definitions: not next to the exe (" + bundled + "): they are not in the repository but attached to the release; without them the app uses the built-in list (SigState 1)");
            }
            else
            {
                byte[] canon = DefinitionsSignature.Canonical(File.ReadAllBytes(f));
                int v = DefinitionsSignature.Verify(canon, File.ReadAllText(s, Encoding.UTF8));
                var p = DefinitionsStore.ParseText(Encoding.UTF8.GetString(canon));
                string id = Regex.Match(File.ReadAllText(s), @"keyid=(\S+)").Groups[1].Value;
                r.Info("bundled definitions: version " + (p != null ? p.Version.ToString() : "?") + ", " + (p != null ? p.Tools.Count + " tools, " + p.Dlls.Count + " DLLs, " + p.DllWords.Count + " DLL words" : "does not parse")
                    + "; signature with the real keys: " + (v == 0 ? "Ok" : v == 1 ? "Missing" : "Invalid") + " (keyid=" + id + ")"
                    + (v != 0 && TrustedKeys.RevokedIds.Contains(id) ? " - signed with the revoked key: Aegis and the app use the built-in list until the owner re-signs it with 91400fdf0f5af4ca (PORT-MAP 9.1 L-11)" : ""));
            }
        }

        /// <summary>DER SubjectPublicKeyInfo of an RSA public key (rsaEncryption, NULL parameters).</summary>
        static byte[] Spki(byte[] n, byte[] e)
        {
            byte[] rsaKey = Der(0x30, DerInt(n).Concat(DerInt(e)).ToArray());
            byte[] algId = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            byte[] bits = Der(0x03, new byte[] { 0 }.Concat(rsaKey).ToArray());
            return Der(0x30, algId.Concat(bits).ToArray());
        }

        static byte[] DerInt(byte[] v)
        {
            int i = 0;
            while (i < v.Length - 1 && v[i] == 0) i++;
            var b = v.Skip(i).ToArray();
            if ((b[0] & 0x80) != 0) b = new byte[] { 0 }.Concat(b).ToArray();
            return Der(0x02, b);
        }

        static byte[] Der(byte tag, byte[] content)
        {
            var o = new List<byte> { tag };
            int len = content.Length;
            if (len < 0x80) o.Add((byte)len);
            else
            {
                var lb = new List<byte>();
                while (len > 0) { lb.Insert(0, (byte)(len & 0xFF)); len >>= 8; }
                o.Add((byte)(0x80 | lb.Count));
                o.AddRange(lb);
            }
            o.AddRange(content);
            return o.ToArray();
        }

        // ================================================================== 7. ParseText
        static void ParseTests(SelfTestRunner r)
        {
            r.Section("definitions format");
            var p = DefinitionsStore.ParseText((char)0xFEFF + "# c\r\n  Version = 7 \r\nversion=9\r\n[tools]\r\nCheatEngine*\r\n  wemod  \r\n# note\r\n[dlls]\r\nVersion.DLL\r\n[dllwords]\r\nMenu\r\n[rules]\r\nkillcooldown=1\r\nabcdefgh\r\n");
            r.Check("parsed", p != null);
            if (p == null) return;
            r.Equal("the first version= only, spaces and any case", 7, p.Version);
            r.Equal("tools in lower case", "cheatengine*,wemod", string.Join(",", p.Tools));
            r.Equal("dlls in lower case", "version.dll", string.Join(",", p.Dlls));
            r.Equal("dllwords in lower case; [rules] skipped", "menu", string.Join(",", p.DllWords));
            r.Check("version=abc -> 0 -> refused", DefinitionsStore.ParseText("version=abc\n[tools]\ncheatengine*\n") == null);
            r.Check("version=0 -> refused", DefinitionsStore.ParseText("version=0\n[tools]\ncheatengine*\n") == null);
            r.Check("no version -> refused", DefinitionsStore.ParseText("[tools]\ncheatengine*\n") == null);
            r.Check("no [tools] entries -> refused", DefinitionsStore.ParseText("version=3\n[dlls]\nversion.dll\n") == null);
            r.Check("[Tools] is not [tools] (exact) -> refused", DefinitionsStore.ParseText("version=3\n[Tools]\ncheatengine*\n") == null);
            r.Check("an entry before any section is ignored", DefinitionsStore.ParseText("version=3\norphan-entry\n[tools]\ncheatengine*\n").Tools.Count == 1);
            var f = DefinitionsStore.ParseText("version=3\n[tools]\nabcd\nabcd*\nabcde\nsteam*\nsteamwebhelper\namongus\ndiscordx\ndiscor*\nclaude\nobs64\nvalorant*\n[dlls]\nevil.dl\nwinhttp.dll\nWINHTTP.DLL\nversion.dll\nsteam_api64.dll\n[dllwords]\nabc\nhttp\nsteam\ncheat\napi6\n");
            r.Equal("tools: shorter than 5, the kept processes (and prefixes hitting them) dropped", "abcde,discordx", string.Join(",", f.Tools));
            r.Equal("dlls: not .dll and the game's own DLLs dropped", "version.dll", string.Join(",", f.Dlls));
            r.Equal("dllwords: shorter than 4 and words inside the game's DLL names dropped", "cheat", string.Join(",", f.DllWords));
            r.Check("every tool dropped -> refused", DefinitionsStore.ParseText("version=3\n[tools]\nsteam*\nabc\n") == null);
            string many = "version=3\n[tools]\n" + string.Join("\n", Enumerable.Range(0, 500).Select(i => "toolname" + i));
            r.Check("500 entries are fine", DefinitionsStore.ParseText(many) != null);
            r.Check("501 entries -> refused", DefinitionsStore.ParseText(many + "\ntoolname500") == null);
            r.Check("more than 65536 characters -> refused", DefinitionsStore.ParseText("version=3\n[tools]\ncheatengine*\n#" + new string('x', 65536)) == null);
            r.Check("65536 characters are fine", DefinitionsStore.ParseText(("version=3\n[tools]\ncheatengine*\n#").PadRight(65536, 'x')) != null);
            r.Check("[section] with '=' is not a version line", DefinitionsStore.ParseText("[version=4]\nversion=5\n[tools]\ncheatengine*\n").Version == 5);
            r.Equal("tool match: name* is a prefix, else the whole name", "True,False,True,False", string.Join(",", new[] { DefinitionsStore.ToolMatch("cheatengine74", "cheatengine*"), DefinitionsStore.ToolMatch("wemodhelper", "wemod"), DefinitionsStore.ToolMatch("wemod", "wemod"), DefinitionsStore.ToolMatch("xcheatengine", "cheatengine*") }));
        }

        // ================================================================== 7. Load
        static void LoadTests(SelfTestRunner r, TestKeys k)
        {
            r.Section("definitions load");
            r.Test("rules", () =>
            {
                string root = r.NewDir("defs-load");
                string bun = Path.Combine(root, "bundled"), st = Path.Combine(root, "state");
                Directory.CreateDirectory(bun); Directory.CreateDirectory(st);

                var d = k.Store(bun, st).Load();
                r.Check("no file: built-in list, version 0, SigState 1", d.SigState == 1 && d.Version == 0);
                r.Equal("built-in tools", "cheatengine*,artmoney*,wemod,extremeinjector*,xenos,xenos64,ghinjector*,squalr,speedhack*,gameguardian,sickomenu*,amongusmenu*,reclass*", string.Join(",", d.Tools));
                r.Equal("built-in dlls", "version.dll,dxgi.dll,d3d11.dll,dinput8.dll,winmm.dll,dsound.dll,xinput1_3.dll,xinput1_4.dll,xinput9_1_0.dll,opengl32.dll", string.Join(",", d.Dlls));
                r.Equal("built-in dll words", "menu,cheat,inject", string.Join(",", d.DllWords));
                r.Check("no bundled folder given: the cache only", k.Store(null, st).Load().SigState == 1);

                WriteDefs(bun, DefsText(2, "bundledtool"), k.A);
                d = k.Store(bun, st).Load();
                r.Check("bundled signed: used (SigState 0)", d.SigState == 0 && d.Version == 2 && d.Tools.SequenceEqual(new[] { "bundledtool" }));

                WriteDefs(st, DefsText(3, "cachedtool"), k.B, withId: false);
                d = k.Store(bun, st).Load();
                r.Check("the cache is newer: the cache", d.Version == 3 && d.Tools[0] == "cachedtool");

                WriteDefs(bun, DefsText(3, "bundledtool"), k.A, bom: true, crlf: true);
                d = k.Store(bun, st).Load();
                r.Check("the same version: the bundled one (read first; CRLF + BOM verify)", d.Version == 3 && d.Tools[0] == "bundledtool");

                WriteDefs(bun, DefsText(9, "bundledtool"), k.X);
                d = k.Store(bun, st).Load();
                r.Check("bundled signed by an untrusted key: skipped, the cache used", d.Version == 3 && d.Tools[0] == "cachedtool");

                WriteDefs(st, DefsText(4, "cachedtool"), k.R);
                d = k.Store(bun, st).Load();
                r.Check("files but none verifies: built-in list, SigState 2", d.SigState == 2 && d.Version == 0 && d.Tools.Count == 13);

                WriteDefs(st, DefsText(4, "cachedtool"), null);
                WriteDefs(bun, DefsText(4, "bundledtool"), null);
                r.Equal("no .sig at all: SigState 2", 2, k.Store(bun, st).Load().SigState);

                WriteDefs(bun, DefsText(5, "bundledtool"), k.A);
                File.AppendAllText(Path.Combine(bun, "definitions.txt.sig"), new string('#', 4096));
                r.Equal(".sig over 4096 bytes is not read (Missing): SigState 2", 2, k.Store(bun, st).Load().SigState);

                var big = Bytes(DefsText(6, "bigtool") + "#" + new string('x', 262144));
                File.WriteAllBytes(Path.Combine(bun, "definitions.txt"), big);
                File.WriteAllText(Path.Combine(bun, "definitions.txt.sig"), k.A.Sig(DefinitionsSignature.Canonical(big)), Utf8NoBom);
                r.Equal("a file over 256 KiB is not read (even when signed): SigState 2", 2, k.Store(bun, st).Load().SigState);
            });
        }

        // ================================================================== 7. Store (rollback guard)
        static void StoreTests(SelfTestRunner r, TestKeys k)
        {
            r.Section("definitions store");
            r.Test("rules", () =>
            {
                string root = r.NewDir("defs-store");
                string bun = Path.Combine(root, "bundled"), st = Path.Combine(root, "state");
                Directory.CreateDirectory(st);
                byte[] v3 = WriteDefs(bun, DefsText(3, "bundledtool"), k.A, crlf: true);
                string cache = Path.Combine(st, "definitions.txt"), cacheSig = cache + ".sig";

                r.Check("Store before Load -> false", !k.Store(bun, st).Store(Bytes(DefsText(4, "x12345")), k.A.Sig(Bytes(DefsText(4, "x12345")))) && !File.Exists(cache));
                var d = k.Store(bun, st).Load();
                r.Check("in use: bundled v3", d.Version == 3);
                byte[] v2 = Bytes(DefsText(2, "oldtool"));
                r.Check("older (v2) -> refused, nothing written", !d.Store(v2, k.A.Sig(v2)) && !File.Exists(cache));
                byte[] v3b = Bytes(DefsText(3, "othertool"));
                r.Check("same version, other bytes -> refused", !d.Store(v3b, k.A.Sig(v3b)) && !File.Exists(cache));
                byte[] v3c = DefinitionsSignature.Canonical(v3);
                r.Check("same version, the very bytes in use -> saved (true)", d.Store(v3, k.B.Sig(v3c)) && File.Exists(cache) && File.ReadAllBytes(cache).SequenceEqual(v3c));
                byte[] v4 = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes(DefsText(4, "newtool").Replace("\n", "\r\n"))).ToArray();
                byte[] v4c = DefinitionsSignature.Canonical(v4);
                r.Check("newer but unsigned -> refused", !d.Store(v4, null) && !d.Store(v4, "") && !d.Store(v4, k.X.Sig(v4c)) && !d.Store(v4, k.R.Sig(v4c)));
                r.Check("newer, not parseable -> refused", !d.Store(Bytes("version=9\n"), k.A.Sig(Bytes("version=9\n"))));
                r.Check("null data -> refused", !d.Store(null, k.A.Sig(v4c)));
                string sig4 = "  " + k.A.Sig(v4c) + "\n\n";
                r.Check("newer and signed -> saved", d.Store(v4, sig4));
                r.Check("the cache holds the canonical bytes (no BOM, LF)", File.ReadAllBytes(cache).SequenceEqual(v4c));
                byte[] sigBytes = File.ReadAllBytes(cacheSig);
                r.Check("the .sig is the text trimmed + \\n, UTF-8 without BOM", Encoding.UTF8.GetString(sigBytes) == sig4.Trim() + "\n" && sigBytes[0] != 0xEF);
                r.Check("no .tmp left", !File.Exists(cache + ".tmp") && !File.Exists(cacheSig + ".tmp"));
                r.Equal("the file in use does not change until the next load (A-1)", 3, d.Version);
                var d2 = k.Store(bun, st).Load();
                r.Check("the next load uses the saved v4", d2.Version == 4 && d2.Tools[0] == "newtool");

                var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local);
                File.SetLastWriteTime(cache, old); File.SetLastWriteTime(cacheSig, old);
                r.Check("already cached (same bytes, verifying .sig) -> true, nothing written", d2.Store(v4, k.B.Sig(v4c)) && File.GetLastWriteTime(cache) == old && File.GetLastWriteTime(cacheSig) == old);
                byte[] v4b = Bytes(DefsText(4, "evilsame"));
                r.Check("v4 with other bytes while v4 is in use -> refused", !d2.Store(v4b, k.A.Sig(v4b)));

                string empty = Path.Combine(root, "empty-state");
                Directory.CreateDirectory(empty);
                var b0 = k.Store(null, empty).Load();
                byte[] v1 = Bytes(DefsText(1, "firsttool"));
                r.Check("built-in list in use: any valid version (1) is saved", b0.Version == 0 && b0.Store(v1, k.A.Sig(v1)) && File.Exists(Path.Combine(empty, "definitions.txt")));
                byte[] v0 = Bytes(DefsText(0, "zerotool"));
                r.Check("built-in list in use: version 0 -> refused", !k.Store(null, r.NewDir("defs-store0")).Load().Store(v0, k.A.Sig(v0)));
            });
        }

        // ================================================================== the one download (no network here)
        static void FetchTests(SelfTestRunner r, TestKeys k)
        {
            r.Section("definitions download");
            r.Test("fake downloads", () =>
            {
                string st = r.NewDir("defs-fetch");
                byte[] v5 = Bytes(DefsText(5, "fetchedtool"));
                string sig5 = k.A.Sig(v5);
                var calls = new List<string>();
                var d = k.Store(null, st).Load();
                string res = d.Fetch((url, max) => { calls.Add(url + " " + max); return url.EndsWith(".sig") ? Encoding.UTF8.GetBytes(sig5) : v5; });
                r.Equal("the file, then its .sig, with the size limits", DefinitionsStore.Url + " 262144|" + DefinitionsStore.Url + ".sig 4096", string.Join("|", calls));
                r.Check("saved", File.Exists(Path.Combine(st, "definitions.txt")) && res.Contains("in the cache"), res);
                r.Equal("the URL", "https://raw.githubusercontent.com/wakayamachannel/PocketRoles/main/aegis/definitions.txt", DefinitionsStore.Url);
                r.Equal("the User-Agent", "Aegis/1.0 (+https://github.com/wakayamachannel/PocketRoles)", DefinitionsStore.UserAgent);
                r.Equal("8-second timeouts", 8000, DefinitionsStore.TimeoutMs);

                st = r.NewDir("defs-fetch2");
                calls.Clear();
                d = k.Store(null, st).Load();
                d.Fetch((url, max) => { calls.Add(url); return null; });
                r.Check("file too large (null): no .sig request, nothing saved", calls.Count == 1 && !File.Exists(Path.Combine(st, "definitions.txt")));
                d.Fetch((url, max) => { if (url.EndsWith(".sig")) return null; return v5; });
                r.Check(".sig too large: nothing saved", !File.Exists(Path.Combine(st, "definitions.txt")));
                res = d.Fetch((url, max) => { throw new System.Net.WebException("404"); });
                r.Check("HTTP / network error: nothing saved, nothing thrown", !File.Exists(Path.Combine(st, "definitions.txt")) && res.Contains("failed"), res);
                d.Fetch((url, max) => url.EndsWith(".sig") ? Encoding.UTF8.GetBytes(k.X.Sig(v5)) : v5);
                r.Check("a download signed by another key: nothing saved", !File.Exists(Path.Combine(st, "definitions.txt")));

                using (var ms = new MemoryStream(new byte[10000])) r.Equal("read limit: exactly the limit", 10000, DefinitionsStore.ReadLimited(ms, 10000).Length);
                using (var ms = new MemoryStream(new byte[10001])) r.Check("read limit: one byte over -> null", DefinitionsStore.ReadLimited(ms, 10000) == null);
            });

            // the app stays open for days: opening the window / playing downloads again after 12 hours (v0.1 review)
            r.Test("download again after 12 hours", () =>
            {
                DateTime t0 = Clock0;
                r.Check("due: never downloaded", AegisService.FetchDue(null, t0));
                r.Check("not due: 11 h 59 min later", !AegisService.FetchDue(t0, t0.AddHours(12).AddMinutes(-1)));
                r.Check("due: 12 h later", AegisService.FetchDue(t0, t0.AddHours(12)));
                r.Check("due: the clock was set back", AegisService.FetchDue(t0, t0.AddMinutes(-5)));

                string root = r.NewDir("defs-refresh");
                string bun = Path.Combine(root, "bundled"), st = Path.Combine(root, "state");
                byte[] v2 = WriteDefs(bun, DefsText(2, "bundledtool"), k.A);
                string sig2 = k.A.Sig(DefinitionsSignature.Canonical(v2));
                var calls = new List<string>();
                byte[] serveData = v2; string serveSig = sig2;
                DateTime now = t0;
                var svc = NewService(k, Path.Combine(root, "game"), st, bun, "ja", FakeSystem.Good());
                svc.Now = () => now;
                svc.Background = work => work();   // at once (the app uses a background thread)
                svc.Download = (url, max) => { calls.Add(url); return url.EndsWith(".sig") ? Encoding.UTF8.GetBytes(serveSig) : serveData; };

                r.Check("not the tray (old tray / not started): nothing", !svc.RefreshDefinitionsIfStale() && calls.Count == 0);
                svc.ActAsTrayForSelfTest();
                r.Check("the tray, never downloaded: downloads (the file, then its .sig)", svc.RefreshDefinitionsIfStale() && calls.Count == 2);
                // a newer signed file (v3) reaches the cache after that download (e.g. the start's own download was later)
                byte[] v3 = WriteDefs(st, DefsText(3, "newertool"), k.A);
                now = t0.AddHours(1);
                calls.Clear();
                r.Check("1 hour later: no download", !svc.RefreshDefinitionsIfStale() && calls.Count == 0);
                now = t0.AddHours(13);
                r.Check("13 hours later: downloads again", svc.RefreshDefinitionsIfStale() && calls.Count == 2);
                r.Check("the bundled v2 bytes downloaded then never replace the cached v3 (a fresh store: never older)", File.ReadAllBytes(Path.Combine(st, "definitions.txt")).SequenceEqual(DefinitionsSignature.Canonical(v3)));
                byte[] v4 = Bytes(DefsText(4, "latesttool"));
                serveData = v4; serveSig = k.A.Sig(DefinitionsSignature.Canonical(v4));
                now = t0.AddHours(26);
                r.Check("a newer v4 a day later: saved", svc.RefreshDefinitionsIfStale() && File.ReadAllBytes(Path.Combine(st, "definitions.txt")).SequenceEqual(DefinitionsSignature.Canonical(v4)));
                r.Check("... and used by the next pre-launch scan", svc.PreLaunchScan(null, CancellationToken.None).Ran && svc.GetSnapshot().DefsVersion == 4);
                byte[] v5 = Bytes(DefsText(5, "untrustedtool"));
                serveSig = k.X.Sig(DefinitionsSignature.Canonical(v5));
                serveData = v5;
                now = t0.AddHours(40);
                svc.RefreshDefinitionsIfStale();
                r.Check("a v5 signed by an unknown key: not saved", File.ReadAllText(Path.Combine(st, "definitions.txt")).Contains("latesttool"));
                svc.Stop();
                now = t0.AddHours(80);
                calls.Clear();
                r.Check("after Stop: nothing", !svc.RefreshDefinitionsIfStale() && calls.Count == 0);
            });
        }

        // ================================================================== 5. the 13 checks
        static string MakeGame(string root)
        {
            string g = Path.Combine(root, "Among Us PocketRoles");
            SelfTestRunner.Touch(Path.Combine(g, "Among Us.exe"));
            WriteLatin1(Path.Combine(g, @"Among Us_Data\globalgamemanagers"), "\0\0unity 2022.3.44f1\0\0 2026.8.18f1 \0 2026.8.18\0 2026.9.2");
            SelfTestRunner.Touch(Path.Combine(g, "winhttp.dll"));
            SelfTestRunner.Touch(Path.Combine(g, "UnityPlayer.dll"));
            SelfTestRunner.Touch(Path.Combine(g, @"BepInEx\core\BepInEx.Core.dll"));
            SelfTestRunner.Touch(Path.Combine(g, @"BepInEx\plugins\PocketRoles.dll"), "mod build A");
            return g;
        }

        static void WriteLatin1(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Encoding.GetEncoding(28591).GetBytes(text));
        }

        static AegisCheck Run1(string key, string game, string state, DefinitionsStore defs, IAegisSystem sys)
        {
            var c = AegisScanner.Build(game, state, defs, sys).Single(x => x.Key == key);
            AegisScanner.Run(new List<AegisCheck> { c }, null, null, CancellationToken.None);
            return c;
        }

        static string D(AegisCheck c) => c.State + " " + c.Detail.Render("ja") + (c.HasFix("ja") ? " → " + c.Fix.Render("ja") : "");

        static void ScanTests(SelfTestRunner r, TestKeys k)
        {
            r.Section("scan");
            r.Test("rows", () =>
            {
                string root = r.NewDir("scan");
                string game = MakeGame(root), st = Path.Combine(root, "state");
                Directory.CreateDirectory(st);
                var none = k.Store(null, Path.Combine(root, "no-defs")).Load();
                var sys = FakeSystem.Good();

                var rows = AegisScanner.Build(game, st, none, sys);
                r.Equal("13 rows in the ps1's (and the prototype's) order", "engine,game,bep,mod,plug,inj,cfg,ban,sb,tpm,kern,vdb,tools", string.Join(",", rows.Select(c => c.Key)));
                r.Equal("the rows that stop a launch (the prototype's \"stop\" marks)", "mod,plug,inj,kern,tools", string.Join(",", rows.Where(c => c.SeriousOnFail).Select(c => c.Key)));
                r.Equal("titles (ja)", "Aegis エンジン|Among Us|BepInEx|MOD 本体の整合性|ほかのプラグイン|ゲームへの注入|Aegis の設定|BAN リスト|セキュアブート|TPM|カーネルの保護|脆弱ドライバーの遮断|実行中のチートツール", string.Join("|", rows.Select(c => c.Title("ja"))));

                // 1 engine
                r.Equal("engine: no file", "2 検知ルール 26 件・組み込みの定義を使用", D(Run1("engine", game, st, none, sys)));
                string bun = Path.Combine(root, "bundled");
                WriteDefs(bun, DefsText(7, "cheatengine*"), k.A);
                var signed = k.Store(bun, Path.Combine(root, "no-defs")).Load();
                r.Equal("engine: signed", "2 検知ルール 26 件・定義ファイル v7・署名 OK", D(Run1("engine", game, st, signed, sys)));
                WriteDefs(bun, DefsText(7, "cheatengine*"), k.X);
                var unsigned = k.Store(bun, Path.Combine(root, "no-defs")).Load();
                r.Equal("engine: unsigned -> yellow, never red", "3 定義の署名なし・不一致（組み込みの定義を使用）", D(Run1("engine", game, st, unsigned, sys)));

                // 2 game
                r.Equal("game: 2026.8.18 (2022.x and 2026.8.18f1 skipped)", "2 2026.8.18（対応版）", D(Run1("game", game, st, none, sys)));
                WriteLatin1(Path.Combine(game, @"Among Us_Data\globalgamemanagers"), "2026.9.2\0");
                r.Equal("game: another version -> yellow", "3 2026.9.2（対応版は 2026.8.18）", D(Run1("game", game, st, none, sys)));
                File.Delete(Path.Combine(game, @"Among Us_Data\globalgamemanagers"));
                r.Equal("game: version unreadable -> OK", "2 Among Us.exe を確認", D(Run1("game", game, st, none, sys)));
                r.Equal("game: no Among Us.exe -> yellow", "3 MOD 用のゲームフォルダが見つかりません", D(Run1("game", Path.Combine(root, "nowhere"), st, none, sys)));

                // 3 BepInEx
                r.Equal("bep: core + winhttp.dll", "2 BepInEx 6（IL2CPP）を確認", D(Run1("bep", game, st, none, sys)));
                File.Delete(Path.Combine(game, "winhttp.dll"));
                r.Equal("bep: no winhttp.dll -> yellow", "3 BepInEx が見つかりません", D(Run1("bep", game, st, none, sys)));
                SelfTestRunner.Touch(Path.Combine(game, "winhttp.dll"));

                // 4 mod integrity (fingerprint)
                string fp = Path.Combine(st, "mod-fingerprint.txt");
                string dll = Path.Combine(game, @"BepInEx\plugins\PocketRoles.dll");
                string shaA = Sha(dll);
                r.Equal("mod: first time -> recorded", "2 v?・指紋を記録しました", D(Run1("mod", game, st, none, sys)));
                r.Equal("mod: the record is \"sha|version\" without BOM", shaA + "|?", Encoding.UTF8.GetString(File.ReadAllBytes(fp)));
                r.Equal("mod: unchanged", "2 v?・前回から変更なし", D(Run1("mod", game, st, none, sys)));
                SelfTestRunner.Touch(dll, "mod build B (tampered, same version)");
                var changed = Run1("mod", game, st, none, sys);
                r.Equal("mod: same version, other bytes -> red with the fix", "4 v? の中身が前回と違います（書き換えられた可能性） → ランチャーの「更新を確認」で MOD を入れ直してください", D(changed));
                r.Equal("mod: the record is not rewritten on a mismatch", shaA + "|?", File.ReadAllText(fp));
                File.Copy(Assembly.GetExecutingAssembly().Location, dll, true);   // a file with a version resource (the app's ProductVersion)
                string exeVer = (FileVersionInfo.GetVersionInfo(dll).ProductVersion ?? "?").Split('+')[0];
                r.Equal("mod: another version -> accepted as an update and recorded (A-4)", "2 v" + exeVer + "・更新を確認（前回 v?）", D(Run1("mod", game, st, none, sys)));
                r.Equal("mod: the new record", Sha(dll) + "|" + exeVer, File.ReadAllText(fp));
                File.WriteAllText(fp, "garbage-without-bar");
                r.Equal("mod: a record without '|' counts as none", "2 v" + exeVer + "・指紋を記録しました", D(Run1("mod", game, st, none, sys)));
                File.Delete(dll);
                var missing = Run1("mod", game, st, none, sys);
                r.Check("mod: no PocketRoles.dll -> yellow, does not stop a launch", missing.State == 3 && !missing.SeriousOnFail && missing.Detail.Render("ja") == "PocketRoles.dll が見つかりません", D(missing));
                SelfTestRunner.Touch(dll, "mod build A");

                // 5 other plugins
                r.Equal("plug: none", "2 なし（PocketRoles だけ）", D(Run1("plug", game, st, none, sys)));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\sub\Evil.dll"));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\old\POCKETROLES.DLL"));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\readme.txt"));
                r.Equal("plug: sub-folders too, PocketRoles.dll in any case ignored -> red", "4 見知らぬプラグイン: Evil.dll → BepInEx\\plugins から Evil.dll を外してください", D(Run1("plug", game, st, none, sys)));
                Directory.Delete(Path.Combine(game, @"BepInEx\plugins\sub"), true);
                Directory.Delete(Path.Combine(game, @"BepInEx\plugins\old"), true);
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\Disabled.dll_off"));
                // Whether "*.dll" also matches "Disabled.dll_off" is NOT ours to decide: Win32 matches a 3-letter
                // extension pattern against the file's SHORT (8.3) name as well, and short names can be switched off
                // per volume. They are on for a normal user's C:, and off on the GitHub runner's disk - which is why
                // this line failed only in CI (2026-09-25). Both answers are correct for the machine they run on, and
                // BepInEx enumerates the same way, so the scan and the loader always agree with each other.
                // A real .dll (Evil.dll above) is found either way; that is the check that matters.
                {
                    string got = D(Run1("plug", game, st, none, sys));
                    bool 見つけた = got == "4 見知らぬプラグイン: Disabled.dll_off → BepInEx\\plugins から Disabled.dll_off を外してください";
                    bool 見つけない = got == "2 なし（PocketRoles だけ）";
                    r.Check("plug: Disabled.dll_off は、8.3 の短い名前が有効な PC でだけ当たる（どちらでも正しい、A-15）",
                            見つけた || 見つけない, got);
                }
                File.Delete(Path.Combine(game, @"BepInEx\plugins\Disabled.dll_off"));

                // 6 injection
                r.Equal("inj: winhttp.dll and UnityPlayer.dll are not suspicious", "2 不審な DLL なし", D(Run1("inj", game, st, none, sys)));
                SelfTestRunner.Touch(Path.Combine(game, "version.dll"));
                SelfTestRunner.Touch(Path.Combine(game, "AUMenu.DLL"));
                SelfTestRunner.Touch(Path.Combine(game, @"Among Us_Data\dxgi.dll"));
                r.Equal("inj: a listed name and a keyword (any case); sub-folders not looked at -> red", "4 ゲームフォルダに AUMenu.DLL, version.dll（チートの読み込みに使われる） → MOD 用のゲームフォルダから AUMenu.DLL, version.dll を削除してください", D(Run1("inj", game, st, none, sys)));
                File.Delete(Path.Combine(game, "version.dll")); File.Delete(Path.Combine(game, "AUMenu.DLL"));
                r.Equal("inj: no game folder -> OK", "2 不審な DLL なし", D(Run1("inj", Path.Combine(root, "nowhere"), st, none, sys)));

                // 7 settings
                string cfg = Path.Combine(game, @"BepInEx\config\jp.pocketroles.mod.cfg");
                r.Equal("cfg: no file -> OK", "2 設定はまだありません（初回起動で作られます）", D(Run1("cfg", game, st, none, sys)));
                SelfTestRunner.Touch(cfg, "[General]\nDetect = false\n\n[anticheat]\n# Detect = false\nAutoKick = False \nCallout=off\n");
                r.Equal("cfg: missing keys are ON, only \"false\" is OFF, section in any case", "2 検知 ON・自動退出 OFF・お知らせ ON・言い当て ON", D(Run1("cfg", game, st, none, sys)));
                r.Equal("cfg: in English", "detect on · auto-remove off · announce on · callout on", Run1("cfg", game, st, none, sys).Detail.Render("en"));
                SelfTestRunner.Touch(cfg, "[AntiCheat]\nDetect = FALSE\n");
                r.Equal("cfg: detection off -> yellow", "3 検知がオフです（/opt anticheat on）", D(Run1("cfg", game, st, none, sys)));

                // 8 ban list
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\PocketRoles\Banlist.txt"), "// header\n; comment\n# comment\n\nPlayerOne\nPlayerTwo // why\n   PlayerThree   \n   // only a comment\nfoo#bar\n");
                r.Equal("ban: comments and blank lines not counted", "2 4 人", D(Run1("ban", game, st, none, sys)));

                // 9-12 this PC
                var p = FakeSystem.Good();
                r.Equal("sb: 1 -> on", "2 有効", D(Run1("sb", game, st, none, p)));
                p.Set(SB, "UEFISecureBootEnabled", 0);
                r.Equal("sb: 0 -> yellow", "3 無効（UEFI の設定でオンにできます）", D(Run1("sb", game, st, none, p)));
                p.Set(SB, "UEFISecureBootEnabled", "1");
                r.Equal("sb: not a DWORD -> yellow", "3 無効（UEFI の設定でオンにできます）", D(Run1("sb", game, st, none, p)));
                p.Keys.Remove(SB);
                r.Equal("sb: no key (legacy BIOS) -> OK", "2 確認できません（レガシー BIOS）", D(Run1("sb", game, st, none, p)));
                r.Equal("tpm: a device -> OK", "2 TPM 2.0 を確認", D(Run1("tpm", game, st, none, p)));
                p.Key(TPM).SubKeys = 0;
                r.Equal("tpm: no device -> yellow", "3 TPM 2.0 が見つかりません", D(Run1("tpm", game, st, none, p)));
                p.Keys.Remove(TPM);
                r.Equal("tpm: no key -> yellow", "3 TPM 2.0 が見つかりません", D(Run1("tpm", game, st, none, p)));

                p = FakeSystem.Good();
                r.Equal("kern: clean, memory integrity on", "2 テスト署名・デバッグモードなし・メモリ整合性 ON", D(Run1("kern", game, st, none, p)));
                p.Keys.Remove(HVCI);
                r.Equal("kern: clean", "2 テスト署名・デバッグモードなし", D(Run1("kern", game, st, none, p)));
                p.Set(CTRL, "SystemStartOptions", " NOEXECUTE=OPTIN  testsigning");
                r.Equal("kern: TESTSIGNING (any case) -> red", "4 TESTSIGNING が有効（署名のないドライバーを読み込める状態） → 管理者のコマンドプロンプトで bcdedit /set testsigning off を実行して再起動してください", D(Run1("kern", game, st, none, p)));
                p.Set(CTRL, "SystemStartOptions", " DISABLE_INTEGRITY_CHECKS DEBUG DEBUGPORT=COM1");
                r.Equal("kern: several -> each once, one fix (debug before nointegritychecks; A-6)", "4 NOINTEGRITYCHECKS / DEBUG が有効（署名のないドライバーを読み込める状態） → 管理者のコマンドプロンプトで bcdedit /set debug off を実行して再起動してください", D(Run1("kern", game, st, none, p)));
                p.Set(CTRL, "SystemStartOptions", "DISABLE_INTEGRITY_CHECKS");
                r.Equal("kern: nointegritychecks", "4 NOINTEGRITYCHECKS が有効（署名のないドライバーを読み込める状態） → 管理者のコマンドプロンプトで bcdedit /set nointegritychecks off を実行して再起動してください", D(Run1("kern", game, st, none, p)));
                p.Set(CTRL, "SystemStartOptions", "TESTSIGNINGX DEBUGGER");
                r.Equal("kern: TESTSIGNINGX and DEBUGGER are not flags", "2 テスト署名・デバッグモードなし", D(Run1("kern", game, st, none, p)));
                p.Keys.Remove(CTRL);
                r.Equal("kern: no key -> clean", "2 テスト署名・デバッグモードなし", D(Run1("kern", game, st, none, p)));
                p.Key(CTRL).ThrowOnGet = true;
                var crashed = Run1("kern", game, st, none, p);
                r.Check("kern: reading fails -> yellow with the error, never red (A-7)", crashed.State == 3 && !crashed.SeriousOnFail && crashed.Detail.Render("ja") == "Requested registry access is not allowed." && !crashed.HasFix("ja"), D(crashed));

                p = FakeSystem.Good();
                r.Equal("vdb: value 1", "2 有効", D(Run1("vdb", game, st, none, p)));
                p.Set(CICFG, "VulnerableDriverBlocklistEnable", 0);
                r.Equal("vdb: value 0 but memory integrity on", "2 有効（メモリ整合性で常に有効）", D(Run1("vdb", game, st, none, p)));
                p.Keys.Remove(HVCI);
                p.Set(CIPOL, "VerifiedAndReputablePolicyState", 1);
                r.Equal("vdb: value 0, Smart App Control on", "2 有効", D(Run1("vdb", game, st, none, p)));
                p.Set(CIPOL, "VerifiedAndReputablePolicyState", 2);
                r.Equal("vdb: value 0 on build 26200 -> yellow (the value says off)", "3 無効（コア分離の設定でオンにできます）", D(Run1("vdb", game, st, none, p)));
                p.Keys.Remove(CICFG);
                r.Equal("vdb: no value on build 26200 -> Windows default", "2 有効（Windows の既定）", D(Run1("vdb", game, st, none, p)));
                p.Set(NT, "CurrentBuildNumber", "22621");
                r.Equal("vdb: no value on build 22621 -> Windows default", "2 有効（Windows の既定）", D(Run1("vdb", game, st, none, p)));
                p.Set(NT, "CurrentBuildNumber", "19045");
                r.Equal("vdb: no value on Windows 10 -> yellow", "3 無効（コア分離の設定でオンにできます）", D(Run1("vdb", game, st, none, p)));
                p.Keys.Remove(NT);
                r.Equal("vdb: build unknown -> yellow", "3 無効（コア分離の設定でオンにできます）", D(Run1("vdb", game, st, none, p)));
                r.Check("vdb never stops a launch", !Run1("vdb", game, st, none, p).SeriousOnFail);

                // 13 tools
                p = FakeSystem.Good();
                r.Equal("tools: none", "2 なし", D(Run1("tools", game, st, none, p)));
                p.Names = new[] { "explorer", "Cheat Engine", "x-e_nos", "Cheat Engine", "wemodhelper", "WeMod", "", "cheatengine-x86_64-SSE4-AVX2" };
                r.Equal("tools: spaces, '-' and '_' removed, any case, name* prefix, each name once -> red", "4 Cheat Engine, x-e_nos, WeMod, cheatengine-x86_64-SSE4-AVX2 が起動中（チートに使えるツール） → Cheat Engine, x-e_nos, WeMod, cheatengine-x86_64-SSE4-AVX2 を終了してから起動してください", D(Run1("tools", game, st, none, p)));
                p.ThrowOnProcesses = true;
                var tc = Run1("tools", game, st, none, p);
                r.Check("tools: the process list fails -> yellow, never red (A-7)", tc.State == 3 && tc.Detail.Render("ja") == "the process list could not be read", D(tc));

                // the whole scan
                var all = AegisScanner.Build(game, st, none, FakeSystem.Good());
                var steps = new List<int>();
                var o = AegisScanner.Run(all, steps.Add, null, CancellationToken.None);
                r.Equal("whole scan: steps 1..13 reported before each row", string.Join(",", Enumerable.Range(1, 13)), string.Join(",", steps));
                r.Equal("whole scan: states", "2,2,2,2,2,2,3,2,2,2,2,2,2", string.Join(",", o.Rows.Select(c => c.State)));
                r.Check("whole scan: 1 warning (cfg detection off), 0 red", o.Warnings == 1 && o.Serious == 0, o.Warnings + "/" + o.Serious);
                var cts = new CancellationTokenSource(); cts.Cancel();
                var oc = AegisScanner.Run(AegisScanner.Build(game, st, none, FakeSystem.Good()), null, null, cts.Token);
                r.Check("cancelled before the first row: nothing ran", oc.Cancelled && oc.Rows.All(c => c.State == 0));
            });
        }

        static string Sha(string file)
        {
            using (var s = File.OpenRead(file)) using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
        }

        // ================================================================== 5. the pre-launch check, scans of the service
        static AegisService NewService(TestKeys k, string game, string state, string bundled, string lang, FakeSystem sys, List<string> clientLog = null) =>
            new AegisService(new AegisContext { GameDir = game, StateDir = state, BundledDir = bundled, Lang = lang, Ui = null, Log = s => clientLog?.Add(s) })
            {
                Probe = sys,
                Trusted = k.Trusted,
                Revoked = k.Revoked,
                Now = () => Clock0,
                GameRunning = () => false,
                Toast = (t, w) => { },
                Download = (u, m) => throw new InvalidOperationException("no network in the self-test"),
            };

        static void PreLaunchTests(SelfTestRunner r, TestKeys k)
        {
            r.Section("pre-launch");
            r.Test("blocked", () =>
            {
                string root = r.NewDir("prelaunch");
                string game = MakeGame(root), st = Path.Combine(root, "state"), bun = Path.Combine(root, "bundled");
                WriteDefs(bun, DefsText(5, "cheatengine*"), k.A);
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\Evil.dll"));
                var sys = FakeSystem.Good().Set(CTRL, "SystemStartOptions", "TESTSIGNING");
                var svc = NewService(k, game, st, bun, "ja", sys);
                int changes = 0;
                svc.Changed += (s, e) => changes++;
                var progress = new List<int>();
                var res = svc.PreLaunchScan(p => progress.Add(p.Step * 100 + p.Of), CancellationToken.None);
                r.Check("ran and blocked (the ps1's exit code 3)", res.Ran && res.Blocked);
                string l1 = "ほかのプラグイン: 見知らぬプラグイン: Evil.dll → BepInEx\\plugins から Evil.dll を外してください";
                string l2 = "カーネルの保護: TESTSIGNING が有効（署名のないドライバーを読み込める状態） → 管理者のコマンドプロンプトで bcdedit /set testsigning off を実行して再起動してください";
                r.Equal("the red rows as \"title: detail → fix\"", l1 + "\n" + l2, string.Join("\n", res.Lines));
                byte[] file = File.ReadAllBytes(Path.Combine(st, "prelaunch-result.txt"));
                r.Check("prelaunch-result.txt: UTF-8 with BOM, one line each, CRLF", file.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }) && Encoding.UTF8.GetString(file, 3, file.Length - 3) == l1 + "\r\n" + l2 + "\r\n");
                string stamp = Clock0.ToString("yyyy-MM-dd HH:mm:ss");
                r.Equal("events.log: \"<time>  pre-launch scan: blocked (2)\"", stamp + "  pre-launch scan: blocked (2)\r\n", ReadNoBom(Path.Combine(st, "events.log")));
                r.Check("the definitions were loaded fresh (engine row v5)", svc.GetSnapshot().Rows[0].Detail == "検知ルール 26 件・定義ファイル v5・署名 OK");
                r.Equal("progress 1..13 of 13", string.Join(",", Enumerable.Range(1, 13).Select(i => i * 100 + 13)), string.Join(",", progress));
                r.Check("the panel was told (Changed)", changes > 13, changes.ToString());

                var snap = svc.GetSnapshot();
                r.Check("snapshot: 13 rows, 2 red, the red badge", snap.Rows.Count == 13 && snap.Rows.Count(x => x.State == 4) == 2 && snap.LastScanSerious == 2);
                r.Equal("snapshot: headline", "起動を止めました — 赤い項目 2 件を直してから起動してください", snap.Summary);
                r.Equal("snapshot: Aegis not started -> off", "off", snap.State);
                r.Check("snapshot: definitions v5 signed", snap.DefsVersion == 5 && snap.SigState == 0);
                svc.SetLanguage("en");
                snap = svc.GetSnapshot();
                r.Equal("a language change reaches the rows at once", "Other plugins|unknown plugin: Evil.dll|Remove Evil.dll from BepInEx\\plugins", snap.Rows[4].Title + "|" + snap.Rows[4].Detail + "|" + snap.Rows[4].Fix);
                r.Equal("... and the headline", "Start blocked — fix the 2 red row(s), then start", snap.Summary);
                svc.SetLanguage("zh-CN");
                r.Equal("... zh-CN", "已阻止启动 — 请先处理 2 个红色项目", svc.GetSnapshot().Summary);

                // the whole "start with mod" flow with this Aegis (GameLauncher, fakes for the rest)
                var started = new List<ProcessStartInfo>();
                var gl = new GameLauncher
                {
                    Paths = ModPaths.For(game),
                    Lang = () => "ja",
                    GameRunning = () => false,
                    SteamRunning = () => true,
                    ComputeStatus = () => LaunchStatus.Compute(false, new InstallInfo { Exe = true, Bep = true, Dll = true, GameVer = "2026.8.18" }, "2026.8.18", null, true, false, false),
                    PreLaunchScan = svc.PreLaunchScan,
                    SaveGameLog = () => true,
                    StartProcess = started.Add,
                };
                svc.SetLanguage("ja");
                var o = gl.Launch(false, _ => { });
                r.Check("launch: blocked, the game not started", o.Blocked && started.Count == 0);
                r.Equal("launch: the launcher's message", "Aegis が次の問題を見つけたので、起動を止めました。直してからもう一度起動してください。\n・" + l1 + "\n・" + l2, o.Text);
                File.Delete(Path.Combine(game, @"BepInEx\plugins\Evil.dll"));
                sys.Set(CTRL, "SystemStartOptions", "");
                o = gl.Launch(false, _ => { });
                r.Check("launch: fixed -> the game starts", o.Ok && started.Count == 1);
            });

            r.Test("not blocked", () =>
            {
                string root = r.NewDir("prelaunch-ok");
                string game = MakeGame(root), st = Path.Combine(root, "state");
                var sys = FakeSystem.Good();
                sys.Set(SB, "UEFISecureBootEnabled", 0);   // yellow only
                SelfTestRunner.Touch(Path.Combine(st, "prelaunch-result.txt"), "old reason");
                SelfTestRunner.Touch(Path.Combine(st, "events.log"), "2026-09-22 20:00:00  earlier\r\n");
                var svc = NewService(k, game, st, null, "ja", sys);
                var res = svc.PreLaunchScan(null, CancellationToken.None);
                r.Check("yellow rows never block", res.Ran && !res.Blocked && res.Lines.Length == 0);
                r.Check("prelaunch-result.txt rewritten empty (only the BOM)", File.ReadAllBytes(Path.Combine(st, "prelaunch-result.txt")).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
                r.Equal("events.log: ok (appended to the file that exists, no BOM added)", "2026-09-22 20:00:00  earlier\r\n" + Clock0.ToString("yyyy-MM-dd HH:mm:ss") + "  pre-launch scan: ok\r\n", File.ReadAllText(Path.Combine(st, "events.log"), Utf8NoBom));
                r.Equal("headline: go", "スキャン完了 — 起動します", svc.GetSnapshot().Summary);
                r.Equal("no definitions: engine row says built-in", "検知ルール 26 件・組み込みの定義を使用", svc.GetSnapshot().Rows[0].Detail);
            });

            r.Test("never blocks by failing", () =>
            {
                string root = r.NewDir("prelaunch-fail");
                string st = Path.Combine(root, "state");
                var log = new List<string>();
                var svc = NewService(k, root + @"\bad|name", st, null, "ja", FakeSystem.Good(), log);   // '|' is not allowed in a path
                var res = svc.PreLaunchScan(null, CancellationToken.None);
                r.Check("an exception -> not ran (the game starts)", !res.Ran && !res.Blocked);
                string alog = Path.Combine(st, "aegis.log");
                r.Check("aegis.log: \"<time> Aegis failed: <exception>\" (UTF-8 with BOM)", File.Exists(alog) && File.ReadAllBytes(alog)[0] == 0xEF && File.ReadAllText(alog).StartsWith(Clock0.ToString("yyyy-MM-dd HH:mm:ss") + " Aegis failed: System.ArgumentException"));
                r.Check("client.log told", log.Any(l => l.StartsWith("Aegis failed: ")));
                var cts = new CancellationTokenSource(); cts.Cancel();
                string st2 = Path.Combine(root, "state2");
                svc = NewService(k, MakeGame(root), st2, null, "ja", FakeSystem.Good(), log);
                res = svc.PreLaunchScan(null, cts.Token);
                r.Check("deadline passed before the scan ends -> not ran, nothing written", !res.Ran && !File.Exists(Path.Combine(st2, "prelaunch-result.txt")) && !File.Exists(Path.Combine(st2, "events.log")));
            });

            r.Test("scans need the running Aegis", () =>
            {
                string root = r.NewDir("scan-service");
                var svc = NewService(k, MakeGame(root), Path.Combine(root, "state"), null, "ja", FakeSystem.Good());
                r.Check("the port (not the stub)", svc.IsPorted && AegisFactory.Create(new AegisContext { StateDir = root }) is AegisService);
                r.Equal("scan again before start -> a calm error", "Aegis はまだ始まっていません。", svc.Rescan(null).Error);
                r.Equal("scan only before start -> a calm error", "Aegis はまだ始まっていません。", svc.ScanOnly(null).Error);
                r.Equal("events.log path", Path.Combine(root, @"state\events.log"), svc.EventsLogPath);
                r.Equal("old tray text (ja)", "古い Aegis トレイ（PowerShell）が動いています。そのトレイを終了すると、ここで見張りを始めます。", AegisText.Get("ja", "c.oldtray"));
            });
        }

        static string ReadNoBom(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            int start = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
            return Encoding.UTF8.GetString(b, start, b.Length - start);
        }

        // ================================================================== 9-10. watching the game's log
        sealed class Harness
        {
            public DateTime Now = Clock0;
            public double Secs;
            public bool Game;
            public readonly List<string> Events = new List<string>();
            public readonly List<string> Toasts = new List<string>();
            public List<string> Tools = new List<string>();
            public LogWatcher W;
            public string Log;

            public Harness(string log, string lang = "ja")
            {
                Log = log;
                W = new LogWatcher
                {
                    LogPath = log,
                    GameRunning = () => Game,
                    Now = () => Now,
                    Seconds = () => Secs,
                    FindTools = () => Tools,
                    AddEvent = Events.Add,
                    ShowToast = (t, warn) => Toasts.Add((warn ? "W " : "I ") + t),
                    Lang = () => lang,
                };
            }

            /// <summary>One second passes, then the watcher polls.</summary>
            public void Tick(double seconds = 1) { Now = Now.AddSeconds(seconds); Secs += seconds; W.Poll(); }

            public void Append(string text) => File.AppendAllText(Log, text, Utf8NoBom);
            public void AppendBytes(byte[] b) { using (var f = new FileStream(Log, FileMode.Append)) f.Write(b, 0, b.Length); }
        }

        static void WatcherTests(SelfTestRunner r)
        {
            r.Section("game watch");
            r.Test("log lines", () =>
            {
                string dir = r.NewDir("watch");
                string log = Path.Combine(dir, "LogOutput.log");
                File.WriteAllText(log, "[Info] old run\nCheatDetector: removing #9 OldGuy (client 4) with a room ban: KillRole\n", Utf8NoBom);
                File.SetLastWriteTime(log, Clock0.AddHours(-1));
                var h = new Harness(log);

                h.W.Ready();
                r.Equal("ready toast (information)", "I 起動しました。ゲームを始めると監視します。", string.Join("|", h.Toasts));
                h.Tick();
                r.Check("no game: standing by, nothing read", !h.W.Watching && h.Events.Count == 0 && h.W.State == "idle");

                h.Game = true;
                h.Tick();
                r.Check("game appears: watching, event b.watch0, no toast", h.W.Watching && h.W.State == "watching" && h.Events.SequenceEqual(new[] { "監視を始めました" }) && h.Toasts.Count == 1);
                h.Tick();
                r.Equal("last run's log (not shrunk, written before the game) is not read", 0, h.W.Removed);

                // BepInEx rewrites the log (shorter than before)
                File.WriteAllText(log, "[Info   :   BepInEx] Loading\n[Message:PocketRoles] PocketRoles v0.5.5 loaded\n", Utf8NoBom);
                h.Tick(5);
                r.Equal("rewritten log read from the start: mod version, information toast", "I 監視を始めました（PocketRoles v0.5.5）", h.Toasts.Last());
                r.Equal("mod version", "0.5.5", h.W.ModVersion);

                h.Append("[Warning:PocketRoles] CheatDetector: removing #3 Bad Guy  (client 7) with a room ban: KillRole\n");
                h.Tick();
                r.Check("removal: counted, event, forced warning toast", h.W.Removed == 1 && h.Events.Last() == "Bad Guy を退出させました（キルできない役職のキル）" && h.Toasts.Last() == "W Bad Guy を退出させました（キルできない役職のキル）");
                r.Equal("12 s of warning after a removal", "kicked", h.W.State);
                h.Secs += 11.5; r.Equal("... still at 11.5 s", "kicked", h.W.State);
                h.Secs += 0.6; r.Equal("... then watching again", "watching", h.W.State);

                int toasts = h.Toasts.Count;
                h.Append("CheatDetector: KillCooldown (Certain) #5 Speedy (client 8) at 1.0\n");
                h.Tick(5);
                r.Check("Certain: counted, event, warning toast", h.W.Flagged == 1 && h.Events.Last() == "Speedy: クールダウンより早いキル" && h.Toasts.Count == toasts + 1 && h.Toasts.Last() == "W Speedy: クールダウンより早いキル");
                h.Append("CheatDetector: Teleport (Certain) #6 Jumper (client 9) x\n");
                h.Tick(2);
                r.Check("a second one within 4 s: event only, toast dropped (A-3)", h.W.Flagged == 2 && h.Events.Last() == "Jumper: 瞬間移動" && h.Toasts.Count == toasts + 1);
                h.Append("CheatDetector: removing #6 Jumper (client 9) with a room ban: Teleport\n");
                h.Tick(1);
                r.Check("a removal is always shown (forced)", h.Toasts.Count == toasts + 2 && h.Toasts.Last() == "W Jumper を退出させました（瞬間移動）");
                h.Append("CheatDetector: SpeedFast (Notice) #7 Runner (client 3) y\n");
                h.Tick(10);
                r.Check("Notice: counted, event, no toast", h.W.Flagged == 3 && h.Events.Last() == "Runner: 設定より速い移動" && h.Toasts.Count == toasts + 2);
                h.Append("CheatDetector: [test] ChatFlood (Notice) #1 Host (client 0) z\n");
                h.Tick(5);
                r.Check("[test] Notice: not counted, event with [テスト], toast", h.W.Flagged == 3 && h.Events.Last() == "[テスト] Host: チャットの連投" && h.Toasts.Last() == "W [テスト] Host: チャットの連投");
                int ev = h.Events.Count; toasts = h.Toasts.Count;
                h.Append("CheatDetector: Callout (Certain) #2 A (client 1) q\nCheatDetector: CalloutRepeat (Repeat) #2 A (client 1) q\nCheatDetector: VoteCallout (Certain) #2 A (client 1) q\n");
                h.Tick(5);
                r.Check("callout rules (they name impostors): nothing at all", h.Events.Count == ev && h.Toasts.Count == toasts && h.W.Flagged == 3);
                h.Append("CheatDetector: BrandNewRule (Repeat) #4 Newbie (client 2) q\n");
                h.Tick(5);
                r.Check("a rule without a text: its name", h.Events.Last() == "Newbie: BrandNewRule" && h.W.Flagged == 4);
                h.Append("CheatDetector: removing #3 Pa");
                h.Tick(5);
                ev = h.Events.Count;
                r.Check("half a line waits", h.W.Removed == 2);
                h.Append("rtial Name (client 9) with a room ban: VentFar\n");
                h.Tick(5);
                r.Check("... and is read with its rest", h.W.Removed == 3 && h.Events.Last() == "Partial Name を退出させました（ベントから遠い位置でのベント）");

                // a character split across two reads (1 byte per read)
                h.W.MaxRead = 1;
                h.AppendBytes(Encoding.UTF8.GetBytes("CheatDetector: TaskBurst (Certain) #8 あいう (client 5) q\n"));
                for (int i = 0; i < 80; i++) h.Tick(5);
                r.Equal("UTF-8 split across reads keeps the characters", "あいう: ありえない速さのタスク", h.Events.Last());
                h.W.MaxRead = 4 * 1024 * 1024;

                // the file shrinks while watching (a new BepInEx run): read again from the start
                File.WriteAllText(log, "CheatDetector: KillDead (Certain) #1 Ghost (client 2) q\n", Utf8NoBom);
                h.Tick(5);
                r.Equal("the log became shorter: read from the start", "Ghost: 死んでいるのにキル", h.Events.Last());

                h.Game = false;
                h.Tick(2);
                r.Check("the game closed: event, information toast (dropped within 4 s, not forced)", !h.W.Watching && h.Events.Last() == "ゲームが終わりました。待機中です。" && h.W.State == "idle");
                int flaggedBefore = h.W.Flagged;
                h.Game = true;
                File.SetLastWriteTime(log, h.Now.AddSeconds(1));   // the game will be seen at Now + 2 s
                h.Tick(2);
                r.Check("a new game: counts back to 0", h.W.Flagged == 1 && h.W.Removed == 0 && flaggedBefore > 0, h.W.Flagged + "/" + h.W.Removed);
                r.Equal("a log written within 2 s before the game was seen is this run's: read", "Ghost: 死んでいるのにキル", h.Events.Last());
            });

            r.Test("cheat tools every 30 polls", () =>
            {
                string dir = r.NewDir("watch-tools");
                var h = new Harness(Path.Combine(dir, "LogOutput.log"), "en");
                h.Tools = new List<string> { "Cheat Engine" };
                for (int i = 0; i < 29; i++) h.Tick();
                r.Check("not before the 30th poll", h.Events.Count == 0);
                h.Tick();
                r.Check("30th poll: event and forced warning toast", h.Events.SequenceEqual(new[] { "Cheat Engine is running (usable for cheating)" }) && h.Toasts.Last() == "W Cheat Engine is running (usable for cheating)");
                for (int i = 0; i < 30; i++) h.Tick(0.1);
                r.Check("the same tool is told once (A-2)", h.Events.Count == 1);
                h.Tools = new List<string> { "Cheat Engine", "xenos" };
                for (int i = 0; i < 30; i++) h.Tick(0.1);
                r.Check("a new tool is told, even within 4 s (forced)", h.Events.Count == 2 && h.Toasts.Last() == "W xenos is running (usable for cheating)");
                r.Check("no game: no log reading, still idle", !h.W.Watching && h.W.State == "idle");
            });

            r.Test("toast pacing", () =>
            {
                var h = new Harness(Path.Combine(r.NewDir("watch-pace"), "none.log"));
                h.W.Balloon("a", false);
                h.Now = h.Now.AddSeconds(3.9); h.W.Balloon("b", true);
                h.Now = h.Now.AddSeconds(3.9); h.W.Balloon("c", true, true);
                h.Now = h.Now.AddSeconds(3.9); h.W.Balloon("d", false);
                h.Now = h.Now.AddSeconds(0.2); h.W.Balloon("e", false);
                r.Equal("within 4 s dropped; forced always shown and restarts the 4 s", "I a|W c|I e", string.Join("|", h.Toasts));
            });
        }

        // ================================================================== 12. events.log
        static void EventsLogTests(SelfTestRunner r)
        {
            r.Section("events.log");
            r.Test("append and prune", () =>
            {
                string st = r.NewDir("events");
                string path = Path.Combine(st, "events.log");
                EventsLog.Append(st, Clock0, "監視を始めました");
                EventsLog.Append(st, Clock0.AddSeconds(1), "second");
                byte[] b = File.ReadAllBytes(path);
                string stamp = Clock0.ToString("yyyy-MM-dd HH:mm:ss"), stamp1 = Clock0.AddSeconds(1).ToString("yyyy-MM-dd HH:mm:ss");
                r.Check("a new file starts with a BOM; lines \"<time>  text\" CRLF", b.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }) && ReadNoBom(path) == stamp + "  監視を始めました\r\n" + stamp1 + "  second\r\n");
                // PORT-MAP 9.2 A-13, fixed 2026-09-26: "HH:mm:ss" with the CURRENT culture asks Windows for that culture's
                // TIME SEPARATOR, so on a PC where it is not ":" every stamp came out "21.04.12" (fi-FI, id-ID, en-DK ... 14
                // cultures) or "21h04h12" (oc-FR) while Prune only read ":" - and not one line with a player name in it was
                // ever dropped there. These two run the writer under those cultures on ANY PC, so the hole cannot reopen
                // quietly: the old code fails them on an en-US runner too.
                r.Equal("the stamp is Invariant, not this PC's culture", Clock0.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), stamp);
                foreach (var name in new[] { "fi-FI", "id-ID", "en-DK", "oc-FR" })
                {
                    var was = System.Threading.Thread.CurrentThread.CurrentCulture;
                    string wrote;
                    try
                    {
                        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(name);
                        string one = r.NewDir("events-" + name);
                        EventsLog.Append(one, Clock0, "Bad Guy を退出させました");
                        wrote = ReadNoBom(Path.Combine(one, "events.log"));
                    }
                    finally { System.Threading.Thread.CurrentThread.CurrentCulture = was; }
                    r.Equal(name + ": written with \":\" whatever this PC's time separator is", stamp + "  Bad Guy を退出させました\r\n", wrote);
                }
                // and the lines ALREADY written the broken way, on those same PCs, still have to age out
                foreach (var sep in new[] { '.', 'h', ':' })
                {
                    string old = "2026-08-01 10" + sep + "00" + sep + "00  old event", now2 = "2026-09-20 09" + sep + "00" + sep + "00  new event";
                    string one = r.NewDir("events-sep-" + (sep == ':' ? "colon" : sep == '.' ? "dot" : "h"));
                    string p2 = Path.Combine(one, "events.log");
                    File.WriteAllText(p2, old + "\r\n" + now2 + "\r\n", Utf8NoBom);
                    r.Equal("stamps written with '" + sep + "' are pruned too (A-13)", 1, EventsLog.Prune(one, 30, Clock0));
                    r.Equal("... and the newer line stays", now2 + "\r\n", ReadNoBom(p2));
                }
                r.Check("a line of text is still unstamped", !EventsLog.TryStamp("note at the top", out _) && !EventsLog.TryStamp("2026-08-01 10:00.00  mixed", out _) && !EventsLog.TryStamp("2026-08-01 10000000  digits", out _));

                string text = "note at the top\r\n2026-08-01 10:00:00  old event\r\ncontinued old line\r\n2026-08-23 21:04:12  exactly 30 days\r\n2026-09-20 09:00:00  new event\r\ncontinued new\r\n";
                File.WriteAllText(path, text, Utf8NoBom);
                int dropped = EventsLog.Prune(st, 30, Clock0);
                r.Equal("30 days: the old line and the line after it dropped", 2, dropped);
                byte[] after = File.ReadAllBytes(path);
                r.Check("rewritten with a BOM", after.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
                r.Equal("kept: the unstamped top, exactly 30 days, newer", "note at the top\r\n2026-08-23 21:04:12  exactly 30 days\r\n2026-09-20 09:00:00  new event\r\ncontinued new\r\n", ReadNoBom(path));
                r.Check("no events.log.tmp left", !File.Exists(path + ".tmp"));
                File.WriteAllText(path, "2026-09-20 09:00:00  new\n", Utf8NoBom);
                var t0 = new DateTime(2020, 1, 1); File.SetLastWriteTime(path, t0);
                r.Check("nothing to drop: the file is not touched", EventsLog.Prune(st, 30, Clock0) == 0 && File.GetLastWriteTime(path) == t0 && File.ReadAllBytes(path)[0] == (byte)'2');
                File.Delete(path);
                r.Equal("no file: 0", 0, EventsLog.Prune(st, 30, Clock0));
                EventsLog.AegisLog(st, Clock0, "Aegis failed: test");
                r.Equal("aegis.log line", stamp + " Aegis failed: test\r\n", ReadNoBom(Path.Combine(st, "aegis.log")));
            });
        }

        // ================================================================== the toast's picture (no window)
        static void ToastTests(SelfTestRunner r)
        {
            r.Section("toast");
            r.Test("picture", () =>
            {
                foreach (var c in new[] { new { k = 1f, lang = "ja", warn = true, text = "Bad Guy を退出させました（キルできない役職のキル）" }, new { k = 1.5f, lang = "zh-CN", warn = false, text = "开始监视（PocketRoles v0.5.5）" }, new { k = 1.25f, lang = "en", warn = true, text = "Cheat Engine, xenos, WeMod, Squalr, ArtMoney and a very long list of tools is running (usable for cheating)" } })
                {
                    using (var bmp = AegisToast.DrawCard(c.text, c.warn, c.lang, c.k))
                    {
                        int w = (int)Math.Round(428 * c.k), h = (int)Math.Round(106 * c.k);
                        r.Check("size " + c.k + "x (400x78 card + shadow room)", Math.Abs(bmp.Width - w) <= 1 && Math.Abs(bmp.Height - h) <= 1, bmp.Width + "x" + bmp.Height);
                        r.Check("corner transparent (" + c.lang + ")", bmp.GetPixel(0, 0).A == 0 && bmp.GetPixel(bmp.Width - 1, 0).A == 0);
                        var band = bmp.GetPixel((int)(16 * c.k), bmp.Height / 2);
                        var want = c.warn ? AegisToast.Amber1 : AegisToast.Green1;
                        r.Check("colour band (" + (c.warn ? "amber" : "green") + ")", Math.Abs(band.R - want.R) < 8 && Math.Abs(band.G - want.G) < 8 && Math.Abs(band.B - want.B) < 8, band.ToString());
                        var card = bmp.GetPixel(bmp.Width - (int)(40 * c.k), (int)(22 * c.k));
                        r.Check("card opaque navy (" + c.lang + ")", card.A >= 240 && card.B > card.R, card.ToString());
                        string png = Path.Combine(r.Root, "toast-" + c.lang + ".png");
                        bmp.Save(png, ImageFormat.Png);
                        r.Info("toast picture: " + png);
                    }
                }
            });
            // the app's own notice (first hide to the tray): app name + icon A + teal band, as tall as the text
            r.Test("app notice picture", () =>
            {
                foreach (var l in Lang.Codes)
                {
                    string text = S.T(l, "tray.stillRunning");
                    float k = 1.25f;
                    int h = AegisToast.NoticeHeight(AppInfo.Name, text, l, k);
                    r.Check("tall enough for the whole text, not more than 150 (" + l + ")", h >= 78 && h <= 150, h.ToString());
                    using (var bmp = AegisToast.DrawNotice(AppInfo.Name, text, l, k))
                    {
                        r.Check("size = card + shadow room (" + l + ")", Math.Abs(bmp.Width - (int)Math.Round(428 * k)) <= 1 && Math.Abs(bmp.Height - (int)Math.Round((h + 28) * k)) <= 1, bmp.Width + "x" + bmp.Height);
                        var band = bmp.GetPixel((int)(16 * k), bmp.Height / 2);
                        var want = AegisToast.Teal1;
                        r.Check("teal band (" + l + ")", Math.Abs(band.R - want.R) < 8 && Math.Abs(band.G - want.G) < 8 && Math.Abs(band.B - want.B) < 8, band.ToString());
                        string png = Path.Combine(r.Root, "notice-" + l + ".png");
                        bmp.Save(png, ImageFormat.Png);
                        r.Info("notice picture: " + png);
                    }
                }
                r.Equal("a short notice keeps the 78 px card", 78, AegisToast.NoticeHeight(AppInfo.Name, "OK", "en", 1f));
            });
        }
    }
}
