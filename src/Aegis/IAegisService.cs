// What the app shell needs from Aegis - the seam between the shell (v0.1 step 1) and the Aegis port (v0.1 step 2).
//
// The Aegis port (PORT-MAP 2.2, 3.6, 3.8-3.12) implements IAegisService in src\Aegis\AegisService.cs; AegisFactory.Create
// gives it (the stub only if it cannot be made). Everything the shell does with Aegis goes through this interface:
//   - ClientApp calls Start() once after the window and the tray exist, Stop() on quit, SetLanguage() when the viewer
//     changes the language, and listens to Changed (tray dot + tooltip, taskbar badge, the UI's "aegis" event).
//   - GameLauncher calls PreLaunchScan() on a worker thread; the 60-second deadline (PORT-MAP 3.6, 9.1 L-1) is the
//     SHELL's (it stops waiting and starts the game); the scan itself only has to watch the CancellationToken.
//   - Bridge calls Rescan() / ScanOnly() on a worker thread for the invokes aegis.rescan / aegis.scanOnly and the tray's
//     "scan again", and opens EventsLogPath in Notepad for aegis.events.
// Threading: Start / Stop / SetLanguage / Changed are on the UI thread; GetSnapshot is callable from any thread;
// the scans run on the calling worker thread and report progress through the callback (the shell marshals it).
// Nothing here may throw into the shell: an Aegis failure never blocks the game (PORT-MAP 3.6).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace Starpocket.Client.Aegis
{
    internal interface IAegisService : IDisposable
    {
        /// <summary>false only for AegisStub: the shell then answers aegis.* invokes with "not in this version".</summary>
        bool IsPorted { get; }

        /// <summary>UI thread, once: take the Aegis mutex (Local\wakayamachannel.Aegis.AntiCheat; if the old tray holds it,
        /// stay "off" and retry every second, PORT-MAP 3.15), prune events.log, load the definitions, fetch them once in the
        /// background, run the start scan, start the 1-second game watcher, toast b.ready (PORT-MAP 5 step 6).</summary>
        void Start();

        /// <summary>UI thread: stop watching, close toasts, release the Aegis mutex.</summary>
        void Stop();

        /// <summary>UI thread: "ja" / "zh-CN" / "en" for scan texts, toasts and the snapshot's texts from now on.</summary>
        void SetLanguage(string lang);

        /// <summary>UI thread, when the viewer opens the window or plays: the one definitions download of Start again (same
        /// URL, checks and "never older" rule, a fresh DefinitionsStore), but only when the last one began 12 hours ago or
        /// more. The app stays open for days in the tray; Aegis.ps1's tray started with every launcher, so a new signed
        /// file reached the pre-launch scan within a day. True when a download was started.</summary>
        bool RefreshDefinitionsIfStale();

        /// <summary>A copy of the current state (any thread).</summary>
        AegisSnapshot GetSnapshot();

        /// <summary>Raised on the UI thread whenever the snapshot changed (state, counts, rows, events, definitions).</summary>
        event EventHandler Changed;

        /// <summary>The launcher's pre-launch check (Aegis.ps1 -PreLaunch): fresh definitions (no fetch), the 13 checks,
        /// events.log "pre-launch scan: ..." and prelaunch-result.txt written as today. Worker thread. Blocked = a red row.</summary>
        PreLaunchResult PreLaunchScan(Action<ScanProgress> progress, CancellationToken cancel);

        /// <summary>"Scan again" with the tray's definitions (aegis.rescan, tray menu). Worker thread.</summary>
        ScanSummary Rescan(Action<ScanProgress> progress);

        /// <summary>Aegis.ps1 -ScanOnly: fresh definitions, no fetch (aegis.scanOnly). Worker thread.</summary>
        ScanSummary ScanOnly(Action<ScanProgress> progress);

        /// <summary>%LOCALAPPDATA%\PocketRoles\Aegis\events.log (opened in Notepad by aegis.events).</summary>
        string EventsLogPath { get; }
    }

    /// <summary>What Aegis gets from the shell.</summary>
    internal sealed class AegisContext
    {
        /// <summary>The mod's game copy (the launcher's rules, PORT-MAP 3.1 and 9.1 L-7).</summary>
        public string GameDir;
        /// <summary>%LOCALAPPDATA%\PocketRoles\Aegis (shared with Aegis.ps1; PORT-MAP 3.8).</summary>
        public string StateDir;
        /// <summary>&lt;exe folder&gt;\aegis (the bundled definitions.txt / .sig).</summary>
        public string BundledDir;
        /// <summary>"ja" / "zh-CN" / "en" at start.</summary>
        public string Lang;
        /// <summary>The main window: owner of the UI thread (timers, toasts, BeginInvoke).</summary>
        public Control Ui;
        /// <summary>client.log.</summary>
        public Action<string> Log;
    }

    internal sealed class ScanProgress
    {
        public int Step { get; set; }
        public int Of { get; set; } = 13;
    }

    internal sealed class ScanSummary
    {
        /// <summary>Set only by AegisStub.</summary>
        public bool NotAvailable { get; set; }
        /// <summary>Set when Aegis cannot scan now (the old tray holds the mutex, or Aegis has not started): the text to show.</summary>
        public string Error { get; set; }
        public int Warnings { get; set; }
        public int Serious { get; set; }
    }

    internal sealed class PreLaunchResult
    {
        /// <summary>false: nothing was checked (stub or Aegis failed) - the game starts, like a missing Aegis.ps1 today.</summary>
        public bool Ran { get; set; }
        public bool Blocked { get; set; }
        /// <summary>The red rows as "&lt;title&gt;: &lt;detail&gt; → &lt;fix&gt;" (the lines of prelaunch-result.txt).</summary>
        public string[] Lines { get; set; } = new string[0];
        /// <summary>The keys of the red rows (PORT-MAP 13.3).</summary>
        public string[] Keys { get; set; } = new string[0];

        /// <summary>The start was stopped only because PocketRoles.dll is no longer the file that was installed (R-41):
        /// the play button becomes 「修復」 and putting the mod back solves it.</summary>
        public bool OnlyModChanged => Blocked && Keys != null && Keys.Length == 1 && Keys[0] == "mod";
    }

    internal sealed class AegisRow
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Fix { get; set; }
        /// <summary>0 waiting, 1 running, 2 OK, 3 warning, 4 stops the start (PORT-MAP 3.9).</summary>
        public int State { get; set; }
    }

    internal sealed class AegisSnapshot
    {
        /// <summary>idle / watching / kicked (12 s after a removal) / off (not running, or the old tray holds the mutex).</summary>
        public string State { get; set; } = "off";
        /// <summary>The old PowerShell Aegis tray (Aegis.ps1) holds the mutex: it is the one watching (State is "off" here).</summary>
        public bool OldTray { get; set; }
        public int Detected { get; set; }
        public int Kicked { get; set; }
        /// <summary>Red rows in the last scan (start, scan again or pre-launch): the red taskbar badge until a scan without.</summary>
        public int LastScanSerious { get; set; }
        public int LastScanWarnings { get; set; }
        public List<AegisRow> Rows { get; } = new List<AegisRow>();
        /// <summary>"HH:mm:ss  text", newest first (at most 200).</summary>
        public List<string> Events { get; } = new List<string>();
        public int DefsVersion { get; set; }
        /// <summary>0 signed OK, 1 no file (built-in list), 2 unsigned / mismatch (built-in list).</summary>
        public int SigState { get; set; } = 1;
        /// <summary>The headline under the panel title (scanning n/13, done, done with n warnings, old tray running ...).</summary>
        public string Summary { get; set; } = "";
        /// <summary>"" nothing has run yet, "scanning" while the rows are being worked through, "done" after. v0.4: the
        /// scan card at the bottom right (Shell\ScanPopup) opens on "scanning" whenever the window is hidden; v1.1: and
        /// for the start scan on every start (ClientApp.UpdateScanCard, Settings → 起動時の動作).</summary>
        public string Phase { get; set; } = "";
        /// <summary>Which scan the rows belong to: start / rescan / scanOnly / prelaunch (v0.4: a pre-launch scan's card
        /// stays on screen longer when it stopped a start, as the ps1's scan window did).</summary>
        public string Kind { get; set; } = "";
        /// <summary>v1.2: how many scans have ended so far (0 before the first). The page plays its "found" sound once per
        /// scan that ended with a red row, and this is how it tells a new result from the same one sent again (the
        /// watcher's 1-second updates carry the same rows).</summary>
        public int Scan { get; set; }

        /// <summary>The "aegis" event for the UI (PORT-MAP 7.2). v1.2 adds phase / kind / scan (above).</summary>
        public Dictionary<string, object> ToEvent()
        {
            var rows = new List<object>();
            foreach (var r in Rows)
                rows.Add(new Dictionary<string, object> { ["key"] = r.Key, ["title"] = r.Title, ["detail"] = r.Detail ?? "", ["fix"] = r.Fix ?? "", ["state"] = r.State });
            return new Dictionary<string, object>
            {
                ["state"] = State,
                ["detected"] = Detected,
                ["kicked"] = Kicked,
                ["serious"] = LastScanSerious,
                ["warnings"] = LastScanWarnings,
                ["rows"] = rows,
                ["events"] = Events.ToArray(),
                ["defs"] = new Dictionary<string, object> { ["version"] = DefsVersion, ["sig"] = SigState },
                ["summary"] = Summary ?? "",
                ["rules"] = AppInfo.AegisRuleCount,
                ["phase"] = Phase ?? "",
                ["kind"] = Kind ?? "",
                ["scan"] = Scan,
            };
        }
    }

    internal static class AegisFactory
    {
        /// <summary>Aegis in the app (AegisService). Should it fail to be made, the stub: nothing is checked and nothing is
        /// blocked, like a launcher without aegis\Aegis.ps1 today.</summary>
        public static IAegisService Create(AegisContext context)
        {
            try { return new AegisService(context); }
            catch (Exception ex)
            {
                context?.Log?.Invoke("Aegis failed: " + ex);
                return new AegisStub(context);
            }
        }
    }
}
