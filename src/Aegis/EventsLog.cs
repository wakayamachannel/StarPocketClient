// %LOCALAPPDATA%\PocketRoles\Aegis\events.log and aegis.log (aegis\Aegis.ps1: Tray.AddEvent, Entry.PreLaunch,
// class EventsLog and Write-AegisLog; v0.5.5 lines 24-27, 1381-1387, 1444-1496, 1511). Same files as the ps1 (shared
// with it), same line format, same encoding:
//   events.log  "yyyy-MM-dd HH:mm:ss  text" (two spaces), CRLF, UTF-8 (File.AppendAllText: a BOM only when the file is new)
//   aegis.log   "yyyy-MM-dd HH:mm:ss text", CRLF, UTF-8 (Add-Content -Encoding UTF8: a BOM only when the file is new)
// The times are formatted with the current culture like the ps1 (PORT-MAP 9.2 A-13). The ps1's tray and its -PreLaunch
// processes appended without a lock (9.2 A-9); inside the app the writers share one lock (the file format is the same).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Starpocket.Client.Aegis
{
    internal static class EventsLog
    {
        static readonly object Gate = new object();

        public static string PathIn(string stateDir) => Path.Combine(stateDir, "events.log");

        /// <summary>One line "yyyy-MM-dd HH:mm:ss  text" appended (Tray.AddEvent / Entry.PreLaunch). Never throws.</summary>
        public static void Append(string stateDir, DateTime now, string text)
        {
            lock (Gate)
            {
                try { File.AppendAllText(PathIn(stateDir), now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, Encoding.UTF8); } catch (Exception) { }
            }
        }

        /// <summary>Write-AegisLog: "yyyy-MM-dd HH:mm:ss text" appended to aegis.log (Aegis's own failures). Never throws.</summary>
        public static void AegisLog(string stateDir, DateTime now, string text)
        {
            lock (Gate)
            {
                try { File.AppendAllText(Path.Combine(stateDir, "aegis.log"), now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + "\r\n", Encoding.UTF8); } catch (Exception) { }
            }
        }

        /// <summary>
        /// v0.5.5 privacy: events.log holds player names from the game log. Lines whose leading "yyyy-MM-dd HH:mm:ss" (local
        /// time) is older than keepDays are dropped when the tray starts (inside its one-at-a-time mutex); a line without a
        /// stamp follows the line before it (kept at the top). The file is rewritten through events.log.tmp (UTF-8 with BOM)
        /// only when a line goes. Returns the lines dropped; never throws. (EventsLog.Prune, ported line for line; the clock
        /// is a parameter for the self-test.)
        /// </summary>
        public static int Prune(string stateDir, int keepDays, DateTime now)
        {
            string path = null, tmp = null;
            lock (Gate)
            {
                try
                {
                    path = Path.Combine(stateDir, "events.log");
                    if (!File.Exists(path)) return 0;
                    tmp = path + ".tmp";
                    string[] lines;
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    using (var sr = new StreamReader(fs, Encoding.UTF8, true))
                    {
                        var list = new List<string>();
                        string l;
                        while ((l = sr.ReadLine()) != null) list.Add(l);
                        lines = list.ToArray();
                    }
                    DateTime limit = now.AddDays(-keepDays);
                    var keep = new List<string>(lines.Length);
                    bool keepPrev = true;
                    int dropped = 0;
                    foreach (var line in lines)
                    {
                        DateTime t = DateTime.MinValue;
                        bool stamped = line.Length >= 19 && DateTime.TryParseExact(line.Substring(0, 19), "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out t);
                        if (stamped) keepPrev = t >= limit;
                        if (keepPrev) keep.Add(line);
                        else dropped++;
                    }
                    if (dropped == 0) return 0;
                    var sb = new StringBuilder();
                    foreach (var line in keep) sb.Append(line).Append(Environment.NewLine);
                    File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                    File.Copy(tmp, path, true);
                    File.Delete(tmp);
                    return dropped;
                }
                catch (Exception)
                {
                    try { if (tmp != null && File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                    return 0;
                }
            }
        }
    }
}
