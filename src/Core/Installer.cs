// Install, repair, Steam sync and the mod's update check (v0.2) - the PowerShell launcher's Invoke-Install,
// Sync-GameCopy and Invoke-CheckUpdate (ps1:884-1070), ported step for step and word for word, but in-process:
//   - the game copy is made by src\Core\FileCopy.cs (no robocopy.exe)
//   - the zips are unpacked by src\Core\ZipFiles.cs (no Expand-Archive, no tar.exe)
//   - the release is read from the GitHub API and the files are downloaded by src\Core\Downloads.cs (no curl, no BITS)
//   - launcher-state.json is read and written like Update-State (src\Core\LauncherStateFile.cs)
//   - no shortcut is written (SignPath: nothing is added to the Desktop without the viewer asking; that button is v0.3)
// Every step can be run again: what is already there is skipped, nothing in the copy is deleted.
// Runs on a worker thread; progress goes to the UI as "progress" events (the play button's card).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Starpocket.Client.Core
{
    /// <summary>One "progress" event for the UI (PORT-MAP 13.4): the task, which of its steps, and for a download the
    /// bytes, the speed and the time left (the play button's progress card).</summary>
    internal sealed class TaskProgress
    {
        public string Task;
        public int Step, Of;
        public double Value;
        public long Bytes, Total;
        public double Bps;
        public double EtaSec = double.NaN;
        /// <summary>"steam" while the app waits for Steam to finish installing Among Us.</summary>
        public string Waiting;

        public Dictionary<string, object> ToData()
        {
            var d = new Dictionary<string, object> { ["task"] = Task, ["step"] = Step, ["of"] = Of, ["value"] = Value };
            if (Waiting != null) d["waiting"] = Waiting;
            if (Total > 0) { d["bytes"] = Bytes; d["total"] = Total; }
            if (Bps > 0) d["bps"] = Bps;
            if (!double.IsNaN(EtaSec) && EtaSec > 0) d["etaSec"] = EtaSec;
            return d;
        }
    }

    internal sealed class TaskOutcome
    {
        public bool Ok, NeedGame, Cancelled;
        public string Error, Text;
        public Dictionary<string, object> Data;

        public static TaskOutcome Good(string text, Dictionary<string, object> data = null) => new TaskOutcome { Ok = true, Text = text, Data = data };
        public static TaskOutcome Bad(string error) => new TaskOutcome { Error = error };

        public Dictionary<string, object> ToResult()
        {
            var r = new Dictionary<string, object> { ["ok"] = Ok };
            var data = Data ?? new Dictionary<string, object>();
            if (Text != null) data["text"] = Text;
            r["data"] = data;
            if (!Ok)
            {
                if (NeedGame) r["needGame"] = true;
                if (Cancelled) r["cancelled"] = true;
                r["error"] = Error ?? "";
            }
            return r;
        }
    }

    internal sealed class Installer
    {
        public ModPaths Paths;
        /// <summary>The launcher's $script:Here: where an offline PocketRoles-&lt;ver&gt;.zip or an unpacked mod may lie.</summary>
        public string Src;
        public string CacheDir;
        public string AegisStateDir;
        /// <summary>v1.1: the app's own data folder, where mod-origin.txt says which DLL this app put in the copy
        /// (<see cref="ModOriginFile"/>). null (the self-test's fakes): nothing is written down.</summary>
        public string OriginDir;
        public bool DevMode;
        public LauncherStateFile State;
        public IWebFetch Web = WebFetch.Instance;
        public Func<string> Lang = () => "ja";
        public Action<string> Log = _ => { };
        public Action<TaskProgress> Progress = _ => { };
        public Func<bool> GameRunning = Processes.GameRunning;
        public Func<DateTime> Now = () => DateTime.Now;
        public Func<bool> Cancelled = () => false;
        /// <summary>Steam's Among Us as the app knows it now (null when it was never found).</summary>
        public Func<string> SteamDir = () => null;
        /// <summary>Look for Steam's Among Us again (SteamLocator.Find).</summary>
        public Func<string> FindSteam = () => null;
        /// <summary>Keep the folder that was found / picked (ClientContext.SteamDir).</summary>
        public Action<string> SetSteamDir = _ => { };
        /// <summary>Opens steam://install/945360 when the viewer pressed "install it in Steam".</summary>
        public Action OpenSteamInstall = () => ShellOpen.SteamUrl(AppInfo.SteamInstallUrl);
        public int HandoffPollMs = 3000;
        public int HandoffTimeoutMs = 30 * 60 * 1000;
        /// <summary>Set while the viewer picked a folder during the wait for Steam (FLOW pickSteam ends the wait).</summary>
        public Func<bool> HandoffEnded = () => false;
        /// <summary>The SHA-256 the BepInEx zip of a version must have (AppInfo's pinned table; the self-test hands it
        /// fingerprints of its own small zips). null for a version means "we do not know that file" - and then nothing
        /// is fetched and nothing is unpacked.</summary>
        public Func<string, string> BepSha256 = AppInfo.BepSha256;

        const string ModDllEntry = "BepInEx/plugins/PocketRoles.dll";
        /// <summary>The one entry a BepInEx zip has to hold. internal: "--verify-download" looks for the same entry,
        /// from the same line (<see cref="BepVerifier"/>).</summary>
        internal const string BepCoreEntry = "BepInEx/core/BepInEx.Core.dll";
        /// <summary>Links on the index page that name the version this build installs. Built FROM
        /// <see cref="AppInfo.BepInExVersion"/>, never written out again: the index page is the spare way in when both
        /// pinned addresses 404 (the file name on builds.bepinex.dev carries a per-build suffix such as +5fef357, so
        /// the written-down addresses go stale on their own), and a version number raised in AppInfo while this line
        /// still said the old one would quietly find nothing (v0.4 review).
        ///
        /// <para>2026-10-01: ここには "win-x86" が直に書いてありました。本体が 64bit になって
        /// <see cref="AppInfo.BepZipName"/> を x64 に替えたとき、この行だけが x86 のまま残ると、
        /// 固定アドレスが 404 になった日に「予備の道」が 32bit の方を拾ってきます。それは動かないファイルです。
        /// 版と同じ理由で、**種類もここには書きません**。ファイル名そのもの（拡張子を除いた部分）から組み立てます。</para></summary>
        internal static readonly Regex BepHref = new Regex(
            "href=\"([^\"]*" + Regex.Escape(Path.GetFileNameWithoutExtension(AppInfo.BepZipName)) + "[^\"'\\s]*\\.zip)\"",
            RegexOptions.IgnoreCase);

        string T(string key, params object[] args) => S.T(Lang(), key, args);

        // ------------------------------------------------------------------ install (5 steps, resumable)
        /// <summary>mode: "" first install, "resume" after a failure, "repair" (the R state: the copy is there but not
        /// whole), "repairMod" (Aegis found the mod's file changed: the mod is put back whatever its version says),
        /// "steam" (wait for Steam to install Among Us first).</summary>
        public TaskOutcome Install(string mode)
        {
            try
            {
                if (GameRunning()) { Log(T("game_running")); return TaskOutcome.Bad(T("game_running")); }
                Log(T("in_start"));
                int failed = 0;
                bool forceMod = mode == "repairMod";
                // "repair" means "put this copy right", and an unpacking that was cut off leaves files that are new and
                // files that are old with no way to tell: both force BepInEx and the mod back in whatever the version
                // numbers say. Without this the version checks answer "already there" for ever and only deleting the
                // whole copy by hand helps (v0.3 review).
                bool interrupted = ExtractionWasInterrupted();
                if (interrupted) Log(T("in_interrupted"));
                bool forceFiles = mode == "repair" || interrupted;

                // [1/5] find Steam's Among Us (the launcher does this inside its step 1)
                Step(1, 0);
                string steam = FindSteamCopy(mode == "steam");
                if (steam == null)
                {
                    // the app is closing while the install waited for Steam: no words and no "not found" (nothing was
                    // copied wrong; the next start carries on)
                    if (Cancelled()) return new TaskOutcome { Cancelled = true, Error = "" };
                    // Store / Xbox 版を持っている人には別の文を出します（StoreGame.cs。選べないフォルダを選べと言わないため）
                    string why = T(StoreGame.NoGameKey());
                    Log(why);
                    return new TaskOutcome { NeedGame = true, Error = why };
                }
                Log(T("in_steam_found", steam));
                Step(1, 1);

                // [2/5] the game copy. Everything else is unpacked INTO this folder, so a failure here stops the
                // install: BepInEx and the mod must never go into a half-copied game, and nothing is recorded as
                // installed (v0.3 review: the steps carried on and the state was written anyway).
                Log(T("in_step1"));
                if (!RunStep(2, () => StepCopyGame(steam, forceFiles)))
                {
                    if (Cancelled()) return new TaskOutcome { Cancelled = true, Error = "" };
                    State.Set("lastCheck", LauncherStateFile.Stamp(Now()));
                    Log(T("in_partial", 1));
                    return TaskOutcome.Bad(T("in_partial", 1));
                }
                // [3/5] BepInEx
                Log(T("in_step2", AppInfo.BepInExVersion));
                if (!RunStep(3, () => StepBepInEx(forceFiles))) failed++;
                // [4/5] the mod
                Log(T("in_step3"));
                if (!RunStep(4, () => StepMod(forceMod || forceFiles))) failed++;
                // [5/5] the files the game needs to start outside Steam, then the state
                Log(T("in_step4"));
                if (!RunStep(5, StepFinish)) failed++;
                Log(T("in_step5"));
                Step(5, 1);
                if (failed > 0)
                {
                    // a step failed: only "we looked" is recorded. Writing installedVersion / gameVersion now would
                    // tell this app AND the PowerShell launcher that a broken copy is up to date.
                    State.Set("lastCheck", LauncherStateFile.Stamp(Now()));
                    Log(T("in_partial", failed));
                    return WithConsentNotice(TaskOutcome.Bad(T("in_partial", failed)));
                }
                // 5 つの手順が全部そろって初めて、前回の中断の記録を消します。
                // 展開のたびに消していた（finally）のをやめた代わりがここです。途中の手順が失敗した時に
                // 消してしまうと、次回 forceFiles が立たず、半分だけ入ったファイルが
                // 「版が合っているので飛ばします」と扱われてしまいます（それが元の不具合でした）。
                if (interrupted) DeleteQuiet(ExtractMarkerPath);
                var info = InstallInfo.Read(Paths);
                State.Update(new Dictionary<string, object>
                {
                    ["installedVersion"] = info.DllVer,
                    ["gameVersion"] = info.GameVer,
                    ["bepinex"] = info.BepVer,
                    ["lastCheck"] = LauncherStateFile.Stamp(Now()),
                    ["launcher"] = AppInfo.Name + " " + AppInfo.Version,
                    ["lang"] = Lang(),
                });
                Log(T("in_done"));
                Log(T("in_firstrun"));
                return WithConsentNotice(TaskOutcome.Good(T("in_done")));
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return TaskOutcome.Bad(e);
            }
        }

        bool RunStep(int step, Func<bool> body)
        {
            Step(step, 0);
            bool ok = false;
            try { ok = body(); }
            catch (Exception ex) { Log(T("err", ex.Message)); }
            if (!ok) Log(T("hint_continue"));
            Step(step, 1);
            return ok;
        }

        void Step(int step, double value) => Progress(new TaskProgress { Task = "install", Step = step, Of = 5, Value = value });

        /// <summary>Steam's copy: the one the app knows, else looked for again; with <paramref name="handoff"/> the viewer
        /// pressed "install it in Steam", so Steam's page is opened and the app waits until the game is there (or the
        /// viewer picks a folder, or the wait is cancelled).</summary>
        string FindSteamCopy(bool handoff)
        {
            string steam = SteamDir();
            if (!string.IsNullOrEmpty(steam) && GameFolders.PathExists(GameFolders.Join(steam, "Among Us.exe"))) return steam;
            steam = FindSteam();
            if (steam == null && handoff) steam = WaitForSteam();
            if (steam == null) return null;
            SetSteamDir(steam);
            State.Set("steamDir", steam);
            return steam;
        }

        string WaitForSteam()
        {
            try { OpenSteamInstall(); }
            catch (Exception ex) { Log("Steam: " + ex.Message); }
            Progress(new TaskProgress { Task = "install", Step = 1, Of = 5, Waiting = "steam" });
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < HandoffTimeoutMs)
            {
                if (Cancelled()) return null;
                Thread.Sleep(Math.Min(HandoffPollMs, 500));
                if (clock.ElapsedMilliseconds % HandoffPollMs < 500)
                {
                    string found = SteamDir();
                    if (string.IsNullOrEmpty(found) || !GameFolders.PathExists(GameFolders.Join(found, "Among Us.exe"))) found = FindSteam();
                    if (found != null) return found;
                }
                if (HandoffEnded())
                {
                    string picked = SteamDir();
                    if (!string.IsNullOrEmpty(picked)) return picked;
                }
            }
            return null;
        }

        // ---- [2/5] Step-CopyGame
        internal bool StepCopyGame(string steam, bool force)
        {
            string sv = GameVersion.Read(steam), mv = GameVersion.Read(Paths.Modded);
            if (!force && GameFolders.PathExists(Paths.GameExe) && sv != null && mv != null &&
                string.Equals(sv, mv, StringComparison.OrdinalIgnoreCase) && string.Equals(State.CopiedGameVersion, mv, StringComparison.OrdinalIgnoreCase))
            {
                Log(T("in_copy_skip", mv));
                return true;
            }
            return CopyGameFiles(steam, Paths.Modded, 2);
        }

        /// <summary>Copy-GameFiles: the same files, the same exclusions and the same "is it complete?" check as robocopy,
        /// in-process (FileCopy).</summary>
        internal bool CopyGameFiles(string src, string dst, int step)
        {
            Log(T("in_copying", src, dst));
            var clock = Stopwatch.StartNew();
            var copy = new FileCopy
            {
                Log = Log,
                Cancelled = Cancelled,
                Progress = (done, total) => Bytes("install", step, done, total, clock),
            };
            var res = copy.Run(src, dst);
            if (res.NotEnoughSpace)
            {
                // said before the copy starts, so a full disk is not found out after minutes and a half-copied folder
                string msg = T("in_nospace", Mb(res.NeedBytes), Mb(res.FreeBytes));
                Log(msg);
                Log(T("in_copy_fail", msg));
                return false;
            }
            if (res.PathTooLong)
            {
                // the same idea, with the real cause: the chosen folder is too deep. Closing Steam cannot help, and the
                // app used to say exactly that (2026-09-23).
                string msg = T("in_toolong", res.LongestLength, FileCopy.MaxPath);
                Log(msg);
                Log(T("in_copy_fail", msg));
                return false;
            }
            if (!res.Ok)
            {
                Log(T("in_copy_fail", res.Error ?? "?"));
                // only here: a file that will not copy is nearly always one the running game or Steam is holding open
                Log(T("in_copy_close"));
                return false;
            }
            if (!GameFolders.PathExists(GameFolders.Join(dst, "Among Us.exe")))
            {
                Log(T("in_copy_fail", "no exe"));
                return false;
            }
            Log(T("in_copy_done", res.Copied, res.Same));
            State.Set("copiedGameVersion", GameVersion.Read(dst));
            return true;
        }

        void Bytes(string task, int step, long done, long total, Stopwatch clock)
        {
            double sec = clock.Elapsed.TotalSeconds;
            double bps = sec > 0.5 ? done / sec : 0;
            Progress(new TaskProgress
            {
                Task = task,
                Step = step,
                Of = 5,
                Value = total > 0 ? Math.Min(1.0, (double)done / total) : 0,
                Bytes = done,
                Total = total,
                Bps = bps,
                EtaSec = bps > 0 && total > done ? (total - done) / bps : double.NaN,
            });
        }

        // ---- [3/5] Step-BepInEx
        /// <summary><paramref name="force"/>: put the files back even when the version says they are already there
        /// ("repair", or an unpacking that was cut off).</summary>
        internal bool StepBepInEx(bool force)
        {
            string core = GameFolders.Join(Paths.Modded, @"BepInEx\core\BepInEx.Core.dll");
            if (GameFolders.PathExists(core))
            {
                string pv = GameVersion.ProductVersion(core);
                // 2026-10-01: 版が合っていても、ゲームと種類 (32bit / 64bit) の違う winhttp.dll は Windows が読み込みません。
                // 2026.9.29 で本体が 64bit になったので、32bit の BepInEx を入れたままの人がここに来ます。
                // その人のフォルダーは「BepInEx\core も winhttp.dll も在る」状態なので、版だけを見ていると飛ばしてしまい、
                // ファイルは全部そろっているのに MOD だけ黙って動かない —— 一番わかりにくい壊れ方になります。
                // だから「入っているから飛ばす」の前に、ゲームと同じ種類かどうかを見ます。
                string doorstop = GameFolders.Join(Paths.Modded, "winhttp.dll");
                ushort exeArch = PeArch.Machine(Paths.GameExe), dsArch = PeArch.Machine(doorstop);
                bool archOk = GameFolders.PathExists(doorstop)
                    && (exeArch == PeArch.Unknown || dsArch == PeArch.Unknown || exeArch == dsArch);
                if (!archOk)
                {
                    // 飛ばさずに入れ直します（下の処理がそのまま上書きします）。force と同じ扱い。
                    Log(T("in_bep_arch", PeArch.Name(exeArch), PeArch.Name(dsArch)));
                }
                else if (SkipBepInEx(force, pv))
                {
                    Log(T("in_bep_skip", AppInfo.BepInExVersion));
                    return true;
                }
                else if (!force) Log(T("in_bep_other", pv, AppInfo.BepInExVersion));
            }
            // Before anything is fetched or unpacked: do we know what the right file looks like? A version with no
            // pinned SHA-256 is refused outright - there is no "install it anyway" path, because the one thing worse
            // than not installing BepInEx is installing a file nobody checked (the owner, 2026-09-23).
            string want = BepSha256(AppInfo.BepInExVersion);
            if (string.IsNullOrEmpty(want)) { Log(T("in_bep_nohash", AppInfo.BepInExVersion)); return false; }
            string zip = GameFolders.Join(CacheDir, AppInfo.BepZipName);
            // a file already in the cache is put through the very same check: %TEMP% is shared with the PowerShell
            // launcher, so what is lying there was not necessarily put there by this app. One that does not match is
            // deleted by CheckAndExpandBep, and we simply fetch it again.
            int n = File.Exists(zip) ? CheckAndExpandBep(zip, want, zip, true) : -1;
            if (n < 0) n = DownloadBepInEx(zip, want);
            if (n < 0) { Log(T("in_bep_fail")); return false; }
            Log(T("in_extract_done", n));
            if (!GameFolders.PathExists(core)) { Log(T("in_bep_fail")); return false; }
            State.Set("bepinex", GameVersion.ProductVersion(core));
            return true;
        }

        /// <summary>
        /// The file on disk is the file we pinned, and THAT file is the one that gets unpacked. The zip is opened once
        /// (<see cref="VerifiedZip"/>, FileShare.Read: nobody else may write to it while we hold it) and the same
        /// handle answers all three questions in order - the SHA-256 first, before the file is so much as parsed as an
        /// archive, so a half-written, truncated or swapped download is never opened as a zip and never unpacked; then
        /// the entry we need; then the unpacking itself. The v0.4 review found the three used to be three separate
        /// opens with the file unheld in between, in a %TEMP% folder shared with everything else running as this
        /// Windows user - long enough to swap the file after the log said "checked".
        ///
        /// <para>Returns how many files were unpacked, or -1 when the file was refused. A refused file is deleted on
        /// the spot (after the handle is closed), so nothing can pick it up later and nothing is left lying in the
        /// shared cache folder. <paramref name="where"/> only names the file in the message.</para>
        /// </summary>
        internal int CheckAndExpandBep(string zip, string want, string where, bool cached)
        {
            string why = null, got = null;
            int n = -1;
            try
            {
                using (var vz = VerifiedZip.Open(zip))
                {
                    got = vz.Sha256;
                    if (!FileHash.Same(got, want)) why = "hash";
                    else if (!vz.Has(BepCoreEntry)) why = "entry";
                    else
                    {
                        Log(T("in_hash_ok", AppInfo.BepInExVersion));
                        if (cached) Log(T("in_cached", zip));
                        Log(T("in_extract", Path.GetFileName(zip)));
                        n = Expand(vz, new string[0]);
                    }
                }
            }
            catch (Exception ex) { Log(T("err", ex.Message)); why = "open"; }
            if (why == "hash")
            {
                Log(T("in_hash_bad", where));
                Log(T("in_hash_detail", want, got));
            }
            else if (why == "entry") Log(T("in_zip_bad", where, "BepInEx.Core.dll"));
            if (why != null) { DeleteQuiet(zip); return -1; }
            return n;
        }

        void DeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }

        /// <summary>The addresses the app would fetch BepInEx from, in order: the pinned ones, then whatever the index
        /// page offers for THIS version. Anything the app may not fetch at all is refused here, not later
        /// (<paramref name="logRefused"/> is given the address that was dropped).
        ///
        /// <para>It sits on its own so that the install and "--verify-download" (<see cref="BepVerifier"/>) read the
        /// SAME list in the SAME order. Two lists written out separately would drift apart, and then the one real check
        /// before a release would be checking addresses the install does not use.</para></summary>
        internal static List<string> BepAddresses(IWebFetch web, Action<string> logRefused)
        {
            var urls = new List<string>(AppInfo.BepUrls);
            try
            {
                string html = web.GetText(AppInfo.BepIndexUrl);
                var basePage = new Uri(AppInfo.BepIndexUrl);
                foreach (Match m in BepHref.Matches(html ?? ""))
                {
                    Uri abs;
                    if (!Uri.TryCreate(basePage, m.Groups[1].Value, out abs)) continue;
                    string u = abs.AbsoluteUri;
                    // a link on a page decides nothing: what comes down is unpacked into the game and loaded as code,
                    // so only the hosts the app fetches from at all are used (v0.3 review)
                    if (!WebFetch.AllowedUrl(u)) { if (logRefused != null) logRefused(u); continue; }
                    if (!urls.Contains(u)) urls.Add(u);
                }
            }
            catch (Exception) { }
            return urls;
        }

        /// <summary>Fetches the zip from the pinned addresses (plus whatever the index page offers for this version)
        /// and hands each one straight to <see cref="CheckAndExpandBep"/>. Returns how many files were unpacked, or -1
        /// when no address produced the file we pinned.</summary>
        int DownloadBepInEx(string zip, string want)
        {
            var urls = BepAddresses(Web, u => Log(T("in_dl_refused", u)));
            foreach (var url in urls)
            {
                if (Cancelled()) return -1;
                try
                {
                    Log(T("in_dl", url));
                    var clock = Stopwatch.StartNew();
                    Web.Download(url, zip, (done, total) => Bytes("install", 3, done, total, clock));
                    // after the download, before any unpacking: the file itself, not just the address it came from -
                    // and the unpacking happens on the same open handle, so it is that file and no other
                    int n = CheckAndExpandBep(zip, want, url, false);
                    if (n >= 0) return n;
                }
                catch (Exception ex) { Log(T("err", ex.Message)); DeleteQuiet(zip); }
            }
            return -1;
        }

        // ---- [4/5] Step-Mod
        internal bool StepMod(bool force)
        {
            var installed = ReleaseInfo.Normalize(GameVersion.DllVersionString(Paths.DllPath));
            var rel = ReleaseInfo.Latest(Web);
            if (rel.Ok)
            {
                if (SkipMod(force, installed, rel.Version, rel.AssetKey, State.ModSource)) { Log(T("in_mod_skip", installed)); return true; }
                bool ok = InstallModRelease(rel, 4);
                if (ok) State.Set("modSource", rel.AssetKey);
                return ok;
            }
            Log(rel.ErrorText(Lang()));
            string localZip = FindLocalModZip();
            if (localZip != null)
            {
                var lv = ReleaseInfo.Normalize(Path.GetFileName(localZip));
                string key = ReleaseInfo.FileSourceKey("zip", localZip);
                if (SkipMod(force, installed, lv, key, State.ModSource)) { Log(T("in_mod_skip", installed)); return true; }
                Log(T("in_mod_local", localZip));
                bool ok = InstallModZip(localZip);
                if (ok) State.Set("modSource", key);
                return ok;
            }
            string localDll = FindLocalModDll();
            if (localDll != null)
            {
                var lv = ReleaseInfo.Normalize(GameVersion.DllVersionString(localDll));
                string key = ReleaseInfo.FileSourceKey("dll", localDll);
                if (SkipMod(force, installed, lv, key, State.ModSource)) { Log(T("in_mod_skip", installed)); return true; }
                bool ok = InstallModDir(localDll);
                if (ok) State.Set("modSource", key);
                return ok;
            }
            if (installed != null && !force) { Log(T("in_mod_skip", installed)); return true; }
            Log(rel.Status == 404 ? T("in_mod_norelease") : T("in_mod_fail"));
            return false;
        }

        internal bool InstallModRelease(ReleaseInfo rel, int step)
        {
            // 2026-10-01: ここは %TEMP% のキャッシュに rel.AssetName（= "PocketRoles-0.5.5.zip"）だけを
            // 名前にして置いていました。作者が**同じ版番号のまま中身を差し替えて出し直す**と名前は変わらないので、
            // キャッシュに残っている古い zip がそのまま使い回されます。しかもそのあと StepMod / CheckUpdate は
            // launcher-state.json の modSource に「新しい方」の AssetKey を書くので、
            // ReleaseInfo.ModCurrent は以後ずっと「最新です」と答えます。
            // つまり**本人は古い MOD を使い続け、しかも気づく手がかりが 1 つも残らない**という形でした。
            //
            // MOD の zip には固定の SHA-256 がありません（InstallModZip のコメント参照）。
            // なので「ハッシュで確かめる」はここでは使えません。代わりに、この launcher が既に
            // 「どの配布物か」を表す物として使っている AssetKey（名前 + バイト数 + 更新日時。ReleaseInfo.Latest）を
            // キャッシュの**ファイル名に入れます**。state に記録する鍵とキャッシュを引く鍵が同じ物になるので、
            // 「古いファイルを入れたのに新しい鍵を記録する」というずれが起きなくなります。
            // 中身が差し替わると AssetKey が変わり、AssetKey が変わると名前も変わるので、古い zip には当たりません。
            // ついでに %TEMP% が PowerShell 版ランチャーと共用である問題（同名の別物を拾う）も、
            // 同じ理屈で閉じます。
            string name = CacheZipName(rel.AssetName, rel.AssetKey);
            string zip = GameFolders.Join(CacheDir, name);
            // 版を出し直すたびに zip が 1 つ増えるので、同じ配布物名で鍵だけ違う古い物は先に片付けます
            // （落とす前に片付けるのは、ディスクの空きのため）。
            DropOtherCachedBuilds(name, rel.AssetName);
            // AssetKey が無い（Ok でない ReleaseInfo を渡された等）時は、どの配布物なのか見分けられません。
            // 見分けられない物を使い回すと上の不具合に戻るので、その時はキャッシュを信用せず必ず落とし直します。
            bool mayReuse = !string.IsNullOrEmpty(rel.AssetKey);
            if (mayReuse && File.Exists(zip) && ZipFiles.Has(zip, ModDllEntry)) Log(T("in_cached", zip));
            else
            {
                Log(T("in_dl", rel.AssetUrl));
                var clock = Stopwatch.StartNew();
                Web.Download(rel.AssetUrl, zip, (done, total) => Bytes(step == 4 ? "install" : "update", step, done, total, clock));
            }
            return InstallModZip(zip);
        }

        /// <summary>キャッシュに置く zip の名前: "PocketRoles-0.5.5-&lt;鍵 16 桁&gt;.zip"。
        /// 同じ版番号で中身を差し替えて出し直されても、鍵が変わるので別の名前になります。</summary>
        internal static string CacheZipName(string assetName, string assetKey)
        {
            string baseName = Path.GetFileNameWithoutExtension(assetName ?? "");
            string ext = Path.GetExtension(assetName ?? "");
            if (string.IsNullOrEmpty(baseName)) baseName = "PocketRoles";
            if (string.IsNullOrEmpty(ext)) ext = ".zip";
            return baseName + "-" + CacheTag(assetKey) + ext;
        }

        /// <summary>AssetKey を、ファイル名に使える 16 桁の 16 進にします。
        /// AssetKey はそのままでは名前に使えません（":" が入っていて、長さも決まっていません）。
        /// これは**真偽を確かめるハッシュではありません**（MOD の zip には固定の SHA-256 がありません）。
        /// 「別の配布物なら別の名前になる」ための見分け札です。</summary>
        internal static string CacheTag(string assetKey)
        {
            using (var alg = SHA256.Create())
            {
                var h = alg.ComputeHash(Encoding.UTF8.GetBytes(assetKey ?? ""));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>同じ配布物名で、鍵だけが違う古いキャッシュを消します。
        /// 消すのは**この app が付けた形（&lt;名前&gt;-&lt;16 桁の 16 進&gt;.zip）だけ**です。
        /// %TEMP%\PocketRolesLauncher は PowerShell 版ランチャーと共用なので、
        /// あちらが置いた鍵なしの "PocketRoles-0.5.5.zip" や "PocketRoles-0.5.5-setup.zip" は
        /// この形に当てはまらず、触りません。</summary>
        void DropOtherCachedBuilds(string keep, string assetName)
        {
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(assetName ?? "");
                string ext = Path.GetExtension(assetName ?? "");
                if (string.IsNullOrEmpty(baseName) || string.IsNullOrEmpty(ext)) return;
                if (string.IsNullOrEmpty(CacheDir) || !Directory.Exists(CacheDir)) return;
                foreach (var f in Directory.GetFiles(CacheDir, baseName + "-*" + ext))
                {
                    string n = Path.GetFileName(f);
                    if (string.Equals(n, keep, StringComparison.OrdinalIgnoreCase)) continue;
                    // 検索の形（*.zip）は短い名前にも当たることがあるので、前後を自分でもう一度確かめます
                    if (!n.StartsWith(baseName + "-", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!n.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
                    int tagLen = n.Length - (baseName.Length + 1) - ext.Length;
                    if (tagLen != 16) continue;
                    string tag = n.Substring(baseName.Length + 1, tagLen);
                    if (!IsLowerHex(tag)) continue;
                    DeleteQuiet(f);
                }
            }
            catch (Exception) { }
        }

        static bool IsLowerHex(string s)
        {
            foreach (char c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return s.Length > 0;
        }

        /// <summary>
        /// NOT fingerprint-checked, and this is the only place that says so. Only the BepInEx zip has a pinned SHA-256
        /// (<see cref="AppInfo.BepSha256"/>, docs\BEPINEX-PIN.md): the mod's own zip is checked for the entry it must
        /// hold and nothing more, because our releases carry no published checksum to compare against yet. The DLL in
        /// it is loaded as code by the game exactly like BepInEx's files are, so whoever can replace a release asset
        /// can still reach the game folder through this path. The fix is a signed SHA256SUMS.txt beside each release;
        /// until then, do not read "the installer checks the file itself" as covering both.
        /// </summary>
        internal bool InstallModZip(string zip)
        {
            if (!ZipFiles.Has(zip, ModDllEntry)) { Log(T("in_zip_bad", zip, "PocketRoles.dll")); return false; }
            Log(T("in_extract", Path.GetFileName(zip)));
            int n = Expand(zip, new[] { @"BepInEx\config\" });
            Log(T("in_extract_done", n));
            return FinishModInstall();
        }

        // ---- "an unpacking is going on": a file that exists only while a zip is being written into the game copy.
        // If it is still there at the next install, the last one was cut off and the files cannot be trusted, so
        // BepInEx and the mod are put back whatever their version numbers say.
        internal string ExtractMarkerPath => GameFolders.Join(Paths.Modded, ".starpocket-unpacking");

        internal bool ExtractionWasInterrupted() => GameFolders.PathExists(ExtractMarkerPath);

        /// <summary>展開を始める前に目印を置きます。戻り値は「この呼び出しが置いたか」。
        ///
        /// <para>**既に目印がある時は上書きせず false を返します。**その目印は前回の中断の記録で、まだ誰も
        /// 片付けていません。自分の展開が成功したからといってそれを消すと、たとえば
        /// 「BepInEx が半分だけ入った状態 → 次の起動で BepInEx は入れ直せなかったが MOD の展開だけ成功」
        /// のときに目印が消え、半分だけの BepInEx が次から「入っている」ことになります。
        /// 消してよいのは**自分が置いた目印だけ**です。前回の分を消すのは、5 つの手順が全部そろった時
        /// （<see cref="Install"/> の最後）だけです。</para></summary>
        bool MarkExtracting(string zipName)
        {
            if (GameFolders.PathExists(ExtractMarkerPath)) return false;
            try { Directory.CreateDirectory(Paths.Modded); File.WriteAllText(ExtractMarkerPath, zipName + "\r\n", new UTF8Encoding(false)); }
            catch (Exception) { }
            return true;
        }

        internal int Expand(string zip, string[] skip)
        {
            // 2026-10-01: ここは目印を finally で消していました。finally は**失敗した時にも走ります**。
            // つまり展開が途中で例外になっても（ディスクが満杯、ファイルが掴まれている、zip が壊れている）
            // 目印が消えてしまい、半分だけ展開されたファイルが次の起動で「入っている」ことになっていました。
            // 目印がある目的そのものが、その finally で打ち消されていた、ということです。
            // だから消すのは**正常に戻ってきた時だけ**。例外はそのまま上に投げ、目印は残します
            // （残っていれば次回 ExtractionWasInterrupted() が true になり、版に関係なく入れ直します）。
            bool mine = MarkExtracting(Path.GetFileName(zip));
            int n = ZipFiles.ExpandOver(zip, Paths.Modded, skip);
            if (mine) DeleteQuiet(ExtractMarkerPath);
            return n;
        }

        /// <summary>The same, from the handle the fingerprint was read on (<see cref="CheckAndExpandBep"/>).</summary>
        internal int Expand(VerifiedZip zip, string[] skip)
        {
            // 上と同じ理由: 成功して戻った時だけ目印を消します（finally では消しません）。
            bool mine = MarkExtracting(AppInfo.BepZipName);
            int n = zip.ExpandOver(Paths.Modded, skip);
            if (mine) DeleteQuiet(ExtractMarkerPath);
            return n;
        }

        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0", CultureInfo.InvariantCulture) + " MB";

        /// <summary>"BepInEx is already there, leave it": only when the file says it is the version we install AND we
        /// were not asked to force it back (「修復」, or an unpacking that was cut off). Forcing has to beat the version
        /// check, because the very thing that goes wrong is files that are new and files that are old side by side.</summary>
        internal static bool SkipBepInEx(bool force, string coreProductVersion) =>
            !force && !string.IsNullOrEmpty(coreProductVersion) &&
            coreProductVersion.IndexOf(AppInfo.BepInExVersion, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The same for the mod: Test-ModCurrent, unless we were asked to put it back whatever it says.</summary>
        internal static bool SkipMod(bool force, Version installed, Version available, string sourceKey, string stateModSource) =>
            !force && ReleaseInfo.ModCurrent(installed, available, sourceKey, stateModSource);

        /// <summary>Install-ModDir: an unpacked PocketRoles next to the launcher (the "はじめに.txt" way).</summary>
        internal bool InstallModDir(string dll)
        {
            Log(T("in_mod_localdir", Src));
            try
            {
                Directory.CreateDirectory(GameFolders.Join(Paths.Modded, @"BepInEx\plugins"));
                File.Copy(dll, Paths.DllPath, true);
                string langSrc = GameFolders.Join(Src, @"BepInEx\PocketRoles\lang");
                if (Directory.Exists(langSrc))
                {
                    string langDst = GameFolders.Join(Paths.Modded, @"BepInEx\PocketRoles\lang");
                    Directory.CreateDirectory(langDst);
                    foreach (var f in Directory.GetFiles(langSrc, "*.json")) File.Copy(f, Path.Combine(langDst, Path.GetFileName(f)), true);
                }
            }
            catch (Exception ex) { Log(T("err", ex.Message)); return false; }
            return FinishModInstall();
        }

        /// <summary>Finish-ModInstall: the old plugin goes, the version is recorded, Aegis gets the fingerprint of the file
        /// this app just installed.</summary>
        internal bool FinishModInstall()
        {
            string old = GameFolders.Join(Paths.Modded, @"BepInEx\plugins\HostRoles.dll");
            if (GameFolders.PathExists(old))
            {
                try { File.Delete(old); Log("HostRoles.dll (old plugin) removed"); } catch (Exception) { }
            }
            string v = GameVersion.DllVersionString(Paths.DllPath);
            State.Update(new Dictionary<string, object>
            {
                ["installedVersion"] = v,
                ["installedAt"] = LauncherStateFile.Stamp(Now()),
                ["gameVersion"] = GameVersion.Read(Paths.Modded),
            });
            Log(T("in_mod_done", v));
            SaveAegisFingerprint();
            ApplyConsentToMod();
            ModOriginFile.Record(OriginDir, Paths.DllPath, ModOrigin.Release, v, Now(), Log);   // v1.1: 配布用 is what is in the copy now
            return true;
        }

        /// <summary>最初の同意画面の 2 つの答え（チャット翻訳・自動通報）を MOD の設定ファイルに書く。
        ///
        /// 2026-09-27 に足しました。<see cref="Consent"/> のコメントは「インストールの時に MOD の設定に書く」と
        /// 言っていたのに、書く側がどこにもありませんでした（この PC では「自動通報はしない」と答えた記録があるのに
        /// [AntiCheat] AutoReport = true のままでした）。同意の画面で選ばせておいて効いていない、という形だったので塞ぎます。
        ///
        /// **同じ答えに対して一度しか書きません。**その後にホストが自分で /opt や設定タブで変えたものを、
        /// 次のアップデートや修復で黙って元に戻さないためです。文書が変わって同意を取り直したら（agreedAt が変わる）、
        /// その新しい答えでもう一度書きます。</summary>
        internal void ApplyConsentToMod()
        {
            try
            {
                if (string.IsNullOrEmpty(OriginDir) || Paths == null || string.IsNullOrEmpty(Paths.CfgPath)) return;
                var c = Consent.Load(Consent.PathIn(OriginDir));
                if (!c.Agreed || c.AgreedAt.Length == 0) return;
                if (State != null && State.Str("consentAppliedFor") == c.AgreedAt) return;   // この答えではもう書いた

                bool tr = c.ChatTranslate == "on", ar = c.AutoReport == "on";
                // 2026-10-03（公開前の粗探し 3）: Set の false は「同じ値だった」と「書けなかった」の両方だったので、4 値の Apply で分ける。
                //   - 書けなかった（読み取り専用など）: 印（consentAppliedFor）を付けない。付けると二度と書かず、同意の答えが MOD に届かないままになる
                //   - 別の値から本当に変わった（Changed）: ログだけでなく、画面の札で 1 回知らせる（ConsentNotice → TaskOutcome の consentNotice。
                //     /opt や設定タブで自分でオンにしていたホストが、更新や修復で黙って戻されていた）
                //   - 無かったので作った（Created。初めてのインストールは cfg そのものがまだ無い）: 印を付け、ログに書く。知らせない
                //     （崩す係 4: 自分で変えた物は何も無いのに「変えました…自分で変えていた人は」の札が、初めての人全員に出ていた）
                //   - 同じ値だった: 印だけ付け、知らせない（何も変わっていないのに「変えました」と言わない）
                var trOut = ModConfigFile.Apply(Paths.CfgPath, "Translate", "Enabled", tr ? "true" : "false", Log);
                var arOut = ModConfigFile.Apply(Paths.CfgPath, "AntiCheat", "AutoReport", ar ? "true" : "false", Log);
                if (trOut == SetOutcome.Failed || arOut == SetOutcome.Failed)
                {
                    Log("consent -> mod config: not written completely; it is tried again at the next install or repair");
                    return;
                }
                if (State != null)
                    State.Update(new Dictionary<string, object> { ["consentAppliedFor"] = c.AgreedAt });
                string trWord = T(tr ? "in_consent_on" : "in_consent_off"), arWord = T(ar ? "in_consent_on" : "in_consent_off");
                if (trOut == SetOutcome.Unchanged && arOut == SetOutcome.Unchanged) Log("consent -> mod config: already as answered (nothing changed)");
                else Log(T("in_consent", trWord, arWord));
                if (trOut == SetOutcome.Changed || arOut == SetOutcome.Changed) ConsentNotice = T("in_consent_changed", trWord, arWord);
                else if (trOut == SetOutcome.Created || arOut == SetOutcome.Created) Log("consent -> mod config: added where nothing was written yet (no notice: nobody's own choice was changed)");
            }
            catch (Exception ex) { Log("consent -> mod config: " + ex.Message); }
        }

        /// <summary>2026-10-03（粗探し 3）: この作業で <see cref="ApplyConsentToMod"/> が MOD の設定を**本当に変えた**時の、画面に出す文
        /// （in_consent_changed）。変えていなければ null。作業の答え（TaskOutcome.Data の "consentNotice"）に載せ、ClientApp が札で
        /// 1 回出し、--action はコンソールに 1 行出す（Headless.RunTask）。</summary>
        internal string ConsentNotice { get; private set; }

        /// <summary>TaskOutcome.Data の "consentNotice" の鍵。</summary>
        internal const string ConsentNoticeKey = "consentNotice";

        /// <summary><see cref="ConsentNotice"/> を作業の答えに載せる（Ok でも失敗でも。失敗の答えにも MOD の手順は済んでいることがある）。
        /// internal: 自己点検が、載る事を直に確かめる（崩す係 5 の M9: これを空にしても試験が通っていた）。</summary>
        internal TaskOutcome WithConsentNotice(TaskOutcome o)
        {
            if (o == null || string.IsNullOrEmpty(ConsentNotice)) return o;
            if (o.Data == null) o.Data = new Dictionary<string, object>();
            o.Data[ConsentNoticeKey] = ConsentNotice;
            return o;
        }

        /// <summary>作業の答えに載った知らせの文、無ければ null（ClientApp.DoTask と Headless.RunTask が読む）。</summary>
        internal static string ConsentNoticeOf(TaskOutcome o)
        {
            object v;
            if (o == null || o.Data == null || !o.Data.TryGetValue(ConsentNoticeKey, out v)) return null;
            var s = v as string;
            return string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>Save-AegisFingerprint (ps1:818-828): the DLL this app installed is the one Aegis expects.</summary>
        internal void SaveAegisFingerprint()
        {
            try
            {
                if (!GameFolders.PathExists(Paths.DllPath) || string.IsNullOrEmpty(AegisStateDir)) return;
                Directory.CreateDirectory(AegisStateDir);
                string sha;
                using (var s = new FileStream(Paths.DllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var alg = SHA256.Create())
                    sha = BitConverter.ToString(alg.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
                string ver = GameVersion.DllVersionString(Paths.DllPath);
                File.WriteAllText(Path.Combine(AegisStateDir, "mod-fingerprint.txt"), sha + "|" + ver, new UTF8Encoding(false));
            }
            catch (Exception ex) { Log("Aegis fingerprint: " + ex.Message); }
        }

        /// <summary>Find-LocalModZip: the newest PocketRoles-&lt;ver&gt;.zip next to the app (offline copies).</summary>
        internal string FindLocalModZip()
        {
            string best = null;
            Version bestVer = null;
            try
            {
                foreach (var f in Directory.GetFiles(Src ?? "", "PocketRoles-*.zip"))
                {
                    string name = Path.GetFileName(f);
                    if (ReleaseInfo.LooksLikeSetup(name)) continue;
                    var v = ReleaseInfo.Normalize(name);
                    if (v != null && (bestVer == null || v > bestVer)) { best = f; bestVer = v; }
                }
            }
            catch (Exception) { }
            return best;
        }

        /// <summary>Find-LocalModDll: an unpacked PocketRoles next to the app (never the copy's own DLL).</summary>
        internal string FindLocalModDll()
        {
            string dll = GameFolders.Join(Src ?? "", @"BepInEx\plugins\PocketRoles.dll");
            if (!GameFolders.PathExists(dll)) return null;
            try { if (string.Equals(Path.GetFullPath(dll), Path.GetFullPath(Paths.DllPath), StringComparison.OrdinalIgnoreCase)) return null; }
            catch (Exception) { }
            return dll;
        }

        // ---- [5/5] Step-Finish (no shortcut: SignPath)
        internal bool StepFinish()
        {
            Directory.CreateDirectory(Paths.Modded);
            string appid = GameFolders.Join(Paths.Modded, "steam_appid.txt");
            if (!GameFolders.PathExists(appid)) File.WriteAllText(appid, AppInfo.SteamAppId, Encoding.ASCII);
            return true;
        }

        // ------------------------------------------------------------------ Sync-GameCopy (the R state's "repair", friend mode)
        public TaskOutcome SyncGameCopy()
        {
            try
            {
                if (GameRunning()) { Log(T("game_running")); return TaskOutcome.Bad(T("game_running")); }
                string steam = SteamDir();
                if (string.IsNullOrEmpty(steam) || !GameFolders.PathExists(GameFolders.Join(steam, "Among Us.exe"))) steam = FindSteam();
                if (steam == null) { string why = T(StoreGame.NoGameKey()); Log(why); return new TaskOutcome { NeedGame = true, Error = why }; }
                SetSteamDir(steam);
                State.Set("steamDir", steam);
                Log(T("sync_start"));
                Progress(new TaskProgress { Task = "sync", Step = 1, Of = 2, Value = 0 });
                if (!CopyGameFiles(steam, Paths.Modded, 1)) return TaskOutcome.Bad(T("in_copy_fail", "copy"));
                Progress(new TaskProgress { Task = "sync", Step = 2, Of = 2, Value = 0.9 });
                ClearInterop();
                State.Set("gameVersion", GameVersion.Read(Paths.Modded));

                // ★ 2026-10-01（公開前レビューで見つかった穴）:
                //   ここは **BepInEx を一切触りません**（FileCopy が winhttp.dll と BepInEx フォルダーを
                //   わざと除外しています）。ゲームの種類が変わった更新のあとだと、
                //   「64bit のゲーム + 32bit の winhttp.dll」が残り、MOD はエラーも出さずに読み込まれません。
                //   しかもこの直後にゲームが起動する作り（ui\index.html の thenLaunch）なので、
                //   状態表示を直すだけでは **1 回は素のゲームで部屋を立ててしまいます。**
                //   だからここで種類を確かめ、違っていたらその場で入れ直します。
                string doorstop2 = GameFolders.Join(Paths.Modded, "winhttp.dll");
                bool archOk = GameFolders.PathExists(doorstop2) && PeArch.Matches(Paths.GameExe, doorstop2);
                if (!archOk)
                {
                    Log(T("in_bep_arch", PeArch.Name(PeArch.Machine(Paths.GameExe)), PeArch.Name(PeArch.Machine(doorstop2))));
                    if (!StepBepInEx(true)) return TaskOutcome.Bad(T("in_bep_fail"));
                }

                Log(T("sync_done"));
                Progress(new TaskProgress { Task = "sync", Step = 2, Of = 2, Value = 1 });
                return TaskOutcome.Good(T("sync_done"));
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return TaskOutcome.Bad(e);
            }
        }

        internal void ClearInterop()
        {
            foreach (var d in new[] { @"BepInEx\interop", @"BepInEx\cache" })
            {
                string path = GameFolders.Join(Paths.Modded, d);
                try { if (Directory.Exists(path)) Directory.Delete(path, true); }
                catch (Exception ex) { Log(T("err", ex.Message)); }
            }
        }

        // ------------------------------------------------------------------ Invoke-CheckUpdate
        /// <summary>The tools list's "check for updates" (<paramref name="install"/> false: only says what there is; the
        /// play button's 「アップデート」 is the consent, so that one installs).</summary>
        public TaskOutcome CheckUpdate(bool install)
        {
            try
            {
                Log(T("up_checking"));
                var rel = ReleaseInfo.Latest(Web);
                State.Set("lastCheck", LauncherStateFile.Stamp(Now()));
                if (!rel.Ok)
                {
                    string e = rel.ErrorText(Lang());
                    Log(e);
                    return TaskOutcome.Bad(e);
                }
                string instText = GameVersion.DllVersionString(Paths.DllPath);
                var inst = ReleaseInfo.Normalize(instText);
                if (string.IsNullOrEmpty(instText)) instText = T("v_none");
                Log(T("up_latest", rel.Version, instText));
                if (ReleaseInfo.ModCurrent(inst, rel.Version, rel.AssetKey, State.ModSource))
                    return TaskOutcome.Good(T("up_uptodate", instText), new Dictionary<string, object> { ["upToDate"] = true, ["version"] = rel.Version.ToString(), ["installed"] = instText });
                // 番号が同じなのに「新しいのがあります」と出ると、読んだ人には意味が通りません。
                // ModCurrent が false になるのは 2 通りあります:
                //   ・本当に新しい版が出た            → up_available（{0} 新しい版 / {1} 入っている版）
                //   ・番号は同じだが、中身が別のファイル → up_same_other_file（{0} その番号だけ）
                // 後者は、手で置いた DLL や、同じ番号で出し直された zip の時に起きます。
                bool sameNumber = inst != null && inst == rel.Version;
                string available = sameNumber
                    ? T(DevMode ? "up_same_other_file_dev" : "up_same_other_file", instText)
                    : T(DevMode ? "up_available_dev" : "up_available", rel.Version, instText);
                if (!install)
                {
                    Log(available);
                    return TaskOutcome.Good(available, new Dictionary<string, object> { ["available"] = true, ["version"] = rel.Version.ToString(), ["installed"] = instText });
                }
                if (GameRunning()) { Log(T("game_running")); return TaskOutcome.Bad(T("game_running")); }
                Progress(new TaskProgress { Task = "update", Step = 1, Of = 2, Value = 0 });
                if (!InstallModRelease(rel, 2)) return TaskOutcome.Bad(T("in_mod_fail"));
                State.Set("modSource", rel.AssetKey);
                string done = T("up_done", GameVersion.DllVersionString(Paths.DllPath));
                Log(done);
                Progress(new TaskProgress { Task = "update", Step = 2, Of = 2, Value = 1 });
                return WithConsentNotice(TaskOutcome.Good(done, new Dictionary<string, object> { ["updated"] = true, ["version"] = rel.Version.ToString() }));
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return TaskOutcome.Bad(e);
            }
        }

        /// <summary>The folder the viewer picked for Steam's Among Us (pickSteam): it must hold Among Us.exe, and it may not
        /// be the mod's own copy (copying a folder onto itself).</summary>
        public string CheckPickedSteam(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            if (!GameFolders.PathExists(GameFolders.Join(folder, "Among Us.exe"))) return null;
            if (FileCopy.SameOrInside(folder, Paths.Modded) || FileCopy.SameOrInside(Paths.Modded, folder)) return null;
            return folder;
        }

        public void RememberSteam(string folder)
        {
            SetSteamDir(folder);
            State.Set("steamDir", folder);
            Log(T("in_steam_found", folder));
        }
    }
}
