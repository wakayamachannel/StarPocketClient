// The running app: one process holding the window (WebView2), the tray icon, the taskbar button and Aegis.
// Start order (PORT-MAP 5): tray icon -> window + WebView2 (shown once the page is ready) -> Aegis -> status ->
// 30-day expiry and Save-GameLog in the background (as when the launcher opens).
// v1.1: Aegis's start scan (AegisService.StartOwned, a worker thread) is drawn on the little card at the bottom right on
// EVERY start (UpdateScanCard / ShowWaitingStartCard): the window first, the result after it; a red row keeps the card
// and opens the page's Aegis panel. Settings → 全般 → 起動時の動作 turns the card off (ClientSettings.StartScan).
// Answers the page's invokes (Bridge), sends events, and decides close / quit (PORT-MAP 4.3).
// v1.2: ✕・トレイ退避・終了は 140 ms 薄くなってから消える（HideToTray / RequestQuit → MainForm.FadeOut、ページへは "window" の closing）。
// v1.3: プロフィールの絵（profile.pickImage / profile.clearImage / profile.set、"shell" イベントの avatar / avatarError / profile）。
//       読むのは src\Core\ProfileImage.cs で、ここは選ぶ窓（OpenFileDialog）と返事だけ。選んだ元の画像のパスはログにも返事にも書かない。
// v0.1 writes no Run key, no shortcut and no file association (SignPath: no system change without the user asking).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Starpocket.Client.Aegis;
using Starpocket.Client.Core;

namespace Starpocket.Client.Shell
{
    internal sealed class ClientApp : ApplicationContext, ITrayActions
    {
        readonly ClientContext ctx;
        readonly string uiDir;
        readonly bool devTools;
        readonly MainForm form;
        readonly Taskbar taskbar;
        readonly TrayController tray;
        readonly IAegisService aegis;
        readonly GameLauncher launcher;
        readonly SingleInstance instance;
        readonly Timer gameTimer;
        readonly List<string> pending = new List<string>();
        /// <summary>What the command line asked for: with or without a window, play at once, the 15-second rule (v0.4).</summary>
        readonly StartupPlan plan;

        bool uiReady, shownOnce, quitting, blockedByAegis, gameRunning;
        /// <summary>v1.2: closing = 窓を薄くしている最中（✕ → トレイ / 終了）、quitFading = 終了の演出に入った（もう戻らない）。</summary>
        bool closing, quitFading;
        /// <summary>WebView2 has been asked to build itself (a start with no window waits until the window is wanted).</summary>
        bool webStarted;
        /// <summary>The window has been on screen at least once in this run. A start in the notification area
        /// (--tray, --autolaunch) has not, and that is what the 15-second rule looks at.</summary>
        bool windowShown;
        /// <summary>Someone asked for the window before the page was ready (the tray, or a second start).</summary>
        bool showWhenReady;
        /// <summary>SPEC 5.4: the window was put away because a game was started, so it comes back when the game ends.
        /// A window that was already hidden stays hidden - the app never opens itself over what someone is doing.</summary>
        bool restoreAfterGame;
        /// <summary>A game has been seen in this run, and when it went (Aegis.ps1's manual tray: it ends 15 s later).</summary>
        bool everWatched;
        DateTime? gameGoneAt;
        /// <summary>The scan card at the bottom right while the window is hidden (v0.4); null while none is open.</summary>
        ScanPopup scanCard;
        string scanCardPhase = "";
        /// <summary>v1.1: the start scan's card is wanted although the window is (about to be) on screen, and waits for
        /// the window (ScanCardPlan "wait"): the window first, the result after it, where it is seen.</summary>
        bool startCardWaiting;
        /// <summary>v1.1: the start scan of this run found a red row and the Aegis panel was opened for it - once.</summary>
        bool startFoundShown;
        /// <summary>Held while a long task runs, so "--action install" in another window says "busy" (SPEC 5.1).</summary>
        TaskLock taskLock;
        /// <summary>The last pre-launch scan stopped the start only because PocketRoles.dll changed (R-41): the play button
        /// says 「修復」 instead of "Aegis stopped the launch" (PORT-MAP 13.3).</summary>
        bool repairMod;
        /// <summary>The viewer picked Steam's folder while an install was waiting for Steam to install Among Us.</summary>
        volatile bool steamPicked;
        /// <summary>v1.3: 画像を選ぶ窓が出ているか、写しを作っている最中（二重に開かない。長い処理の busy とは別）。</summary>
        bool avatarBusy;
        string longTask;
        string badgeKind = "";
        LaunchStatus lastStatus;
        int statusSeq;

        public ClientApp(ClientContext ctx, SingleInstance instance, string uiDir, bool devTools, StartupPlan plan = null)
        {
            this.ctx = ctx;
            this.uiDir = uiDir;
            this.devTools = devTools;
            this.instance = instance;
            this.plan = plan ?? new StartupPlan();

            form = new MainForm(ctx.Log.Write);
            IntPtr created = form.Handle;   // the (hidden) window exists from now on: WebView2, taskbar and BeginInvoke need it
            ctx.Log.Write("window " + created.ToString("X"));
            taskbar = new Taskbar(form, ctx.Log.Write);
            form.TaskbarButtonCreated += (s, e) => taskbar.OnButtonCreated();
            form.CloseRequested += (s, e) => OnCloseButton();
            form.WebMessage += OnWebMessage;
            form.UiReady += (s, e) => OnUiReady();
            form.FadeCancelled += (s, e) => OnFadeCancelled();
            form.FormClosed += (s, e) => { if (!quitting) Shutdown(false); };   // Windows is signing out: the window is already closed

            aegis = AegisFactory.Create(new AegisContext
            {
                GameDir = ctx.Paths.Modded,
                StateDir = ctx.AegisStateDir,
                BundledDir = Path.Combine(ctx.ExeDir, "aegis"),
                Lang = ctx.Lang,
                Ui = form,
                Log = ctx.Log.Write,
            });
            aegis.Changed += (s, e) => OnAegisChanged();

            launcher = new GameLauncher
            {
                Paths = ctx.Paths,
                DevMode = ctx.DevMode,
                Lang = () => ctx.Lang,
                ComputeStatus = () => ctx.ComputeStatus(false),
                PreLaunchScan = aegis.PreLaunchScan,
                SaveGameLog = () => ctx.NewGameLogs().SaveGameLog(),
                Log = ctx.Log.Write,
            };

            tray = new TrayController(this);
            tray.SetLanguage(ctx.Lang);
            tray.Update(ctx.Lang, aegis.GetSnapshot());

            gameTimer = new Timer { Interval = 2000 };
            gameTimer.Tick += (s, e) => PollGame();

            instance.OnShowRequested(() => Post(ShowWindow));
            // v0.4: a second start with --autolaunch asks this one to play. It does NOT ask for the window: the person
            // wanted to play, and the window would be put away again a second later anyway (SPEC 5.4).
            instance.OnPlayRequested(windowed => Post(() => PlayFromOutside(windowed)));
            Post(Start);
        }

        // ------------------------------------------------------------------ start

        /// <summary>The answer of the first-run screen, read once at start (src\Core\Consent.cs).</summary>
        Consent consent;
        /// <summary>Screen ① is on top and nothing has been answered yet. While this is true the app has NOT touched the
        /// network and must not: Aegis is not started, housekeeping does not run, and every invoke outside
        /// <see cref="Bridge.BeforeConsent"/> is refused (Terms of Use Article 12(3)).</summary>
        bool waitingForConsent;

        async void Start()
        {
            tray.Show();
            gameRunning = Processes.GameRunning();

            // Before anything else: may this app go online at all? (first-run.md 1, Terms of Use Article 12(3))
            consent = Consent.Load(Consent.PathIn(ctx.DataDir));
            if (!consent.Covers(AppInfo.TermsVersion, AppInfo.PrivacyVersion, AppInfo.RulesVersion))
            {
                waitingForConsent = true;
                // A start with no window still has to ask. --tray and --autolaunch are exactly the two ways someone
                // could otherwise be online without ever having seen the screen, which is the promise we are keeping.
                plan.HideWindow = false;
                plan.AutoLaunch = false;
                ctx.Log.Write("consent: the first-run screen is needed - nothing is sent until it is answered");
                if (!await StartWebView()) return;
                return;   // the rest of the start waits for consent.set (OnConsentGiven)
            }
            await StartAfterConsent();
        }

        /// <summary>Everything the start used to do, from the moment the app is allowed to go online: the page (unless a
        /// start with no window), Aegis, the status, the game timer and the background housekeeping. Called straight
        /// from <see cref="Start"/> for someone who agreed on an earlier run, or from the answer of screen ①.</summary>
        async Task StartAfterConsent()
        {
            // a start with no window does not build the page yet (see StartWebView)
            if (!plan.HideWindow && !await StartWebView()) return;
            try { aegis.Start(); }
            catch (Exception ex) { ctx.Log.Write("Aegis failed: " + ex); }
            OnAegisChanged();
            RefreshStatus();
            gameTimer.Start();
            StartupHousekeeping();
            if (plan.HideWindow) ctx.Log.Write("start: no window (" + (plan.TrayStart ? "--tray" : "--autolaunch") + ")");
            if (plan.HideWindow && !ctx.Settings.TrayHintShown) ShowTrayHint();   // nothing on screen: say where the app is
            // a page that never reports "ready" still gets its window after 10 s - unless no window was asked for
            if (plan.HideWindow) return;
            var fallback = new Timer { Interval = 10000 };
            fallback.Tick += (s, e) => { fallback.Stop(); fallback.Dispose(); if (!shownOnce && !quitting) FirstShow(); };
            fallback.Start();
        }

