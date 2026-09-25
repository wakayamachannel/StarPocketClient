// Aegis inside the app (the port of aegis\Aegis.ps1's tray and -PreLaunch / -ScanOnly; PORT-MAP 2.2, 3.6, 3.8-3.12, 3.15,
// 5 step 6). One process: the tray's work runs on the app's UI thread (the 1-second watcher, toasts), the scans on worker
// threads, one scan at a time.
//   Start      the Aegis mutex (Local\wakayamachannel.Aegis.AntiCheat; while the old PowerShell tray holds it, Aegis stays
//              "off" and tries again every second), events.log pruned to 30 days, the definitions loaded and fetched once
//              in the background, the start scan, then the watcher and the "ready" toast - the ps1's order.
//   Refresh    the app stays open for days (the close button goes to the tray), while Aegis.ps1's tray started with every
//              launcher: so opening the window or playing downloads the definitions again when the last download began
//              12 hours ago or more (same URL, checks and rules, into a fresh DefinitionsStore so "never older" holds).
//   PreLaunch  fresh definitions (no fetch), the 13 checks, events.log "pre-launch scan: ..." and prelaunch-result.txt
//              written as today; blocked when a row is red. The mutex is not needed (like the ps1).
//   Rescan     the tray's definitions (those loaded at start: 9.2 A-1). ScanOnly: fresh definitions, no fetch.
// Aegis's own failures never stop the game: they go to aegis.log ("Aegis failed: ...") and client.log.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Starpocket.Client.Core;

namespace Starpocket.Client.Aegis
{
    internal sealed class AegisService : IAegisService
    {
        readonly AegisContext ctx;
        readonly Action<string> log;
        readonly object gate = new object();       // the snapshot's data
        readonly object scanGate = new object();   // one scan at a time (the fingerprint file is read and written by a scan)
        readonly Stopwatch clock = Stopwatch.StartNew();
        volatile string lang;
        volatile bool ownsMutex, stopped;
        bool started;
        string offReason;                           // null, "oldtray" (the ps1's tray holds the mutex), "failed"
        Mutex mutex;
        System.Windows.Forms.Timer timer;
        DefinitionsStore trayDefs;
        LogWatcher watcher;
        string lastRaised = "";
        int changePending;
        int fetching;                               // 1 while a definitions download runs
        DateTime? lastFetchAt;                      // when the last download began (under gate)

        // the snapshot's data (under gate)
        List<AegisCheck> rows = new List<AegisCheck>();
        string phase = "", lastKind = "";
        int scanStep, lastWarnings, lastSerious, defsVersion, sigState = 1;
        int scanSeq;                                // v1.2: how many scans have ENDED - the page plays "found" once per one that ended red
        readonly List<string> events = new List<string>();

        // what the app reads of the world (the self-test replaces them)
        public IAegisSystem Probe = RealAegisSystem.Instance;
        public DefinitionKey[] Trusted;
        public string[] Revoked;
        public Func<DateTime> Now = () => DateTime.Now;
        /// <summary>"A game to watch": since v0.2 the mod's own copy, told by the exe PATH (Processes.ModdedGameRunning),
        /// not by the process name - plain Among Us from Steam is no longer watched (PORT-MAP 13.5). Set in the constructor
        /// from the game folder; the self-test replaces it.</summary>
        public Func<bool> GameRunning;
        /// <summary>null: Aegis's own toast window (AegisToast).</summary>
        public Action<string, bool> Toast;
        public Func<string, int, byte[]> Download = DefinitionsStore.Download;
        /// <summary>How a download runs: a background thread (the self-test runs it at once).</summary>
        public Action<Action> Background = work => new Thread(() => work()) { IsBackground = true, Name = "Aegis definitions" }.Start();

        public AegisService(AegisContext context)
        {
            ctx = context ?? throw new ArgumentNullException(nameof(context));
            log = ctx.Log ?? (_ => { });
            lang = string.IsNullOrEmpty(ctx.Lang) ? "ja" : ctx.Lang;
            GameRunning = () => Processes.ModdedGameRunning(ctx.GameDir);
        }

        public bool IsPorted => true;
        public event EventHandler Changed;
        public string EventsLogPath => EventsLog.PathIn(ctx.StateDir);

        string T(string key, params object[] args) => AegisText.Get(lang, key, args);

        DefinitionsStore NewDefinitions() => new DefinitionsStore(ctx.BundledDir, ctx.StateDir, Trusted, Revoked);

