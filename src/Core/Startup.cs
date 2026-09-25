// What this process is going to do, decided from the command line alone (v0.4; SPEC 5.1 / 6.4, PORT-MAP 14.5).
//
// One place decides it, and it decides nothing else: no file is read, no window is made, no mutex is taken here. That
// is what lets the self-test ask "what would the app do with these words?" for every spelling, every pair of modes and
// every typo, without a window ever opening.
//
// The rule that matters most is at the top of Decide: a word the app does not know is a USAGE error. The bug that
// shipped in v0.3 (PORT-MAP 15.1) was the opposite - "--uninstall" was not understood, fell into a list that was only
// logged, and the app opened its window instead; when one was already open, the second one even signalled the first and
// exited 0, so Windows was told the uninstall had SUCCEEDED. So: unknown words first, before any mode is chosen.
//
// The other rule that matters: which modes may hand their work to an already running Client (Handoff). --action never
// does (SPEC 5.1 keeps it out of the single-instance rule entirely); --uninstall refuses; --tray says "it is already
// there"; only a plain start raises the running window, and --autolaunch asks it to play.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Starpocket.Client.Core
{
    internal enum StartupMode
    {
        /// <summary>The app with its window (with --autolaunch: hidden, and it plays at once).</summary>
        Normal,
        /// <summary>--tray: the app in the notification area, no window (Aegis.ps1 -Tray).</summary>
        Tray,
        /// <summary>--action install|check|report|status: no window at all, an exit code (the launcher's -Action).</summary>
        Action,
        /// <summary>--scan-only: one Aegis scan, then the process ends (Aegis.ps1 -ScanOnly).</summary>
        ScanOnly,
        /// <summary>--verify-download: fetch the pinned BepInEx zip, check it, unpack it in %TEMP% and delete it all
        /// again. Never names the game folder. Run by hand once before a release.</summary>
        VerifyDownload,
        /// <summary>--uninstall [--quiet]: the Apps &amp; Features entry.</summary>
        Uninstall,
        /// <summary>--self-test &lt;dir&gt;.</summary>
        SelfTest,
        /// <summary>The words could not be understood: say so and stop (never open the app instead).</summary>
        Usage,
    }

    /// <summary>What a second start sends to the first one, if anything.</summary>
    internal enum SignalKind { None, Show, Play }

    /// <summary>What a second start does when a Client is already running.</summary>
    internal enum Handoff
    {
        /// <summary>Never asks: this mode does its own work whatever else is running (--action, --scan-only, --self-test).</summary>
        None,
        /// <summary>Bring the running window to the front and end with 0 (a plain start: what the person wanted).</summary>
        Show,
        /// <summary>Ask the running Client to play; only a delivered request ends with 0 (--autolaunch).</summary>
        Play,
        /// <summary>Say "it is already running in the notification area" and end with 0, WITHOUT raising its window (--tray).</summary>
        AlreadyThere,
        /// <summary>Refuse: this job cannot be done while another Client holds the data folder (--uninstall).</summary>
        Refuse,
    }

    internal sealed class StartupPlan
    {
        public StartupMode Mode;
        /// <summary>install / check / report / status, in lower case (Mode == Action).</summary>
        public string Action = "";
        /// <summary>The app starts without showing its window (--tray, --autolaunch).</summary>
        public bool HideWindow;
        /// <summary>Play as soon as the app is ready (--autolaunch).</summary>
        public bool AutoLaunch;
        /// <summary>The game starts in a 1600x900 window (--windowed).</summary>
        public bool Windowed;
        /// <summary>The 15-second rule of the Aegis tray applies (started as the tray from the command line).</summary>
        public bool TrayStart;
        public bool Quiet;
        /// <summary>What a second start does (see <see cref="Handoff"/>).</summary>
        public Handoff WhenRunning;
        /// <summary>Mode == Action: the job needs the task mutex (install / check write into the game copy).</summary>
        public bool NeedsTaskLock;
        /// <summary>Mode == Usage: what was wrong, in the viewer's language, followed by the usage text.</summary>
        public string Problem = "";
        /// <summary>The exit code for a mode that decides it here (Usage: 1).</summary>
        public int ExitCode;

        /// <summary>True while this process may take the single-instance mutex at all. --action and --scan-only are
        /// outside the "one Client" rule (SPEC 5.1): they read and work on their own and must never be turned into
        /// "another one is running, so this one did nothing and succeeded".</summary>
        public bool UsesSingleInstance => Mode == StartupMode.Normal || Mode == StartupMode.Tray || Mode == StartupMode.Uninstall;
    }

    internal static class Startup
    {
        /// <summary>The four jobs of the launcher's -Action (ps1:1912-1930), in the spelling this app answers to.</summary>
        public static readonly string[] Actions = { "install", "check", "report", "status" };

        /// <summary>install and check copy into the game folder and write launcher-state.json: they wait for nobody, so
        /// they take the task mutex and say "busy" instead (SPEC 5.1). status and report only read.</summary>
        public static bool ActionNeedsLock(string action) => action == "install" || action == "check";

        /// <summary>
        /// Terms of Use Article 12(3): "before you agree, the Client does not connect to the internet". The first-run
        /// screen keeps that promise for the window - <see cref="Shell.Bridge.BeforeConsent"/> refuses every page command
        /// that could go online - but the jobs with NO window were dispatched before any of that, and had no check of
        /// their own. So on a PC that had never answered the screen, "--action install" downloaded BepInEx and the mod
        /// anyway, and said "インストール完了" (found by testing the v1.0.0 build, 2026-09-26). These are the same jobs the
        /// page is refused, so they answer the same way.
        ///
        /// <para>Only the jobs that really go online are asked, and the list was checked one by one rather than guessed:
        /// "install" and "check" fetch (<see cref="Headless.Install"/>, <see cref="Headless.Check"/>), and
        /// "--verify-download" downloads by definition. "--scan-only" does NOT - it reads the definitions that are already
        /// on this PC (<c>new DefinitionsStore(bundledDir, stateDir).Load()</c>) and looks at the game folder, so gating it
        /// would refuse a purely local check for no reason. "status" and "report" read THIS PC and send nothing, and
        /// someone who has not agreed yet should still be able to see what the app found and hand over a report.
        /// "--uninstall" is never asked either - it has to work without agreeing (first-run.md 2.2 item 8, a SignPath
        /// condition), and it is dispatched before this is ever reached.</para>
        /// </summary>
        public static bool NeedsConsentFirst(StartupMode mode, string action)
        {
            if (mode == StartupMode.VerifyDownload) return true;
            if (mode != StartupMode.Action) return false;
            return action == "install" || action == "check";
        }

        public static StartupPlan Plan(CommandLine cli)
        {
            var p = Decide(cli ?? CommandLine.Parse(null));
            if (p.Mode == StartupMode.Usage) p.ExitCode = 1;
            return p;
        }

        static StartupPlan Decide(CommandLine c)
        {
            var p = new StartupPlan { Quiet = c.Quiet, Windowed = c.Windowed };

            // 1. a word the app does not know: never "start anyway"
            if (c.Unknown.Count > 0) return Usage(p, "cl_unknown", string.Join(" ", c.Unknown.ToArray()));
            if (c.Extra.Count > 0) return Usage(p, "cl_extra", string.Join(" ", c.Extra.ToArray()));
            if (c.Empty.Count > 0) return Usage(p, "cl_no_value", string.Join(" ", c.Empty.ToArray()));
            // the same option twice with two different values. "--action install --action status" asks for two jobs;
            // the parser used to let the later one win, so one of the two ran and nothing said the other was dropped
            // (v0.4 review). It is the same mistake as two modes at once, and it is refused the same way.
            if (c.Twice.Count > 0) return Usage(p, "cl_twice", string.Join(" ", c.Twice.ToArray()));

            // 2. only one mode at a time. Two modes together always meant a mistake, and picking one of them silently
            //    would do something the person did not ask for (--action install --uninstall is the frightening one).
            var modes = new List<string>();
            if (c.SelfTest) modes.Add("--self-test");
            if (c.Uninstall) modes.Add("--uninstall");
            if (c.Action != null) modes.Add("--action");
            if (c.ScanOnly) modes.Add("--scan-only");
            if (c.VerifyDownload) modes.Add("--verify-download");
            if (c.Tray) modes.Add("--tray");
            if (modes.Count > 1) return Usage(p, "cl_two_modes", string.Join(" ", modes.ToArray()));

            // 3. --autolaunch and --windowed only mean something where there is a game to start
            bool noLaunch = c.SelfTest || c.Uninstall || c.Action != null || c.ScanOnly || c.VerifyDownload;
            if (noLaunch && c.AutoLaunch) return Usage(p, "cl_no_launch", "--autolaunch");
            if (noLaunch && c.Windowed) return Usage(p, "cl_no_launch", "--windowed");

            if (c.SelfTest) { p.Mode = StartupMode.SelfTest; p.WhenRunning = Handoff.None; return p; }
            if (c.Uninstall) { p.Mode = StartupMode.Uninstall; p.WhenRunning = Handoff.Refuse; return p; }
            if (c.Action != null)
            {
                string a = (c.Action ?? "").Trim().ToLowerInvariant();
                if (Array.IndexOf(Actions, a) < 0) return Usage(p, "cl_action_unknown", c.Action ?? "");
                p.Mode = StartupMode.Action;
                p.Action = a;
                p.NeedsTaskLock = ActionNeedsLock(a);
                p.WhenRunning = Handoff.None;
                return p;
            }
            if (c.ScanOnly) { p.Mode = StartupMode.ScanOnly; p.WhenRunning = Handoff.None; return p; }
            // Handoff.None, and UsesSingleInstance is false for it (below): there is no path that hands this job to a
            // running Client, so there is no path that reports 0 without having checked anything - which is exactly how
            // --uninstall "succeeded" in v0.3 (PORT-MAP 15.1). The whole point of the command is one honest answer.
            if (c.VerifyDownload) { p.Mode = StartupMode.VerifyDownload; p.WhenRunning = Handoff.None; return p; }
            if (c.Tray)
            {
                p.Mode = StartupMode.Tray;
                p.HideWindow = true;
                p.TrayStart = true;               // Aegis.ps1's manual start: it ends by itself 15 s after a game
                p.AutoLaunch = c.AutoLaunch;      // --tray --autolaunch: in the notification area, and play at once
                p.WhenRunning = Handoff.AlreadyThere;
                return p;
            }
            p.Mode = StartupMode.Normal;
            p.AutoLaunch = c.AutoLaunch;
            // --autolaunch means "play now": the window would hide itself the moment the game started (SPEC 5.4), so it
            // never comes up in the first place. It is the one modifier that changes where the app starts.
            p.HideWindow = c.AutoLaunch;
            p.WhenRunning = c.AutoLaunch ? Handoff.Play : Handoff.Show;
            return p;
        }

        /// <summary>What a second start actually sends to the running Client. Nothing for --tray: a Client IS in the
        /// notification area, which is what was asked for, and raising its window would be the opposite of "no window".
        /// Nothing for --action / --scan-only / --uninstall either - they never hand their work over.</summary>
        public static SignalKind SignalFor(Handoff h) => h == Handoff.Show ? SignalKind.Show : h == Handoff.Play ? SignalKind.Play : SignalKind.None;

        /// <summary>The exit code of a second start. The only 0 that does not mean "the thing was done here" is a
        /// signal that was really delivered to a Client that will do it (Show, Play) - and "it is already in the
        /// notification area", which is the whole of what --tray asks for. A mode that never signals must not reach
        /// this at all; if it somehow does, it fails (1) rather than reporting a success nobody worked for.</summary>
        public static int SecondStartExit(Handoff h, bool delivered)
        {
            switch (h)
            {
                case Handoff.Show: return 0;
                case Handoff.AlreadyThere: return 0;
                case Handoff.Play: return delivered ? 0 : 1;
                default: return 1;   // Refuse (--uninstall) and None (--action, --scan-only)
            }
        }

        static StartupPlan Usage(StartupPlan p, string key, string what)
        {
            p.Mode = StartupMode.Usage;
            p.Problem = key + "\0" + what;   // rendered by UsageText in the viewer's language
            return p;
        }

        /// <summary>What the person sees when the words could not be understood: the mistake, then the usage.</summary>
        public static string UsageText(string lang, StartupPlan plan)
        {
            string key = "cl_unknown", what = "";
            string problem = plan != null ? plan.Problem ?? "" : "";
            int z = problem.IndexOf('\0');
            if (z >= 0) { key = problem.Substring(0, z); what = problem.Substring(z + 1); }
            return S.T(lang, key, what) + "\r\n\r\n" + S.T(lang, "cl_usage");
        }
    }
}
