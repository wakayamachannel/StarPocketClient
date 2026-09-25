// "Start with the mod" exactly like the PowerShell launcher's Invoke-Launch (PORT-MAP 3.5 / 3.6; ps1:1822-1876):
// checks -> Aegis pre-launch scan (60 s) -> keep the last log -> start <Modded>\Among Us.exe.
// Same checks in the same order, same file, working folder and arguments, the environment passed on untouched.
// v0.1.1: plain Among Us (Settings → 起動するゲーム) is LaunchVanilla: Steam's URL only, none of the mod's steps.
// Every outside effect is a delegate so --self-test can run the whole flow with fakes.
// Runs on a worker thread (the pre-launch scan and the log copy take time); never throws.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Starpocket.Client.Aegis;

namespace Starpocket.Client.Core
{
    internal sealed class LaunchOutcome
    {
        public bool Ok, Unsupported, Blocked;
        public string Error, Needs, Text;
        public string[] Reasons = new string[0];
        /// <summary>The block was only "PocketRoles.dll has changed" (R-41): the play button becomes 「修復」 (PORT-MAP 13.3).</summary>
        public bool RepairMod;

        /// <summary>The invoke result (SPEC 3, PORT-MAP 3.5).</summary>
        public Dictionary<string, object> ToResult(string cmd)
        {
            var r = new Dictionary<string, object> { ["ok"] = Ok };
            if (Ok) { r["data"] = new Dictionary<string, object> { ["text"] = Text ?? "" }; return r; }
            if (Unsupported)
            {
                r["unsupported"] = true;
                r["needs"] = Needs;
                r["data"] = new Dictionary<string, object> { ["cmd"] = cmd, ["needs"] = Needs, ["version"] = AppInfo.UiVersion };
                return r;
            }
            if (Blocked)
            {
                r["blocked"] = true;
                var data = new Dictionary<string, object> { ["reasons"] = Reasons, ["text"] = Text ?? "" };
                if (RepairMod) data["repair"] = "mod";
                r["data"] = data;
                if (RepairMod) r["needs"] = "repair";
                return r;
            }
            r["error"] = Error ?? "";
            if (!string.IsNullOrEmpty(Needs)) r["needs"] = Needs;
            return r;
        }
    }

    internal sealed class GameLauncher
    {
        /// <summary>Unity's arguments for "launch in a window" (ps1:1868-1871); a normal start has no arguments.</summary>
        public const string WindowedArguments = "-screen-fullscreen 0 -screen-width 1600 -screen-height 900";

        public ModPaths Paths;
        public bool DevMode;
        public Func<string> Lang = () => "ja";
        public Func<bool> GameRunning = Processes.GameRunning;
        public Func<bool> SteamRunning = Processes.SteamRunning;
        /// <summary>Get-StatusLines again (3.4) right before the decision.</summary>
        public Func<LaunchStatus> ComputeStatus;
        public Func<Action<ScanProgress>, CancellationToken, PreLaunchResult> PreLaunchScan;
        public int PreLaunchTimeoutMs = 60000;
        public Func<bool> SaveGameLog = () => true;
        /// <summary>Starts the game, or Steam's URL for plain Among Us: ShellOpen (ShellExecute like Start-Process) is the
        /// only place the app starts anything. The self-test records the start info instead.</summary>
        public Action<ProcessStartInfo> StartProcess = ShellOpen.StartLaunch;
        public Action<string> Log = _ => { };

        string T(string key, params object[] args) => S.T(Lang(), key, args);

        /// <summary>The start info of the game: &lt;Modded&gt;\Among Us.exe, working folder &lt;Modded&gt;, no arguments (windowed:
        /// the 1600x900 ones), ShellExecute with the default verb, nothing added to or removed from the environment.</summary>
        public static ProcessStartInfo GameStartInfo(ModPaths p, bool windowed) => new ProcessStartInfo
        {
            FileName = GameFolders.Join(p.Modded, "Among Us.exe"),
            WorkingDirectory = p.Modded,
            Arguments = windowed ? WindowedArguments : "",
            UseShellExecute = true,
        };

        /// <summary>Plain Among Us through Steam: steam://rungameid/945360, opened like the launcher's "Launch vanilla (Steam)"
        /// (ps1:2002 Start-Process). No arguments, no working folder: Steam starts its own copy.</summary>
        public static ProcessStartInfo VanillaStartInfo() => new ProcessStartInfo
        {
            FileName = AppInfo.SteamRunGameUrl,
            UseShellExecute = true,
        };

