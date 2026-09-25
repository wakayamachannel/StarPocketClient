// Which programs run. Names come from the process list; the exe PATH of an "Among Us" is read with
// QueryFullProcessImageName (PROCESS_QUERY_LIMITED_INFORMATION only - no memory of another process is ever opened),
// so the app can tell the mod's copy from plain Among Us.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Starpocket.Client.Core
{
    internal static class Processes
    {
        /// <summary>Get-Process -Name: a process of that name runs (any folder).</summary>
        public static bool IsRunning(string name)
        {
            Process[] ps = null;
            try
            {
                ps = Process.GetProcessesByName(name);
                return ps.Length > 0;
            }
            catch (Exception) { return false; }
            finally { if (ps != null) foreach (var p in ps) p.Dispose(); }
        }

        /// <summary>"Among Us" of any folder (the launcher's Game-Running; PORT-MAP 9.1 L-3).</summary>
        public static bool GameRunning() => IsRunning("Among Us");

        public static bool SteamRunning() => IsRunning("steam");

        /// <summary>Test-ModdedGameRunning: an "Among Us" whose exe is &lt;Modded&gt;\Among Us.exe; one whose path cannot be read
        /// counts as ours. The path is read with QueryFullProcessImageName (PORT-MAP 9.1 L-9: same result, no memory access).</summary>
        /// Since v0.2 this also decides what Aegis watches: plain Among Us from Steam is not the mod's copy, so the
        /// watcher stays idle for it (PORT-MAP 13.5; before, any process NAMED "Among Us" turned the tray to 監視中).
        public static bool ModdedGameRunning(string modded) => IsModded(GameExePaths(), modded);

        /// <summary>The exe path of every running "Among Us" (null for one whose path cannot be read).</summary>
        public static List<string> GameExePaths()
        {
            var paths = new List<string>();
            Process[] ps = null;
            try
            {
                ps = Process.GetProcessesByName("Among Us");
                foreach (var p in ps) paths.Add(Native.TryGetProcessImagePath(p.Id));
            }
            catch (Exception) { }
            finally { if (ps != null) foreach (var p in ps) p.Dispose(); }
            return paths;
        }

        /// <summary>Is one of these running exes the mod's copy? A path that could not be read counts as the mod's copy
        /// (Aegis then watches instead of missing a game). Both sides are compared as full long paths, so a short (8.3)
        /// folder name in --game-dir still matches the game's own path.</summary>
        public static bool IsModded(IEnumerable<string> runningExePaths, string modded)
        {
            if (runningExePaths == null) return false;
            string exe = LongPath(GameFolders.Join(modded ?? "", "Among Us.exe"));
            foreach (var path in runningExePaths)
            {
                if (path == null) return true;
                if (string.Equals(LongPath(path), exe, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>The full path with long folder names (C:\Users\SOMEON~1\x and C:\Users\someone\x are one path).</summary>
        public static string LongPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string full = path;
            try { full = Path.GetFullPath(path); } catch (Exception) { }
            return Native.TryGetLongPath(full) ?? full;
        }
    }
}