        /// <summary>consent.set: the answer of screen ① (first-run.md 2.5). The app writes it down and only then does
        /// the rest of its start - that order is the whole point, so the record exists before the first byte goes out.
        ///
        /// <para>Refused when the page sends a version the app does not ship. Otherwise a page could write "agreed to
        /// 2.0" into the record and the screen would never come back. The versions are not taken from the page at all
        /// beyond this check: what is written is what <see cref="AppInfo"/> says, so the record can only ever name the
        /// documents this build actually showed.</para></summary>
        void DoConsentSet(string id, Dictionary<string, object> args)
        {
            if (!waitingForConsent) { Reply(id, Bridge.Fail("already answered")); return; }
            if (!Json.Bool(args, "agreed")) { Reply(id, Bridge.Fail("declining writes nothing; use consent.decline")); return; }

            string terms = Json.Str(args, "terms"), privacy = Json.Str(args, "privacy"), rules = Json.Str(args, "rules");
            if (terms != AppInfo.TermsVersion || privacy != AppInfo.PrivacyVersion || rules != AppInfo.RulesVersion)
            {
                ctx.Log.Write("consent: refused (the page named other versions than this build ships)");
                Reply(id, Bridge.Fail("version mismatch"));
                return;
            }

            var c = Consent.FromAnswer(true, AppInfo.TermsVersion, AppInfo.PrivacyVersion, AppInfo.RulesVersion,
                                       Json.Str(args, "chatTranslate"), Json.Str(args, "autoReport"),
                                       AppInfo.Version, DateTime.Now);
            if (c == null) { Reply(id, Bridge.Fail("invalid answer")); return; }

            try { c.Save(Consent.PathIn(ctx.DataDir)); }
            catch (Exception ex)
            {
                // The answer could not be kept. Carrying on would mean going online with nothing written down, and
                // asking again on the next start with no way to know why - so say so and stay where we are.
                ctx.Log.Write("consent: could not write " + Consent.FileName + ": " + ex.Message);
                Reply(id, Bridge.Fail(S.T(ctx.Lang, "err", ex.Message)));
                return;
            }

            consent = c;
            waitingForConsent = false;
            // The two opt-in answers travel to the mod's config when the mod is installed (first-run.md 2.5).
            if (ClientSettings.IsValidChatTranslate(c.ChatTranslate) && ctx.Settings.SetChatTranslate(c.ChatTranslate))
            {
                try { ctx.Settings.Save(ctx.SettingsPath); } catch (Exception ex) { ctx.Log.Write("settings: " + ex.Message); }
            }
            ctx.Log.Write("consent: agreed (terms " + c.Terms + ", privacy " + c.Privacy + ", rules " + c.Rules
                          + ", chatTranslate " + c.ChatTranslate + ", autoReport " + c.AutoReport + ")");
            Reply(id, Bridge.Ok());
            Post(async () => await StartAfterConsent());
        }

        /// <summary>「同意しない」 → 「Client を閉じる」: nothing is written and nothing is sent (first-run.md 2.4).
        /// The next start asks again from screen ①.</summary>
        void DeclineAndQuit()
        {
            ctx.Log.Write("consent: declined - closing without writing anything");
            RequestQuit();
        }