        /// <summary>PLAY while Settings → 起動するゲーム is plain Among Us (v0.1.1), and the tools list's "start plain Among Us".
        /// Only "is Among Us already running?" (the same words as the mod launch), then Steam's URL. Nothing of the mod runs:
        /// no status check, no Aegis pre-launch scan, no log copy, no file in the mod copy is read or written. Steam starts
        /// itself when it is not running, so there is no "start Steam first" either.</summary>
        public LaunchOutcome LaunchVanilla()
        {
            try
            {
                if (GameRunning()) { Log(T("la_running")); return new LaunchOutcome { Error = T("la_running") }; }
                StartProcess(VanillaStartInfo());
                Log(T("la_vanilla"));
                return new LaunchOutcome { Ok = true, Text = T("la_vanilla") };
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return new LaunchOutcome { Error = e };
            }
        }

        public LaunchOutcome Launch(bool windowed, Action<ScanProgress> progress)
        {
            try
            {
                if (GameRunning()) { Log(T("la_running")); return new LaunchOutcome { Error = T("la_running") }; }
                var st = ComputeStatus();
                if (!DevMode && !st.Installed) { Log(T("la_notinstalled")); return new LaunchOutcome { Error = T("la_notinstalled"), Needs = "install" }; }
                // the launcher shows a message box here; the app shows the same words inside the window
                if (!SteamRunning()) { Log(T("la_steam")); return new LaunchOutcome { Error = T("la_steam") }; }
                // the launcher asks "update / sync first?" here with a dialog. v0.2 can sync, so the answer names it and the
                // play button becomes 「更新してプレイ」 (one press: sync, then the launch); rebuilding and the developer
                // update are still v0.3 (PORT-MAP 3.5 step 5, 13.2)
                if (DevMode)
                {
                    if (st.NeedsUpdate) return new LaunchOutcome { Unsupported = true, Needs = "devUpdate" };
                    if (st.NeedsRebuild) return new LaunchOutcome { Unsupported = true, Needs = "rebuild" };
                }
                else if (st.NeedsUpdate)
                {
                    string ask = T("la_sync_first", st.ModVer, st.SteamVer);
                    Log(ask);
                    return new LaunchOutcome { Error = ask, Needs = "syncSteam" };
                }

                if (!GameFolders.PathExists(GameFolders.Join(Paths.Modded, @"BepInEx\interop\Assembly-CSharp.dll"))) Log(T("in_firstrun"));

                var pre = RunPreLaunch(progress);
                if (pre != null && pre.Blocked)
                {
                    var reasons = new List<string>();
                    foreach (var line in pre.Lines ?? new string[0]) if (!string.IsNullOrEmpty(line)) reasons.Add(line);
                    string msg = T("la_aegis_block");
                    foreach (var line in reasons) msg += "\n・" + line;
                    Log(msg);
                    return new LaunchOutcome { Blocked = true, Reasons = reasons.ToArray(), Text = msg, RepairMod = pre.OnlyModChanged };
                }

                SaveGameLog();   // BepInEx overwrites LogOutput.log when the game starts: keep the previous session first
                Log(T("la_start"));
                StartProcess(GameStartInfo(Paths, windowed));
                if (windowed) Log(S.WindowedLaunchLog);
                Log(T("la_started"));   // whether the game really came up is not checked (PORT-MAP 9.1 L-8)
                return new LaunchOutcome { Ok = true, Text = T("la_started") };
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return new LaunchOutcome { Error = e };
            }
        }

        /// <summary>Test-AegisPreLaunch: waits at most 60 s; no answer in time, an Aegis failure or no Aegis never blocks
        /// the game (PORT-MAP 3.6, 9.1 L-1).</summary>
        PreLaunchResult RunPreLaunch(Action<ScanProgress> progress)
        {
            if (PreLaunchScan == null) return null;
            var cts = new CancellationTokenSource();
            var task = Task.Run(() => PreLaunchScan(progress ?? (_ => { }), cts.Token));
            try
            {
                if (!task.Wait(PreLaunchTimeoutMs))
                {
                    cts.Cancel();   // the scan may still be reading; it is left to finish on its own (not disposed here)
                    Log("Aegis: the pre-launch scan did not finish in " + (PreLaunchTimeoutMs / 1000) + " s; the game starts (same as the launcher)");
                    return null;
                }
            }
            catch (AggregateException ex)
            {
                Log("Aegis: " + ex.GetBaseException().Message);
                return null;
            }
            var r = task.Result;
            if (r != null && !r.Ran) Log("Aegis: pre-launch scan not available (the game starts, like a launcher without Aegis)");
            return r;
        }
    }
}
