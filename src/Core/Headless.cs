// "StarPocket Client.exe" --action install|check|report|status and --scan-only: the work of the app with NO WINDOW at
// all - no WebView2, no tray icon, no message loop (the scan popup of --scan-only is the one drawn thing, and it is a
// parameter here so the self-test never opens it). The launcher's headless block (ps1:1912-1930) and Aegis.ps1
// -ScanOnly, ported; SPEC 6.4.
//
// What it prints goes to whoever typed the command (ConsoleOut) AND to client.log, in the app's language, and the exit
// code says how it went:
//   0  done            1  it failed, or another Client is busy with the same kind of work
//   3  --scan-only found a red row (the same 3 the pre-launch scan uses for "do not start")
// Questions are always answered "no" (SPEC 6.4): the update check looks and reports, it never installs by itself, and
// no folder dialog is ever opened.
//
// Everything this class touches is a field, so the self-test runs every action against fake folders and fake jobs.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Threading;
using Starpocket.Client.Aegis;

namespace Starpocket.Client.Core
{
    internal sealed class Headless
    {
        public ConsoleOut Out = ConsoleOut.None;
        public Action<string> Log = _ => { };
        public Func<string> Lang = () => "ja";

        // ---- the four jobs themselves (Program wires the real ones; the self-test its own). Delegates, not an
        // Installer: the self-test then runs every action, every failure and every exit code without a folder to copy.
        public Func<Action<TaskProgress>, TaskOutcome> Install;
        public Func<Action<TaskProgress>, TaskOutcome> Check;
        public Func<ReportResult> Report;
        public Func<LaunchStatus> ComputeStatus;
        /// <summary>Remove-ExpiredLocalData then Save-GameLog, before every job, as the launcher does (ps1:1918).</summary>
        public Action Housekeep = () => { };
        /// <summary>The header lines the launcher writes before every headless job (ps1:1913-1916).</summary>
        public bool DevMode;
        public string Src = "", ModdedDir = "", SteamDir;
        /// <summary>SPEC 5.1: install and check take Local\StarPocketGames.Client.Task first.</summary>
        public Func<string, TaskLock> TakeLock = TaskLock.TryTake;

        string T(string key, params object[] args) => S.T(Lang(), key, args);

        /// <summary>Both at once: the person watching the console and the log they send in a report.</summary>
        void Say(string line)
        {
            Out.Write(line ?? "");
            Log(line ?? "");
        }

        // ------------------------------------------------------------------ --action
        /// <summary>One of <see cref="Startup.Actions"/>. Never throws: an exception is a failure of this job, written
        /// down like any other.</summary>
        public int RunAction(string action)
        {
            TaskLock locked = null;
            try
            {
                Say(AppInfo.Name + " " + AppInfo.Version + " --action " + action);
                Say(T("log_mode", DevMode ? T("mode_dev", Src) : T("mode_friend")));
                Say(T("log_modded", ModdedDir));
                Say(T("log_steam", string.IsNullOrEmpty(SteamDir) ? T("v_notfound") : SteamDir));
                if (Startup.ActionNeedsLock(action))
                {
                    locked = TakeLock(AppInfo.TaskMutexName);
                    if (locked == null) { Say(T("cl_busy")); return 1; }
                }
                // as when the launcher opens: throw away what is 30 days old, then keep the last game's log
                Housekeeping();
                bool ok;
                switch (action)
                {
                    case "install": ok = RunTask(Install); break;
                    case "check": ok = RunTask(Check); break;   // "shall I install it?" is always answered no (SPEC 6.4)
                    case "report": ok = RunReport(); break;
                    case "status": ok = RunStatus(); break;
                    default: Say(T("cl_action_unknown", action)); return 1;
                }
                return ok ? 0 : 1;
            }
            catch (Exception ex)
            {
                Say(T("err", ex.Message));
                Log("--action " + action + ": " + ex);
                return 1;
            }
            finally
            {
                if (locked != null) locked.Dispose();
                Out.Flush();
            }
        }

        void Housekeeping()
        {
            try { Housekeep(); }
            catch (Exception ex) { Log("housekeeping: " + ex.Message); }
        }