        // ------------------------------------------------------------------ start / stop (UI thread)
        public void Start()
        {
            if (started || stopped) return;
            started = true;
            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) => Tick();
            try { mutex = new Mutex(false, AppInfo.AegisMutexName); }
            catch (Exception ex)
            {
                offReason = "failed";
                Failed(ex);
                RaiseChanged();
                return;
            }
            if (TakeMutex()) StartOwned();
            else
            {
                offReason = "oldtray";
                log("Aegis: the old Aegis tray (Aegis.ps1) is running; Aegis starts here when it quits");
                timer.Start();
                RaiseChanged();
            }
        }

        bool TakeMutex()
        {
            try { ownsMutex = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }   // its owner ended without releasing it
            catch (Exception ex) { log("Aegis mutex: " + ex.Message); ownsMutex = false; }
            return ownsMutex;
        }

        /// <summary>Entry.Run after the mutex: prune, load, fetch in the background, the start scan, then the tray.</summary>
        void StartOwned()
        {
            offReason = null;
            try
            {
                try { Directory.CreateDirectory(ctx.StateDir); } catch (Exception) { }
                int pruned = EventsLog.Prune(ctx.StateDir, 30, Now());   // v0.5.5 privacy: event lines (player names) older than 30 days
                if (pruned > 0) log("Aegis: " + pruned + " events.log line(s) older than 30 days removed");
                var defs = NewDefinitions().Load();
                trayDefs = defs;
                lock (gate) { defsVersion = defs.Version; sigState = defs.SigState; }
                log("Aegis: definitions v" + defs.Version + " (signature state " + defs.SigState + ")");
                StartFetch(defs);
                Task.Run(() => RunScan(defs, "start", null, CancellationToken.None))
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted) Failed(t.Exception.GetBaseException());
                        Post(StartTray);
                    });
            }
            catch (Exception ex) { Failed(ex); }
            RaiseChanged();
        }

        /// <summary>Defs.FetchAsync: one download on a background thread (never two at once); the result is used from the next
        /// load (the next pre-launch scan, scan-only, or start).</summary>
        bool StartFetch(DefinitionsStore defs)
        {
            if (Interlocked.CompareExchange(ref fetching, 1, 0) != 0) return false;
            lock (gate) lastFetchAt = Now();
            var download = Download;
            try
            {
                Background(() =>
                {
                    try
                    {
                        string r = defs.Fetch(download);
                        if (!stopped) log("Aegis " + r);
                    }
                    finally { Interlocked.Exchange(ref fetching, 0); }
                });
                return true;
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref fetching, 0);
                log("Aegis definitions download: " + ex.Message);
                return false;
            }
        }

        /// <summary>After this long the next "open the window" or "play" downloads the definitions again.</summary>
        public static readonly TimeSpan RefetchAfter = TimeSpan.FromHours(12);

        /// <summary>A download is due: none yet, 12 hours since the last one began, or the clock was set back.</summary>
        internal static bool FetchDue(DateTime? last, DateTime now) => last == null || now < last.Value || now - last.Value >= RefetchAfter;

        public bool RefreshDefinitionsIfStale()
        {
            if (stopped || !ownsMutex || offReason != null) return false;   // not the tray (the old one runs, or not started)
            DateTime? last;
            lock (gate) last = lastFetchAt;
            if (!FetchDue(last, Now())) return false;
            DefinitionsStore fresh;
            try { fresh = NewDefinitions().Load(); }   // never the start's trayDefs: its Version may be older than the cache now
            catch (Exception ex) { log("Aegis definitions: " + ex.Message); return false; }
            bool started = StartFetch(fresh);
            if (started) log("Aegis: definitions download again (last one " + (last == null ? "never" : last.Value.ToString("yyyy-MM-dd HH:mm")) + ")");
            return started;
        }

        /// <summary>Self-test only: act as the tray (the self-test never takes the real Aegis mutex).</summary>
        internal void ActAsTrayForSelfTest()
        {
            ownsMutex = true;
            offReason = null;
        }

        /// <summary>Tray.StartTray: the watcher, "ready" when no game runs, one poll now.</summary>
        void StartTray()
        {
            if (stopped || watcher != null) return;
            watcher = new LogWatcher
            {
                LogPath = GameFolders.Join(ctx.GameDir ?? "", "BepInEx", "LogOutput.log"),
                GameRunning = GameRunning,
                Now = Now,
                Seconds = () => clock.Elapsed.TotalSeconds,
                FindTools = () => AegisScanner.FindTools(trayDefs, Probe),
                AddEvent = AddEvent,
                ShowToast = ShowToast,
                Lang = () => lang,
            };
            bool game = false;
            try { game = GameRunning(); } catch (Exception) { }
            if (!game) watcher.Ready();   // a running game gets the "watching" event instead
            timer.Start();
            Tick();
            RaiseChanged();
        }

        void Tick()
        {
            if (stopped) return;
            if (!ownsMutex)
            {
                if (offReason == "oldtray" && TakeMutex())
                {
                    log("Aegis: the old Aegis tray quit; starting here");
                    StartOwned();
                }
                return;
            }
            if (watcher == null) return;
            try { watcher.Poll(); }
            catch (Exception ex) { log("Aegis watch: " + ex.Message); }
            string now = watcher.State + "|" + watcher.Flagged + "|" + watcher.Removed;
            if (now != lastRaised) { lastRaised = now; RaiseChanged(); }
        }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            if (ctx.Ui != null) { try { AegisToast.CloseAll(); } catch (Exception) { } }
            if (mutex != null)
            {
                if (ownsMutex) { try { mutex.ReleaseMutex(); } catch (Exception) { } }
                ownsMutex = false;
                mutex.Dispose();
                mutex = null;
            }
        }

        public void Dispose() => Stop();

        public void SetLanguage(string l)
        {
            if (!string.IsNullOrEmpty(l)) lang = l;
            RaiseChanged();
        }

        // ------------------------------------------------------------------ events, toasts
        /// <summary>Tray.AddEvent: the list shown in the Aegis panel (200, newest first) and a line in events.log.</summary>
        void AddEvent(string text)
        {
            DateTime now = Now();
            lock (gate)
            {
                events.Add(now.ToString("HH:mm:ss") + "  " + text);
                if (events.Count > 200) events.RemoveAt(0);
            }
            EventsLog.Append(ctx.StateDir, now, text);
            RaiseChanged();
        }

        void ShowToast(string text, bool warning)
        {
            if (stopped) return;
            if (Toast != null) { Toast(text, warning); return; }
            string l = lang;
            Post(() => { if (!stopped) AegisToast.Show(text, warning, l); });
        }

        void Failed(Exception ex)
        {
            EventsLog.AegisLog(ctx.StateDir, Now(), "Aegis failed: " + ex);
            log("Aegis failed: " + ex.Message);
        }

        // ------------------------------------------------------------------ scans (worker threads)
        ScanOutcome RunScan(DefinitionsStore defs, string kind, Action<ScanProgress> progress, CancellationToken cancel)
        {
            while (!Monitor.TryEnter(scanGate, 250))
                if (cancel.IsCancellationRequested) return new ScanOutcome { Rows = new List<AegisCheck>(), Cancelled = true };
            try
            {
                var checks = AegisScanner.Build(ctx.GameDir ?? "", ctx.StateDir, defs, Probe);
                lock (gate)
                {
                    rows = checks; phase = "scanning"; scanStep = 0; lastKind = kind;
                    defsVersion = defs.Version; sigState = defs.SigState;
                }
                RaiseChanged();
                var o = AegisScanner.Run(checks,
                    step =>
                    {
                        lock (gate) scanStep = step;
                        try { progress?.Invoke(new ScanProgress { Step = step, Of = checks.Count }); } catch (Exception) { }
                        RaiseChanged();
                    },
                    (c, state) => { lock (gate) c.State = state; },
                    cancel);
                lock (gate)
                {
                    if (o.Cancelled) phase = "";
                    else { phase = "done"; lastWarnings = o.Warnings; lastSerious = o.Serious; scanSeq++; }
                }
                RaiseChanged();
                return o;
            }
            finally { Monitor.Exit(scanGate); }
        }

        public PreLaunchResult PreLaunchScan(Action<ScanProgress> progress, CancellationToken cancel)
        {
            try
            {
                try { Directory.CreateDirectory(ctx.StateDir); } catch (Exception) { }
                var defs = NewDefinitions().Load();
                var o = RunScan(defs, "prelaunch", progress, cancel);
                if (o.Cancelled)
                {
                    log("Aegis: the pre-launch scan was stopped at the deadline (nothing written)");
                    return new PreLaunchResult { Ran = false };
                }
                EventsLog.Append(ctx.StateDir, Now(), "pre-launch scan: " + (o.Serious > 0 ? "blocked (" + o.Serious + ")" : "ok"));
                string[] lines = o.RedLines(lang);
                try { File.WriteAllLines(Path.Combine(ctx.StateDir, "prelaunch-result.txt"), lines, new UTF8Encoding(true)); } catch (Exception) { }
                return new PreLaunchResult { Ran = true, Blocked = o.Serious > 0, Lines = lines, Keys = o.RedKeys() };
            }
            catch (Exception ex)
            {
                Failed(ex);   // never block the game because Aegis itself failed
                return new PreLaunchResult { Ran = false };
            }
        }

        public ScanSummary Rescan(Action<ScanProgress> progress)
        {
            var defs = trayDefs;
            if (defs == null || !ownsMutex) return new ScanSummary { Error = OffText() };
            var o = RunScan(defs, "rescan", progress, CancellationToken.None);
            return new ScanSummary { Warnings = o.Warnings, Serious = o.Serious };
        }

        public ScanSummary ScanOnly(Action<ScanProgress> progress)
        {
            // Aegis.ps1 -ScanOnly does nothing while a tray holds the mutex; in the app the tray is the app itself
            if (!ownsMutex) return new ScanSummary { Error = OffText() };
            var o = RunScan(NewDefinitions().Load(), "scanOnly", progress, CancellationToken.None);
            return new ScanSummary { Warnings = o.Warnings, Serious = o.Serious };
        }

        string OffText() => T(offReason == "oldtray" ? "c.oldtray" : "c.notstarted");

        // ------------------------------------------------------------------ snapshot
        public AegisSnapshot GetSnapshot()
        {
            string l = lang;
            var s = new AegisSnapshot();
            lock (gate)
            {
                var w = watcher;
                s.State = !ownsMutex || offReason != null ? "off" : w != null ? w.State : "idle";
                s.OldTray = offReason == "oldtray";
                s.Detected = w != null ? w.Flagged : 0;
                s.Kicked = w != null ? w.Removed : 0;
                s.LastScanSerious = lastSerious;
                s.LastScanWarnings = lastWarnings;
                foreach (var c in rows)
                {
                    bool done = c.State >= 2;
                    s.Rows.Add(new AegisRow { Key = c.Key, Title = c.Title(l), Detail = done ? c.Detail.Render(l) : "", Fix = done ? c.Fix.Render(l) : "", State = c.State });
                }
                for (int i = events.Count - 1; i >= 0; i--) s.Events.Add(events[i]);
                s.DefsVersion = defsVersion;
                s.SigState = sigState;
                s.Phase = phase;
                s.Kind = lastKind;
                s.Scan = scanSeq;
                s.Summary = offReason == "oldtray" ? AegisText.Get(l, "c.oldtray")
                          : phase == "scanning" ? AegisText.Get(l, "scanning", Math.Min(scanStep, rows.Count), rows.Count)
                          : phase == "done" ? (lastKind == "prelaunch" ? (lastSerious > 0 ? AegisText.Get(l, "blocked", lastSerious) : AegisText.Get(l, "go"))
                                                                   // v1.1: a red row in the start scan / "scan again" says so (the card at the bottom right
                                                                   // stays for it), instead of counting it among the warnings
                                                                   : (lastSerious > 0 ? AegisText.Get(l, "found", lastSerious)
                                                                    : lastWarnings > 0 ? AegisText.Get(l, "done.warn", lastWarnings) : AegisText.Get(l, "done")))
                          : "";
            }
            return s;
        }

        // ------------------------------------------------------------------ UI thread
        void RaiseChanged()
        {
            if (Interlocked.Exchange(ref changePending, 1) == 1) return;
            if (ctx.Ui == null)
            {
                Interlocked.Exchange(ref changePending, 0);
                Changed?.Invoke(this, EventArgs.Empty);   // the self-test: no window, synchronous
                return;
            }
            if (!Post(() => { Interlocked.Exchange(ref changePending, 0); if (!stopped) Changed?.Invoke(this, EventArgs.Empty); }))
                Interlocked.Exchange(ref changePending, 0);
        }

        bool Post(Action a)
        {
            var ui = ctx.Ui;
            if (ui == null) { a(); return true; }
            if (ui.IsDisposed || !ui.IsHandleCreated) return false;
            try { ui.BeginInvoke(a); return true; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
