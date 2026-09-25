// Where the launcher folder (Src) and the mod's game copy (Modded) are (PORT-MAP 3.1 / 3.2; ps1:55-84).
// Pure functions: every input (arguments, environment, Desktop, LOCALAPPDATA) is passed in, so --self-test can use fake folders.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;

namespace Starpocket.Client.Core
{
    internal sealed class ModdedInputs
    {
        public string GameDirArg;      // --game-dir / -GameDir
        public string EnvGameDir;      // POCKETROLES_GAMEDIR
        public bool DevMode;
        public string Src;
        public string DesktopDirArg;   // --desktop-dir / -DesktopDir
        public string DesktopDefault;  // Environment.GetFolderPath(Desktop) (after OneDrive redirection)
        public string EnvOneDrive;     // OneDrive (OneDriveCommercial is not looked at, like the ps1)
        public string LocalAppData;    // LOCALAPPDATA
    }

    internal static class GameFolders
    {
        public const string CopyFolderName = "Among Us PocketRoles";

        /// <summary>Join-Path that never throws. .NET Framework's Path.Combine throws on a character Windows does not allow in
        /// a path (" &lt; &gt; | ..., e.g. POCKETROLES_GAMEDIR set together with its quotes); the ps1's Join-Path does not, and
        /// its Test-Path then says "not there". So the text is joined with '\' as it is: the path simply does not exist
        /// (PathExists false, "not found / not installed"), never a crash.</summary>
        public static string Join(string a, string b)
        {
            try { return Path.Combine(a ?? "", b ?? ""); }
            catch (ArgumentException)
            {
                string left = (a ?? "").TrimEnd('\\', '/');
                string right = (b ?? "").TrimStart('\\', '/');
                return left.Length == 0 ? right : right.Length == 0 ? left : left + "\\" + right;
            }
        }

        public static string Join(string a, string b, string c) => Join(Join(a, b), c);

        public static string Join(string a, string b, string c, string d) => Join(Join(Join(a, b), c), d);