        /// <summary>Builds WebView2 and opens the page. A start with no window (--tray, --autolaunch) waits with this
        /// until somebody asks for the window: a page nobody can see still costs several msedgewebview2 processes and a
        /// few hundred MB, and --tray is what the "open when Windows starts" switch writes into the Run key.
        /// Everything the app wants to tell the page meanwhile is kept (SendEvent's pending list) and sent when the page
        /// says it is ready. True when the page is on its way.</summary>
        async Task<bool> StartWebView()
        {
            if (webStarted) return true;
            webStarted = true;
            try
            {
                await form.InitializeWebViewAsync(Path.Combine(ctx.DataDir, "WebView2"), uiDir, ctx.Lang, devTools,
                    AccentColor.BootScript(ctx.Settings.Accent));
                return true;
            }
            catch (Exception ex)
            {
                ctx.Log.Write("WebView2 start failed: " + ex);
                MessageBox.Show(S.T(ctx.Lang, "ui.failed", ex.Message), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Shutdown();
                return false;
            }
        }

        /// <summary>Remove-ExpiredLocalData, Save-GameLog, then the day zips, in the launcher's own order (ps1:2135).
        /// A long task: a launch waits for it. The logs-folder size is announced afterwards, like
        /// Update-LogSizeLabel -Announce, so the 2 GB notice is said once per start and not on every redraw.</summary>
        void StartupHousekeeping()
        {
            if (!TryBeginTask("startup")) { AfterStartup(); return; }
            Task.Run(() =>
            {
                var gl = ctx.NewGameLogs();
                gl.RemoveExpired();
                gl.SaveGameLog();
                // v0.4: loose logs older than 7 days into one zip per day. Room only - no retention number changes.
                gl.CompressOldLogs();
                return gl.ArchiveInfo();
            }).ContinueWith(t => Post(() =>
            {
                if (t.IsFaulted) ctx.Log.Write("housekeeping: " + t.Exception.GetBaseException().Message);
                else SendLogSize(t.Result, true);
                EndTask();
                AfterStartup();
            }));
        }

        /// <summary>The launcher's Form.Shown ends with -AutoLaunch (ps1:2137): once the 30-day clean-up and the log copy
        /// are done, --autolaunch plays. It waits for them on purpose - a launch while they run would only be told
        /// "busy", which is what the launcher's own Set-Busy did.</summary>
        void AfterStartup()
        {
            if (quitting || !plan.AutoLaunch) return;
            plan.AutoLaunch = false;   // once per run, whatever else calls this
            ctx.Log.Write(S.T(ctx.Lang, "auto_launch"));
            StartPlay(plan.Windowed, "--autolaunch");
        }

        /// <summary>Update-LogSizeLabel: 「ログ: 123 MB」 beside 「ログのフォルダを開く」, in the danger colour past 2 GB.
        /// <paramref name="announce"/> also writes the 2 GB line into the log, once per start.</summary>
        void SendLogSize(LogFolderInfo info, bool announce = false)
        {
            if (info == null || quitting) return;
            lastLogInfo = info;
            if (announce && info.Big) ctx.Log.Write(S.T(ctx.Lang, "lg_big", info.Size));
            SendEvent("logs", LogData(info));
        }

        /// <summary>What the last walk of the logs folder found. The log page asks again every two seconds while it is
        /// open, and until the v0.4 review each of those asks counted the whole folder AGAIN, on the window's own
        /// thread. The number is the same one RefreshLogSize already works out on a pool thread, so the page is handed
        /// that instead and nothing is counted twice.</summary>
        LogFolderInfo lastLogInfo;

        Dictionary<string, object> LogData(LogFolderInfo info) => new Dictionary<string, object>
        {
            ["bytes"] = info.Bytes,
            ["size"] = info.Size,
            ["big"] = info.Big,
            ["logs"] = info.Logs,
            ["zips"] = info.Zips,
            ["text"] = S.T(ctx.Lang, "lg_size", info.Size),
            ["tip"] = info.Big ? S.T(ctx.Lang, "lg_big", info.Size) : S.T(ctx.Lang, "lg_tip"),
        };

        /// <summary>Walks the logs folder off the UI thread and sends the size line (the launcher works it out at the
        /// end of every Refresh-Status).</summary>
        void RefreshLogSize()
        {
            Task.Run(() => ctx.NewGameLogs().ArchiveInfo()).ContinueWith(t => Post(() =>
            {
                if (t.IsFaulted) { ctx.Log.Write("log size: " + t.Exception.GetBaseException().Message); return; }
                SendLogSize(t.Result);
            }));
        }

        void OnUiReady()
        {
            uiReady = true;
            foreach (var json in pending) form.PostJson(json);
            pending.Clear();
            // v1.3: 自分のプロフィールの絵の写し（profile\avatar.png）。無ければ null を毎回載せる（ページは 4 か所に一度に描く）。
            // 読めなかった写しは消えていて、その理由を 1 回だけ言う（avatarError）。
            string avatarErr;
            var png = ProfileImage.Load(ctx.AvatarPath, ctx.Lang, out avatarErr);
            if (avatarErr != null) ctx.Log.Write("avatar: " + avatarErr);
            var shell = new Dictionary<string, object>
            {
                ["version"] = AppInfo.UiVersion,
                ["app"] = AppInfo.Version,
                ["dragRegion"] = form.DragRegionSupported,
                ["close"] = ctx.Settings.Close,
                ["startGame"] = ctx.Settings.StartGame,   // v0.1.1: Settings → 起動するゲーム (settings.json wins over the page's storage)
                ["autostart"] = AutoStart.IsOn(),         // v0.3: what Windows will really do, not what the page remembers
                ["accent"] = ctx.Settings.Accent,         // v0.3.1: which swatch the settings page shows as chosen
                ["startScan"] = ctx.Settings.StartScan,   // v1.1: the start scan's card on every start (settings.json wins over the page's storage)
                ["sound"] = ctx.Settings.Sound,           // v1.2: ページの音（Aegis が見つけた時の 1 つ）の入切と音量（0-100）。settings.json が勝つ。
                ["volume"] = ctx.Settings.Volume,         //       どちらも、起動時のスキャンの結果を運ぶ最初の "aegis" イベント（下）より前に届く
                ["vars"] = AccentColor.Vars(ctx.Settings.Accent),
                                                          // ... and the colours themselves. MainForm has already put
                                                          // them on <html> before the first paint, so nothing flashes;
                                                          // sending them again costs nothing and means the page can
                                                          // always put itself right, whatever a reload left behind.
                ["mode"] = ctx.DevMode ? "dev" : "friend",
                // v1.1: the author's switch (Settings → PocketRoles → 開発). devFolder is the working copy of the mod
                // found on this PC - and null on every other PC, where the page then draws no switch at all. The
                // account name is taken out of the path (the owner streams with this window open). devOn is the
                // setting as saved; "mode" above is what this run really is (src\Core\DevSource.cs).
                ["devFolder"] = ctx.DevSwitchShown ? Mask.Home(ctx.DevFolder, Environment.GetEnvironmentVariable("USERPROFILE")) : null,
                ["devOn"] = ctx.Settings.DevBuild,
                ["avatar"] = png == null ? null : ProfileImage.DataUrl(png),   // v1.3: 自分の画像（data: の PNG、256×256）か null
                // 最初の同意の画面（first-run.md ①）。null なら出しません。null でない時、ページは重ね画面を出して
                // 答えを consent.set で返すまで、ほかの命令を送っても needConsent で断られます（Bridge.BeforeConsent）。
                // 版の番号はここで渡した物をそのまま返してもらい、ClientApp が突き合わせます（作り話を書かせないため）。
                // chatTranslate は「今のランチャーから移ってきた人」の今の値です（first-run.md 2.6）。
                ["firstRun"] = !waitingForConsent ? null : new Dictionary<string, object>
                {
                    ["terms"] = AppInfo.TermsVersion,
                    ["privacy"] = AppInfo.PrivacyVersion,
                    ["rules"] = AppInfo.RulesVersion,
                    ["chatTranslate"] = ctx.Settings.ChatTranslate == "on",
                    ["again"] = consent != null && consent.Agreed,   // 前に同意した人への「新しくなりました」（first-run.md 5）
                },
            };
            if (avatarErr != null) shell["avatarError"] = avatarErr;
            // v1.3: 名前と絵の番号は settings.json のもの（startGame と同じくページの保存に勝つ）。まだ無ければ載せず、ページが
            // 自分の分を profile.set で送ってくる
            if (ctx.Settings.HasProfile)
                shell["profile"] = new Dictionary<string, object> { ["name"] = ctx.Settings.ProfileName, ["avatar"] = ctx.Settings.ProfileAvatar };
            SendEvent("shell", shell);
            SendLang();
            SendEvent("aegis", aegis.GetSnapshot().ToEvent());
            SendEvent("game", new Dictionary<string, object> { ["running"] = gameRunning });
            if (lastStatus != null) SendEvent("status", StatusData(lastStatus));
            // --tray / --autolaunch: the page is ready but nothing is shown. Only someone asking for it opens the window.
            if (plan.HideWindow && !form.Visible) SendEvent("window", new Dictionary<string, object> { ["visible"] = false });
            if (showWhenReady) { showWhenReady = false; ShowWindow(); return; }
            if (!shownOnce && !plan.HideWindow) FirstShow();
        }

        void FirstShow()
        {
            shownOnce = true;
            windowShown = true;
            form.EnsureOpaque();
            form.Show();
            form.Activate();
            SendEvent("window", new Dictionary<string, object> { ["visible"] = true });
            ShowWaitingStartCard();   // v1.1: the start scan's result, now that there is a window to see it beside
        }

        /// <summary>The window is not on screen for the person: started with --tray or --autolaunch and never opened, or
        /// opened and then put away (the close button, or a game start). This is what decides whether a scan is drawn on
        /// the little card instead of the page's Aegis panel.</summary>
        bool WindowAway => !quitting && (windowShown ? !form.Visible || form.WindowState == FormWindowState.Minimized : plan.HideWindow);

        // ------------------------------------------------------------------ events to the page
        void SendEvent(string name, object data)
        {
            string json = Bridge.EventJson(name, data);
            if (!uiReady)
            {
                if (pending.Count < 200) pending.Add(json);
                return;
            }
            form.PostJson(json);
        }

        /// <summary>The answer to one invoke. id is null when nothing asked through the page (--autolaunch, a play asked
        /// for by another start): there is then nobody to answer, and the log already holds what happened.</summary>
        void Reply(string id, Dictionary<string, object> result)
        {
            if (id == null) return;
            form.PostJson(Bridge.ResultJson(id, result));
        }

        void SendLang() => SendEvent("lang", new Dictionary<string, object> { ["lang"] = Lang.ToUi(ctx.Lang), ["pref"] = Lang.PrefToUi(ctx.Settings.Lang) });

        Dictionary<string, object> StatusData(LaunchStatus s) => new Dictionary<string, object>
        {
            ["mode"] = ctx.DevMode ? "dev" : "friend",
            // v1.1: which PocketRoles.dll is in the copy - the release this app installed, the author's own build, or
            // one put there by something else (ModOrigin); null when there is no DLL. The words are the app's own.
            ["dllOrigin"] = s.Origin != null ? s.Origin.Kind : null,
            ["dllOriginText"] = s.Origin != null ? s.Origin.Text(ctx.Lang) : null,
            ["pstate"] = s.PState,
            ["steamVersion"] = s.SteamVer,
            ["copyVersion"] = s.ModVer,
            ["bepinex"] = s.Info.BepVer,
            ["bepinexOk"] = s.Info.BepOk,
            ["dll"] = s.Info.DllVer,
            ["interop"] = s.Info.Interop,
            ["installed"] = s.Installed,
            ["steamFound"] = ctx.SteamDir != null,
            ["steamRunning"] = s.SteamRunning,
            ["gameRunning"] = s.GameRunning,
            ["warn"] = s.Warn(ctx.Lang),
        };

        /// <summary>Get-StatusLines off the UI thread (it reads globalgamemanagers twice); only the newest answer is sent.</summary>
        void RefreshStatus()
        {
            int seq = ++statusSeq;
            bool blocked = blockedByAegis, repair = repairMod;
            Task.Run(() => ctx.ComputeStatus(blocked, repair)).ContinueWith(t => Post(() =>
            {
                if (seq != statusSeq) return;
                if (t.IsFaulted) { ctx.Log.Write("status: " + t.Exception.GetBaseException().Message); return; }
                lastStatus = t.Result;
                SendEvent("status", StatusData(lastStatus));
            }));
            RefreshLogSize();   // ps1: Refresh-Status ends with Update-LogSizeLabel
        }

        void PollGame()
        {
            bool now = Processes.GameRunning();
            if (now != gameRunning)
            {
                gameRunning = now;
                if (now) { everWatched = true; gameGoneAt = null; }
                else gameGoneAt = DateTime.Now;
                SendEvent("game", new Dictionary<string, object> { ["running"] = now });
                RefreshStatus();
                // SPEC 5.4: the window that stepped aside for the game comes back - without taking the screen from
                // whatever is in front now
                if (!now && restoreAfterGame) { restoreAfterGame = false; ShowAfterGame(); }
            }
            double gone = gameGoneAt.HasValue ? (DateTime.Now - gameGoneAt.Value).TotalSeconds : 0;
            if (TrayAutoQuit(plan.TrayStart, windowShown, everWatched, now, gone, longTask != null))
            {
                ctx.Log.Write("tray: started with --tray, the game ended more than 15 s ago: quitting (Aegis.ps1's rule)");
                Shutdown();
            }
        }

        /// <summary>Aegis.ps1's manual start (Tray.Poll, v0.5.5 line ~1307): a tray nobody opened ends by itself 15
        /// seconds after the game it watched. It is off the moment the person opens the window - from then on this is
        /// their app, not a helper that started with a game.</summary>
        internal static bool TrayAutoQuit(bool trayStart, bool windowShown, bool everWatched, bool gameRunning, double secondsSinceGone, bool busy) =>
            trayStart && !windowShown && everWatched && !gameRunning && !busy && secondsSinceGone > 15;

        /// <summary>SPEC 5.4: a window that is on screen steps aside for the game it just started, and only such a
        /// window comes back when the game ends. One that was already away (started with --tray or --autolaunch, or put
        /// away by the close button) is left alone in both directions.</summary>
        internal static bool StepAsideForGame(bool windowVisible) => windowVisible;

        void OnAegisChanged()
        {
            if (quitting) return;
            var snap = aegis.GetSnapshot();
            tray.Update(ctx.Lang, snap);
            UpdateBadge(snap);
            UpdateScanCard(snap);
            SendEvent("aegis", snap.ToEvent());
        }

        // ------------------------------------------------------------------ the scan card (v0.4, v1.1)
        /// <summary>While the window is away, a scan is drawn on the little card at the bottom right instead of the
        /// page's Aegis panel, which nobody can see then (PORT-MAP 2.2). The rows, the line under the title and when it
        /// closes all come from the same snapshot the panel uses, so the two can never say different things.
        /// v1.1 (the owner, 2026-09-23 「起動のたびに出す形に変えて」): the START scan gets the card on every start, window or no
        /// window, because the panel is closed when the app opens and nobody saw the 13 checks run - Aegis.ps1's Splash
        /// showed them with every launcher. On a normal start the card waits for the window (ScanCardPlan "wait"): the
        /// window comes first, the result after it, where it is seen. Quiet: the card fades by itself (1.8 s; 4.2 s with
        /// warnings). A red row: the card stays until it is clicked, and the page's Aegis panel opens (FoundAtStart).
        /// Settings → 全般 → 起動時の動作 turns the card off; the panel still opens for a red row.</summary>
        void UpdateScanCard(AegisSnapshot snap)
        {
            if (snap == null) { CloseScanCard(); return; }
            bool start = snap.Kind == "start";
            if (start && snap.Phase == "done" && snap.LastScanSerious > 0 && !startFoundShown) FoundAtStart(snap.LastScanSerious);
            switch (ScanCardPlan(snap.Kind, WindowAway, windowShown, ctx.Settings.StartScan))
            {
                case "live": DrawScanCard(snap, start); break;
                case "wait": startCardWaiting = true; break;
                default: CloseScanCard(); break;
            }
        }

        /// <summary>Where a scan is drawn. "live": on the card as the rows run (the window is away, v0.4 - or the start
        /// scan runs while the window is up); "wait": on the card once the window is on screen (the start scan of a
        /// normal start, which runs before the page is ready); "none": the page's panel has it, and any card closes.</summary>
        internal static string ScanCardPlan(string kind, bool windowAway, bool windowShown, bool startScanSetting)
        {
            if (windowAway) return "live";
            if (kind == "start" && startScanSetting) return windowShown ? "live" : "wait";
            return "none";
        }

        /// <summary>The card follows the snapshot: opened on "scanning", finished on "done".</summary>
        void DrawScanCard(AegisSnapshot snap, bool startScan)
        {
            if (snap.Phase == "scanning")
            {
                bool preLaunch = snap.Kind == "prelaunch";
                if (scanCard == null) OpenScanCard(preLaunch, startScan);
                else if (scanCardPhase != "scanning")
                {
                    // a new scan on a card still on screen (--autolaunch: the pre-launch scan begins while the start
                    // scan's card is there): it starts over as its own scan, with its own waits - a start card that
                    // stays for a red row must not make a pre-launch card stay too
                    scanCard.KeepWhenSerious = startScan;
                    scanCard.Begin(ctx.Lang, preLaunch);
                }
                scanCardPhase = "scanning";
                if (scanCard != null) scanCard.Update(ScanRows(snap), snap.Summary);
            }
            else if (snap.Phase == "done" && scanCardPhase == "scanning")
            {
                scanCardPhase = "done";
                if (scanCard == null) return;
                scanCard.Update(ScanRows(snap), snap.Summary);
                scanCard.Done(snap.LastScanSerious, snap.LastScanWarnings, snap.Summary);   // it closes itself after that (a red start row: on a click)
            }
        }

        /// <summary>FirstShow / ShowWindow: the start scan's card that waited for the window (v1.1). A scan that has
        /// already ended is shown ended - the 13 rows and the headline, then Done, so it stays its full time from NOW,
        /// beside the window that has just appeared. One still running carries on live.</summary>
        void ShowWaitingStartCard()
        {
            if (!startCardWaiting || quitting) return;
            startCardWaiting = false;
            AegisSnapshot snap;
            try { snap = aegis.GetSnapshot(); }
            catch (Exception ex) { ctx.Log.Write("scan card: " + ex.Message); return; }
            if (snap.Kind != "start" || !ctx.Settings.StartScan) return;
            if (snap.Phase == "scanning") { DrawScanCard(snap, true); return; }
            if (snap.Phase != "done") return;
            if (scanCard == null) OpenScanCard(false, true);
            if (scanCard == null) return;
            scanCardPhase = "done";
            scanCard.Update(ScanRows(snap), snap.Summary);
            scanCard.Done(snap.LastScanSerious, snap.LastScanWarnings, snap.Summary);
        }

        /// <summary>v1.1: the start scan found a red row. Said once per run, whatever the card setting says: the page's
        /// Aegis panel opens (as it does when a start is blocked), so the red rows and how to fix them are in front of
        /// the person and stay there; the red badge and the tray dot are already set (UpdateBadge, tray.Update). While
        /// the window is away the event waits for the page, and the panel is open when the window is opened.</summary>
        void FoundAtStart(int serious)
        {
            startFoundShown = true;
            ctx.Log.Write("Aegis start scan: " + serious + " red row(s); the Aegis panel opens");
            SendEvent("nav", new Dictionary<string, object> { ["open"] = "d-aegis" });
        }

        void OpenScanCard(bool preLaunch, bool startScan = false)
        {
            try
            {
                var card = new ScanPopup(ctx.Lang) { KeepWhenSerious = startScan };   // v1.1: a red row at start stays until clicked
                card.FormClosed += (s, e) => { if (scanCard == card) { scanCard = null; scanCardPhase = ""; } };
                card.Begin(ctx.Lang, preLaunch);
                scanCard = card;
            }
            catch (Exception ex) { ctx.Log.Write("scan card: " + ex.Message); scanCard = null; }
        }

        void CloseScanCard()
        {
            if (scanCard == null) { scanCardPhase = ""; return; }
            var card = scanCard;
            scanCard = null;
            scanCardPhase = "";
            try { card.CloseNow(); } catch (Exception) { }
        }

        static List<ScanRowView> ScanRows(AegisSnapshot snap)
        {
            var rows = new List<ScanRowView>();
            foreach (var r in snap.Rows) rows.Add(new ScanRowView { Title = r.Title, Detail = r.Detail, Fix = r.Fix, State = r.State });
            return rows;
        }

        /// <summary>Red while the last scan has a red row, amber for 12 s after a removal, else none (PORT-MAP 4.4).</summary>
        void UpdateBadge(AegisSnapshot snap)
        {
            string kind = snap.LastScanSerious > 0 ? "serious" : snap.State == "kicked" ? "kicked" : "";
            if (kind == badgeKind) return;
            badgeKind = kind;
            int size = SystemInformation.SmallIconSize.Width;
            if (kind == "serious") taskbar.SetOverlay(Icons.Badge(size, Icons.Serious, System.Drawing.Color.White), S.T(ctx.Lang, "badge.serious"));
            else if (kind == "kicked") taskbar.SetOverlay(Icons.Badge(size, Icons.Kicked, Icons.Navy), S.T(ctx.Lang, "badge.kicked"));
            else taskbar.SetOverlay(null, null);
        }

        void OnScanProgress(string task, ScanProgress p)
        {
            int of = p.Of > 0 ? p.Of : 13;
            SendEvent("progress", new Dictionary<string, object> { ["task"] = task, ["step"] = p.Step, ["of"] = of, ["value"] = Math.Min(1.0, (double)p.Step / of) });
            taskbar.SetProgress(TaskbarProgress.Normal, (ulong)Math.Max(0, p.Step), (ulong)of);
        }

        // ------------------------------------------------------------------ long tasks (one at a time)
        /// <summary>v0.4: besides "one at a time in this app", a long task now also holds the named mutex of SPEC 5.1,
        /// so "StarPocket Client.exe --action install" typed in another window is told "busy" instead of unpacking into
        /// the same game copy at the same moment.</summary>
        bool TryBeginTask(string name)
        {
            if (longTask != null) return false;
            taskLock = TaskLock.TryTake(AppInfo.TaskMutexName);
            if (taskLock == null) { ctx.Log.Write("task " + name + ": " + S.T(ctx.Lang, "cl_busy")); return false; }
            longTask = name;
            return true;
        }

        void EndTask()
        {
            longTask = null;
            if (taskLock != null) { taskLock.Dispose(); taskLock = null; }
        }

        void Post(Action a)
        {
            if (form.IsDisposed || !form.IsHandleCreated) return;
            try { form.BeginInvoke(a); }
            catch (InvalidOperationException) { }   // the window is gone (ObjectDisposedException is one of these)
        }

        // ------------------------------------------------------------------ invokes
        void OnWebMessage(string json)
        {
            var inv = Invoke.Parse(json);
            if (inv == null) return;
            try { HandleInvoke(inv); }
            catch (Exception ex)
            {
                ctx.Log.Write("invoke " + inv.Cmd + ": " + ex);
                Reply(inv.Id, Bridge.Fail(S.T(ctx.Lang, "err", ex.Message)));
            }
        }

        void HandleInvoke(Invoke inv)
        {
            string id = inv.Id, cmd = inv.Cmd;
            switch (Bridge.Classify(cmd))
            {
                case Bridge.Kind.Later: Reply(id, Bridge.Unsupported(cmd)); return;
                case Bridge.Kind.Unknown: Reply(id, Bridge.Fail("unknown command")); return;
            }
            // Screen ① has not been answered: only the few things that screen itself needs (Bridge.BeforeConsent).
            // This is the gate that keeps Terms of Use Article 12(3) true even if the page is replaced or scripted.
            if (waitingForConsent && !Bridge.BeforeConsent.Contains(cmd))
            {
                ctx.Log.Write("consent: refused '" + cmd + "' (the first-run screen has not been answered)");
                Reply(id, Bridge.NeedConsent());
                return;
            }
            if (longTask != null && Bridge.BusyGated.Contains(cmd)) { Reply(id, Bridge.Busy()); return; }
            switch (cmd)
            {
                case "consent.set": DoConsentSet(id, inv.Args); break;
                case "consent.decline": Reply(id, Bridge.Ok()); Post(() => DeclineAndQuit()); break;
                case "launch":
                case "launchWindowed": DoLaunch(id, cmd); break;
                case "launchVanilla": DoLaunchVanilla(id); break;
                case "install": DoInstall(id, inv.Args); break;
                case "syncSteam": DoTask(id, cmd, i => i.SyncGameCopy()); break;
                case "checkUpdate": DoTask(id, cmd, i => i.CheckUpdate(Json.Bool(inv.Args, "install"))); break;
                case "pickSteam": Reply(id, PickSteam()); break;
                case "aegis.rescan": RunScan(false, r => Reply(id, r)); break;
                case "aegis.scanOnly": RunScan(true, r => Reply(id, r)); break;
                case "aegis.events": Reply(id, OpenInNotepad(aegis.EventsLogPath, "events.log")); break;
                case "openModFolder": Reply(id, OpenInExplorer(ctx.Paths.Modded)); break;
                case "openLogsFolder":
                    if (!GameFolders.PathExists(ctx.Paths.Modded)) { Reply(id, NotFound(S.T(ctx.Lang, "st_mod"), ctx.Paths.Modded)); break; }
                    try { Directory.CreateDirectory(ctx.Paths.LogArchiveDir); } catch (Exception ex) { Reply(id, Bridge.Fail(S.T(ctx.Lang, "err", ex.Message))); break; }
                    Reply(id, OpenInExplorer(ctx.Paths.LogArchiveDir));
                    break;
                // ---- v0.3: the report zip, one player's evidence, the mail links, the shortcut button, the uninstall
                case "makeReport": DoReport(id, "makeReport", r => r.MakeReport()); break;
                case "exportOne": DoReport(id, "exportOne", r => r.ExportOne(Json.Str(inv.Args, "who"))); break;
                case "mailBug": Reply(id, OpenMail(AppInfo.MailBug, "rp_subject_bug", Json.Str(inv.Args, "zip"))); break;
                case "mailRequest": Reply(id, OpenMail(AppInfo.MailRequest, "rp_subject_req", Json.Str(inv.Args, "zip"))); break;
                case "openReportFolder": Reply(id, OpenInExplorer(ctx.Desktop)); break;
                case "shortcut.create": Reply(id, MakeShortcut(inv.Args)); break;
                case "uninstall": DoUninstall(id, inv.Args); break;
                // ---- v0.4: the app's own log page, and developer mode's rebuild / update
                case "showLog": Reply(id, ShowLog()); break;
                case "rebuild": DoDev(id, cmd, d => d.Rebuild()); break;
                case "devUpdate": DoDev(id, cmd, d => d.DevUpdate()); break;
                case "openConfig": Reply(id, OpenInNotepad(ctx.Paths.CfgPath, S.T(ctx.Lang, "f_cfg"))); break;
                case "openLog": Reply(id, OpenInNotepad(ctx.Paths.LogPath, S.T(ctx.Lang, "f_log"))); break;
                case "openReadme": Reply(id, OpenInNotepad(LauncherFiles.ReadmePath(ctx.DevMode, ctx.Src, ctx.Paths.Modded, ctx.Lang), S.T(ctx.Lang, "f_readme"))); break;
                case "window.minimize": form.WindowState = FormWindowState.Minimized; Reply(id, Bridge.Ok()); break;
                case "window.close": Reply(id, Bridge.Ok()); Post(OnCloseButton); break;
                case "window.drag": Reply(id, Bridge.Ok()); Post(form.BeginDrag); break;   // the move loop runs outside the WebView2 event
                case "app.quit": Reply(id, Bridge.Ok()); Post(RequestQuit); break;
                case "settings.set": Reply(id, SetSetting(inv.Args)); break;
                case "setLang": Reply(id, SetLanguage(inv.Args)); break;
                // ---- v1.3: プロフィール（名前と絵の番号、自分の画像）
                case "profile.set": Reply(id, SetProfile(inv.Args)); break;
                case "profile.pickImage": PickAvatarImage(id); break;
                case "profile.clearImage": Reply(id, ClearAvatarImage()); break;
                default: Reply(id, Bridge.Fail("unknown command")); break;
            }
        }

        void DoLaunch(string id, string cmd)
        {
            if (!TryBeginTask(cmd)) { Reply(id, Bridge.Busy()); return; }
            RefreshDefinitions();   // at most every 12 h; used from the next pre-launch scan (this one does not wait for it)
            // --windowed was given on the command line: every start of this run is the 1600x900 one, as the launcher's
            // -Windowed switch made its own play button windowed (ps1:1868)
            bool windowed = cmd == "launchWindowed" || plan.Windowed;
            Task.Run(() => launcher.Launch(windowed, p => Post(() => OnScanProgress("prelaunch", p)))).ContinueWith(t => Post(() =>
            {
                EndTask();
                taskbar.ClearProgress();
                LaunchOutcome o = t.Status == TaskStatus.RanToCompletion && t.Result != null
                    ? t.Result
                    : new LaunchOutcome { Error = S.T(ctx.Lang, "err", t.Exception != null ? t.Exception.GetBaseException().Message : "?") };
                if (o.Blocked) blockedByAegis = true;
                else if (o.Ok) blockedByAegis = false;
                // R-41: the only red row was "PocketRoles.dll has changed" -> the play button offers 修復 (PORT-MAP 13.3)
                repairMod = o.Blocked && o.RepairMod;
                RefreshStatus();
                if (o.Ok) AfterLaunched();
                Reply(id, o.ToResult(cmd));
            }));
        }

        /// <summary>SPEC 5.4: the game is starting, so the window steps aside into the notification area and comes back
        /// when the game ends. A window that was ALREADY away stays away - a start from the tray, from --autolaunch or
        /// from another start must never put a window in front of anybody.</summary>
        void AfterLaunched()
        {
            if (quitting) return;
            if (!StepAsideForGame(form.Visible)) { restoreAfterGame = false; return; }
            restoreAfterGame = true;
            ctx.Log.Write("the window goes to the notification area while the game runs (it comes back when the game ends)");
            HideToTray();
        }

        /// <summary>The game has ended: the window comes back WITHOUT taking the focus (SW_SHOWNOACTIVATE, SPEC 5.4) -
        /// whatever the person turned to while the game was on stays in front.</summary>
        void ShowAfterGame()
        {
            if (quitting) return;
            if (!uiReady) { ShowWindow(); return; }   // the page is not there yet: it comes up as soon as it is
            shownOnce = true;
            windowShown = true;
            CloseScanCard();
            form.EnsureOpaque();
            try { Native.ShowWindow(form.Handle, Native.SW_SHOWNOACTIVATE); }
            catch (Exception ex) { ctx.Log.Write("window: " + ex.Message); form.Show(); }
            SendEvent("window", new Dictionary<string, object> { ["visible"] = true });
        }

        /// <summary>Play without anybody having pressed the button in the page: --autolaunch, and a second start that
        /// asked this one to play. It goes through the very same launch as the button (the same checks, the same Aegis
        /// scan, the same words in the log); nothing is answered, because nothing asked.</summary>
        void StartPlay(bool windowed, string why)
        {
            if (quitting) return;
            if (longTask != null) { ctx.Log.Write("play (" + why + "): " + longTask + " is running"); return; }
            if (gameRunning) { ctx.Log.Write("play (" + why + "): " + S.T(ctx.Lang, "la_running")); return; }
            if (windowed) plan.Windowed = true;
            if (ctx.Settings.PlaysVanilla) DoLaunchVanilla(null);   // Settings → 起動するゲーム: plain Among Us
            else DoLaunch(null, "launch");
        }

        /// <summary>Another start asked this Client to play (SingleInstance's .Play event, v0.4).</summary>
        void PlayFromOutside(bool windowed)
        {
            if (quitting) return;
            ctx.Log.Write("another start asked for play" + (windowed ? " (--windowed)" : ""));
            StartPlay(windowed, "another start");
        }

        /// <summary>Plain Among Us through Steam (Settings → 起動するゲーム, v0.1.1). One task at a time like the mod launch, but no
        /// definitions refresh, no pre-launch scan and no status change: nothing of the mod copy is involved. The 2 s game
        /// poll reports the game once Steam has started it.</summary>
        void DoLaunchVanilla(string id)
        {
            if (!TryBeginTask("launchVanilla")) { Reply(id, Bridge.Busy()); return; }
            Task.Run(() => launcher.LaunchVanilla()).ContinueWith(t => Post(() =>
            {
                EndTask();
                LaunchOutcome o = t.Status == TaskStatus.RanToCompletion && t.Result != null
                    ? t.Result
                    : new LaunchOutcome { Error = S.T(ctx.Lang, "err", t.Exception != null ? t.Exception.GetBaseException().Message : "?") };
                if (o.Ok) AfterLaunched();   // SPEC 5.4: the window steps aside for plain Among Us too
                Reply(id, o.ToResult("launchVanilla"));
            }));
        }

        // ------------------------------------------------------------------ install / Steam sync / update check (v0.2)
        /// <summary>install: the page's 「インストール」・「修復」・「続きから」 button. The page sends {} (first install),
        /// {resume:true} (carry on after a failed step), {handoff:"steam"} (wait while Steam installs Among Us) or
        /// {repair:"mod"}; when it sends nothing the app decides from the status it last sent the page, so the R state
        /// (PORT-MAP 13.3) repairs instead of installing from the beginning.</summary>
        void DoInstall(string id, Dictionary<string, object> args)
        {
            string mode = InstallMode(args, lastStatus);
            DoTask(id, "install", i => i.Install(mode));
        }

        /// <summary>Which <see cref="Installer.Install"/> mode an install invoke means.</summary>
        internal static string InstallMode(Dictionary<string, object> args, LaunchStatus status)
        {
            if (Json.Str(args, "handoff") == "steam") return "steam";
            if (Json.Bool(args, "resume")) return "resume";
            string repair = Json.Str(args, "repair");
            if (repair == null && status != null && status.PState == "repair") repair = status.Repair;
            if (repair == "mod") return "repairMod";
            if (repair != null) return "repair";
            return "";
        }

        /// <summary>One long Installer task (install / syncSteam / checkUpdate): off the UI thread, one at a time, progress
        /// to the page and the taskbar button, and the status redrawn when it ends (the copy on disk has changed).</summary>
        void DoTask(string id, string cmd, Func<Installer, TaskOutcome> body)
        {
            if (!TryBeginTask(cmd)) { Reply(id, Bridge.Busy()); return; }
            steamPicked = false;
            var installer = ctx.NewInstaller(p => Post(() => OnTaskProgress(p)));
            installer.Cancelled = () => quitting;
            installer.HandoffEnded = () => steamPicked;
            Task.Run(() => body(installer)).ContinueWith(t => Post(() =>
            {
                EndTask();
                taskbar.ClearProgress();
                TaskOutcome o = t.Status == TaskStatus.RanToCompletion && t.Result != null
                    ? t.Result
                    : TaskOutcome.Bad(S.T(ctx.Lang, "err", t.Exception != null ? t.Exception.GetBaseException().Message : "?"));
                if (t.IsFaulted) ctx.Log.Write(cmd + ": " + t.Exception.GetBaseException());
                if (o.Ok) { repairMod = false; blockedByAegis = false; }
                RefreshStatus();
                Reply(id, o.ToResult());
            }));
        }

        /// <summary>An install / update step: the page's progress card (host:progress) and the taskbar button.</summary>
        void OnTaskProgress(TaskProgress p)
        {
            SendEvent("progress", p.ToData());
            if (p.Waiting != null) { taskbar.ClearProgress(); return; }
            if (p.Total > 0) taskbar.SetProgress(TaskbarProgress.Normal, (ulong)Math.Max(0, p.Bytes), (ulong)p.Total);
            else taskbar.SetProgress(TaskbarProgress.Normal, (ulong)Math.Max(0, p.Step), (ulong)Math.Max(1, p.Of));
        }

        /// <summary>pickSteam: the viewer points at Steam's Among Us themselves (the launcher's folder dialog). Not gated by
        /// a running task on purpose: an install waiting for Steam ends its wait as soon as a folder is picked.</summary>
        Dictionary<string, object> PickSteam()
        {
            string picked;
            using (var dlg = new FolderBrowserDialog { Description = S.T(ctx.Lang, "in_pick"), ShowNewFolderButton = false })
            {
                if (dlg.ShowDialog(form) != DialogResult.OK) return Bridge.Fail("");
                picked = dlg.SelectedPath;
            }
            // Installer.CheckPickedSteam: it must hold Among Us.exe AND not be the mod's own copy. Picking the mod copy
            // as "Steam's Among Us" would make every later copy step say "already the same version" and no update would
            // ever be offered again (v0.3 review: the check existed but nothing called it).
            var installer = ctx.NewInstaller(null);
            if (installer.CheckPickedSteam(picked) == null)
                return NotFound("Among Us.exe", picked);
            installer.RememberSteam(picked);
            steamPicked = true;   // an install waiting for Steam carries on with this folder
            RefreshStatus();
            return Bridge.Ok(new Dictionary<string, object> { ["path"] = picked });
        }

        // ------------------------------------------------------------------ the profile picture (v1.3)
        /// <summary>profile.pickImage: 画像を選ぶ窓（OpenFileDialog）を出し、選ばれたら src\Core\ProfileImage.Import を pool スレッドで
        /// 回して、写し（DataDir\profile\avatar.png、256×256 の PNG）を data: の URL で返す。キャンセルは {ok:true, data:{cancelled:true}}
        /// （error が空の Fail にすると画面は「失敗しました」と言う）。断りは ProfileImage の日本語文で {ok:false, error}。
        /// 長い処理の busy には入れない（pickSteam と同じ理由）。二重に開かないのは avatarBusy。
        /// 選んだ元の画像のパスは、アカウント名が入るので、ログにも返事にも書かない。</summary>
        void PickAvatarImage(string id)
        {
            if (avatarBusy) { Reply(id, Bridge.Busy()); return; }
            avatarBusy = true;   // 選ぶ窓が出ている間も（モーダルの間にもう一度来た invoke は busy）
            string picked;
            try
            {
                using (var dlg = new OpenFileDialog
                {
                    Title = S.T(ctx.Lang, "av_pick"),
                    Filter = S.T(ctx.Lang, "av_filter"),
                    CheckFileExists = true,
                    Multiselect = false,
                    DereferenceLinks = true,
                })
                {
                    if (dlg.ShowDialog(form) != DialogResult.OK)
                    {
                        avatarBusy = false;
                        Reply(id, Bridge.Ok(new Dictionary<string, object> { ["cancelled"] = true }));
                        return;
                    }
                    picked = dlg.FileName;
                }
            }
            catch (Exception ex)
            {
                avatarBusy = false;
                ctx.Log.Write("avatar: dialog: " + ex.Message);
                Reply(id, Bridge.Fail(S.T(ctx.Lang, "err", ex.Message)));
                return;
            }
            Task.Run(() => ProfileImage.Import(picked, ctx.AvatarPath, ctx.Lang)).ContinueWith(t => Post(() =>
            {
                avatarBusy = false;
                if (t.IsFaulted || t.Status != TaskStatus.RanToCompletion || t.Result == null)
                {
                    string why = t.Exception != null ? t.Exception.GetBaseException().Message : "?";
                    ctx.Log.Write("avatar: " + Mask.Home(why, Environment.GetEnvironmentVariable("USERPROFILE")));
                    Reply(id, Bridge.Fail(S.T(ctx.Lang, "av_unreadable")));
                    return;
                }
                var res = t.Result;
                if (!res.Ok)
                {
                    ctx.Log.Write("avatar: refused (" + res.Reason + ")");
                    Reply(id, Bridge.Fail(res.Error));
                    return;
                }
                ctx.Log.Write(S.T(ctx.Lang, "av_set", res.Width, res.Height));
                Reply(id, Bridge.Ok(new Dictionary<string, object> { ["avatar"] = res.DataUrl }));
            }));
        }

        /// <summary>profile.clearImage: 「画像をやめて元の絵に戻す」。写しを消すだけ（無くても ok）。答えは {ok:true, data:{avatar:null}}。</summary>
        Dictionary<string, object> ClearAvatarImage()
        {
            string error;
            if (!ProfileImage.Delete(ctx.AvatarPath, out error))
            {
                ctx.Log.Write("avatar: delete failed (" + error + ")");
                return Bridge.Fail(S.T(ctx.Lang, "av_write", error));
            }
            ctx.Log.Write(S.T(ctx.Lang, "av_cleared"));
            return Bridge.Ok(new Dictionary<string, object> { ["avatar"] = null });
        }

        /// <summary>profile.set {name, avatar:0-5}: settings.json の profileName / profileAvatar（ClientSettings.SetProfile）。
        /// 同じ値なら書かない（SetAccent と同じ）。0〜5 でない avatar は "invalid value"。</summary>
        Dictionary<string, object> SetProfile(Dictionary<string, object> args)
        {
            string beforeName = ctx.Settings.ProfileName;
            int beforeAv = ctx.Settings.ProfileAvatar;
            object avatarRaw = args != null && args.ContainsKey("avatar") ? args["avatar"] : null;
            if (!ctx.Settings.SetProfile(Json.Str(args, "name"), avatarRaw)) return Bridge.Fail("invalid value");
            if (ctx.Settings.ProfileName == beforeName && ctx.Settings.ProfileAvatar == beforeAv) return Bridge.Ok();
            var saved = SaveSettings();
            if (!(saved["ok"] is bool ok && ok)) { ctx.Settings.SetProfile(beforeName, beforeAv); return saved; }
            // 名前そのものはログに書かない（ログの窓は配信中にも出る）
            ctx.Log.Write("profile: avatar " + ctx.Settings.ProfileAvatar + ", name " + (ctx.Settings.ProfileName.Length > 0 ? ctx.Settings.ProfileName.Length + " chars" : "none"));
            return saved;
        }

        // ------------------------------------------------------------------ report zip / one player's evidence (v0.3)
        /// <summary>makeReport / exportOne: a long task like an install (both read every log on disk), with the same
        /// one-at-a-time rule. The answer carries the file name and what went in, which the page's panel shows.</summary>
        void DoReport(string id, string cmd, Func<ReportBuilder, ReportResult> body)
        {
            if (!TryBeginTask(cmd)) { Reply(id, Bridge.Busy()); return; }
            var builder = ctx.NewReportBuilder();
            Task.Run(() => body(builder)).ContinueWith(t => Post(() =>
            {
                EndTask();
                ReportResult o = t.Status == TaskStatus.RanToCompletion && t.Result != null
                    ? t.Result
                    : new ReportResult { Error = S.T(ctx.Lang, "err", t.Exception != null ? t.Exception.GetBaseException().Message : "?") };
                if (t.IsFaulted) ctx.Log.Write(cmd + ": " + t.Exception.GetBaseException());
                if (!o.Ok) { Reply(id, Bridge.Fail(o.Error)); return; }
                var data = new Dictionary<string, object>
                {
                    ["title"] = S.T(ctx.Lang, cmd == "makeReport" ? "rp_title" : "ex_title"),
                    ["open"] = S.T(ctx.Lang, "rp_open"),
                    ["close"] = S.T(ctx.Lang, "rp_close"),
                    ["name"] = o.Name,
                    ["records"] = o.Records,
                    ["withBacking"] = o.WithBacking,
                    // the words the launcher's dialog showed, for the page's panel
                    ["text"] = cmd == "makeReport"
                        ? S.T(ctx.Lang, "rp_msg", "\n", o.Name, AppInfo.MailBug, AppInfo.MailRequest)
                        : S.T(ctx.Lang, "ex_msg", "\n", o.Name, o.Records, AppInfo.MailHost),
                    ["evidence"] = cmd == "makeReport"
                        ? (o.Records > 0 ? S.T(ctx.Lang, "rp_ev", o.Records) : S.T(ctx.Lang, "rp_ev_none"))
                        : S.T(ctx.Lang, "ex_warn", AppInfo.MailHost),
                    ["autoDelete"] = S.T(ctx.Lang, "rp_autodelete"),
                };
                // the one-player zip: the ids, so the host sees whose zip it is before attaching it. The path of the zip
                // is not sent to the page - it opens the folder instead (the page never gets a path it could show off).
                if (cmd == "exportOne") data["ids"] = o.Ids;
                Reply(id, Bridge.Ok(data));
            }));
        }

        // ------------------------------------------------------------------ the log page / the developer tools (v0.4)
        /// <summary>showLog: the app's own log (client.log), as a page inside the window instead of the launcher's log
        /// box. Never busy-gated - watching a long task go by is what it is for. The Windows account name is taken out
        /// of the path and out of every line (Mask.Home): this page is on screen while the owner streams.</summary>
        Dictionary<string, object> ShowLog()
        {
            var tail = ctx.Log.Tail();
            string home = Environment.GetEnvironmentVariable("USERPROFILE");
            var lines = new string[tail.Lines.Length];
            for (int i = 0; i < lines.Length; i++) lines[i] = Mask.Home(tail.Lines[i], home);
            var data = new Dictionary<string, object>
            {
                ["title"] = S.T(ctx.Lang, "lg_log_title"),
                ["sub"] = S.T(ctx.Lang, "lg_log_sub"),
                ["none"] = S.T(ctx.Lang, "lg_log_none"),
                ["close"] = S.T(ctx.Lang, "rp_close"),
                ["path"] = Mask.Home(ctx.Log.Path ?? "", home),
                ["lines"] = lines,
                ["truncated"] = tail.Truncated,
                ["bytes"] = tail.Bytes,
            };
            if (tail.Truncated) data["more"] = S.T(ctx.Lang, "lg_log_more");
            // the size line comes from the last walk RefreshLogSize did on a pool thread. Counting it here would put a
            // walk of the whole logs folder on the UI thread every two seconds, for a number that is already in hand.
            var info = lastLogInfo;
            if (info != null) data["logs"] = LogData(info);
            else RefreshLogSize();   // nothing walked yet (the page was opened very early): ask for it, off this thread
            return Bridge.Ok(data);
        }

        /// <summary>rebuild / devUpdate: the author's own two buttons. One long task at a time like an install, with the
        /// same progress card, and the status redrawn afterwards (the DLL and the game copy have changed). Friend mode
        /// never gets here - the rows are hidden - but an invoke that arrives anyway is answered, not acted on.</summary>
        void DoDev(string id, string cmd, Func<DevBuild, TaskOutcome> body)
        {
            if (!ctx.DevMode) { Reply(id, Bridge.Fail(S.T(ctx.Lang, "dev_only"))); return; }
            if (!TryBeginTask(cmd)) { Reply(id, Bridge.Busy()); return; }
            var dev = ctx.NewDevBuild(p => Post(() => OnTaskProgress(p)));
            Task.Run(() => body(dev)).ContinueWith(t => Post(() =>
            {
                EndTask();
                taskbar.ClearProgress();
                TaskOutcome o = t.Status == TaskStatus.RanToCompletion && t.Result != null
                    ? t.Result
                    : TaskOutcome.Bad(S.T(ctx.Lang, "err", t.Exception != null ? t.Exception.GetBaseException().Message : "?"));
                if (t.IsFaulted) ctx.Log.Write(cmd + ": " + t.Exception.GetBaseException());
                if (o.Ok) { repairMod = false; blockedByAegis = false; }
                RefreshStatus();
                Reply(id, o.ToResult());
            }));
        }

        /// <summary>mailBug / mailRequest: the viewer's mail app with the subject and the template filled in. The app
        /// sends nothing - the person attaches the zip and presses send (ps1 Open-Mailto).</summary>
        Dictionary<string, object> OpenMail(string to, string subjectKey, string zipName)
        {
            string ver = GameVersion.DllVersionString(ctx.Paths.DllPath);
            if (string.IsNullOrEmpty(ver)) ver = AppInfo.Version;
            string name = string.IsNullOrEmpty(zipName) ? "PocketRoles-report-....zip" : Path.GetFileName(zipName);
            ShellOpen.MailTo(to, S.T(ctx.Lang, subjectKey, ver), S.T(ctx.Lang, "rp_body", name, "\r\n"));
            return Bridge.Ok();
        }

        // ------------------------------------------------------------------ shortcut / uninstall (v0.3)
        /// <summary>shortcut.create {where: "desktop" | "startmenu"}: the ONLY way this app ever writes a shortcut, and
        /// it is reached only from the button in Settings that says so (SignPath).</summary>
        Dictionary<string, object> MakeShortcut(Dictionary<string, object> args)
        {
            string where = Json.Str(args, "where") ?? "desktop";
            ShortcutPlace place;
            if (where == "desktop") place = ShortcutPlace.Desktop;
            else if (where == "startmenu") place = ShortcutPlace.StartMenu;
            else return Bridge.Fail("unknown place");
            try
            {
                string lnk = Shortcuts.Create(place, AppInfo.ExePath());
                ctx.Log.Write("shortcut: " + lnk);
                return Bridge.Ok(new Dictionary<string, object> { ["where"] = where, ["path"] = lnk });
            }
            catch (Exception ex)
            {
                ctx.Log.Write("shortcut: " + ex.Message);
                return Bridge.Fail(S.T(ctx.Lang, "err", ex.Message));
            }
        }

        /// <summary>uninstall {confirm:true, modCopy:bool}: only after the page's own confirm, which names everything
        /// that goes. Without confirm it answers with the plan, so the page can show it.
        ///
        /// Nothing is deleted here. The app holds its own data folder open - the WebView2 profile and client.log - so a
        /// delete from inside the running app always stops half-way. The answer is remembered and the app closes;
        /// <see cref="Program"/> runs the uninstall after the message loop has ended and WebView2 is disposed.</summary>
        void DoUninstall(string id, Dictionary<string, object> args)
        {
            var un = ctx.NewUninstaller();
            bool modCopy = Json.Bool(args, "modCopy");
            if (!Json.Bool(args, "confirm"))
            {
                var plan = un.Plan(modCopy);
                Reply(id, Bridge.Ok(new Dictionary<string, object>
                {
                    ["folders"] = plan.Folders.ToArray(),
                    ["files"] = plan.Files.ToArray(),
                    ["registry"] = plan.Registry.ToArray(),
                    ["modCopy"] = plan.ModCopy,
                    ["kept"] = plan.Kept.ToArray(),
                    ["title"] = S.T(ctx.Lang, "un_title"),
                    ["text"] = S.T(ctx.Lang, "un_confirm", ctx.DataDir),
                    ["keepsModCopy"] = S.T(ctx.Lang, "un_keep_mod"),
                    ["delModLabel"] = S.T(ctx.Lang, "un_del_mod"),
                    ["yes"] = S.T(ctx.Lang, "un_yes"),
                }));
                return;
            }
            // the mod copy is the running game's own folder: the install, the Steam sync and the update check all refuse
            // while Among Us runs, and so does this (deleting a mapped GameAssembly.dll leaves a mutilated 1 GB folder)
            if (Processes.GameRunning())
            {
                ctx.Log.Write(S.T(ctx.Lang, "game_running"));
                Reply(id, Bridge.Fail(S.T(ctx.Lang, "game_running")));
                return;
            }
            if (!TryBeginTask("uninstall")) { Reply(id, Bridge.Busy()); return; }
            var planned = un.Plan(modCopy);
            PendingUninstall = new UninstallRequest { ModCopy = modCopy };
            ctx.Log.Write("uninstall: asked for (mod copy " + (modCopy ? "too" : "kept") + "); it runs after the window closes");
            Reply(id, Bridge.Ok(new Dictionary<string, object>
            {
                ["removed"] = new string[0],
                ["kept"] = planned.Kept.ToArray(),
                ["restart"] = planned.ProgramFolder != null,
                ["text"] = S.T(ctx.Lang, "un_closing"),
                ["leftText"] = planned.ProgramFolder == null ? null
                    : S.T(ctx.Lang, planned.ProgramFolderIsTheirs ? "un_leftfolder" : "un_selffolder", planned.ProgramFolder),
            }));
            EndTask();
            Post(() => Shutdown());
        }

        /// <summary>Set when the viewer confirmed the uninstall: <see cref="Program"/> does it once this app is gone.</summary>
        public UninstallRequest PendingUninstall { get; private set; }

        /// <summary>aegis.rescan (tray definitions) / aegis.scanOnly (fresh definitions) and the tray's "scan again".</summary>
        void RunScan(bool fresh, Action<Dictionary<string, object>> done)
        {
            string cmd = fresh ? "aegis.scanOnly" : "aegis.rescan";
            if (!aegis.IsPorted) { done(Bridge.Unsupported(cmd)); return; }
            if (!TryBeginTask(cmd)) { done(Bridge.Busy()); return; }
            string task = fresh ? "scanOnly" : "rescan";
            Action<ScanProgress> progress = p => Post(() => OnScanProgress(task, p));
            Task.Run(() => fresh ? aegis.ScanOnly(progress) : aegis.Rescan(progress)).ContinueWith(t => Post(() =>
            {
                EndTask();
                taskbar.ClearProgress();
                if (t.Status != TaskStatus.RanToCompletion || t.Result == null)
                {
                    ctx.Log.Write(cmd + " failed: " + (t.Exception != null ? t.Exception.GetBaseException().Message : "?"));
                    done(Bridge.Fail(S.T(ctx.Lang, "err", t.Exception != null ? t.Exception.GetBaseException().Message : "?")));
                    return;
                }
                var sum = t.Result;
                if (sum.NotAvailable) { done(Bridge.Unsupported(cmd)); return; }
                if (!string.IsNullOrEmpty(sum.Error)) { done(Bridge.Fail(sum.Error)); return; }
                if (sum.Serious == 0 && blockedByAegis) { blockedByAegis = false; RefreshStatus(); }
                done(Bridge.Ok(new Dictionary<string, object> { ["warnings"] = sum.Warnings, ["serious"] = sum.Serious }));
            }));
        }

        Dictionary<string, object> NotFound(string what, string path)
        {
            string msg = S.T(ctx.Lang, "op_notfound", what, path);
            ctx.Log.Write(msg);
            return Bridge.Fail(msg);
        }

        /// <summary>Open-File: the file in the viewer's text editor (the app itself never reads it), or
        /// "&lt;what&gt; not found: &lt;path&gt;". Through ShellOpen, which refuses anything that is a program.</summary>
        Dictionary<string, object> OpenInNotepad(string path, string what)
        {
            if (!GameFolders.PathExists(path)) return NotFound(what, path);
            ShellOpen.TextFile(path);
            return Bridge.Ok();
        }

        /// <summary>The folder in the viewer's file manager (openModFolder does not check that it exists, like the launcher).</summary>
        static Dictionary<string, object> OpenInExplorer(string folder)
        {
            ShellOpen.Folder(folder);
            return Bridge.Ok();
        }

        /// <summary>The Client's accent colour: kept in settings.json, and the answer carries the colours the page is to
        /// paint with. A colour that cannot be read is never refused and never taken as it is - it is moved as little
        /// as it must be, and the person is told so in their own language. The page writes that sentence itself, from
        /// the flags below, so it can show the two colours AS COLOURS rather than as hex codes nobody typed.
        /// The colour that is already set writes nothing and logs nothing: pressing the same swatch twice, or the page
        /// sending the value it already sent, must not rewrite settings.json.</summary>
        Dictionary<string, object> SetAccent(string value)
        {
            string before = ctx.Settings.Accent;
            if (!ctx.Settings.SetAccent(value)) return Bridge.Fail("invalid value");
            bool changed = ctx.Settings.Accent != before;
            if (changed)
            {
                var saved = SaveSettings();
                if (!(saved["ok"] is bool ok && ok)) { ctx.Settings.SetAccent(before); return saved; }
            }
            var p = AccentColor.Derive(ctx.Settings.Accent);
            if (changed)
            {
                ctx.Log.Write("accent: " + ctx.Settings.Accent + (p.Adjusted ? " -> " + AccentColor.Hex(p.Fill) : ""));
                if (p.HueMoved) ctx.Log.Write(S.T(ctx.Lang, "ac_red"));
                else if (p.LightMoved) ctx.Log.Write(S.T(ctx.Lang, "ac_fixed", p.Chosen, AccentColor.Hex(p.Fill)));
                // the first-paint script must say the new colour too: WebView2 reloads the page by itself when its
                // render process dies, and the script from start-up would paint the old colour over the whole window
                var again = form.SetBootScriptAsync(AccentColor.BootScript(ctx.Settings.Accent));
                if (again.IsFaulted) ctx.Log.Write("accent: " + again.Exception.GetBaseException().Message);
            }
            return Bridge.Ok(AccentData(p));
        }

        /// <summary>What the page needs in order to paint the colour and to say what was done to it.</summary>
        Dictionary<string, object> AccentData(AccentColor.Palette p) => new Dictionary<string, object>
        {
            ["accent"] = ctx.Settings.Accent,
            ["vars"] = AccentColor.Vars(ctx.Settings.Accent),
            ["adjusted"] = p.Adjusted,
            ["hueMoved"] = p.HueMoved,
            ["lightMoved"] = p.LightMoved,
            ["lighter"] = p.Lighter,
            ["chosen"] = p.Chosen,
            ["fill"] = AccentColor.Hex(p.Fill),
        };

        /// <summary>settings.set: close and (v0.1.1) startGame go to settings.json (Bridge.SetSetting); (v0.3) autostart
        /// is the one Run value, kept in the registry rather than in settings.json so the switch always shows what
        /// Windows will really do (SignPath: the viewer turns it on themselves, and turning it off removes it);
        /// (v0.3.1) accent goes to settings.json, and its answer carries the colours to paint with.</summary>
        Dictionary<string, object> SetSetting(Dictionary<string, object> args)
        {
            string key = Json.Str(args, "key");
            if (key == "accent") return SetAccent(Json.Str(args, "value"));
            if (key == "devBuild") return SetDevBuild(args);   // v1.1: saved, then the app starts itself again
            if (key == "autostart")
            {
                // v1.1: the page's toggle sends a real JSON true / false, not a word (Json.Str gave null for it, and the
                // switch was answered "invalid value"); OnOffValue takes both
                object rawOn;
                bool? on = Bridge.OnOffValue(args != null && args.TryGetValue("value", out rawOn) ? rawOn : null);
                if (!on.HasValue) return Bridge.Fail("invalid value");
                if (!AutoStart.Set(on.Value, AppInfo.ExePath())) return Bridge.Fail(S.T(ctx.Lang, "err", "registry"));
                ctx.Log.Write("start with Windows: " + (on.Value ? "on" : "off"));
                return Bridge.Ok(new Dictionary<string, object>
                {
                    ["autostart"] = AutoStart.IsOn(),
                    ["text"] = S.T(ctx.Lang, on.Value ? "as_on" : "as_off"),
                });
            }
            var r = Bridge.SetSetting(ctx.Settings, args, SaveSettings);
            if (r["ok"] is bool ok && ok)
            {
                if (key == "startGame") ctx.Log.Write("start game: " + ctx.Settings.StartGame);
                else if (key == "startScan") ctx.Log.Write("start scan card: " + (ctx.Settings.StartScan ? "on" : "off"));   // v1.1
                else if (key == "sound") ctx.Log.Write("sounds: " + (ctx.Settings.Sound ? "on" : "off"));                   // v1.2
                else if (key == "volume") ctx.Log.Write("volume: " + ctx.Settings.Volume);                                    // v1.2
            }
            return r;
        }

        /// <summary>v1.1: the author's switch, Settings → PocketRoles → 開発 (the owner, 2026-09-24 「ランチャーじゃなくて
        /// クライアントからがいいな。開発用もそこで何とかしてよ」). The page draws it only where the app found the working copy
        /// of the mod (the "shell" event's devFolder); an invoke that arrives anyway on a PC without one is refused in
        /// words, never acted on. The mode is decided once at start (ClientContext.Detect, src\Core\DevSource.cs), so
        /// the switch is saved and the app closes and starts itself again (Program.RestartForDevSwitch) - not while a
        /// long task runs (the copy is being written) and not while Among Us runs (Aegis is watching it). The reply
        /// carries the setting as it now stands, so the page's switch is put back on a refusal.</summary>
        Dictionary<string, object> SetDevBuild(Dictionary<string, object> args)
        {
            object rawOn;
            bool? on = Bridge.OnOffValue(args != null && args.TryGetValue("value", out rawOn) ? rawOn : null);
            if (!on.HasValue) return DevSwitchAnswer(Bridge.Fail("invalid value"));
            string refusal = DevSwitchRefusal(on.Value, ctx.DevFolder != null, Processes.GameRunning());
            if (refusal != null) return DevSwitchAnswer(Bridge.Fail(S.T(ctx.Lang, refusal)));
            // the same value again (the page repeating what it was told): nothing to save, nothing to restart
            if (on.Value == ctx.Settings.DevBuild) return DevSwitchAnswer(Bridge.Ok());
            if (!TryBeginTask("devSwitch")) return DevSwitchAnswer(Bridge.Busy());
            bool before = ctx.Settings.DevBuild;
            ctx.Settings.SetDevBuild(on.Value);
            var saved = SaveSettings();
            if (!(saved["ok"] is bool ok && ok)) { ctx.Settings.SetDevBuild(before); EndTask(); return DevSwitchAnswer(saved); }
            ctx.Log.Write("developer switch: " + (on.Value ? "on (" + ctx.DevFolder + ")" : "off") + "; the app closes and starts again");
            PendingRestart = true;
            EndTask();
            Post(() => Shutdown());
            var r = Bridge.Ok(new Dictionary<string, object> { ["restart"] = true, ["text"] = S.T(ctx.Lang, on.Value ? "dev_switch_on" : "dev_switch_off") });
            return DevSwitchAnswer(r);
        }

        /// <summary>Why the switch cannot be flipped right now, as a string key, or null when it can. Turning it ON needs
        /// the working copy; turning it OFF never does (a folder that went away must not lock the app in developer mode).</summary>
        internal static string DevSwitchRefusal(bool on, bool folderFound, bool gameRunning)
        {
            if (on && !folderFound) return "dev_nofolder";
            if (gameRunning) return "game_running";
            return null;
        }

        /// <summary>Every answer to the switch names the key and the setting as it stands, so the page can put its
        /// switch back after a refusal (the prototype flips it before the app has answered).</summary>
        Dictionary<string, object> DevSwitchAnswer(Dictionary<string, object> r)
        {
            object existing;
            var data = r.TryGetValue("data", out existing) ? existing as Dictionary<string, object> : null;
            if (data == null) { data = new Dictionary<string, object>(); r["data"] = data; }
            data["key"] = "devBuild";
            data["devBuild"] = ctx.Settings.DevBuild;
            return r;
        }

        /// <summary>Set when the developer switch was flipped: <see cref="Program"/> starts the app again once this one is gone.</summary>
        public bool PendingRestart { get; private set; }

        Dictionary<string, object> SaveSettings()
        {
            try { ctx.Settings.Save(ctx.SettingsPath); return Bridge.Ok(); }
            catch (Exception ex)
            {
                ctx.Log.Write("settings.json: " + ex.Message);
                return Bridge.Fail(S.T(ctx.Lang, "err", ex.Message));
            }
        }

        /// <summary>setLang {lang: auto|ja|zh|en}: stored in settings.json (not launcher-state.json), applied everywhere now.</summary>
        Dictionary<string, object> SetLanguage(Dictionary<string, object> args)
        {
            string pref = Lang.PrefFromUi(Json.Str(args, "lang"));
            if (pref == null) return Bridge.Fail("invalid language");
            ctx.ChooseLanguage(pref);   // --language no longer wins after this (like the launcher's combo box over -Language)
            var saved = SaveSettings();
            ApplyLanguage();
            if (!(saved["ok"] is bool ok && ok)) return saved;
            return Bridge.Ok(new Dictionary<string, object> { ["lang"] = Lang.ToUi(ctx.Lang), ["pref"] = Lang.PrefToUi(ctx.Settings.Lang) });
        }

        void ApplyLanguage()
        {
            ctx.ResolveLanguage();
            tray.SetLanguage(ctx.Lang);
            try { aegis.SetLanguage(ctx.Lang); } catch (Exception ex) { ctx.Log.Write("Aegis language: " + ex.Message); }
            var snap = aegis.GetSnapshot();
            tray.Update(ctx.Lang, snap);
            badgeKind = "?";   // redraw the badge with the new description
            UpdateBadge(snap);
            SendLang();
            RefreshStatus();
        }

        // ------------------------------------------------------------------ window, close, quit
        void OnCloseButton()
        {
            if (quitting || closing) return;
            if (ctx.Settings.Close == ClientSettings.CloseQuits) RequestQuit();
            else HideToTray();
        }

        /// <summary>v1.2: 優しく閉じる。窓を 140 ms 薄くしてから隠す（MainForm.FadeOut）。ページには "window" {closing:true, ms} を
        /// 送り、ページはその間ほんの少し縮む（host-v01.js）。アプリはページの返事を待たない: ページが固まっていても窓は消える。
        /// 演出が無い時（窓が見えていない・「アニメーション効果」OFF）は ms が 0 で、then はもう呼ばれている。
        /// AfterLaunched（ゲーム開始で窓がよける）もここを通るので同じ演出になる。</summary>
        void HideToTray()
        {
            if (closing) return;
            closing = true;
            int ms = form.FadeOut(() =>
            {
                closing = false;
                form.Hide();
                SendEvent("window", new Dictionary<string, object> { ["visible"] = false });
                if (!ctx.Settings.TrayHintShown) ShowTrayHint();
            });
            if (ms > 0) SendEvent("window", new Dictionary<string, object> { ["closing"] = true, ["ms"] = ms });
        }

        /// <summary>v1.2: 薄くなっている最中に窓をもう一度求められて、MainForm が演出をやめた時（MainForm.EnsureOpaque）。
        /// 窓は出たままなので「閉じかけ」の印を戻し、ページの「ほんの少し縮む」も外させる（"window" {visible:true} で外れる）。
        /// 印を戻さないと closing が立ったままになり、**次に ✕ を押しても何も起きなくなる**。
        /// 終了の最中はここへ来ない（ShowWindow が quitFading で先に返すので EnsureOpaque まで進まない）。</summary>
        void OnFadeCancelled()
        {
            if (!closing) return;
            closing = false;
            ctx.Log.Write("close fade cancelled: the window was asked for again");
            SendEvent("window", new Dictionary<string, object> { ["visible"] = true });
        }

        /// <summary>The first time the window goes to the tray, once ever (SPEC 5.4): the app is still running, and how to quit
        /// it - so a closed window never looks like something left running in secret.</summary>
        void ShowTrayHint()
        {
            ctx.Settings.TrayHintShown = true;
            SaveSettings();   // a failure is logged; the notice still shows (and shows again next time)
            ctx.Log.Write("tray: first hide, notice shown");
            try { AegisToast.ShowNotice(AppInfo.Name, S.T(ctx.Lang, "tray.stillRunning"), ctx.Lang); }
            catch (Exception ex) { ctx.Log.Write("tray notice: " + ex.Message); }
        }

        void ShowWindow()
        {
            // v1.2: 終わりかけ（140 ms 薄くなっている最中に「終了」が押された後）も、もう窓は出さない。
            //   quitting が立つのは Shutdown に入ってからなので、それだけでは演出中をすり抜ける。
            if (quitting || quitFading) return;
            // started with --tray or --autolaunch: nothing is on screen and the page has not been built yet. Whoever
            // asked (the tray menu, a second start) gets the window as soon as it can be drawn.
            if (!uiReady)
            {
                showWhenReady = true;
                if (!webStarted) { var building = StartWebView(); }   // it reports its own failure
                return;
            }
            shownOnce = true;
            windowShown = true;
            CloseScanCard();   // the panel shows the scan from here on
            form.EnsureOpaque();
            if (!form.Visible)
            {
                form.Show();
                SendEvent("window", new Dictionary<string, object> { ["visible"] = true });
            }
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
            Native.SetForegroundWindow(form.Handle);
            RefreshDefinitions();
            ShowWaitingStartCard();   // v1.1: a start whose window was asked for before the page was ready
        }

        /// <summary>Aegis downloads its definitions again when the last download began 12 hours ago or more.</summary>
        void RefreshDefinitions()
        {
            try { aegis.RefreshDefinitionsIfStale(); }
            catch (Exception ex) { ctx.Log.Write("Aegis definitions: " + ex.Message); }
        }

        /// <summary>During a game, quitting also stops Aegis's watch: ask first (PORT-MAP 3.13, SPEC 5.4).</summary>
        void RequestQuit()
        {
            if (quitting || quitFading) return;
            if (Processes.GameRunning())
            {
                IWin32Window owner = form.Visible ? form : null;
                var r = MessageBox.Show(owner, S.T(ctx.Lang, "quit.confirm"), AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (r != DialogResult.Yes) return;
            }
            // v1.2: 優しく閉じる。トレイへ行く途中（closing）に「終了」が来たら、薄くなり終わった時にすることが終了に差し替わる
            // （後から来た方が勝つ）。Shutdown は省略引数付きなのでラムダで（method group は Action にならない）。
            quitFading = true;
            closing = true;
            int ms = form.FadeOut(() => Shutdown());
            if (ms > 0) SendEvent("window", new Dictionary<string, object> { ["closing"] = true, ["ms"] = ms });
        }

        void Shutdown(bool closeWindow = true)
        {
            if (quitting) return;
            quitting = true;
            ctx.Log.Write("quit");
            gameTimer.Stop();
            CloseScanCard();
            try { aegis.Stop(); } catch (Exception ex) { ctx.Log.Write("Aegis stop: " + ex.Message); }
            try { aegis.Dispose(); } catch (Exception) { }
            tray.Dispose();
            taskbar.Dispose();
            // let go of "one Client" now (Aegis's mutex and the tray icon are already gone), before WebView2's slow clean-up:
            // a start in this moment is a normal start instead of a signal to this closing window
            instance.ReleaseEarly();
            if (taskLock != null) { taskLock.Dispose(); taskLock = null; }
            form.AllowClose = true;
            if (closeWindow && !form.IsDisposed) form.Close();
            ExitThread();
        }

        // ------------------------------------------------------------------ tray
        public void TrayOpen() => ShowWindow();

        public void TrayPlay()
        {
            // v0.4: while the window is away it STAYS away. Opening it only to put it back a second later (SPEC 5.4)
            // would be a window flashing over whatever is on screen; the scan card shows what Aegis is doing instead.
            if (WindowAway) { StartPlay(plan.Windowed, "tray"); return; }
            ShowWindow();
            // the page runs its own PLAY flow (one invoke): launch, or launchVanilla while Settings → 起動するゲーム is plain Among Us
            SendEvent("nav", new Dictionary<string, object> { ["run"] = "play" });
        }

        public void TrayAegisStatus()
        {
            ShowWindow();
            SendEvent("nav", new Dictionary<string, object> { ["open"] = "d-aegis" });
        }

        /// <summary>Like "play": the window comes up and the page runs its own "scan again" (the Aegis panel opens, the rows
        /// move, then "scan finished" - or the busy / error notice in the page's words). One invoke, sent by the page.</summary>
        public void TrayRescan()
        {
            ShowWindow();
            SendEvent("nav", new Dictionary<string, object> { ["run"] = "aegis.rescan" });
        }

        public void TrayQuit() => RequestQuit();

        public bool TrayCanPlay() => TrayPlayable(longTask != null, gameRunning, ctx.Settings.PlaysVanilla, ctx.DevMode, lastStatus);

        /// <summary>The tray's "play": not during a task or a game; a friend's mod copy that is not installed has nothing to start,
        /// but plain Among Us (Settings → 起動するゲーム) needs no mod copy at all.</summary>
        internal static bool TrayPlayable(bool busy, bool gameRunning, bool vanilla, bool devMode, LaunchStatus status) =>
            !busy && !gameRunning && (vanilla || !(status != null && !devMode && !status.Installed));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gameTimer.Dispose();
                if (!form.IsDisposed) form.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
