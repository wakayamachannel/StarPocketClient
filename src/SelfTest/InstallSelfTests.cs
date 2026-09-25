// v0.3 review: the install, what the app is allowed to fetch, the file copy, launcher-state.json and which past logs a
// report zip carries. Everything runs on fake folders inside <dir>\work; no network call is made (the only IWebFetch
// here is a fake, and the one real check asserts that a wrong address is refused BEFORE anything is opened).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Starpocket.Client.Core;

namespace Starpocket.Client.SelfTest
{
    internal static class InstallSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            FetchTests(r);
            BepHashTests(r);
            VerifyTests(r);
            ReleaseNameTests(r);
            UpdateMessageTests(r);
            ZipTests(r);
            ForceTests(r);
            CopyTests(r);
            StateFileTests(r);
            PastLogTests(r);
            PickedSteamTests(r);
            r.Section("");
        }

        /// <summary>A web that is not there: every call fails, so a test can never reach the network.</summary>
        sealed class NoWeb : IWebFetch
        {
            public string GetText(string url) => throw new IOException("no network in the self-test");
            public void Download(string url, string dest, Action<long, long> progress) => throw new IOException("no network in the self-test");
        }

        // ------------------------------------------------------------------ where the app may fetch from
        static void FetchTests(SelfTestRunner r)
        {
            r.Section("downloads");
            r.Test("only https, and only the app's own hosts", () =>
            {
                foreach (var ok in new[]
                {
                    "https://api.github.com/repos/x/y/releases/latest",
                    "https://github.com/x/y/releases/download/v1/PocketRoles-1.zip",
                    "https://objects.githubusercontent.com/whatever",
                    "https://builds.bepinex.dev/projects/bepinex_be",
                })
                    r.Check("yes: " + ok, WebFetch.AllowedUrl(ok));
                foreach (var no in new[]
                {
                    "http://builds.bepinex.dev/projects/bepinex_be/735/x.zip",   // plaintext: a link on a page could be this
                    "https://evil.example/x.zip",
                    "https://builds.bepinex.dev.evil.example/x.zip",             // the name only LOOKS right
                    "https://github.com.evil.example/x.zip",
                    "https://raw.githubusercontent.com/x",                       // a GitHub host, but not one of ours
                    "https://user:pw@github.com/x.zip",
                    "ftp://github.com/x.zip",
                    "file:///C:/x.zip",
                    "builds.bepinex.dev/x.zip",
                    "",
                })
                    r.Check("no: " + (no == "" ? "(nothing)" : no), !WebFetch.AllowedUrl(no));
                // and the check is in the code, not only in a comment: a wrong address never opens a connection
                bool refused = false;
                try { WebFetch.Instance.GetText("http://example.com/"); }
                catch (IOException) { refused = true; }
                catch (Exception) { }
                r.Check("GetText refuses it before it opens anything", refused);
                refused = false;
                try { WebFetch.Instance.Download("https://evil.example/x.zip", Path.Combine(r.NewDir("dl"), "x.zip"), null); }
                catch (IOException) { refused = true; }
                catch (Exception) { }
                r.Check("Download refuses it too", refused);
            });
            // v0.4 review: where the answer really CAME FROM, after any redirects. Download has always checked this;
            // GetText did not, and GetText is what reads builds.bepinex.dev's file list - a server nobody here
            // controls, which could have redirected the app anywhere and been read and parsed in silence.
            r.Test("a redirect may not leave the list either", () =>
            {
                foreach (var ok in new[]
                {
                    "https://objects.githubusercontent.com/really/here",   // where github.com hands release files on to
                    "https://builds.bepinex.dev/projects/bepinex_be",
                })
                {
                    bool threw = false;
                    try { WebFetch.CheckArrivedFrom(new Uri(ok)); } catch (IOException) { threw = true; }
                    r.Check("arriving from " + ok + " is fine", !threw);
                }
                foreach (var no in new[]
                {
                    "https://evil.example/list.html",
                    "http://builds.bepinex.dev/list.html",                 // dropped to plaintext on the way
                    "https://raw.githubusercontent.com/x",                 // a GitHub host, but not one of ours
                })
                {
                    bool threw = false;
                    try { WebFetch.CheckArrivedFrom(new Uri(no)); } catch (IOException) { threw = true; }
                    r.Check("arriving from " + no + " is refused", threw);
                }
                bool quiet = true;
                try { WebFetch.CheckArrivedFrom(null); } catch (Exception) { quiet = false; }
                r.Check("a response with no address of its own is left to the caller's own check", quiet);
            });
        }

        // ------------------------------------------------------------------ the BepInEx zip's own fingerprint
        // The host list says where the file may come from; the pinned SHA-256 says which file it has to be. Everything
        // in that zip is unpacked into the game copy and loaded as code by the game, so these four cases are the ones
        // that matter: the right file, a swapped file, a file that stopped half-way, and a version we have no pin for.
        // No network is touched: FakeWeb writes bytes this test chose into the file the installer asked for.

        /// <summary>A web that hands back exactly the bytes the test chose (and counts how often it was asked).</summary>
        sealed class FakeWeb : IWebFetch
        {
            public string Html = "";
            public byte[] Payload = new byte[0];
            public int Downloads;
            /// <summary>The addresses it was asked for, in order (B-12 compares this with Installer.BepAddresses).</summary>
            public readonly List<string> Asked = new List<string>();
            /// <summary>Different bytes per address (B-8: the first one hands back something else, the second the real file).</summary>
            public Func<string, byte[]> PayloadFor;
            /// <summary>Nothing can be reached: every download throws, and no file is written.</summary>
            public bool Offline;
            /// <summary>What this address throws instead of answering, or null to answer normally. B-17 and B-19 use
            /// it for the two failures that used to be reported as "your connection is down": a drive with no room
            /// left, and an address the app is not allowed to fetch from.</summary>
            public Func<string, Exception> ThrowFor;

            public string GetText(string url)
            {
                if (Offline) throw new IOException("no network in the self-test");
                return Html;
            }

            public void Download(string url, string dest, Action<long, long> progress)
            {
                Downloads++;
                Asked.Add(url);
                if (Offline) throw new IOException("no network in the self-test");
                if (ThrowFor != null) { var ex = ThrowFor(url); if (ex != null) throw ex; }
                byte[] payload = PayloadFor != null ? PayloadFor(url) : Payload;
                string dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                // null: the fetch "worked" and left nothing behind (B-20 - bytes that never became a readable file)
                if (payload == null) { progress?.Invoke(0, 0); return; }
                File.WriteAllBytes(dest, payload);
                progress?.Invoke(payload.Length, payload.Length);
            }
        }

        /// <summary>The exception Windows really gives when the drive fills up. What matters is the CODE, not the
        /// words - the words are in the system's own language and BepVerifier must not be reading them.</summary>
        static IOException DiskFull() => new IOException("the drive is full", unchecked((int)0x80070070));

        /// <summary>Did this throw the app's own "I do not fetch from there"?</summary>
        static bool RefusedBy(Action a)
        {
            try { a(); return false; }
            catch (AddressRefusedException) { return true; }
            catch (Exception) { return false; }
        }

        /// <summary>A little zip shaped like BepInEx's: it holds the one entry the installer looks for.</summary>
        static string MakeBepZip(string path, string coreText)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            using (var z = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Entry(z, "BepInEx/core/BepInEx.Core.dll", coreText);
                Entry(z, "winhttp.dll", "the loader");
            }
            return path;
        }

        static Installer BepInstaller(SelfTestRunner r, string root, IWebFetch web, out string core)
        {
            string game = Path.Combine(root, "game");
            core = Path.Combine(game, @"BepInEx\core\BepInEx.Core.dll");
            return new Installer
            {
                Paths = ModPaths.For(game),
                Src = root,
                CacheDir = Path.Combine(root, "cache"),
                Web = web,
                State = LauncherStateFile.InMemory(),
            };
        }

        static void BepHashTests(SelfTestRunner r)
        {
            r.Section("BepInEx: the file itself");

            // ---- the pin that ships with this build cannot quietly rot
            r.Test("the version this build installs has a pinned fingerprint", () =>
            {
                string pinned = AppInfo.BepSha256(AppInfo.BepInExVersion);
                r.Check("BepInEx " + AppInfo.BepInExVersion + " has one", !string.IsNullOrEmpty(pinned));
                r.Check("and it is written as 64 lowercase hex characters", FileHash.LooksLikeSha256(pinned), pinned ?? "(none)");
                r.Check("a version nobody pinned has none", AppInfo.BepSha256("6.0.0-be.999") == null);
                r.Check("no version at all has none", AppInfo.BepSha256(null) == null && AppInfo.BepSha256("") == null);
                // the pinned addresses and the cached file's name are for that same version: a raised version number
                // with the old URLs left behind would download the old zip and then fail the hash for the wrong reason
                r.Check("the cached file's name names that version", AppInfo.BepZipName.IndexOf(AppInfo.BepInExVersion, StringComparison.OrdinalIgnoreCase) >= 0, AppInfo.BepZipName);
                foreach (var u in AppInfo.BepUrls)
                {
                    r.Check("the pinned address names that version: " + u, u.IndexOf(AppInfo.BepInExVersion, StringComparison.OrdinalIgnoreCase) >= 0);
                    r.Check("... and the app is allowed to fetch it", WebFetch.AllowedUrl(u));
                }
            });

            // ---- how a hash is read and compared at all
            r.Test("a fingerprint is read the same way every time", () =>
            {
                string root = r.NewDir("sha");
                string f = SelfTestRunner.Touch(Path.Combine(root, "a.bin"), "abc");
                // the SHA-256 of "abc" is the one everybody's sha256sum prints
                r.Equal("the known answer for \"abc\"", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", FileHash.Sha256(f));
                r.Check("reading it twice gives the same answer", FileHash.Sha256(f) == FileHash.Sha256(f));
                r.Check("upper and lower case are the same fingerprint", FileHash.Same("ABCD", "abcd"));
                r.Check("nothing never matches anything", !FileHash.Same(null, null) && !FileHash.Same("", "") && !FileHash.Same("abcd", null));
                r.Check("a short or odd value is not a fingerprint", !FileHash.LooksLikeSha256("abcd") && !FileHash.LooksLikeSha256(new string('g', 64)) && !FileHash.LooksLikeSha256(null));
                r.Check("upper case is not how we write one down", !FileHash.LooksLikeSha256(new string('A', 64)));
            });

            // ---- 1. the right file: it is checked, then unpacked
            r.Test("the right file is installed", () =>
            {
                string root = r.NewDir("bep-good");
                string source = MakeBepZip(Path.Combine(root, "source.zip"), "the real core");
                byte[] good = File.ReadAllBytes(source);
                string sha = FileHash.Sha256(source);
                var web = new FakeWeb { Payload = good };
                string core;
                var inst = BepInstaller(r, root, web, out core);
                var lines = new List<string>();
                inst.Log = lines.Add;
                inst.BepSha256 = _ => sha;
                r.Check("the step succeeds", inst.StepBepInEx(false), string.Join(" | ", lines.ToArray()));
                r.Equal("it was fetched once", 1, web.Downloads);
                r.Check("BepInEx is in the game copy", File.Exists(core));
                r.Check("and the log says the file was checked", lines.Exists(l => l.IndexOf(S.T("ja", "in_hash_ok", AppInfo.BepInExVersion), StringComparison.Ordinal) >= 0), string.Join(" | ", lines.ToArray()));
                // run again: the cached file is checked too, and is good, so nothing is fetched a second time
                Directory.Delete(Path.Combine(root, "game"), true);
                r.Check("the second run uses the cached file", inst.StepBepInEx(true));
                r.Equal("and fetched nothing again", 1, web.Downloads);
            });

            // ---- 2. a swapped file: refused, deleted, nothing unpacked
            r.Test("a file that is not the pinned one is never unpacked", () =>
            {
                string root = r.NewDir("bep-bad");
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                // a perfectly good zip, with the entry the installer looks for - but not OUR file
                string swapped = MakeBepZip(Path.Combine(root, "swapped.zip"), "somebody else's core");
                string sha = FileHash.Sha256(real);
                r.Check("the two files really do differ", FileHash.Sha256(swapped) != sha);
                var web = new FakeWeb { Payload = File.ReadAllBytes(swapped) };
                string core;
                var inst = BepInstaller(r, root, web, out core);
                var lines = new List<string>();
                inst.Log = lines.Add;
                inst.BepSha256 = _ => sha;
                r.Check("the step fails", !inst.StepBepInEx(false));
                r.Check("nothing was unpacked into the game copy", !File.Exists(core));
                r.Check("the file that came down was deleted", !File.Exists(Path.Combine(root, "cache", AppInfo.BepZipName)));
                r.Check("no half-file is left in the cache either", !Directory.Exists(Path.Combine(root, "cache")) || Directory.GetFiles(Path.Combine(root, "cache"), "*.part").Length == 0);
                r.Check("the person is told plainly", lines.Exists(l => l.IndexOf("消しました", StringComparison.Ordinal) >= 0), string.Join(" | ", lines.ToArray()));
                r.Check("and the two values are written down", lines.Exists(l => l.IndexOf(sha, StringComparison.OrdinalIgnoreCase) >= 0));
            });

            // ---- 3. a file that stopped half-way: refused by the hash BEFORE it is opened as a zip
            r.Test("a download that stopped half-way is refused", () =>
            {
                string root = r.NewDir("bep-cut");
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core, long enough to cut in half");
                byte[] whole = File.ReadAllBytes(real);
                var half = new byte[whole.Length / 2];
                Array.Copy(whole, half, half.Length);
                string sha = FileHash.Sha256(real);
                var web = new FakeWeb { Payload = half };
                string core;
                var inst = BepInstaller(r, root, web, out core);
                var lines = new List<string>();
                inst.Log = lines.Add;
                inst.BepSha256 = _ => sha;
                r.Check("the step fails", !inst.StepBepInEx(false));
                r.Check("nothing was unpacked", !File.Exists(core));
                r.Check("the half file was deleted", !File.Exists(Path.Combine(root, "cache", AppInfo.BepZipName)));
                // it must fail on the fingerprint, not on "this is not a zip": the check has to come first, so a file
                // that is broken in some cleverer way is stopped before anything opens it
                r.Check("it was the fingerprint that stopped it", lines.Exists(l => l.IndexOf("ファイルの照合", StringComparison.Ordinal) >= 0), string.Join(" | ", lines.ToArray()));
                r.Check("and it never said the zip was merely missing an entry", !lines.Exists(l => l.IndexOf("BepInEx.Core.dll", StringComparison.Ordinal) >= 0), string.Join(" | ", lines.ToArray()));
            });

            // ---- 4. a version we have no pin for: refused, and NOT fetched "anyway"
            r.Test("a version with no pinned fingerprint is refused, not installed anyway", () =>
            {
                string root = r.NewDir("bep-unknown");
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                string core;
                var inst = BepInstaller(r, root, web, out core);
                var lines = new List<string>();
                inst.Log = lines.Add;
                inst.BepSha256 = _ => null;
                r.Check("the step fails", !inst.StepBepInEx(false));
                r.Equal("and nothing was fetched at all", 0, web.Downloads);
                r.Check("nothing was unpacked", !File.Exists(core));
                r.Check("it says why, in the person's own words", lines.Exists(l => l == S.T("ja", "in_bep_nohash", AppInfo.BepInExVersion)), string.Join(" | ", lines.ToArray()));
                // and not even a file already sitting in the shared cache folder gets through
                var web2 = new FakeWeb { Payload = File.ReadAllBytes(real) };
                string core2;
                var inst2 = BepInstaller(r, root, web2, out core2);
                inst2.BepSha256 = _ => null;
                Directory.CreateDirectory(Path.Combine(root, "cache"));
                File.Copy(real, Path.Combine(root, "cache", AppInfo.BepZipName), true);
                r.Check("a cached file is not a way round it", !inst2.StepBepInEx(false));
                r.Check("... and it was not unpacked", !File.Exists(core2));
            });

            // ---- 5. the file we checked IS the file we unpack: one open handle, held the whole way (v0.4 review)
            r.Test("the checked file cannot be swapped before it is unpacked", () =>
            {
                string root = r.NewDir("bep-hold");
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                string byPath = FileHash.Sha256(real);

                using (var vz = VerifiedZip.Open(real))
                {
                    r.Equal("the fingerprint read from the handle is the file's own", byPath, vz.Sha256);
                    r.Check("reading it again gives the same answer", vz.Sha256 == byPath);
                    // the whole point: while we hold it, nothing else running as this Windows user may write to it
                    bool refused = false;
                    try { using (new FileStream(real, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } }
                    catch (IOException) { refused = true; }
                    r.Check("nobody else can write to it while it is held", refused);
                    r.Check("the entry check reads the same handle", vz.Has("BepInEx/core/BepInEx.Core.dll"));
                    r.Check("and an entry that is not in it is still not in it", !vz.Has("BepInEx/core/nope.dll"));
                    string dest = Path.Combine(root, "out");
                    r.Equal("the unpacking reads it too, and writes every file", 2, vz.ExpandOver(dest, new string[0]));
                    r.Equal("what landed is what was in the zip", "the real core",
                        File.ReadAllText(Path.Combine(dest, @"BepInEx\core\BepInEx.Core.dll")));
                }
                // and the hold is let go again - a refused file could never be deleted otherwise
                bool writable = false;
                try { using (new FileStream(real, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } writable = true; }
                catch (IOException) { }
                r.Check("the file is let go afterwards", writable);
            });

            // ---- 6. the index page is searched for the version this build installs, not one written out by hand
            r.Test("the spare way in follows the pinned version", () =>
            {
                string v = AppInfo.BepInExVersion;
                string page =
                    "<a href=\"BepInEx-Unity.IL2CPP-win-x86-" + v + "+deadbee.zip\">this one</a>" +
                    "<a href=\"BepInEx-Unity.IL2CPP-win-x86-9.9.9-be.1+cafe.zip\">a different version</a>" +
                    "<a href=\"BepInEx-Unity.IL2CPP-win-x64-" + v + "+deadbee.zip\">the wrong architecture</a>";
                var hits = new List<string>();
                foreach (System.Text.RegularExpressions.Match m in Installer.BepHref.Matches(page)) hits.Add(m.Groups[1].Value);
                r.Equal("exactly one link is taken from the page", 1, hits.Count);
                r.Check("and it is the one naming the version this build installs",
                    hits.Count == 1 && hits[0].IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0, string.Join(" | ", hits.ToArray()));
                // and the pattern carries that version, not one written out beside it: raise AppInfo.BepInExVersion
                // and leave a literal here, and the two checks above stop finding anything at all
                r.Check("the pattern is built from AppInfo, so raising the version moves it too",
                    Installer.BepHref.ToString().IndexOf(System.Text.RegularExpressions.Regex.Escape(v), StringComparison.Ordinal) >= 0,
                    Installer.BepHref.ToString());
            });

            // ---- and a file lying in the shared cache folder is checked like any other
            r.Test("a cached file nobody checked is not trusted", () =>
            {
                string root = r.NewDir("bep-cache");
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                string other = MakeBepZip(Path.Combine(root, "other.zip"), "put there by something else");
                string sha = FileHash.Sha256(real);
                string cached = Path.Combine(root, "cache", AppInfo.BepZipName);
                Directory.CreateDirectory(Path.GetDirectoryName(cached));
                File.Copy(other, cached, true);
                var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                string core;
                var inst = BepInstaller(r, root, web, out core);
                inst.BepSha256 = _ => sha;
                r.Check("the step succeeds", inst.StepBepInEx(false));
                r.Equal("because the cached file was thrown away and the real one fetched", 1, web.Downloads);
                r.Equal("and what is in the game copy is the real one", "the real core", File.ReadAllText(core));
            });
        }

        // ------------------------------------------------------------------ "--verify-download" (src\Core\BepVerifier.cs)
        // The command that is run once by hand before a release. Everything here uses the fake IWebFetch and zips this
        // file makes: no byte leaves the PC and no window opens.
        //
        // Two of these are the reason the command exists at all and must never be deleted:
        //   B-10 the game folder is not touched - not one byte, not one timestamp, and no .starpocket-unpacking marker;
        //   B-11 %TEMP%\PocketRolesLauncher, which is shared with the PowerShell launcher, gains nothing.
        // A check that quietly wrote into the folder the owner plays in would be worse than no check.

        /// <summary>A little zip with exactly these entries (name, text, name, text, ...).</summary>
        static string MakeZip(string path, params string[] nameThenText)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            using (var z = ZipFile.Open(path, ZipArchiveMode.Create))
                for (int i = 0; i + 1 < nameThenText.Length; i += 2) Entry(z, nameThenText[i], nameThenText[i + 1]);
            return path;
        }

        static BepVerifier NewVerifier(string temp, IWebFetch web, string want, List<string> log, List<string> said = null) => new BepVerifier
        {
            Web = web,
            BepSha256 = _ => want,
            Lang = () => "ja",
            Log = log.Add,
            Say = said != null ? (Action<string>)said.Add : _ => { },
            TempRoot = temp,
            LogPath = Path.Combine(temp, "client.log"),
        };

        /// <summary>Every file under <paramref name="dir"/> with its size and the moment it was last written: two of
        /// these taken either side of a run say whether anything at all happened to the folder.</summary>
        static string Snapshot(string dir)
        {
            if (!Directory.Exists(dir)) return "(no folder)";
            var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            foreach (var f in files)
            {
                var fi = new FileInfo(f);
                sb.Append(f.Substring(dir.Length)).Append('|').Append(fi.Length).Append('|').Append(fi.LastWriteTimeUtc.Ticks).Append('\n');
            }
            return sb.ToString();
        }

        static int FileCount(string dir) => Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length : -1;

        static bool HasLine(List<string> log, string part) => log.Exists(l => l.IndexOf(part, StringComparison.Ordinal) >= 0);

        static void VerifyTests(SelfTestRunner r)
        {
            r.Section("verify download");
            string v = AppInfo.BepInExVersion;

            // ---- B-1 / B-2: the real file, and nothing left behind
            r.Test("B-1: the right file is checked and unpacked", () =>
            {
                string root = r.NewDir("vd-good");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                string want = FileHash.Sha256(real);
                var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                var log = new List<string>();
                var res = NewVerifier(temp, web, want, log).Run();
                r.Check("it says so", res.Outcome == VerifyOutcome.Ok, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 0", 0, res.ExitCode);
                r.Equal("both files came out of the zip", 2, res.Files);
                r.Equal("it was fetched once (the first address answered)", 1, web.Downloads);
                r.Equal("the sentence is the finished Japanese one", S.T("ja", "vd_ok", v, 2), res.Text);
                r.Check("the log carries the two values, whether they matched or not", HasLine(log, S.T("ja", "in_hash_detail", want, want)), string.Join(" | ", log.ToArray()));
                r.Check("and that the file was the pinned one", HasLine(log, S.T("ja", "in_hash_ok", v)));

                // B-2: the 150 MB goes again, and so does the folder it was in
                r.Check("the work folder is gone", !Directory.Exists(Path.Combine(temp, BepVerifier.WorkFolderName)));
                r.Equal("nothing at all is left under %TEMP%", 0, Directory.GetFileSystemEntries(temp).Length);
                r.Check("no folder was reported as left behind", res.LeftFolder == null);
                r.Equal("and no half-file anywhere", 0, Directory.GetFiles(root, "*.part", SearchOption.AllDirectories).Length);
            });

            // ---- B-3: a file that is not ours. This is the answer that stops a release.
            r.Test("B-3: a swapped file is refused with its own exit code", () =>
            {
                string root = r.NewDir("vd-swapped");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                string swapped = MakeBepZip(Path.Combine(root, "swapped.zip"), "somebody else's core");
                string want = FileHash.Sha256(real);
                r.Check("the two files really do differ", FileHash.Sha256(swapped) != want);
                var web = new FakeWeb { Payload = File.ReadAllBytes(swapped) };
                var log = new List<string>();
                var verifier = NewVerifier(temp, web, want, log);
                var res = verifier.Run();
                r.Check("the value did not match", res.Outcome == VerifyOutcome.Mismatch, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 2, not 1: this is not our side failing", 2, res.ExitCode);
                r.Check("the sentence starts with what went wrong", res.Text.StartsWith(S.T("ja", "vd_ng_hash"), StringComparison.Ordinal), res.Text);
                r.Check("and it says where the full record is", res.Text.Contains(S.T("ja", "vd_seelog", Path.Combine(temp, "client.log"))), res.Text);
                r.Check("the file that came down is gone", !File.Exists(Path.Combine(temp, BepVerifier.WorkFolderName, AppInfo.BepZipName)));
                r.Equal("and so is everything else", 0, Directory.GetFileSystemEntries(temp).Length);
                r.Equal("every address was tried", 2, web.Downloads);
            });

            // ---- B-4: the value is checked BEFORE the file is read as an archive at all
            r.Test("B-4: a download that stopped half-way fails on the value, not on the zip", () =>
            {
                string root = r.NewDir("vd-cut");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core, long enough to cut in half");
                byte[] whole = File.ReadAllBytes(real);
                var half = new byte[whole.Length / 2];
                Array.Copy(whole, half, half.Length);
                var web = new FakeWeb { Payload = half };
                var log = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(real), log).Run();
                r.Check("it is a value that did not match", res.Outcome == VerifyOutcome.Mismatch, string.Join(" | ", log.ToArray()));
                r.Check("not \"the zip is missing something\"", res.Outcome != VerifyOutcome.BadZip);
                r.Equal("exit code 2", 2, res.ExitCode);
                // the proof that the order is right: the two values were written down, and nothing ever looked inside
                r.Check("the two values were written down", HasLine(log, "ファイルの照合"), string.Join(" | ", log.ToArray()));
                r.Check("and the entry was never even looked for", !HasLine(log, "BepInEx.Core.dll"), string.Join(" | ", log.ToArray()));
            });

            // ---- B-5: the value matches, the contents do not
            r.Test("B-5: a zip without BepInEx.Core.dll is refused too", () =>
            {
                string root = r.NewDir("vd-odd");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                string odd = MakeZip(Path.Combine(root, "odd.zip"), "winhttp.dll", "the loader", "readme.txt", "and nothing else");
                var web = new FakeWeb { Payload = File.ReadAllBytes(odd) };
                var log = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(odd), log).Run();
                r.Check("the contents were wrong", res.Outcome == VerifyOutcome.BadZip, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 2 as well: what came down was not ours", 2, res.ExitCode);
                r.Check("the sentence names what was missing", res.Text.Contains("BepInEx.Core.dll"), res.Text);
                r.Equal("nothing is left", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-6: a version with no pin fetches NOTHING (the installer's own rule)
            r.Test("B-6: a version nobody pinned is not fetched at all", () =>
            {
                string root = r.NewDir("vd-nopin");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                var web = new FakeWeb { Payload = new byte[] { 1, 2, 3 } };
                var log = new List<string>();
                var res = NewVerifier(temp, web, null, log).Run();
                r.Check("it says there is no record of the right file", res.Outcome == VerifyOutcome.NoPin);
                r.Equal("exit code 1: our side could not tell", 1, res.ExitCode);
                r.Equal("and nothing was fetched", 0, web.Downloads);
                r.Check("the sentence is the installer's own", res.Text.StartsWith(S.T("ja", "in_bep_nohash", v), StringComparison.Ordinal), res.Text);
                r.Check("no work folder was even made", !Directory.Exists(Path.Combine(temp, BepVerifier.WorkFolderName)));
                r.Equal("%TEMP% was not touched", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-7: nothing answered
            r.Test("B-7: no connection says exactly that", () =>
            {
                string root = r.NewDir("vd-offline");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                var web = new FakeWeb { Offline = true };
                var log = new List<string>();
                var res = NewVerifier(temp, web, new string('a', 64), log).Run();
                r.Check("no file came down", res.Outcome == VerifyOutcome.NoFile, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 1, not 2: nobody's file was wrong", 1, res.ExitCode);
                r.Check("the sentence says so", res.Text.StartsWith(S.T("ja", "vd_ng_net"), StringComparison.Ordinal), res.Text);
                r.Equal("nothing is left behind", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-8: the second address, like the install
            r.Test("B-8: a wrong first address does not stop the right second one", () =>
            {
                string root = r.NewDir("vd-second");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                string swapped = MakeBepZip(Path.Combine(root, "swapped.zip"), "somebody else's core");
                byte[] good = File.ReadAllBytes(real), bad = File.ReadAllBytes(swapped);
                string first = AppInfo.BepUrls[0];
                var web = new FakeWeb { PayloadFor = u => u == first ? bad : good };
                var log = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(real), log).Run();
                r.Check("the second address answered properly", res.Outcome == VerifyOutcome.Ok, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 0", 0, res.ExitCode);
                r.Equal("two addresses were tried, in order", 2, web.Downloads);
                r.Equal("the one that worked is the second", AppInfo.BepUrls[1], res.Url);
                r.Equal("and nothing is left", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-9: whose folder is it? The rule is the self-test's own: only our own leftovers are deleted.
            r.Test("B-9: a folder of somebody else's is stepped around, never deleted", () =>
            {
                string root = r.NewDir("vd-folder");
                string mine = Path.Combine(root, "mine");
                string theirs = Path.Combine(root, "theirs");
                Directory.CreateDirectory(mine);
                Directory.CreateDirectory(theirs);

                // ours: it carries our marker and nothing holds it, so it is thrown away and made again
                string ourOld = Path.Combine(mine, BepVerifier.WorkFolderName);
                SelfTestRunner.Touch(Path.Combine(ourOld, BepVerifier.MarkerName), BepVerifier.MarkerText);
                SelfTestRunner.Touch(Path.Combine(ourOld, "leftover.bin"), "from a run that was killed");
                var got = BepVerifier.PrepareWorkFolder(mine);
                r.Equal("the same name is used again", ourOld, got.Path);
                r.Equal("and it says which leftovers it cleared", ourOld, got.Cleaned);
                r.Check("what was in it is gone", !File.Exists(Path.Combine(ourOld, "leftover.bin")));
                r.Check("and the marker is back", File.Exists(Path.Combine(ourOld, BepVerifier.MarkerName)));
                r.Equal("the folder is now empty but for the marker and the lock", 2, Directory.GetFiles(ourOld).Length);
                got.Dispose();
                r.Check("letting go takes the lock file with it", !File.Exists(Path.Combine(ourOld, BepVerifier.LockName)));
                r.Check("it is cleaned up again", BepVerifier.RemoveWorkFolder(ourOld) == null);

                // theirs: no marker of ours, so it is not touched and we stand aside
                string notOurs = Path.Combine(theirs, BepVerifier.WorkFolderName);
                SelfTestRunner.Touch(Path.Combine(notOurs, "somebody-elses.txt"), "please leave this alone");
                var aside = BepVerifier.PrepareWorkFolder(theirs);
                r.Check("a different folder is used", !string.Equals(aside.Path, notOurs, StringComparison.OrdinalIgnoreCase), aside.Path);
                r.Check("and it is named after this process", Path.GetFileName(aside.Path).StartsWith(BepVerifier.WorkFolderName + "-", StringComparison.Ordinal), aside.Path);
                r.Check("nothing of anyone else's was cleared", aside.Cleaned == null);
                r.Check("their file is still theirs", File.Exists(Path.Combine(notOurs, "somebody-elses.txt")));
                r.Equal("and their folder has nothing of ours in it", 1, Directory.GetFiles(notOurs).Length);
                aside.Dispose();
                r.Check("ours is cleaned up", BepVerifier.RemoveWorkFolder(aside.Path) == null);
            });

            // ---- B-10: THE reason this command exists rather than "install into a folder you can throw away"
            r.Test("B-10: the game folder is not touched, not by one byte", () =>
            {
                string root = r.NewDir("vd-gamefolder");
                string temp = Path.Combine(root, "temp");
                string game = Path.Combine(root, "Among Us PocketRoles");
                Directory.CreateDirectory(temp);
                SelfTestRunner.Touch(Path.Combine(game, "Among Us.exe"), "the game");
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\core\BepInEx.Core.dll"), "the one that is installed");
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\PocketRoles.dll"), "the mod");
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\config\PocketRoles.cfg"), "the owner's own settings");
                string before = Snapshot(game);

                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                var log = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(real), log).Run();

                r.Check("the check itself worked", res.Outcome == VerifyOutcome.Ok, string.Join(" | ", log.ToArray()));
                r.Equal("the game folder is exactly as it was", before, Snapshot(game));
                r.Equal("no file was added to it", 4, FileCount(game));
                r.Equal("and no \"unpacking in progress\" marker was written anywhere",
                    0, Directory.GetFiles(root, ".starpocket-unpacking", SearchOption.AllDirectories).Length);
                // the folder's name never reaches this code: nothing in what was said even mentions it
                r.Check("the game folder is not so much as named in the log", !HasLine(log, game), string.Join(" | ", log.ToArray()));
                r.Check("nor in the sentence the owner reads", !res.Text.Contains(game), res.Text);
            });

            // ---- B-11: the folder shared with the PowerShell launcher gains nothing
            r.Test("B-11: the shared cache folder gains nothing", () =>
            {
                string root = r.NewDir("vd-cache");
                string temp = Path.Combine(root, "temp");
                string shared = Path.Combine(temp, "PocketRolesLauncher");   // what ClientContext.CacheDir would be
                Directory.CreateDirectory(temp);
                SelfTestRunner.Touch(Path.Combine(shared, "something.txt"), "put here by the PowerShell launcher");
                string before = Snapshot(shared);

                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                var log = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(real), log).Run();

                r.Check("the check itself worked", res.Outcome == VerifyOutcome.Ok, string.Join(" | ", log.ToArray()));
                r.Equal("the shared folder has exactly what it had", before, Snapshot(shared));
                r.Equal("one file, the one that was already there", 1, FileCount(shared));
                r.Check("no BepInEx zip was left for a later install to pick up", !File.Exists(Path.Combine(shared, AppInfo.BepZipName)));
                r.Equal("and %TEMP% holds only that folder", 1, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-12: one list, read by both. Two lists written out separately always drift apart, and then the one
            //            real check before a release would be checking addresses the install does not use.
            r.Test("B-12: the check and the install read the same addresses in the same order", () =>
            {
                string root = r.NewDir("vd-addresses");
                string page =
                    "<a href=\"735/BepInEx-Unity.IL2CPP-win-x86-" + v + "+deadbee.zip\">this one</a>" +
                    "<a href=\"http://builds.bepinex.dev/projects/bepinex_be/735/BepInEx-Unity.IL2CPP-win-x86-" + v + "+plain.zip\">plaintext</a>" +
                    "<a href=\"735/BepInEx-Unity.IL2CPP-win-x86-9.9.9-be.1+other.zip\">another version</a>";

                var refused = new List<string>();
                var listWeb = new FakeWeb { Html = page };
                var expected = Installer.BepAddresses(listWeb, refused.Add);
                r.Equal("the two pinned addresses plus the one the page offers", 3, expected.Count);
                r.Equal("the pinned ones come first", AppInfo.BepUrls[0], expected[0]);
                r.Equal("... both of them", AppInfo.BepUrls[1], expected[1]);
                r.Equal("a plaintext link is refused, not fetched", 1, refused.Count);
                r.Check("and the refused one is not in the list", !expected.Contains(refused[0]), refused[0]);

                // now what the INSTALL really asks for: a payload that never matches, so every address is tried
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                string other = MakeBepZip(Path.Combine(root, "other.zip"), "not ours");
                var web = new FakeWeb { Html = page, Payload = File.ReadAllBytes(other) };
                var lines = new List<string>();
                string core;
                var inst = BepInstaller(r, root, web, out core);
                inst.Log = lines.Add;
                inst.BepSha256 = _ => FileHash.Sha256(real);
                r.Check("the install's BepInEx step fails (nothing matched)", !inst.StepBepInEx(false));
                r.Equal("it tried as many addresses as the list has", expected.Count, web.Asked.Count);
                for (int i = 0; i < expected.Count && i < web.Asked.Count; i++)
                    r.Equal("address " + (i + 1) + " is the same one", expected[i], web.Asked[i]);
                r.Check("and the install says out loud which link it would not follow", lines.Exists(l => l == S.T("ja", "in_dl_refused", refused[0])), string.Join(" | ", lines.ToArray()));

                // and the verifier walks that very list too
                var vWeb = new FakeWeb { Html = page, Payload = File.ReadAllBytes(other) };
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                var log = new List<string>();
                NewVerifier(temp, vWeb, FileHash.Sha256(real), log).Run();
                r.Equal("the check asked for the same number of addresses", expected.Count, vWeb.Asked.Count);
                for (int i = 0; i < expected.Count && i < vWeb.Asked.Count; i++)
                    r.Equal("the check's address " + (i + 1) + " is the same one", expected[i], vWeb.Asked[i]);
            });

            // ---- B-13: the little window belongs to Program, never to this class
            r.Test("B-13: BepVerifier only builds the sentence; Program opens the window", () =>
            {
                var used = new HashSet<string>(StringComparer.Ordinal);
                bool whole = NamespacesUsedBy(typeof(BepVerifier), used);
                r.Check("the whole of BepVerifier could be read", whole);
                // proof that the reading really worked, so that "it uses no WinForms" is not an empty answer
                r.Check("the reading found what it should (it does work with files)", used.Contains("System.IO"), string.Join(", ", used));
                r.Check("BepVerifier never reaches into WinForms", !used.Contains("System.Windows.Forms"), string.Join(", ", used));

                var boxed = new HashSet<string>(StringComparer.Ordinal);
                var opener = typeof(Program).GetMethod("RunVerifyDownload", BindingFlags.NonPublic | BindingFlags.Static);
                r.Check("Program.RunVerifyDownload is still the one that runs the check", opener != null);
                if (opener != null)
                {
                    WalkIl(opener, boxed);
                    r.Check("... and it is the one that opens the window", boxed.Contains("System.Windows.Forms"), string.Join(", ", boxed));
                }

                // and running the whole thing really does open nothing
                string root = r.NewDir("vd-nowindow");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                var log = new List<string>();
                int windowsBefore = System.Windows.Forms.Application.OpenForms.Count;
                var res = NewVerifier(temp, web, FileHash.Sha256(real), log).Run();
                r.Equal("no window was opened by the run", windowsBefore, System.Windows.Forms.Application.OpenForms.Count);
                r.Check("and the sentence came back as a plain string", res.Text.Length > 0 && res.Outcome == VerifyOutcome.Ok);
            });

            // ---- B-14: three languages, and the same holes in each
            r.Test("B-14: the new words are in ja / zh-CN / en with the same {0}s", () =>
            {
                string[] keys =
                {
                    "vd_start", "vd_where", "vd_prog", "vd_prog_mb", "vd_prog_files", "vd_got", "vd_ok", "vd_ng_hash",
                    "vd_ng_zip", "vd_ng_expand", "vd_ng_net", "vd_ng_read", "vd_ng_space", "vd_ng_refused",
                    "vd_ng_temp", "vd_temp_taken", "vd_busy", "vd_cleaned", "vd_left", "vd_seelog",
                };
                r.Equal("twenty words of its own", 20, keys.Length);
                foreach (var k in keys)
                {
                    bool all = S.Table["ja"].ContainsKey(k) && S.Table["zh-CN"].ContainsKey(k) && S.Table["en"].ContainsKey(k);
                    r.Check(k + " is in all three languages", all);
                    if (!all) continue;
                    int ja = Holes(S.Table["ja"][k]);
                    r.Equal(k + ": zh-CN has the same holes to fill", ja, Holes(S.Table["zh-CN"][k]));
                    r.Equal(k + ": en has the same holes to fill", ja, Holes(S.Table["en"][k]));
                }
                r.Equal("vd_ok carries the version and the number of files", 2, Holes(S.T("ja", "vd_ok")));
                r.Equal("vd_ng_hash needs nothing filled in", 0, Holes(S.T("ja", "vd_ng_hash")));
                // and the keys it borrows are really there in all three
                foreach (var k in new[] { "in_bep_nohash", "in_dl", "in_dl_refused", "in_hash_ok", "in_hash_detail", "err" })
                    r.Check("it borrows " + k + ", which exists everywhere",
                        S.Table["ja"].ContainsKey(k) && S.Table["zh-CN"].ContainsKey(k) && S.Table["en"].ContainsKey(k));

                // every outcome there is has a sentence of its OWN. Until the v0.5 review five different things all
                // ended up saying "the official site could not be reached, so no file came down", including a full
                // drive and the official site pointing somewhere off the list.
                var seen = new Dictionary<string, VerifyOutcome>(StringComparer.Ordinal);
                var speaker = new BepVerifier { Lang = () => "ja" };
                foreach (VerifyOutcome o in Enum.GetValues(typeof(VerifyOutcome)))
                {
                    string said = speaker.Reason(new VerifyResult { Outcome = o, ExpandError = "why", Error = "why" });
                    r.Check(o + " has something to say", !string.IsNullOrEmpty(said));
                    VerifyOutcome already;
                    r.Check(o + " does not borrow another outcome's sentence",
                        !seen.TryGetValue(said ?? "", out already), o + " says what " + already + " says: " + said);
                    seen[said ?? ""] = o;
                }
            });

            // ---- B-15: the owner double-clicked the .cmd twice. The second one must not clear up after the first.
            r.Test("B-15: a second check leaves the one that is running alone", () =>
            {
                string root = r.NewDir("vd-twice");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);

                var first = BepVerifier.PrepareWorkFolder(temp);
                string half = Path.Combine(first.Path, AppInfo.BepZipName + ".part");
                using (new FileStream(half, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                    var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                    var log = new List<string>();
                    var res = NewVerifier(temp, web, FileHash.Sha256(real), log).Run();

                    r.Check("the second one waits instead of working", res.Outcome == VerifyOutcome.Busy, string.Join(" | ", log.ToArray()));
                    r.Equal("exit code 1: it checked nothing", 1, res.ExitCode);
                    r.Equal("and says so in one sentence, and only that", S.T("ja", "vd_busy"), res.Text);
                    r.Check("nobody is sent looking for a log over it", !res.Text.Contains(S.T("ja", "vd_seelog", "")), res.Text);
                    r.Equal("nothing was fetched", 0, web.Downloads);
                    // what used to happen: the marker was deleted first, then the half-received file could not be
                    r.Check("the running check still has its marker", File.Exists(Path.Combine(first.Path, BepVerifier.MarkerName)));
                    r.Check("and its half-received file", File.Exists(half));
                    r.Check("and its folder", Directory.Exists(first.Path));

                    // and the narrow version of the same thing: two that both got past the look and raced for the name
                    bool busy = false;
                    try { BepVerifier.Make(first.Path, null).Dispose(); }
                    catch (VerifyBusyException) { busy = true; }
                    r.Check("the one that loses the race waits as well", busy);
                    r.Check("and clears away nothing of the winner's", Directory.Exists(first.Path) && File.Exists(half));
                }
                first.Dispose();
                r.Check("when the first one lets go, the lock goes with it", !File.Exists(Path.Combine(first.Path, BepVerifier.LockName)));
                BepVerifier.RemoveWorkFolder(first.Path);
                r.Equal("and %TEMP% is empty again", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-16: the window is the one thing the owner has to be able to read
            r.Test("B-16: a name held by somebody else is explained in the owner's own language", () =>
            {
                string root = r.NewDir("vd-taken");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                string plain = Path.Combine(temp, BepVerifier.WorkFolderName);
                string ours = plain + "-" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
                SelfTestRunner.Touch(Path.Combine(plain, "someone-elses.txt"), "not ours");
                SelfTestRunner.Touch(Path.Combine(ours, "someone-elses.txt"), "not ours either");

                var web = new FakeWeb { Payload = new byte[] { 1, 2, 3 } };
                var log = new List<string>();
                var res = NewVerifier(temp, web, new string('a', 64), log).Run();
                r.Check("no work folder could be made", res.Outcome == VerifyOutcome.NoWorkFolder, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 1", 1, res.ExitCode);
                r.Equal("the sentence, and the reason inside it, are both from the string table",
                    S.T("ja", "vd_ng_temp", S.T("ja", "vd_temp_taken", ours)), FirstLine(res.Text));
                r.Check("the English of the exception is nowhere near the window",
                    !res.Text.Contains("was not made by this command"), res.Text);
                r.Equal("nothing was fetched", 0, web.Downloads);
                r.Equal("and neither folder was touched", 2, Directory.GetDirectories(temp).Length);
            });

            // ---- B-17: a full drive is not a network problem
            r.Test("B-17: no room while receiving says exactly that", () =>
            {
                string root = r.NewDir("vd-space");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                var web = new FakeWeb { ThrowFor = _ => DiskFull() };
                var log = new List<string>();
                var res = NewVerifier(temp, web, new string('a', 64), log).Run();
                r.Check("it is read as a full drive", res.Outcome == VerifyOutcome.NoRoom, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 1", 1, res.ExitCode);
                r.Check("the sentence is about room", res.Text.StartsWith(S.T("ja", "vd_ng_space"), StringComparison.Ordinal), res.Text);
                r.Check("and never tells the owner to check the network", !res.Text.Contains(S.T("ja", "vd_ng_net")), res.Text);
                r.Equal("it stopped instead of fetching 31 MB from every address onto the same full drive", 1, web.Downloads);
                r.Equal("nothing is left", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-18: the codes, not the words. A Japanese Windows says it in Japanese.
            r.Test("B-18: \"no room\" is recognised by its code, in either half of the job", () =>
            {
                r.Check("ERROR_DISK_FULL", BepVerifier.OutOfRoom(DiskFull()));
                r.Check("ERROR_HANDLE_DISK_FULL", BepVerifier.OutOfRoom(new IOException("x", unchecked((int)0x80070027))));
                r.Check("and when it comes wrapped in something else",
                    BepVerifier.OutOfRoom(new InvalidOperationException("unpacking", DiskFull())));
                r.Check("an ordinary file error is not mistaken for it", !BepVerifier.OutOfRoom(new IOException("a file is in use")));
                r.Check("nor is a missing file", !BepVerifier.OutOfRoom(new FileNotFoundException("gone")));
                r.Check("nor the refusal of an address", !BepVerifier.OutOfRoom(new AddressRefusedException("https://example.invalid/x")));
            });

            // ---- B-19: the one failure that should stop a release even though nobody's bytes were wrong
            r.Test("B-19: being pointed off the list is its own answer, not \"try again later\"", () =>
            {
                string root = r.NewDir("vd-refused");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                var web = new FakeWeb { ThrowFor = _ => new AddressRefusedException("https://example.invalid/elsewhere.zip") };
                var log = new List<string>();
                var res = NewVerifier(temp, web, new string('a', 64), log).Run();
                r.Check("it is not filed as a network failure", res.Outcome == VerifyOutcome.Refused, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 1: no file came down", 1, res.ExitCode);
                r.Check("the sentence says to tell the author", res.Text.StartsWith(S.T("ja", "vd_ng_refused"), StringComparison.Ordinal), res.Text);
                r.Check("and not to check the cable", !res.Text.Contains(S.T("ja", "vd_ng_net")), res.Text);
                r.Equal("every address was tried first", 2, web.Downloads);

                // and that exception is what the app's OWN fetching really throws - before it touches the network
                string never = Path.Combine(root, "never.zip");
                r.Check("a page off the list is refused that way", RefusedBy(() => WebFetch.Instance.GetText("https://example.invalid/list")));
                r.Check("a plaintext address on the list too", RefusedBy(() => WebFetch.Instance.Download("http://builds.bepinex.dev/x.zip", never, null)));
                r.Check("and nothing was written while refusing", !File.Exists(never));
            });

            // ---- B-20: bytes that arrived and could not be read is not "nothing arrived"
            r.Test("B-20: a fetch that left no readable file is not called a lost connection", () =>
            {
                string root = r.NewDir("vd-noread");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                var web = new FakeWeb { PayloadFor = _ => null };
                var log = new List<string>();
                var res = NewVerifier(temp, web, new string('a', 64), log).Run();
                r.Check("this PC could not read what came down", res.Outcome == VerifyOutcome.NoRead, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 1", 1, res.ExitCode);
                r.Check("the sentence says receiving stopped part-way", res.Text.StartsWith(S.T("ja", "vd_ng_read"), StringComparison.Ordinal), res.Text);
                r.Check("it does not claim the official site was unreachable", !res.Text.Contains(S.T("ja", "vd_ng_net")), res.Text);
                r.Check("and it does not claim no file came down either", !res.Text.Contains("受け取れていません"), res.Text);
                r.Equal("both addresses were tried", 2, web.Downloads);
                r.Equal("nothing is left", 0, Directory.GetFileSystemEntries(temp).Length);
                // a fetch that really failed still says so, so the two are told apart and not merged
                var off = new FakeWeb { Offline = true };
                r.Check("and a fetch that never answered is still \"could not connect\"",
                    NewVerifier(temp, off, new string('a', 64), new List<string>()).Run().Outcome == VerifyOutcome.NoFile);
            });

            // ---- B-21: whose reason is in the sentence
            r.Test("B-21: the reason shown belongs to the address it happened at", () =>
            {
                string root = r.NewDir("vd-mixed");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                // the fingerprint is right and the unpacking cannot work: "a" is a file, and "a/b" needs it to be a folder
                string awkward = MakeZip(Path.Combine(root, "awkward.zip"),
                    "BepInEx/core/BepInEx.Core.dll", "the real core", "a", "a file", "a/b", "a file inside that file");
                string first = AppInfo.BepUrls[0];
                // the first address unpacks badly; the second fails in a completely unrelated way, afterwards
                var web = new FakeWeb { PayloadFor = u => u == first ? File.ReadAllBytes(awkward) : null };
                var log = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(awkward), log).Run();

                r.Check("the unpacking is what is reported", res.Outcome == VerifyOutcome.ExpandFailed, string.Join(" | ", log.ToArray()));
                r.Equal("exit code 1: the file itself was ours", 1, res.ExitCode);
                r.Check("there is a reason to show", !string.IsNullOrEmpty(res.ExpandError), res.ExpandError ?? "(none)");
                r.Equal("and the sentence carries that one", S.T("ja", "vd_ng_expand", res.ExpandError), FirstLine(res.Text));
                // the trap: the LATER address's own trouble used to be quoted inside this sentence
                r.Check("the second address's words are not in it", !res.Text.Contains(AppInfo.BepZipName), res.Text);
                r.Equal("both addresses were tried", 2, web.Downloads);
                r.Equal("nothing is left", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-22: a folder made and then abandoned poisons the name for ever
            r.Test("B-22: a work folder whose marker cannot be written is not left behind", () =>
            {
                string root = r.NewDir("vd-marker");
                string work = Path.Combine(root, BepVerifier.WorkFolderName);
                // the marker's own name, taken by a folder: writing that file is the one thing that cannot work here
                Directory.CreateDirectory(Path.Combine(work, BepVerifier.MarkerName));
                bool threw = false;
                try { BepVerifier.Make(work, null).Dispose(); }
                catch (Exception) { threw = true; }
                r.Check("it says it could not", threw);
                r.Check("and the folder it was making is gone", !Directory.Exists(work));
                // what used to be left was a folder with no marker in it, which every later run reads as somebody
                // else's and steps around - for ever, because nothing ever cleans it up
                var again = BepVerifier.PrepareWorkFolder(root);
                r.Equal("so the next run uses that very name", work, again.Path);
                again.Dispose();
                r.Check("and tidies up after itself", BepVerifier.RemoveWorkFolder(work) == null);
            });

            // ---- B-23: the black window was closed part-way through
            r.Test("B-23: what a killed run left behind is cleared, and said out loud", () =>
            {
                string root = r.NewDir("vd-leftovers");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                // exactly what a killed run leaves: the marker, the file, half an unpacked folder - and no lock,
                // because Windows closes a DeleteOnClose handle whether or not anyone got the chance to
                string stale = Path.Combine(temp, BepVerifier.WorkFolderName);
                SelfTestRunner.Touch(Path.Combine(stale, BepVerifier.MarkerName), BepVerifier.MarkerText);
                SelfTestRunner.Touch(Path.Combine(stale, AppInfo.BepZipName), "the 31 MB, in miniature");
                SelfTestRunner.Touch(Path.Combine(stale, @"out\BepInEx\core\BepInEx.Core.dll"), "half unpacked");

                string real = MakeBepZip(Path.Combine(root, "real.zip"), "the real core");
                var web = new FakeWeb { Payload = File.ReadAllBytes(real) };
                var log = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(real), log).Run();
                r.Check("the check itself worked", res.Outcome == VerifyOutcome.Ok, string.Join(" | ", log.ToArray()));
                r.Check("the record says what was cleared", HasLine(log, S.T("ja", "vd_cleaned", stale)), string.Join(" | ", log.ToArray()));
                r.Check("the owner's own sentence is not cluttered with it", !res.Text.Contains(S.T("ja", "vd_cleaned", stale)), res.Text);
                r.Equal("and %TEMP% is empty afterwards", 0, Directory.GetFileSystemEntries(temp).Length);
            });

            // ---- B-24: ten seconds of silence look like a program that has hung
            r.Test("B-24: the console is told how the unpacking is going", () =>
            {
                string root = r.NewDir("vd-progress");
                string temp = Path.Combine(root, "temp");
                Directory.CreateDirectory(temp);
                var parts = new List<string> { "BepInEx/core/BepInEx.Core.dll", "the real core" };
                for (int i = 0; i < 250; i++) { parts.Add("BepInEx/core/f" + i.ToString(CultureInfo.InvariantCulture) + ".bin"); parts.Add("x"); }
                string big = MakeZip(Path.Combine(root, "big.zip"), parts.ToArray());
                var web = new FakeWeb { Payload = File.ReadAllBytes(big) };
                var log = new List<string>();
                var said = new List<string>();
                var res = NewVerifier(temp, web, FileHash.Sha256(big), log, said).Run();
                r.Check("it worked", res.Outcome == VerifyOutcome.Ok, string.Join(" | ", log.ToArray()));
                r.Equal("all of it came out", 251, res.Files);
                r.Check("the hundredth file was said", said.Contains(S.T("ja", "vd_prog_files", 100)), string.Join(" | ", said.ToArray()));
                r.Check("and the two hundredth", said.Contains(S.T("ja", "vd_prog_files", 200)), string.Join(" | ", said.ToArray()));
                r.Check("but not every single one of them", said.Count < 40, said.Count.ToString(CultureInfo.InvariantCulture));
            });
        }

        /// <summary>The first line of the sentence the owner reads - the one the instructions tell them to look up.</summary>
        static string FirstLine(string text) => (text ?? "").Split(new[] { "\r\n" }, StringSplitOptions.None)[0];

        /// <summary>How many different {0}.. a text expects to have filled in.</summary>
        static int Holes(string text)
        {
            int n = 0;
            for (int i = 0; i < 10; i++) if ((text ?? "").IndexOf("{" + i + "}", StringComparison.Ordinal) >= 0) n++;
            return n;
        }

        // ---- reading compiled code, for B-13. Asking "does this class name MessageBox?" of the source would mean
        // shipping the source; asking it of the IL works in the built exe and cannot be fooled by a rename.
        static Dictionary<short, OpCode> opcodeTable;

        static Dictionary<short, OpCode> Opcodes()
        {
            if (opcodeTable != null) return opcodeTable;
            var d = new Dictionary<short, OpCode>();
            foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.FieldType == typeof(OpCode)) { var op = (OpCode)f.GetValue(null); d[op.Value] = op; }
            return opcodeTable = d;
        }

        /// <summary>The namespaces the compiled code of <paramref name="t"/> and of its nested types (the compiler puts
        /// lambdas there) actually reaches. False when some method could not be read at all.</summary>
        static bool NamespacesUsedBy(Type t, HashSet<string> into)
        {
            bool whole = true;
            foreach (var nested in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                if (!NamespacesUsedBy(nested, into)) whole = false;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var code = new List<MethodBase>();
            code.AddRange(t.GetMethods(all));
            code.AddRange(t.GetConstructors(all));
            foreach (var m in code) if (!WalkIl(m, into)) whole = false;
            return whole;
        }

        static bool WalkIl(MethodBase m, HashSet<string> into)
        {
            byte[] il;
            try
            {
                var body = m.GetMethodBody();
                if (body == null) return true;          // abstract or extern: there is no code to read
                il = body.GetILAsByteArray();
            }
            catch (Exception) { return false; }
            if (il == null) return true;
            var module = m.Module;
            Type[] typeArgs = m.DeclaringType != null && m.DeclaringType.IsGenericType ? m.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = m.IsGenericMethodDefinition ? m.GetGenericArguments() : null;
            var table = Opcodes();
            int i = 0;
            while (i < il.Length)
            {
                short value = il[i];
                if (il[i] == 0xFE)
                {
                    if (i + 1 >= il.Length) return false;
                    value = (short)((0xFE << 8) | il[i + 1]);
                    i += 2;
                }
                else i += 1;
                OpCode op;
                if (!table.TryGetValue(value, out op)) return false;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: i += 1; break;
                    case OperandType.InlineVar: i += 2; break;
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineI:
                    case OperandType.InlineSig:
                    case OperandType.InlineString:
                    case OperandType.ShortInlineR: i += 4; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: i += 8; break;
                    case OperandType.InlineSwitch:
                        if (i + 4 > il.Length) return false;
                        i += 4 + 4 * BitConverter.ToInt32(il, i);
                        break;
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                        if (i + 4 > il.Length) return false;
                        int token = BitConverter.ToInt32(il, i);
                        i += 4;
                        try
                        {
                            var member = module.ResolveMember(token, typeArgs, methodArgs);
                            Type owner = member as Type ?? member.DeclaringType;
                            if (owner != null && owner.Namespace != null) into.Add(owner.Namespace);
                        }
                        catch (Exception) { }
                        break;
                    default: return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ which file is the mod (the launcher ignores case)
        static void ReleaseNameTests(SelfTestRunner r)
        {
            r.Section("the mod's file");
            r.Test("\"Setup\" is read the way the launcher reads it", () =>
            {
                foreach (var name in new[] { "PocketRoles-Setup-0.5.6.zip", "PocketRoles-0.5.6-setup.zip", "pocketroles-0.5.6-SETUP.zip" })
                    r.Check("a setup zip is not the mod: " + name, ReleaseInfo.LooksLikeSetup(name));
                r.Check("the mod itself is not a setup", !ReleaseInfo.LooksLikeSetup("PocketRoles-0.5.6.zip"));
            });
            r.Test("the newest local zip", () =>
            {
                string src = r.NewDir("localzip");
                foreach (var n in new[] { "PocketRoles-0.5.4.zip", "PocketRoles-0.5.6-setup.zip", "PocketRoles-0.5.5.zip" })
                    SelfTestRunner.Touch(Path.Combine(src, n));
                var inst = new Installer { Src = src, Paths = ModPaths.For(Path.Combine(src, "game")), Web = new NoWeb(), State = LauncherStateFile.InMemory() };
                r.Equal("the setup zip is passed over whatever its case", Path.Combine(src, "PocketRoles-0.5.5.zip"), inst.FindLocalModZip());
            });
        }

        // ------------------------------------------------------------------ 「更新があります」の文が、読んで意味が通るか
        //
        // 2026-09-25 の不具合: 手で置いた DLL のせいで「新しい PocketRoles 0.5.5 があります
        // (導入済み: 0.5.5)」と出ました。番号が同じなのに「新しいのがある」と言っているので、
        // 読んだ人には何が起きているのか分かりません。
        // 止めるべきは判定ではなく（判定は正しい。公式に配ったファイルではないので）、文の方です。
        static void UpdateMessageTests(SelfTestRunner r)
        {
            r.Section("更新のお知らせの文");

            // GitHub が「v0.5.5 の PocketRoles-0.5.5.zip がある」と答える形
            const string Latest =
                "{\"tag_name\":\"v0.5.5\",\"html_url\":\"https://example.invalid/r\",\"assets\":[" +
                "{\"name\":\"PocketRoles-0.5.5.zip\",\"browser_download_url\":\"https://example.invalid/PocketRoles-0.5.5.zip\"," +
                "\"size\":1184888,\"updated_at\":\"2026-09-23T10:00:08Z\"}]}";
            const string Key = "gh:PocketRoles-0.5.5.zip:1184888:2026-09-23T10:00:08Z";

            Func<string, string, Installer> make = (dllVersion, modSource) =>
            {
                string game = r.NewDir("upmsg" + dllVersion + (modSource ?? "none"));
                var paths = ModPaths.For(game);
                Directory.CreateDirectory(Path.GetDirectoryName(paths.DllPath));
                // 版の読み取りは本物の DLL を要るので、ここは State と Web だけで決まる所を見ます
                var st = LauncherStateFile.InMemory();
                if (modSource != null) st.Set("modSource", modSource);
                return new Installer { Paths = paths, Web = new FixedWeb(Latest), State = st };
            };

            r.Test("判定そのもの（Test-ModCurrent）", () =>
            {
                var v555 = ReleaseInfo.Normalize("0.5.5");
                var v556 = ReleaseInfo.Normalize("0.5.6");
                r.Check("同じ番号・同じファイル → 最新", ReleaseInfo.ModCurrent(v555, v555, Key, Key));
                r.Check("同じ番号・別のファイル → 最新ではない", !ReleaseInfo.ModCurrent(v555, v555, Key, "gh:PocketRoles-0.5.4.zip:742520:x"));
                r.Check("同じ番号・出どころの記録が無い → 最新ではない", !ReleaseInfo.ModCurrent(v555, v555, Key, null));
                r.Check("入っている方が古い → 最新ではない", !ReleaseInfo.ModCurrent(v555, v556, Key, Key));
                r.Check("入っている方が新しい → 最新あつかい", ReleaseInfo.ModCurrent(v556, v555, Key, Key));
            });

            r.Test("番号が同じで中身が違う時は「新しいのがあります」と言わない", () =>
            {
                string same = S.T("ja", "up_same_other_file", "0.5.5");
                string avail = S.T("ja", "up_available", "0.5.5", "0.5.4");
                r.Check("別ファイルの文に「新しい」は出ない", same.IndexOf("新しい", StringComparison.Ordinal) < 0, same);
                r.Check("別ファイルの文は「別の物」だと言う", same.IndexOf("別の物", StringComparison.Ordinal) >= 0, same);
                r.Check("本当に新しい時の文は今までどおり", avail.IndexOf("新しい", StringComparison.Ordinal) >= 0, avail);
            });

            r.Test("3 言語そろっている", () =>
            {
                foreach (var key in new[] { "up_same_other_file", "up_same_other_file_dev" })
                {
                    string ja = S.T("ja", key, "0.5.5"), zh = S.T("zh-CN", key, "0.5.5"), en = S.T("en", key, "0.5.5");
                    foreach (var pair in new[] { new[] { "ja", ja }, new[] { "zh", zh }, new[] { "en", en } })
                    {
                        r.Check(key + " の " + pair[0] + " が空でない", !string.IsNullOrEmpty(pair[1]));
                        r.Check(key + " の " + pair[0] + " に {0} が残っていない", pair[1].IndexOf("{0}", StringComparison.Ordinal) < 0, pair[1]);
                        r.Check(key + " の " + pair[0] + " に版が入っている", pair[1].IndexOf("0.5.5", StringComparison.Ordinal) >= 0, pair[1]);
                    }
                    r.Check(key + " の 3 言語が別の文", ja != zh && zh != en && ja != en);
                }
            });

            r.Test("開発モードの文だけが、ローカルビルドの上書きに触れる", () =>
            {
                r.Check("ふつうの文は触れない",
                    S.T("ja", "up_same_other_file", "0.5.5").IndexOf("ローカルビルド", StringComparison.Ordinal) < 0);
                r.Check("開発モードの文は触れる",
                    S.T("ja", "up_same_other_file_dev", "0.5.5").IndexOf("ローカルビルド", StringComparison.Ordinal) >= 0);
            });

            r.Test("GitHub の答えから出どころの鍵が同じ形で作られる", () =>
            {
                var rel = ReleaseInfo.Latest(new FixedWeb(Latest));
                r.Check("読めた", rel.Ok, rel.Error);
                r.Equal("出どころの鍵", Key, rel.AssetKey);
                r.Equal("版", "0.5.5", rel.Version.ToString(3));
            });

            GC.KeepAlive(make);
        }

        /// <summary>決まった答えだけを返す web（网には出ません）。</summary>
        sealed class FixedWeb : IWebFetch
        {
            readonly string body;
            public FixedWeb(string body) { this.body = body; }
            public string GetText(string url) => body;
            public void Download(string url, string dest, Action<long, long> progress) => throw new IOException("no network in the self-test");
        }

        // ------------------------------------------------------------------ unpacking
        static void ZipTests(SelfTestRunner r)
        {
            r.Section("unpacking");
            r.Test("a zip is written file by file, through a half-file", () =>
            {
                string root = r.NewDir("zip");
                string zip = Path.Combine(root, "a.zip");
                string dest = Path.Combine(root, "dest");
                using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
                {
                    Entry(z, "BepInEx/core/BepInEx.Core.dll", "new");
                    Entry(z, "BepInEx/config/keep.cfg", "from the zip");
                    Entry(z, "readme.txt", "new");
                    Entry(z, "../outside.txt", "must not be written");
                }
                SelfTestRunner.Touch(Path.Combine(dest, "readme.txt"), "old");
                SelfTestRunner.Touch(Path.Combine(dest, @"BepInEx\config\keep.cfg"), "the viewer's own settings");
                int n = ZipFiles.ExpandOver(zip, dest, new[] { @"BepInEx\config\" });
                r.Equal("the entries that were written", 2, n);
                r.Equal("a file that was there is replaced", "new", File.ReadAllText(Path.Combine(dest, "readme.txt")));
                r.Equal("the viewer's settings are kept", "the viewer's own settings", File.ReadAllText(Path.Combine(dest, @"BepInEx\config\keep.cfg")));
                r.Check("an entry that would land outside is refused", !File.Exists(Path.Combine(root, "outside.txt")));
                r.Equal("no half-file is left behind", 0, Directory.GetFiles(dest, "*.part", SearchOption.AllDirectories).Length);
            });

            r.Test("an unpacking that was cut off is noticed next time", () =>
            {
                string root = r.NewDir("marker");
                string game = Path.Combine(root, "game");
                string zip = Path.Combine(root, "b.zip");
                using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) Entry(z, "x.txt", "x");
                var inst = new Installer { Paths = ModPaths.For(game), Src = root, CacheDir = Path.Combine(root, "cache"), Web = new NoWeb(), State = LauncherStateFile.InMemory() };
                r.Check("a folder that was never touched is fine", !inst.ExtractionWasInterrupted());
                inst.Expand(zip, new string[0]);
                r.Check("after a whole unpacking, nothing is flagged", !inst.ExtractionWasInterrupted());
                SelfTestRunner.Touch(inst.ExtractMarkerPath, "left behind by a run that was killed");
                r.Check("a marker that stayed means the files cannot be trusted", inst.ExtractionWasInterrupted());
            });
        }

        static void Entry(ZipArchive z, string name, string text)
        {
            using (var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false))) w.Write(text);
        }

        // ------------------------------------------------------------------ "repair" has to beat the version checks
        static void ForceTests(SelfTestRunner r)
        {
            r.Section("repair");
            r.Test("BepInEx", () =>
            {
                r.Check("the version we install is left alone", Installer.SkipBepInEx(false, "6.0.0-be.735+5fef357"));
                r.Check("another version is put right", !Installer.SkipBepInEx(false, "6.0.0-be.700"));
                r.Check("a file whose version cannot be read is put right", !Installer.SkipBepInEx(false, null) && !Installer.SkipBepInEx(false, ""));
                r.Check("「修復」 puts it back even when the version matches", !Installer.SkipBepInEx(true, "6.0.0-be.735+5fef357"));
            });
            r.Test("the mod", () =>
            {
                var v = new Version("0.5.5");
                r.Check("the same build from the same file is left alone", Installer.SkipMod(false, v, v, "gh:a", "gh:a"));
                r.Check("the same number from another file is installed", !Installer.SkipMod(false, v, v, "gh:b", "gh:a"));
                r.Check("an older build is installed", !Installer.SkipMod(false, new Version("0.5.4"), v, "gh:a", "gh:a"));
                r.Check("「修復」 puts it back even when it is current", !Installer.SkipMod(true, v, v, "gh:a", "gh:a"));
            });

            // a failed copy stops the install: BepInEx and the mod are never unpacked into a half-copied game, and
            // launcher-state.json - which the PowerShell launcher reads too - is not told a broken copy is installed
            r.Test("a failed game copy stops the install", () =>
            {
                string root = r.NewDir("install-abort");
                string steam = Path.Combine(root, "Steam");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"));
                SelfTestRunner.Touch(Path.Combine(steam, "globalgamemanagers"));
                // the copy's folder is a FILE: Directory.CreateDirectory throws, so step 2 fails
                string modded = SelfTestRunner.Touch(Path.Combine(root, "game"), "not a folder");
                string statePath = Path.Combine(root, AppInfo.StateFileName);
                File.WriteAllText(statePath, "{\"installedVersion\":\"0.5.4\"}", new UTF8Encoding(false));
                var state = LauncherStateFile.Load(statePath);
                var lines = new List<string>();
                var inst = new Installer
                {
                    Paths = ModPaths.For(modded), Src = root, CacheDir = Path.Combine(root, "cache"),
                    Web = new NoWeb(), State = state, GameRunning = () => false,
                    SteamDir = () => steam, FindSteam = () => steam, Log = lines.Add,
                };
                var outcome = inst.Install("");
                r.Check("it fails", !outcome.Ok, outcome.Error);
                var after = LauncherStateFile.Load(statePath);
                r.Equal("the old installedVersion is left as it was", "0.5.4", after.InstalledVersion);
                r.Check("only \"we looked\" is written", after.Str("lastCheck") != null);
                r.Check("no BepInEx or mod step ran (they would have needed the network)",
                    !lines.Exists(l => l.IndexOf("BepInEx", StringComparison.OrdinalIgnoreCase) >= 0 && l.IndexOf("no network", StringComparison.OrdinalIgnoreCase) >= 0),
                    string.Join(" | ", lines.ToArray()));
            });
        }

        // ------------------------------------------------------------------ the game copy
        static void CopyTests(SelfTestRunner r)
        {
            r.Section("game copy");
            r.Test("a full disk is found out before anything is written", () =>
            {
                string root = r.NewDir("space");
                string src = Path.Combine(root, "src"), dst = Path.Combine(root, "dst");
                SelfTestRunner.Touch(Path.Combine(src, "Among Us.exe"), new string('x', 4096));
                var copy = new FileCopy { FreeSpaceOf = _ => 1024 };
                var res = copy.Run(src, dst);
                r.Check("it says why", !res.Ok && res.NotEnoughSpace && res.NeedBytes > res.FreeBytes);
                r.Check("and nothing was copied", !File.Exists(Path.Combine(dst, "Among Us.exe")));
                var plenty = new FileCopy { FreeSpaceOf = _ => 1024L * 1024 * 1024 * 1024 };
                r.Check("with room it copies", plenty.Run(src, dst).Ok && File.Exists(Path.Combine(dst, "Among Us.exe")));
                r.Check("a drive whose free space cannot be read is not refused", new FileCopy { FreeSpaceOf = _ => -1 }.Run(src, dst).Ok);
            });

            // 2026-09-23: an install into a folder whose path came to exactly 260 characters died on a file deep in
            // StreamingAssets, and the app told the viewer to close the game and Steam - neither of which was the cause.
            r.Test("a folder that sits too deep is found out before anything is written", () =>
            {
                string root = r.NewDir("deep");
                string src = Path.Combine(root, "src");
                SelfTestRunner.Touch(Path.Combine(src, "Among Us.exe"), "x");
                string dst = Path.Combine(root, new string('d', 250));
                var res = new FileCopy { FreeSpaceOf = _ => 1024L * 1024 * 1024 * 1024 }.Run(src, dst);
                r.Check("it says why", !res.Ok && res.PathTooLong);
                r.Check("the length it reports is the path it measured",
                    res.LongestPath != null && res.LongestPath.Length == res.LongestLength && res.LongestLength > FileCopy.MaxPath);
                r.Check("and the folder was not even made", !Directory.Exists(dst));

                // a file is written as "<name>.part" first, so those five characters belong in the measurement
                var plan = new CopyPlan();
                plan.Files.Add("Among Us.exe");
                r.Equal("the name measured is the .part one", @"C:\x\Among Us.exe.part", FileCopy.LongestDestination(@"C:\x", plan));
                plan.Folders.Add("Among Us_Data");
                r.Equal("a folder is measured as it is", @"C:\x\Among Us.exe.part", FileCopy.LongestDestination(@"C:\x", plan));
            });

            r.Test("half-files of a copy that was killed are swept up", () =>
            {
                string root = r.NewDir("sweep");
                string src = Path.Combine(root, "src"), dst = Path.Combine(root, "dst");
                SelfTestRunner.Touch(Path.Combine(src, "a.txt"), "a");
                SelfTestRunner.Touch(Path.Combine(dst, "a.txt.part"), "half");
                SelfTestRunner.Touch(Path.Combine(dst, @"sub\b.dll.part"), "half");
                SelfTestRunner.Touch(Path.Combine(dst, "keep.txt"), "mine");
                var res = new FileCopy { FreeSpaceOf = _ => long.MaxValue / 4 }.Run(src, dst);
                r.Check("the copy is fine", res.Ok, res.Error);
                r.Equal("no .part is left", 0, Directory.GetFiles(dst, "*.part", SearchOption.AllDirectories).Length);
                r.Check("a real file of the destination is untouched", File.Exists(Path.Combine(dst, "keep.txt")));
            });

            r.Test("the copy never walks into itself", () =>
            {
                string root = r.NewDir("inside");
                string a = Path.Combine(root, "a");
                Directory.CreateDirectory(Path.Combine(a, "b"));
                r.Check("a folder is inside itself", FileCopy.SameOrInside(a, a));
                r.Check("and its child is", FileCopy.SameOrInside(a, Path.Combine(a, "b")));
                r.Check("a name that only starts the same is not", !FileCopy.SameOrInside(a, a + "-other"));
                r.Check("written with a detour it is still the same folder", FileCopy.SameOrInside(a, Path.Combine(a, "b", "..")));
            });
        }

        // ------------------------------------------------------------------ launcher-state.json
        static void StateFileTests(SelfTestRunner r)
        {
            r.Section("launcher-state.json");
            r.Test("a file that cannot be read is never written over", () =>
            {
                string root = r.NewDir("state");
                string path = Path.Combine(root, AppInfo.StateFileName);
                File.WriteAllText(path, "{ this is not JSON", new UTF8Encoding(false));
                var s = LauncherStateFile.Load(path);
                r.Check("it is not \"there is no file\"", !s.Found && s.Unreadable);
                r.Check("writing is refused", !s.Set("steamDir", @"C:\Steam"));
                r.Equal("so what was on disk is still on disk", "{ this is not JSON", File.ReadAllText(path));
                r.Check("no half-written .tmp is left", !File.Exists(path + ".tmp"));

                // and the old launcher's file is NOT put in its place
                string old = Path.Combine(root, "old", AppInfo.StateFileName);
                SelfTestRunner.Touch(old, "{\"steamDir\":\"C:\\\\Old\"}");
                r.Check("no migration over a file that is only unreadable", !s.MigrateFrom(old, DateTime.Now));
                r.Equal("... and the file is still the viewer's", "{ this is not JSON", File.ReadAllText(path));
            });

            r.Test("a file that is really missing is filled from the old launcher once", () =>
            {
                string root = r.NewDir("state-migrate");
                string path = Path.Combine(root, "mine", AppInfo.StateFileName);
                string old = Path.Combine(root, "old", AppInfo.StateFileName);
                SelfTestRunner.Touch(old, "{\"steamDir\":\"C:\\\\Old\",\"copiedGameVersion\":\"2026.8.18\"}");
                var s = LauncherStateFile.Load(path);
                r.Check("nothing of ours yet", !s.Found && !s.Unreadable);
                r.Check("it is taken over", s.MigrateFrom(old, new DateTime(2026, 9, 23, 4, 0, 0)));
                r.Equal("with the old values", @"C:\Old", s.SteamDir);
                r.Check("and it says where from", s.MigratedFrom == old && s.Str("migratedAt") != null);
                r.Check("the old file is not changed", File.ReadAllText(old).IndexOf("migratedFrom", StringComparison.Ordinal) < 0);
                r.Check("a second time does nothing", !LauncherStateFile.Load(path).MigrateFrom(old, DateTime.Now));
            });

            r.Test("read, merge, write", () =>
            {
                string root = r.NewDir("state-merge");
                string path = Path.Combine(root, AppInfo.StateFileName);
                var a = LauncherStateFile.Load(path);
                r.Check("the first write makes the file", a.Set("lang", "ja"));
                // the PowerShell launcher wrote something meanwhile: it must not be lost
                File.WriteAllText(path, "{\"lang\":\"ja\",\"steamDir\":\"C:\\\\Steam\"}", new UTF8Encoding(false));
                r.Check("our next write merges", a.Set("installedVersion", "0.5.5"));
                var after = LauncherStateFile.Load(path);
                r.Check("both sides survive", after.SteamDir == @"C:\Steam" && after.InstalledVersion == "0.5.5" && after.Lang == "ja");
                r.Check("no .tmp left behind", !File.Exists(path + ".tmp"));
            });

            r.Test("a file that is far too big is not read into memory", () =>
            {
                string root = r.NewDir("state-big");
                string path = Path.Combine(root, AppInfo.StateFileName);
                using (var f = new FileStream(path, FileMode.Create)) f.SetLength(Json.MaxFileBytes + 1);
                var s = LauncherStateFile.Load(path);
                r.Check("it counts as unreadable, not as missing", !s.Found && s.Unreadable);
            });
        }

        // ------------------------------------------------------------------ which past logs go into a report zip
        static void PastLogTests(SelfTestRunner r)
        {
            r.Section("report: past logs");
            r.Test("the current log is not carried twice", () =>
            {
                string root = r.NewDir("pastlogs");
                string game = Path.Combine(root, "game");
                var paths = ModPaths.For(game);
                string live = SelfTestRunner.Touch(paths.LogPath, "the game's log right now");
                var t = new DateTime(2026, 9, 23, 3, 30, 0);
                File.SetLastWriteTime(live, t);
                Directory.CreateDirectory(paths.LogArchiveDir);
                // Save-GameLog's copy of the log that is open right now: the zip already has LogOutput.log itself
                string twin = SelfTestRunner.Touch(Path.Combine(paths.LogArchiveDir, GameLogs.ArchiveName(t)), "the same session");
                var older = new List<string>();
                foreach (var when in new[] { new DateTime(2026, 9, 22, 20, 0, 0), new DateTime(2026, 9, 21, 20, 0, 0), new DateTime(2026, 9, 20, 20, 0, 0), new DateTime(2026, 9, 19, 20, 0, 0) })
                    older.Add(SelfTestRunner.Touch(Path.Combine(paths.LogArchiveDir, GameLogs.ArchiveName(when)), "older"));
                SelfTestRunner.Touch(Path.Combine(paths.LogArchiveDir, "LogOutput-not-a-time.log"), "someone's own file");

                var rb = new ReportBuilder { Paths = paths, Desktop = root, CacheDir = Path.Combine(root, "cache") };
                var got = rb.RecentArchivedLogs(3);
                r.Equal("three past sessions, not two", 3, got.Count);
                r.Check("the twin of the current log is left out", !got.Contains(twin), string.Join(" | ", got.ToArray()));
                r.Check("a name whose time cannot be read is left out", !got.Exists(p => p.EndsWith("not-a-time.log", StringComparison.Ordinal)));
                r.Equal("newest first", older[0], got[0]);
                r.Equal("then the next", older[1], got[1]);
                r.Equal("and the next", older[2], got[2]);
            });
        }

        // ------------------------------------------------------------------ the folder the viewer picks for Steam
        static void PickedSteamTests(SelfTestRunner r)
        {
            r.Section("pick Steam's folder");
            r.Test("the mod's own copy is not Steam's Among Us", () =>
            {
                string root = r.NewDir("picksteam");
                string steam = Path.Combine(root, "Steam", "Among Us");
                string modded = Path.Combine(root, "Among Us PocketRoles");
                SelfTestRunner.Touch(Path.Combine(steam, "Among Us.exe"));
                SelfTestRunner.Touch(Path.Combine(modded, "Among Us.exe"));
                string kept = null;
                var state = LauncherStateFile.InMemory();
                var inst = new Installer
                {
                    Paths = ModPaths.For(modded), Src = root, Web = new NoWeb(), State = state,
                    SetSteamDir = d => kept = d,
                };
                r.Equal("Steam's own folder is fine", steam, inst.CheckPickedSteam(steam));
                r.Check("the mod's copy is refused", inst.CheckPickedSteam(modded) == null);
                r.Check("... and so is a folder inside it", inst.CheckPickedSteam(Path.Combine(modded, "BepInEx")) == null);
                r.Check("a folder with no game is refused", inst.CheckPickedSteam(root) == null);
                r.Check("nothing at all is refused", inst.CheckPickedSteam(null) == null && inst.CheckPickedSteam("") == null);
                inst.RememberSteam(steam);
                r.Equal("remembering it tells the app", steam, kept);
            });
        }
    }
}
