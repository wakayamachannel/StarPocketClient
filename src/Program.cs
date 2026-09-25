// StarPocket Client - entry point.
//   "StarPocket Client.exe"                     the app (window + tray + Aegis in one process)
//   "StarPocket Client.exe" --tray              the same app with NO window: the notification area only (Aegis.ps1 -Tray)
//   "StarPocket Client.exe" --autolaunch        no window either: it plays at once ([--windowed]: 1600x900)
//   "StarPocket Client.exe" --action <job>      install | check | report | status, no window, an exit code (ps1 -Action)
//   "StarPocket Client.exe" --scan-only         one Aegis scan on a small card, then it ends (Aegis.ps1 -ScanOnly)
//   "StarPocket Client.exe" --verify-download   fetches the pinned BepInEx zip, checks it, unpacks it in %TEMP% and
//                            [--quiet]          deletes the lot; one sentence in a box. The game folder is never named.
//                                               Exit code 2 is its own: the file came down and was NOT the pinned one.
//   "StarPocket Client.exe" --self-test <dir>   headless checks of the ported logic: no window, no tray, no network,
//                                               writes only inside <dir>; PASS/FAIL lines, exit code 0 / 1
//   "StarPocket Client.exe" --uninstall         what the Apps & Features entry runs: one confirm box, then the
//                            [--quiet]          uninstall. No window, no tray, no WebView2; --quiet asks nothing.
// Anything else is a USAGE ERROR: it says what it did not understand and ends with 1. It never opens the app instead -
// that is how "--uninstall" once did nothing at all (PORT-MAP 15.1).
//
// Start order: the crash log -> what the words mean (Startup.Plan) -> what the app decides (ClientContext) -> the files
// next to the exe -> the WebView2 runtime (its "install it" window holds no mutex) -> one instance -> the app.
// The headless jobs skip the last three: they need no page and no "one Client" rule (SPEC 5.1).
// The uninstall the WINDOW asks for is done here too, after Application.Run has returned and WebView2 is disposed: while
// the app runs it holds its own data folder open, so a delete from inside it always stops half-way (see Uninstaller).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var cli = CommandLine.Parse(args);
            var plan = Startup.Plan(cli);
            if (plan.Mode == StartupMode.SelfTest) return SelfTest.SelfTestRunner.Run(cli.SelfTestDir);

            // first of all: whatever goes wrong from here is written to client.log (never a silent end)
            var log = ClientContext.OpenLog();
            AppDomain.CurrentDomain.UnhandledException += (s, e) => log.Write("fatal: " + e.ExceptionObject);

            if (plan.Mode == StartupMode.Usage) return SayUsage(plan, cli, log);

            // before any window: the taskbar groups the app under its own identity (not PowerShell's)
            try { Native.SetCurrentProcessExplicitAppUserModelID(AppInfo.AppUserModelId); } catch (Exception) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // before the first control is made (the WebView2 window below is one)
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => log.Write("error: " + e.Exception);

            string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            // the Apps & Features entry ("<exe>" --uninstall): nothing of the app is opened, and its own log is used so
            // the lines it writes cannot re-create the data folder it is removing
            if (plan.Mode == StartupMode.Uninstall) return UninstallFromCommandLine(cli, exeDir);

            ClientContext ctx;
            try { ctx = ClientContext.Detect(cli, exeDir, log); }
            catch (Exception ex) { return StartFailed(log.Write, cli.Quiet, ex, ErrorBox); }

            // Terms of Use Article 12(3): the ones that go online say no until the first-run screen has been answered,
            // the same way the page's own commands are refused (Shell\Bridge.BeforeConsent). Startup.NeedsConsentFirst
            // says which, and why each one is or is not in the list.
            if (Startup.NeedsConsentFirst(plan.Mode, plan.Action)
                && !Consent.Load(Consent.PathIn(ctx.DataDir)).Covers(AppInfo.TermsVersion, AppInfo.PrivacyVersion, AppInfo.RulesVersion))
                return SayNeedConsent(ctx);

            // the jobs with no window at all. They are outside the "one Client" rule on purpose (SPEC 5.1): they do
            // their own work, they never hand it to a running window, and their exit code is about THIS process.
            if (plan.Mode == StartupMode.Action) return RunAction(plan, ctx);
            if (plan.Mode == StartupMode.ScanOnly) return RunScanOnly(ctx, exeDir);
            if (plan.Mode == StartupMode.VerifyDownload) return RunVerifyDownload(cli, ctx);

            // the zip extracted whole? (checked before anything loads a WebView2 DLL)
            var missing = PackageFiles.Missing(exeDir);
            if (missing.Count > 0)
            {
                log.Write("files missing next to the exe: " + string.Join(", ", missing));
                return FilesMissing(ctx.Lang);
            }

            // the WebView2 runtime; its "install it" window comes before the single-instance mutex (it holds nothing, so a
            // start after installing is a normal start)
            string wv2, problem;
            var st = CheckRuntime(out wv2, out problem);
            if (st == WebView2Gate.State.BrokenFiles)
            {
                log.Write("WebView2 files next to the exe cannot be loaded: " + problem);
                return FilesMissing(ctx.Lang);
            }
            if (st == WebView2Gate.State.NoRuntime)
            {
                log.Write("WebView2 runtime missing: " + problem);
                if (!WebView2Gate.ShowMissing(ctx.Lang, log.Write)) return 0;
                st = CheckRuntime(out wv2, out problem);
                if (st != WebView2Gate.State.Ok) return 0;
            }

            // listen: this IS the process that answers a second start - it raises its window, and it plays
            using (var instance = SingleInstance.Acquire(listen: true))
            {
                if (!instance.IsFirst) return SecondInstance(plan, ctx);
                log.RotateAtStart();
                log.Write(AppInfo.Name + " " + AppInfo.Version + " start (" + (ctx.DevMode ? "developer" + (ctx.DevFromSetting ? " (the switch in Settings)" : "") + ": " + ctx.Src : "friend") + ")");
                if (ctx.DevFolder != null && !ctx.DevMode) log.Write("developer folder found, not in use: " + ctx.DevFolder);
                log.Write("game copy: " + ctx.Paths.Modded);
                log.Write("Steam: " + (ctx.SteamDir ?? "not found"));
                log.Write("WebView2 runtime " + wv2);

                string uiDir = Path.Combine(exeDir, AppInfo.UiFolderName);
                bool devTools = false;
#if DEBUG
                devTools = cli.DevTools;
#endif
                UninstallRequest asked;
                bool restart;
                using (var app = new ClientApp(ctx, instance, uiDir, devTools, plan))
                {
                    Application.Run(app);
                    asked = app.PendingUninstall;   // read before Dispose closes WebView2
                    restart = app.PendingRestart;
                }
                // now: no window, no WebView2 control, no tray. The browser process lets go of the profile folder a
                // moment later, so the deletes are tried a few times before anything is called a failure.
                if (asked != null) return FinishUninstall(ctx, asked);
                if (restart) return RestartForDevSwitch(ctx);
                return 0;
            }
        }

        /// <summary>The app could not work out where anything is (ClientContext.Detect threw). One line in the log,
        /// one box - and NO box at all when --quiet was asked for.
        ///
        /// <para>That last part was missing until the v0.5 review, and it was a promise this build had just made:
        /// docs\BEPINEX-PIN.md step 9 tells anyone running "--verify-download" automatically to add --quiet because
        /// then "no window opens". This runs before the --verify-download branch does, so a start that failed here
        /// opened a box no automated run would ever press OK on, and the process would sit there for ever. The
        /// uninstall has had the same guard since v0.3 (below); only this one was open.</para></summary>
        internal static int StartFailed(Action<string> log, bool quiet, Exception ex, Action<string> show)
        {
            log("start failed: " + ex);
            if (!quiet) show(S.T(Lang.FromCulture(CultureInfo.CurrentUICulture.Name), "err", ex.Message));
            return 1;
        }

        static void ErrorBox(string text)
            => MessageBox.Show(text, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);

        /// <summary>A Client is already running. What that means depends on what was asked for - and never on what is
        /// easiest to report (SPEC 5.1, PORT-MAP 15.1).</summary>
        static int SecondInstance(StartupPlan plan, ClientContext ctx)
        {
            var console = ConsoleOut.Open();
            bool delivered = false;
            switch (Startup.SignalFor(plan.WhenRunning))
            {
                case SignalKind.Show: delivered = SingleInstance.SignalFirst(); break;
                case SignalKind.Play: delivered = SingleInstance.SignalPlay(plan.Windowed); break;
            }
            int code = Startup.SecondStartExit(plan.WhenRunning, delivered);
            if (plan.WhenRunning == Handoff.AlreadyThere)
            {
                ctx.Log.Write("--tray: another StarPocket Client is already running");
                console.Write(S.T(ctx.Lang, "cl_running_tray"));
            }
            else if (plan.WhenRunning == Handoff.Play)
            {
                // a delivered signal means the running Client WILL play; nothing else here may end with 0
                ctx.Log.Write(delivered ? "--autolaunch: the running StarPocket Client was asked to play"
                                        : "--autolaunch: the running StarPocket Client could not be asked to play");
                console.Write(S.T(ctx.Lang, delivered ? "cl_play_sent" : "cl_play_failed"));
            }
            console.Flush();
            return code;
        }

        /// <summary>The answer to a job that would go online before the first-run screen has been answered. Exit code 1,
        /// like every other "I did not do it" from the command line, and nothing is written outward.</summary>
        static int SayNeedConsent(ClientContext ctx)
        {
            var console = ConsoleOut.Open();
            console.Write(AppInfo.Name + " " + AppInfo.Version);
            console.Write(S.T(ctx.Lang, "cl_need_consent"));
            console.Flush();
            ctx.Log.Write("refused before consent (Terms 12(3))");
            return 1;
        }

        /// <summary>"--action install|check|report|status" (ps1:1912-1930).</summary>
        static int RunAction(StartupPlan plan, ClientContext ctx)
        {
            var console = ConsoleOut.Open();
            int code = NewHeadless(ctx, console).RunAction(plan.Action);
            console.Flush();
            return code;
        }

        /// <summary>"--scan-only" (Aegis.ps1 -ScanOnly): the scan runs on a worker thread while the little card draws it;
        /// the process ends when the card has closed itself. Should the card fail to be made, the scan still runs and
        /// still prints - the exit code never depends on anything being drawn.</summary>
        static int RunScanOnly(ClientContext ctx, string exeDir)
        {
            var console = ConsoleOut.Open();
            var headless = NewHeadless(ctx, console);
            string bundled = Path.Combine(exeDir, "aegis");
            ScanPopup card = null;
            try { card = new ScanPopup(ctx.Lang); }
            catch (Exception ex) { ctx.Log.Write("scan card: " + ex.Message); }
            if (card == null)
            {
                int only = headless.RunScanOnly(ctx.Paths.Modded, ctx.AegisStateDir, bundled, null);
                console.Flush();
                return only;
            }
            int code = 1;
            var view = card.Marshalled();
            card.Begin(ctx.Lang, false);
            card.FormClosed += (s, e) => Application.ExitThread();
            // a scan that somehow never ends must not leave a card on screen for ever
            var deadline = new System.Windows.Forms.Timer { Interval = 120000 };
            deadline.Tick += (s, e) => { deadline.Stop(); try { card.CloseNow(); } catch (Exception) { } };
            deadline.Start();
            var worker = new Thread(() =>
            {
                code = headless.RunScanOnly(ctx.Paths.Modded, ctx.AegisStateDir, bundled, view);
            }) { IsBackground = true, Name = "Aegis scan" };
            worker.Start();
            Application.Run();
            deadline.Dispose();
            worker.Join(5000);
            console.Flush();
            return code;
        }

        /// <summary>"--verify-download": fetch the pinned BepInEx zip from the official site, check that it is the file
        /// this build has written down, unpack it in %TEMP% and delete the lot. The game folder is never named
        /// (src\Core\BepVerifier.cs). Run by hand, once, before a release.
        ///
        /// <para>Two things this app has got wrong before are answered here. It is OUTSIDE the single-instance rule -
        /// Handoff.None and UsesSingleInstance false - so there is no way for a running Client to be handed the job and
        /// for this process to report 0 having checked nothing (PORT-MAP 15.1). And the answer is shown in a message
        /// box: the exe is a WinExe, so a console it wrote to may not exist at all or may not be waited for, and the
        /// owner must not have to go and find client.log to learn how it went. The console gets the same words first,
        /// and is flushed before the box appears, so a run whose output was redirected still has them.</para></summary>
        static int RunVerifyDownload(CommandLine cli, ClientContext ctx)
        {
            var console = ConsoleOut.Open();
            string temp = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrEmpty(temp)) temp = Path.GetTempPath();
            var verifier = new BepVerifier
            {
                Lang = () => ctx.Lang,
                Log = ctx.Log.Write,
                Say = console.Write,
                TempRoot = temp,
                LogPath = ctx.Log.Path,
            };
            var res = verifier.Run();
            console.WriteBlock(res.Text);
            console.Flush();
            if (!cli.Quiet)
                MessageBox.Show(res.Text, AppInfo.Name, MessageBoxButtons.OK,
                    res.Outcome == VerifyOutcome.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return res.ExitCode;
        }

        static Headless NewHeadless(ClientContext ctx, ConsoleOut console) => new Headless
        {
            Out = console,
            Log = ctx.Log.Write,
            Lang = () => ctx.Lang,
            DevMode = ctx.DevMode,
            Src = ctx.Src,
            ModdedDir = ctx.Paths.Modded,
            SteamDir = ctx.SteamDir,
            Install = p => ctx.NewInstaller(p).Install(""),
            Check = p => ctx.NewInstaller(p).CheckUpdate(false),   // "shall I install it?" is always answered no
            Report = () => ctx.NewReportBuilder().MakeReport(),
            ComputeStatus = () => ctx.ComputeStatus(false),
            Housekeep = () =>
            {
                var gl = ctx.NewGameLogs();
                gl.RemoveExpired();   // as when the launcher opens: 30 days, then keep the last game's log
                gl.SaveGameLog();
            },
        };

        /// <summary>The words could not be understood. It is said on the console the command came from; when there is no
        /// console (a shortcut with a typo in it, double-clicked) a box says it instead, because a person who sees
        /// nothing at all would simply think the app is broken.</summary>
        static int SayUsage(StartupPlan plan, CommandLine cli, ClientLog log)
        {
            string lang = Lang.Resolve(cli.Language, null, null, CultureInfo.CurrentUICulture.Name);
            string text = Startup.UsageText(lang, plan);
            log.Write("command line: " + text.Split('\n')[0].Trim());   // the mistake only; the usage itself is on screen
            var console = ConsoleOut.Open();
            console.WriteBlock(text);
            console.Flush();
            if (!console.Attached && !plan.Quiet)
                MessageBox.Show(text, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return plan.ExitCode;
        }

        /// <summary>v1.1: the developer switch (Settings → PocketRoles → 開発) was flipped. The mode is decided once at
        /// start (ClientContext.Detect) and everything - the source folder, the shared launcher-state.json, the two
        /// developer buttons - is built from it, so the app starts itself again rather than pretending to switch in
        /// place. By now the window and WebView2 are gone and the single-instance mutex was let go in Shutdown
        /// (ReleaseEarly), so the new process is a plain first start; it reads settings.json and comes up in the mode
        /// the switch asked for. No arguments are passed (ShellOpen.Self). A restart that cannot be started is written
        /// to the log and ends with 1 - the switch itself is already saved, so the next start by hand is in the new mode.</summary>
        static int RestartForDevSwitch(ClientContext ctx)
        {
            try
            {
                ShellOpen.Restart();
                ctx.Log.Write("developer switch: " + AppInfo.Name + " starts again");
                return 0;
            }
            catch (Exception ex)
            {
                ctx.Log.Write("developer switch: could not start again: " + ex.Message);
                return 1;
            }
        }

        /// <summary>The uninstall the window asked for, after the app is gone.</summary>
        static int FinishUninstall(ClientContext ctx, UninstallRequest asked)
        {
            var log = UninstallLog();
            log.Write(AppInfo.Name + " " + AppInfo.Version + " uninstall (from the window)");
            return RunUninstall(ctx, log, asked.ModCopy, quiet: false, attempts: 12);
        }

        /// <summary>"<exe>" --uninstall [--quiet]: the Apps &amp; Features entry. Refuses while a Client window is open
        /// (that one holds the data folder) and while Among Us runs (the mod copy would be mutilated).</summary>
        static int UninstallFromCommandLine(CommandLine cli, string exeDir)
        {
            var log = UninstallLog();
            log.Write(AppInfo.Name + " " + AppInfo.Version + " uninstall (--uninstall" + (cli.Quiet ? " --quiet" : "") + ")");
            ClientContext ctx;
            try { ctx = ClientContext.Detect(cli, exeDir, log); }
            catch (Exception ex)
            {
                log.Write("uninstall: start failed: " + ex);
                if (!cli.Quiet) MessageBox.Show(S.T(Lang.FromCulture(CultureInfo.CurrentUICulture.Name), "err", ex.Message), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            // listen: false - this process removes the app and answers nobody. It holds the same mutex, so without this
            // it would BE "the first one" and would open the Show / Play events that nothing here waits on: a start
            // with --autolaunch would then be told its request was delivered and would end with 0 (PORT-MAP 15.1).
            using (var instance = SingleInstance.Acquire(listen: false))
            {
                if (!instance.IsFirst)
                {
                    log.Write("uninstall: another StarPocket Client is running");
                    if (!cli.Quiet) MessageBox.Show(S.T(ctx.Lang, "un_running"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 1;
                }
                if (Processes.GameRunning())
                {
                    log.Write("uninstall: " + S.T(ctx.Lang, "game_running"));
                    if (!cli.Quiet) MessageBox.Show(S.T(ctx.Lang, "game_running"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 1;
                }
                if (!cli.Quiet)
                {
                    string text = S.T(ctx.Lang, "un_confirm", ctx.DataDir) + "\r\n\r\n" + S.T(ctx.Lang, "un_keep_mod");
                    var answer = MessageBox.Show(text, S.T(ctx.Lang, "un_title"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                    if (answer != DialogResult.OK) { log.Write("uninstall: cancelled"); return 1602; }   // 1602: the person cancelled
                }
                // Apps & Features has no checkbox, so the mod's 1 GB game copy is kept (the owner's decision; the
                // window's own uninstall is where it can be ticked)
                return RunUninstall(ctx, log, modCopy: false, quiet: cli.Quiet, attempts: 3);
            }
        }

        static int RunUninstall(ClientContext ctx, ClientLog log, bool modCopy, bool quiet, int attempts)
        {
            var un = ctx.NewUninstaller(log.Write);
            un.DeleteAttempts = attempts;
            var res = un.Run(modCopy);
            if (!quiet)
            {
                if (res.StoppedAtDataDir)
                    MessageBox.Show(S.T(ctx.Lang, "un_stopped", res.Error ?? ""), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                else if (!res.Ok)
                    MessageBox.Show(S.T(ctx.Lang, "un_partial", res.Failed.Count), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else if (!string.IsNullOrEmpty(res.LeftFolder))
                    MessageBox.Show(S.T(ctx.Lang, res.LeftFolderIsTheirs ? "un_leftfolder" : "un_selffolder", res.LeftFolder), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return res.Ok ? 0 : 1;
        }

        /// <summary>The uninstall's log: %TEMP%, never inside the data folder it is removing.</summary>
        static ClientLog UninstallLog()
        {
            try
            {
                string temp = Environment.GetEnvironmentVariable("TEMP");
                if (string.IsNullOrEmpty(temp)) temp = Path.GetTempPath();
                return new ClientLog(Path.Combine(temp, "StarPocketClient-uninstall.log"));
            }
            catch (Exception) { return ClientLog.None; }
        }

        /// <summary>WebView2Gate.Check, and a WebView2 DLL next to the exe that cannot be loaded at all (the call itself fails
        /// while the method is being prepared) counted as broken files.</summary>
        static WebView2Gate.State CheckRuntime(out string version, out string problem)
        {
            try { return WebView2Gate.Check(out version, out problem); }
            catch (Exception ex) when (WebView2Gate.IsFileProblem(ex) || ex is TypeLoadException)
            {
                version = null;
                problem = ex.GetType().Name + ": " + ex.Message;
                return WebView2Gate.State.BrokenFiles;
            }
        }

        static int FilesMissing(string lang)
        {
            MessageBox.Show(S.T(lang, "files.missing"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
