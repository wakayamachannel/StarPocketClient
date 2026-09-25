// Watching a game (aegis\Aegis.ps1 class Tray: Poll, ReadLog, Line, Balloon, UpdateIcon; v0.5.5 lines 1184-1407),
// ported line for line (PORT-MAP 3.11). Every second: is "Among Us" running (any folder, 9.2 A-11)? While it is, the
// new lines of <game>\BepInEx\LogOutput.log are read and the mod's Aegis lines turned into events and toasts; every 30
// polls the process names are checked for cheat tools (each name told once).
// The outside world (clock, processes, toasts, events) is given by the owner, so the self-test drives it with fakes.
// The ps1's own end ("the launcher is closed and no game runs") is not here: in the app Aegis runs until the app quits.
// UI thread only (like the ps1's WinForms timer).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Starpocket.Client.Aegis
{
    internal sealed class LogWatcher
    {
        static readonly Regex ReRemoved = new Regex(@"CheatDetector: removing #\d+ (.+?) \(client \d+\) with a room ban: (\w+)");
        static readonly Regex ReFlag = new Regex(@"CheatDetector: (\[test\] )?(\w+) \((Certain|Repeat|Notice)\) #\d+ (.+?) \(client");
        static readonly Regex ReLoaded = new Regex(@"PocketRoles v([\d.]+) loaded");

        /// <summary>&lt;game&gt;\BepInEx\LogOutput.log.</summary>
        public string LogPath;
        public Func<bool> GameRunning = () => false;
        /// <summary>Wall clock (DateTime.Now): the log's write time and the 4-second toast rule.</summary>
        public Func<DateTime> Now = () => DateTime.Now;
        /// <summary>Seconds of a monotonic clock (the ps1's Stopwatch): the 12-second warning after a removal.</summary>
        public Func<double> Seconds = () => 0;
        /// <summary>The cheat tools running now (AegisScanner.FindTools with the tray's definitions).</summary>
        public Func<IList<string>> FindTools = () => new string[0];
        public Action<string> AddEvent = _ => { };
        /// <summary>A toast: text, warning (amber, 6 s) or information (green, 3.5 s).</summary>
        public Action<string, bool> ShowToast = (t, w) => { };
        public Func<string> Lang = () => "ja";
        /// <summary>At most this many bytes per read (4 MiB, like the ps1; the self-test lowers it).</summary>
        public int MaxRead = 4 * 1024 * 1024;

        bool watching;
        int flagged, removed;
        double alertUntil;
        DateTime lastBalloon = DateTime.MinValue;
        long logPos = -1, logBaseline = -1;
        DateTime gameSeenAt = DateTime.MinValue;
        readonly Decoder decoder = Encoding.UTF8.GetDecoder();
        int toolTick;
        readonly HashSet<string> toolsSeen = new HashSet<string>();
        string logRest = "", modVersion = "";

        public bool Watching => watching;
        public int Flagged => flagged;
        public int Removed => removed;
        public string ModVersion => modVersion;
        /// <summary>Within 12 seconds of a removal.</summary>
        public bool Alert => Seconds() < alertUntil;
        /// <summary>The tray icon of the ps1: alert (amber) → "kicked", watching (green), else "idle" (teal).</summary>
        public string State => Alert ? "kicked" : watching ? "watching" : "idle";

        string T(string key, params object[] args) => AegisText.Get(Lang(), key, args);

        /// <summary>Once a second (Tray.Poll).</summary>
        public void Poll()
        {
            bool game = GameRunning();
            if (game && !watching)
            {
                watching = true; flagged = 0; removed = 0;
                // BepInEx rewrites the log when its chainloader starts (seconds after the process, longer after an interop
                // rebuild). A log written before the game was seen is last run's: wait until it shrinks or is written again.
                gameSeenAt = Now();
                logPos = -2;
                logBaseline = LogLength();
                logRest = ""; decoder.Reset();
                modVersion = "";
                AddEvent(T("b.watch0"));
            }
            else if (!game && watching)
            {
                watching = false;
                Balloon(T("b.stop"), false);
                AddEvent(T("b.stop"));
            }
            if (watching) ReadLog();
            if (++toolTick >= 30)
            {
                // a cheat tool started after the scan (for any game): tell the host once per tool
                toolTick = 0;
                foreach (var t in FindTools())
                    if (toolsSeen.Add(t)) { string msg = T("tools.warn", t); AddEvent(msg); Balloon(msg, true, true); }
            }
        }

        /// <summary>The first toast when the tray starts and no game runs (the ps1's StartTray).</summary>
        public void Ready() => Balloon(T("b.ready"), false);

        public void ReadLog()
        {
            string path = LogPath;
            try
            {
                if (!File.Exists(path)) return;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long len = fs.Length;
                    if (logPos == -2)
                    {
                        bool rewritten = len < logBaseline || File.GetLastWriteTime(path) >= gameSeenAt.AddSeconds(-2);
                        if (!rewritten) return;   // still the previous run's log
                        logPos = 0; logRest = ""; decoder.Reset();
                    }
                    if (len < logPos) { logPos = 0; logRest = ""; decoder.Reset(); }
                    if (len == logPos) return;
                    fs.Position = logPos;
                    var buf = new byte[Math.Min(len - logPos, MaxRead)];
                    int n = fs.Read(buf, 0, buf.Length);
                    logPos += n;
                    var chars = new char[decoder.GetCharCount(buf, 0, n)];
                    int cn = decoder.GetChars(buf, 0, n, chars, 0);   // keeps a character split across two reads
                    string text = logRest + new string(chars, 0, cn);
                    int cut = text.LastIndexOf('\n');
                    if (cut < 0) { logRest = text; return; }
                    logRest = text.Substring(cut + 1);
                    foreach (var line in text.Substring(0, cut).Split('\n')) Line(line);
                }
            }
            catch (Exception) { }
        }

        long LogLength()
        {
            try { var fi = new FileInfo(LogPath); return fi.Exists ? fi.Length : 0; }
            catch (Exception) { return 0; }
        }

        /// <summary>One line of the game's log (Tray.Line).</summary>
        public void Line(string line)
        {
            Match m;
            if ((m = ReLoaded.Match(line)).Success)
            {
                modVersion = m.Groups[1].Value;
                Balloon(T("b.watch", "v" + modVersion), false);
                return;
            }
            if ((m = ReRemoved.Match(line)).Success)
            {
                removed++;
                alertUntil = Seconds() + 12;
                string msg = T("b.removed", m.Groups[1].Value.Trim(), AegisText.Rule(Lang(), m.Groups[2].Value));
                AddEvent(msg);
                Balloon(msg, true, true);
                return;
            }
            if ((m = ReFlag.Match(line)).Success)
            {
                string rule = m.Groups[2].Value;
                if (rule == "Callout" || rule == "CalloutRepeat" || rule == "VoteCallout") return;   // names impostors: never shown outside the game
                bool test = m.Groups[1].Success;
                if (!test) flagged++;
                string msg = (test ? T("test") : "") + T("b.flag", m.Groups[4].Value.Trim(), AegisText.Rule(Lang(), rule));
                AddEvent(msg);
                if (m.Groups[3].Value != "Notice" || test) Balloon(msg, true);
            }
        }

        /// <summary>Tray.Balloon: Aegis's own toast (Windows hides balloon tips during a game). One that is not forced is
        /// dropped within 4 seconds of the previous toast (9.2 A-3); every toast shown restarts the 4 seconds.</summary>
        public void Balloon(string text, bool warning, bool force = false)
        {
            DateTime now = Now();
            if (!force && (now - lastBalloon).TotalSeconds < 4) return;
            lastBalloon = now;
            ShowToast(text, warning);
        }
    }
}