        bool RunTask(Func<Action<TaskProgress>, TaskOutcome> job)
        {
            if (job == null) { Say(T("err", "installer")); return false; }
            var o = job(ShowProgress);
            if (o == null) { Say(T("err", "?")); return false; }
            if (!string.IsNullOrEmpty(o.Text)) Say(o.Text);
            if (!o.Ok && !string.IsNullOrEmpty(o.Error)) Say(o.Error);
            return o.Ok;
        }

        /// <summary>A step of an install / update check on one line. Bytes are not drawn as a moving bar: a console line
        /// per step is what a person reads afterwards in a log.</summary>
        int lastStep = -1;
        void ShowProgress(TaskProgress p)
        {
            if (p == null || p.Of <= 0) return;
            if (p.Step == lastStep) return;
            lastStep = p.Step;
            Out.Write("[" + p.Step + "/" + p.Of + "]");
        }

        bool RunReport()
        {
            if (Report == null) { Say(T("err", "report")); return false; }
            Say(T("rp_creating"));
            var r = Report();
            if (r == null || !r.Ok) { Say(r != null && !string.IsNullOrEmpty(r.Error) ? r.Error : T("err", "?")); return false; }
            Say(T("rp_done", r.Name));
            Say(r.Records > 0 ? T("rp_ev", r.Records) : T("rp_ev_none"));
            return true;
        }

        bool RunStatus()
        {
            if (ComputeStatus == null) { Say(T("err", "status")); return false; }
            var s = ComputeStatus();
            foreach (var line in StatusLines(Lang(), s, SteamDir, DevMode)) Say(line);
            return true;
        }

        /// <summary>Get-StatusLines as plain lines (ps1:1707-1737): what is installed, what Steam has, and what the play
        /// button would say. Everything is read from the LaunchStatus the app itself uses, so the console and the window
        /// can never disagree.</summary>
        public static List<string> StatusLines(string lang, LaunchStatus s, string steamDir, bool devMode)
        {
            var lines = new List<string>();
            var info = s.Info ?? new InstallInfo();
            Func<string, string> unknown = v => string.IsNullOrEmpty(v) ? S.T(lang, "v_unknown") : v;
            Action<string, string> add = (key, value) => lines.Add(Pad(S.T(lang, key)) + ": " + value);
            add("st_steam", string.IsNullOrEmpty(steamDir) ? S.T(lang, "v_notfound") : unknown(s.SteamVer) + "  " + steamDir);
            add("st_mod", info.Exe ? unknown(s.ModVer) : S.T(lang, "v_none"));
            add("st_bep", info.Bep ? (info.BepVer ?? "") + "  " + S.T(lang, info.BepOk ? "v_ok" : "v_needs") : S.T(lang, "v_none"));
            add("st_dll", info.Dll ? (info.DllVer ?? "") : S.T(lang, "v_none"));
            add("st_interop", info.Interop ? S.T(lang, "v_ok") : S.T(lang, "v_pending"));
            add("st_client", S.T(lang, s.SteamRunning ? "v_running" : "v_stopped"));
            // the game's own row says only whether it runs: "start Steam first" belongs to the Steam row above it
            add("st_game", S.T(lang, s.GameRunning ? "v_running" : "v_notrunning"));
            add("st_play", s.PState + "  " + s.Warn(lang));
            return lines;
        }

        /// <summary>The ps1's Pad 26: the values line up under each other on the console.</summary>
        static string Pad(string s)
        {
            s = s ?? "";
            // a Japanese or Chinese character is about two columns wide on a console
            int width = 0;
            foreach (char ch in s) width += ch < 0x1100 ? 1 : 2;
            return width >= 26 ? s : s + new string(' ', 26 - width);
        }