        /// <summary>Test-Path: a file or a folder of that name.</summary>
        public static bool PathExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return File.Exists(path) || Directory.Exists(path); } catch (Exception) { return false; }
        }

        /// <summary>Split-Path -Parent: the parent folder (a trailing \ is ignored); the root stays itself.</summary>
        public static string Parent(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string p = path;
            while (p.Length > 3 && (p.EndsWith("\\", StringComparison.Ordinal) || p.EndsWith("/", StringComparison.Ordinal))) p = p.Substring(0, p.Length - 1);
            string parent = null;
            try { parent = Path.GetDirectoryName(p); } catch (Exception) { }
            return string.IsNullOrEmpty(parent) ? p : parent;
        }

        /// <summary>The Desktop the launcher uses: --desktop-dir, else the real Desktop.</summary>
        public static string Desktop(string desktopDirArg, string desktopDefault) => string.IsNullOrEmpty(desktopDirArg) ? desktopDefault : desktopDirArg;

        /// <summary>PORT-MAP 3.1: the first that applies - argument, POCKETROLES_GAMEDIR, developer mode (the folder next to Src,
        /// else Desktop), friend mode (Desktop, or %LOCALAPPDATA%\PocketRoles when the Desktop is inside OneDrive and no copy
        /// is on the Desktop yet, only without --desktop-dir).</summary>
        public static string ResolveModded(ModdedInputs i)
        {
            if (!string.IsNullOrEmpty(i.GameDirArg)) return i.GameDirArg;
            if (!string.IsNullOrEmpty(i.EnvGameDir)) return i.EnvGameDir;
            string desktop = Desktop(i.DesktopDirArg, i.DesktopDefault);
            if (i.DevMode)
            {
                string m = GameFolders.Join(Parent(i.Src), CopyFolderName);
                if (!PathExists(GameFolders.Join(m, "Among Us.exe"))) m = GameFolders.Join(desktop, CopyFolderName);
                return m;
            }
            string modded = GameFolders.Join(desktop, CopyFolderName);
            if (string.IsNullOrEmpty(i.DesktopDirArg) && !string.IsNullOrEmpty(i.EnvOneDrive))
            {
                string od = i.EnvOneDrive.TrimEnd('\\') + "\\";
                if (desktop.StartsWith(od, StringComparison.OrdinalIgnoreCase) && !PathExists(GameFolders.Join(modded, "Among Us.exe")))
                    modded = GameFolders.Join(i.LocalAppData ?? "", @"PocketRoles\" + CopyFolderName);
            }
            return modded;
        }

        /// <summary>True only in a build made with DEBUG, i.e. a build the author made for themselves. A RELEASE exe -
        /// the one that is published and signed - can never be in developer mode (see <see cref="ResolveSource"/>),
        /// and so can never reach the 「再ビルド」 button or the compiler behind it.</summary>
        /// (a field rather than a const: a const would let the compiler fold the tests that read it away, and warn)
        public static readonly bool DeveloperBuild =
#if DEBUG
            true;
#else
            false;
#endif

        /// <summary>PORT-MAP 3.2: --source-dir (a developer build only), else the exe's own folder.
        ///
        /// v0.4 review, and this is the whole point of it. 「開発モードは作者の PC だけ」 was not true: --source-dir
        /// survived into the published exe and searched four folders above it for PocketRoles.csproj, so ANY folder
        /// holding a file of that name put a released, signed StarPocket Client into developer mode - and developer
        /// mode draws a 「再ビルド」 button that hands the folder to dotnet.exe, which does whatever the project file
        /// in it says, cmd.exe included. Both roads are closed: --source-dir does not exist in a Release build
        /// (CommandLine), and the search above the exe is gone, so the only folder that can be the source is the one
        /// the exe is in - and anyone who can write a file next to the exe could replace the exe itself.</summary>
        public static string ResolveSource(string sourceDirArg, string exeDir) => ResolveSource(sourceDirArg, exeDir, DeveloperBuild);

        /// <param name="developerBuild">false is what a published exe does, whatever it was given.</param>
        internal static string ResolveSource(string sourceDirArg, string exeDir, bool developerBuild)
        {
            if (developerBuild && !string.IsNullOrEmpty(sourceDirArg)) return sourceDirArg;
            return exeDir;
        }

        /// <summary>Developer mode = not --friend and Src\PocketRoles.csproj exists (ps1:58). Src is the exe's own
        /// folder unless this is a developer build with --source-dir, so in a published exe this asks one question
        /// only: is the mod's project file sitting beside me?</summary>
        public static bool IsDevMode(bool friend, string src) => !friend && PathExists(GameFolders.Join(src ?? "", "PocketRoles.csproj"));
    }

    /// <summary>The paths derived from the game copy (ps1:77-84).</summary>
    internal sealed class ModPaths
    {
        public string Modded, DllPath, CfgPath, LogPath, LogArchiveDir, GameExe;

        public static ModPaths For(string modded) => new ModPaths
        {
            Modded = modded,
            GameExe = GameFolders.Join(modded, "Among Us.exe"),
            DllPath = GameFolders.Join(modded, @"BepInEx\plugins\PocketRoles.dll"),
            CfgPath = GameFolders.Join(modded, @"BepInEx\config\jp.pocketroles.mod.cfg"),
            LogPath = GameFolders.Join(modded, @"BepInEx\LogOutput.log"),
            LogArchiveDir = GameFolders.Join(modded, @"BepInEx\PocketRoles\logs"),
        };
    }

    /// <summary>Files the launcher opens in Notepad (PORT-MAP 7.1 openReadme; ps1 Get-ReadmePath).</summary>
    internal static class LauncherFiles
    {
        /// <summary>Developer: in Src, friend: in the game copy. zh-CN: README.zh-CN.md then README.md, en: README.en.md then
        /// README.md, ja: README.md. When none exists, README.md there (the caller then says "not found").</summary>
        public static string ReadmePath(bool devMode, string src, string modded, string lang)
        {
            string b = devMode ? src : modded;
            string[] names = lang == "zh-CN" ? new[] { "README.zh-CN.md", "README.md" } : lang == "en" ? new[] { "README.en.md", "README.md" } : new[] { "README.md" };
            foreach (var n in names)
            {
                string p = GameFolders.Join(b, n);
                if (GameFolders.PathExists(p)) return p;
            }
            return GameFolders.Join(b, "README.md");
        }
    }

    // launcher-state.json (read, written and taken over) is src\Core\LauncherStateFile.cs since v0.2.

    /// <summary>Steam's copy of Among Us (PORT-MAP 3.3; ps1:617-645).</summary>
    internal static class SteamLocator
    {
        public const string DefaultSteamRoot = @"C:\Program Files (x86)\Steam";
        static readonly System.Text.RegularExpressions.Regex VdfPath = new System.Text.RegularExpressions.Regex("\"path\"\\s+\"([^\"]+)\"");

        /// <summary>SteamPath / InstallPath of HKCU\Software\Valve\Steam, HKLM\SOFTWARE\WOW6432Node\Valve\Steam and
        /// HKLM\SOFTWARE\Valve\Steam (HKLM through the 64-bit view, like the 64-bit PowerShell), '/' made '\'.</summary>
        public static List<string> RegistryRoots()
        {
            var roots = new List<string>();
            var keys = new[]
            {
                new { Hive = Microsoft.Win32.RegistryHive.CurrentUser, Path = @"Software\Valve\Steam" },
                new { Hive = Microsoft.Win32.RegistryHive.LocalMachine, Path = @"SOFTWARE\WOW6432Node\Valve\Steam" },
                new { Hive = Microsoft.Win32.RegistryHive.LocalMachine, Path = @"SOFTWARE\Valve\Steam" },
            };
            foreach (var k in keys)
            {
                try
                {
                    using (var bk = Microsoft.Win32.RegistryKey.OpenBaseKey(k.Hive, Microsoft.Win32.RegistryView.Registry64))
                    using (var key = bk.OpenSubKey(k.Path))
                    {
                        if (key == null) continue;
                        foreach (var n in new[] { "SteamPath", "InstallPath" })
                        {
                            var v = key.GetValue(n) as string;
                            if (!string.IsNullOrEmpty(v)) roots.Add(v.Replace('/', '\\'));
                        }
                    }
                }
                catch (Exception) { }
            }
            return roots;
        }

        /// <summary>1. override (--steam-dir / POCKETROLES_STEAMDIR) as it is  2. launcher-state.json steamDir when it holds
        /// Among Us.exe  3. each root (then C:\Program Files (x86)\Steam) and the libraries in its libraryfolders.vdf: the first
        /// steamapps\common\Among Us with Among Us.exe. Duplicates are dropped case-sensitively (Select-Object -Unique).</summary>
        public static string Find(string steamOverride, string stateSteamDir, IEnumerable<string> registryRoots, string defaultRoot = DefaultSteamRoot)
        {
            if (!string.IsNullOrEmpty(steamOverride)) return steamOverride;
            if (!string.IsNullOrEmpty(stateSteamDir) && GameFolders.PathExists(GameFolders.Join(stateSteamDir, "Among Us.exe"))) return stateSteamDir;
            var roots = new List<string>();
            if (registryRoots != null) roots.AddRange(registryRoots);
            if (!string.IsNullOrEmpty(defaultRoot)) roots.Add(defaultRoot);
            var libs = new List<string>();
            foreach (var r in Unique(roots))
            {
                libs.Add(r);
                try
                {
                    string vdf = GameFolders.Join(r, @"steamapps\libraryfolders.vdf");
                    if (!GameFolders.PathExists(vdf)) continue;
                    string text = File.ReadAllText(vdf, System.Text.Encoding.UTF8);
                    foreach (System.Text.RegularExpressions.Match m in VdfPath.Matches(text)) libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                }
                catch (Exception) { }
            }
            foreach (var l in Unique(libs))
            {
                try
                {
                    string c = GameFolders.Join(l, @"steamapps\common\Among Us");
                    if (GameFolders.PathExists(GameFolders.Join(c, "Among Us.exe"))) return c;
                }
                catch (ArgumentException) { }
            }
            return null;
        }

        static IEnumerable<string> Unique(IEnumerable<string> items)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in items) if (s != null && seen.Add(s)) yield return s;
        }
    }
}
