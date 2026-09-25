// Command line of the app: the folder and language options of the launcher (PORT-MAP 2.1 row 1), written --game-dir X,
// --game-dir=X or the launcher's -GameDir X, plus the modes of v0.4 (--action / --tray / --scan-only / --autolaunch /
// --windowed, PORT-MAP 14.5 and SPEC 6.4), --uninstall [--quiet] and --self-test <dir>.
// One option is not in a published build at all: --source-dir (see below). docs\CODE-SIGNING.md lists the whole set.
// --uninstall (with --quiet) is what the Apps & Features entry runs (Shortcuts.AppsAndFeatures.Register writes exactly
// those two spellings): it must be understood here, or Windows' Uninstall button would only open the app.
// NOTHING IS QUIETLY IGNORED any more. Until v0.3 an option this file did not know went into a list that was only
// logged, and the app opened its window as if nothing had been typed - which is how "--uninstall" shipped doing nothing
// (PORT-MAP 15.1). A word this file does not know now lands in Unknown / Extra, and Startup turns that into a usage
// error with a message and exit code 1.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Starpocket.Client.Core
{
    internal sealed class CommandLine
    {
        public string SelfTestDir;
        public bool SelfTest;
        public string GameDir, SteamDir, DesktopDir, Language;
        /// <summary>--source-dir: the mod's own source folder. A DEVELOPER BUILD ONLY - in a Release build the option
        /// does not exist, so this stays null and developer mode can only come from the exe's own folder (v0.4 review;
        /// see GameFolders.ResolveSource).</summary>
        public string SourceDir;
        public bool Friend;
        /// <summary>--devtools: WebView2's developer tools (Debug builds only).</summary>
        public bool DevTools;
        /// <summary>--uninstall: the Apps &amp; Features entry's UninstallString. No window, no tray, no WebView2.</summary>
        public bool Uninstall;
        /// <summary>--quiet: with --uninstall, the QuietUninstallString: no confirm dialog either. On a usage error it
        /// also keeps the message on the console instead of a box.</summary>
        public bool Quiet;
        /// <summary>--action install|check|report|status (the launcher's -Action): the job runs with no window at all and
        /// the exit code says how it went. Empty when it was not given; the value as it was typed (Startup checks it).</summary>
        public string Action;
        /// <summary>--tray (Aegis.ps1 -Tray): the app starts in the notification area, no window.</summary>
        public bool Tray;
        /// <summary>--scan-only (Aegis.ps1 -ScanOnly): one Aegis scan, then the process ends.</summary>
        public bool ScanOnly;
        /// <summary>--autolaunch (the launcher's -AutoLaunch): play as soon as the app is ready.</summary>
        public bool AutoLaunch;
        /// <summary>--windowed (the launcher's -Windowed): the game starts in a 1600x900 window.</summary>
        public bool Windowed;
        /// <summary>--verify-download: fetch the BepInEx zip the app has pinned, check it, unpack it into a folder of
        /// %TEMP% and delete the lot again. The game folder is never named, read or written (SPEC "--verify-download",
        /// src\Core\BepVerifier.cs). Run once before a release, by hand.</summary>
        public bool VerifyDownload;

        /// <summary>Options this app does not know ("--trey"): a usage error, never ignored.</summary>
        public readonly List<string> Unknown = new List<string>();
        /// <summary>Words that are not options at all (a file dropped on the exe): also a usage error.</summary>
        public readonly List<string> Extra = new List<string>();
        /// <summary>Options that need a value and were given none ("--action" at the end of the line, or "--action
        /// --language en"): a usage error too, never "as if it had not been typed".</summary>
        public readonly List<string> Empty = new List<string>();
        /// <summary>Options given twice with two DIFFERENT values ("--action install --action status"). Until the v0.4
        /// review the last one quietly won, so a line that asked for two different jobs did one of them without a
        /// word - the same shape as two modes at once, which has always been refused. The same value written twice is
        /// not a mistake and never lands here.</summary>
        public readonly List<string> Twice = new List<string>();

        /// <summary>Options with no value of their own.</summary>
        static readonly Dictionary<string, string> FlagOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["friend"] = "friend",
            ["devtools"] = "devtools",
            ["uninstall"] = "uninstall",
            ["quiet"] = "quiet", ["silent"] = "quiet",
            ["tray"] = "tray",
            ["scan-only"] = "scan-only", ["scanonly"] = "scan-only",
            ["verify-download"] = "verify-download", ["verifydownload"] = "verify-download",
            ["autolaunch"] = "autolaunch", ["auto-launch"] = "autolaunch",
            ["windowed"] = "windowed",
        };

        static readonly Dictionary<string, string> ValueOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["self-test"] = "self-test", ["selftest"] = "self-test",
            ["action"] = "action",
            ["game-dir"] = "game-dir", ["gamedir"] = "game-dir",
            ["steam-dir"] = "steam-dir", ["steamdir"] = "steam-dir",
#if DEBUG
            // --source-dir points the app at the MOD's own source, which is the only way into developer mode and so
            // the only way to the 「再ビルド」 button that starts the compiler. A published exe must not have a door
            // like that (v0.4 review: a signed exe plus any folder holding a PocketRoles.csproj was a way to run
            // whatever that project file said), so the option exists in a developer's build ONLY. In a Release build
            // the word is simply not an option of this app, and a command line carrying it is a usage error with a
            // message - never quietly ignored, which is the mistake PORT-MAP 15.1 is about.
            ["source-dir"] = "source-dir", ["sourcedir"] = "source-dir",
#endif
            ["desktop-dir"] = "desktop-dir", ["desktopdir"] = "desktop-dir",
            ["language"] = "language", ["lang"] = "language",
        };

        /// <summary>Every option this app knows, as it is written with two dashes (for the usage text and the self-test).</summary>
        public static IEnumerable<string> KnownOptions
        {
            get
            {
                foreach (var k in FlagOptions.Keys) yield return "--" + k;
                foreach (var k in ValueOptions.Keys) yield return "--" + k;
            }
        }

        /// <summary>Is this word one of this app's own options (whichever way it is written)?</summary>
        static bool IsKnownOption(string a)
        {
            if (string.IsNullOrEmpty(a)) return false;
            string name = a.StartsWith("--", StringComparison.Ordinal) ? a.Substring(2)
                        : a.StartsWith("-", StringComparison.Ordinal) || a.StartsWith("/", StringComparison.Ordinal) ? a.Substring(1)
                        : null;
            if (name == null) return false;
            int eq = name.IndexOf('=');
            if (eq > 0) name = name.Substring(0, eq);
            return FlagOptions.ContainsKey(name) || ValueOptions.ContainsKey(name);
        }

        public static CommandLine Parse(string[] args)
        {
            var c = new CommandLine();
            var given = new Dictionary<string, string>(StringComparer.Ordinal);   // value options already seen
            args = args ?? new string[0];
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i] ?? "";
                if (a.Trim().Length == 0) continue;   // an empty argument (a shortcut with two spaces) is nothing, not a mistake
                string name = a.StartsWith("--", StringComparison.Ordinal) ? a.Substring(2) : a.StartsWith("-", StringComparison.Ordinal) || a.StartsWith("/", StringComparison.Ordinal) ? a.Substring(1) : null;
                if (name == null) { c.Extra.Add(a); continue; }
                string inline = null;
                int eq = name.IndexOf('=');
                if (eq > 0) { inline = name.Substring(eq + 1); name = name.Substring(0, eq); }
                string flag;
                if (FlagOptions.TryGetValue(name, out flag))
                {
                    switch (flag)
                    {
                        case "friend": c.Friend = true; break;
                        case "devtools": c.DevTools = true; break;
                        case "uninstall": c.Uninstall = true; break;
                        case "quiet": c.Quiet = true; break;
                        case "tray": c.Tray = true; break;
                        case "scan-only": c.ScanOnly = true; break;
                        case "verify-download": c.VerifyDownload = true; break;
                        case "autolaunch": c.AutoLaunch = true; break;
                        case "windowed": c.Windowed = true; break;
                    }
                    continue;
                }
                string key;
                if (!ValueOptions.TryGetValue(name, out key)) { c.Unknown.Add(a); continue; }
                string value = inline;
                if (value == null)
                {
                    // the next word is the value - unless it is an option of this app, which would mean the value was
                    // simply forgotten ("--action --language en" is a mistake, not an action called "--language")
                    if (i + 1 < args.Length && !IsKnownOption(args[i + 1])) value = args[++i];
                    else value = "";
                }
                if (value.Trim().Length == 0) c.Empty.Add("--" + key);
                string before;
                if (given.TryGetValue(key, out before))
                {
                    // "--action install --action status": two different answers to one question. Startup makes this the
                    // same usage error as two modes at once, instead of letting the later one win in silence.
                    if (!string.Equals(before, value, StringComparison.Ordinal) && c.Twice.IndexOf("--" + key) < 0) c.Twice.Add("--" + key);
                }
                else given[key] = value;
                switch (key)
                {
                    case "self-test": c.SelfTest = true; c.SelfTestDir = value; break;
                    case "action": c.Action = value ?? ""; break;
                    case "game-dir": c.GameDir = value; break;
                    case "steam-dir": c.SteamDir = value; break;
                    case "source-dir": c.SourceDir = value; break;
                    case "desktop-dir": c.DesktopDir = value; break;
                    case "language": c.Language = value; break;
                }
            }
            return c;
        }
    }
}