        // ------------------------------------------------------------------ --scan-only
        /// <summary>The 13 Aegis checks with freshly read definitions and no download (Aegis.ps1 -ScanOnly), printed row
        /// by row. It writes no events.log line and no prelaunch-result.txt: a look, not a decision.
        /// It takes NO mutex, so it also works while the app is open - unlike Aegis.ps1, where -ScanOnly ran into the
        /// tray's own mutex and the process simply ended having done nothing.
        /// <paramref name="popup"/> is the little window on screen; the self-test passes a recorder instead.</summary>
        public int RunScanOnly(string gameDir, string stateDir, string bundledDir, IScanView popup, IAegisSystem probe = null, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                string lang = Lang();
                Say(AppInfo.Name + " " + AppInfo.Version + " --scan-only");
                Say(T("log_modded", gameDir));
                var defs = new DefinitionsStore(bundledDir, stateDir).Load();
                var checks = AegisScanner.Build(gameDir ?? "", stateDir, defs, probe ?? RealAegisSystem.Instance);
                if (popup != null) { popup.Begin(lang, false); popup.Update(Rows(checks, lang), AegisText.Get(lang, "scanning", 0, checks.Count)); }
                var o = AegisScanner.Run(checks,
                    step => { if (popup != null) popup.Update(Rows(checks, lang), AegisText.Get(lang, "scanning", step, checks.Count)); },
                    null, cancel);
                foreach (var c in checks)
                    Say(Mark(lang, c.State) + " " + c.Title(lang) + ": " + c.Detail.Render(lang)
                        + (c.State == 4 && c.Fix != null && c.Fix.Render(lang).Length > 0 ? "  → " + c.Fix.Render(lang) : ""));
                string summary = o.Serious > 0 ? AegisText.Get(lang, "blocked", o.Serious)
                               : o.Warnings > 0 ? AegisText.Get(lang, "done.warn", o.Warnings)
                               : AegisText.Get(lang, "done");
                Say(summary);
                if (popup != null) { popup.Update(Rows(checks, lang), summary); popup.Done(o.Serious, o.Warnings, summary); }
                return o.Serious > 0 ? 3 : 0;
            }
            catch (Exception ex)
            {
                Say(T("err", ex.Message));
                Log("--scan-only: " + ex);
                if (popup != null) { try { popup.Done(0, 0, T("err", ex.Message)); } catch (Exception) { } }
                return 1;   // Aegis itself failed: not "a red row", and not "everything is fine" either
            }
            finally { Out.Flush(); }
        }

        /// <summary>The state of a row as a word, so the console line reads on its own.</summary>
        public static string Mark(string lang, int state) => S.T(lang, state == 2 ? "scan_ok" : state == 4 ? "scan_stop" : "scan_warn");

        /// <summary>The checks as the little window draws them.</summary>
        public static List<ScanRowView> Rows(IList<AegisCheck> checks, string lang)
        {
            var rows = new List<ScanRowView>();
            foreach (var c in checks)
            {
                bool done = c.State >= 2;
                rows.Add(new ScanRowView
                {
                    Title = c.Title(lang),
                    Detail = done ? c.Detail.Render(lang) : "",
                    Fix = done && c.Fix != null ? c.Fix.Render(lang) : "",
                    State = c.State,
                });
            }
            return rows;
        }
    }

    /// <summary>One row of the scan as the little window draws it (no Aegis types: the app builds these from its
    /// snapshot, the headless scan from its checks).</summary>
    internal sealed class ScanRowView
    {
        public string Title = "", Detail = "", Fix = "";
        /// <summary>0 waiting, 1 running, 2 OK, 3 warning, 4 stops a start.</summary>
        public int State;
    }

    /// <summary>The scan on screen (Shell\ScanPopup). An interface so the headless job and the app share one seam and
    /// the self-test can run a whole scan with nothing drawn at all.</summary>
    internal interface IScanView
    {
        /// <summary>A scan is starting; <paramref name="preLaunch"/> decides how long the finished window stays.</summary>
        void Begin(string lang, bool preLaunch);
        /// <summary>The rows as they are now, and the line under the title.</summary>
        void Update(IList<ScanRowView> rows, string summary);
        /// <summary>The scan has ended: stay for a moment (longer when it stopped a start), then close.</summary>
        void Done(int serious, int warnings, string summary);
        /// <summary>Close now (the window came back, or the app is quitting).</summary>
        void CloseNow();
    }
}
