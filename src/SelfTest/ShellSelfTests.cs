// Self-test of the app shell's ported logic (PORT-MAP 8: 1 game folder, 2 Steam, 3 game version, 4 play-button state,
// 11 language, 12 the log rules, 13 tray tooltip) plus the shell's own parts: command line, settings.json, the launch
// flow with fakes (same checks, file, folder and arguments as the launcher), the bridge's command table, strings,
// the UI folder next to the exe and the exe's resources (no crewmate art inside the exe).
// Everything runs on fake folders inside the self-test folder; nothing real is read or changed.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Starpocket.Client.Aegis;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client.SelfTest
{
    internal static class ShellSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            CommandLineTests(r);
            GameFolderTests(r);
            SourceTests(r);
            SteamTests(r);
            GameVersionTests(r);
            StatusTests(r);
            LanguageTests(r);
            SettingsTests(r);
            LogRuleTests(r);
            SaveGameLogTests(r);
            ExpiryTests(r);
            LaunchTests(r);
            BridgeTests(r);
            ShellOpenTests(r);
            TooltipTests(r);
            CloseFadeTests(r);
            StringTests(r);
            PackageTests(r);
            r.Section("");
        }

        // ------------------------------------------------------------------ command line
        static void CommandLineTests(SelfTestRunner r)
        {
            r.Section("command line");
            r.Test("parse", () =>
            {
                var c = CommandLine.Parse(new[] { "--game-dir", @"D:\AU", "-SteamDir", @"E:\S", "--language=zh-CN", "--friend", "--tray", "/DesktopDir", @"F:\Desk" });
                r.Equal("--game-dir", @"D:\AU", c.GameDir);
                r.Equal("-SteamDir", @"E:\S", c.SteamDir);
                r.Equal("--language=", "zh-CN", c.Language);
                r.Check("--friend", c.Friend);
                r.Equal("/DesktopDir", @"F:\Desk", c.DesktopDir);
                r.Check("--tray (v0.4: read, not ignored)", c.Tray && c.Unknown.Count == 0 && c.Extra.Count == 0 && !c.SelfTest);
                // --source-dir is a developer build's option only (v0.4 review); the "launcher folder" section below
                // checks both halves of that
                if (GameFolders.DeveloperBuild)
                    r.Equal("--source-dir= (a developer build)", @"G:\Src", CommandLine.Parse(new[] { "--source-dir=G:\\Src" }).SourceDir);
                var t = CommandLine.Parse(new[] { "--self-test", @"C:\t" });
                r.Check("--self-test", t.SelfTest && t.SelfTestDir == @"C:\t");
            });
            // what the Apps & Features entry runs. AppsAndFeatures.Register writes exactly these two command lines, so
            // if they were not understood here, Windows' Uninstall button would only open the app (v0.3 review).
            r.Test("--uninstall (the Apps & Features entry)", () =>
            {
                var u = CommandLine.Parse(new[] { "--uninstall" });
                r.Check("--uninstall", u.Uninstall && !u.Quiet, "not read: " + string.Join(" ", u.Unknown.ToArray()));
                r.Equal("... and nothing was left unread", 0, u.Unknown.Count + u.Extra.Count);
                var q = CommandLine.Parse(new[] { "--uninstall", "--quiet" });
                r.Check("--uninstall --quiet", q.Uninstall && q.Quiet && q.Unknown.Count == 0);
                foreach (var spelling in new[] { "-uninstall", "/uninstall", "/Uninstall" })
                {
                    var s = CommandLine.Parse(new[] { spelling });
                    r.Check("the same written " + spelling, s.Uninstall && s.Unknown.Count == 0);
                }
                r.Check("--silent means --quiet", CommandLine.Parse(new[] { "--uninstall", "--silent" }).Quiet);
                r.Check("a normal start is not an uninstall", !CommandLine.Parse(new string[0]).Uninstall);
                r.Check("and it does not turn other options off", CommandLine.Parse(new[] { "--uninstall", "--language", "en" }).Language == "en");
            });
        }

        // ------------------------------------------------------------------ 1. the game copy (PORT-MAP 3.1)
        static void GameFolderTests(SelfTestRunner r)
        {
            r.Section("game folder");
            r.Test("rules", () =>
            {
                string root = r.NewDir("folders");
                string desk = Path.Combine(root, @"User\Desktop");
                string lad = Path.Combine(root, @"User\AppData\Local");
                Directory.CreateDirectory(desk);
                ModdedInputs I() => new ModdedInputs { DesktopDefault = desk, LocalAppData = lad, Src = Path.Combine(root, @"Dev\HostRoles") };

                var i = I(); i.GameDirArg = @"X:\Arg"; i.EnvGameDir = @"Y:\Env";
                r.Equal("argument first", @"X:\Arg", GameFolders.ResolveModded(i));
                i = I(); i.EnvGameDir = @"Y:\Env"; i.DevMode = true;
                r.Equal("POCKETROLES_GAMEDIR next", @"Y:\Env", GameFolders.ResolveModded(i));

                i = I(); i.DevMode = true;
                r.Equal("developer: no copy next to Src -> Desktop", Path.Combine(desk, "Among Us PocketRoles"), GameFolders.ResolveModded(i));
                string sibling = Path.Combine(root, @"Dev\Among Us PocketRoles");
                SelfTestRunner.Touch(Path.Combine(sibling, "Among Us.exe"));
                r.Equal("developer: the copy next to Src", sibling, GameFolders.ResolveModded(i));
                i.EnvOneDrive = Path.Combine(root, "User"); // developer mode never uses the OneDrive rule
                r.Equal("developer: OneDrive ignored", sibling, GameFolders.ResolveModded(i));

                i = I();
                r.Equal("friend: Desktop", Path.Combine(desk, "Among Us PocketRoles"), GameFolders.ResolveModded(i));
                i.EnvOneDrive = Path.Combine(root, "USER") + "\\";   // case and a trailing \ do not matter
                r.Equal("friend: Desktop inside OneDrive -> LOCALAPPDATA", Path.Combine(lad, @"PocketRoles\Among Us PocketRoles"), GameFolders.ResolveModded(i));
                i.DesktopDirArg = desk;
                r.Equal("friend: --desktop-dir turns the OneDrive rule off", Path.Combine(desk, "Among Us PocketRoles"), GameFolders.ResolveModded(i));
                i.DesktopDirArg = null;
                SelfTestRunner.Touch(Path.Combine(desk, @"Among Us PocketRoles\Among Us.exe"));
                r.Equal("friend: a copy already on the Desktop stays there", Path.Combine(desk, "Among Us PocketRoles"), GameFolders.ResolveModded(i));
                i = I(); i.EnvOneDrive = Path.Combine(root, "Other");
                r.Equal("friend: Desktop outside OneDrive", Path.Combine(desk, "Among Us PocketRoles"), GameFolders.ResolveModded(i));

                var p = ModPaths.For(@"C:\G");
                r.Equal("DllPath", @"C:\G\BepInEx\plugins\PocketRoles.dll", p.DllPath);
                r.Equal("CfgPath", @"C:\G\BepInEx\config\jp.pocketroles.mod.cfg", p.CfgPath);
                r.Equal("LogPath", @"C:\G\BepInEx\LogOutput.log", p.LogPath);
                r.Equal("LogArchiveDir", @"C:\G\BepInEx\PocketRoles\logs", p.LogArchiveDir);
                r.Equal("Parent of a folder with a trailing \\", @"C:\a", GameFolders.Parent(@"C:\a\b\"));
            });
            // POCKETROLES_GAMEDIR / --game-dir / --source-dir / launcher-state.json steamDir with characters Windows does not
            // allow (" < > |): "not found / not installed" like the ps1's Join-Path + Test-Path, never an exception at start
            r.Test("characters not allowed in a path", () =>
            {
                string root = r.NewDir("badpath");
                string quoted = "\"" + Path.Combine(root, "Among Us PocketRoles") + "\"";
                r.Equal("Join as Path.Combine", @"C:\a\b", GameFolders.Join(@"C:\a", "b"));
                r.Equal("Join with a quote: the text as it is", "\"D:\\G\"\\Among Us.exe", GameFolders.Join("\"D:\\G\"", "Among Us.exe"));
                r.Equal("Join with | and a trailing \\", @"C:\a|b\c\d", GameFolders.Join(@"C:\a|b\", @"c\d"));
                string modded = GameFolders.ResolveModded(new ModdedInputs { EnvGameDir = quoted, DesktopDefault = root, LocalAppData = root });
                r.Equal("POCKETROLES_GAMEDIR with its quotes is taken as it is (like the ps1)", quoted, modded);
                ModPaths p = null;
                r.Check("ModPaths.For does not throw", (p = ModPaths.For(modded)) != null && p.GameExe.EndsWith("\\Among Us.exe"));
                r.Check("... and nothing there exists", !GameFolders.PathExists(p.GameExe) && !GameFolders.PathExists(p.DllPath));
                var info = InstallInfo.Read(p);
                r.Check("not installed (no exception)", !info.Installed && !info.Interop);
                r.Check("--source-dir with < : not developer mode", !GameFolders.IsDevMode(false, @"C:\x<y"));
                r.Equal("the exe folder with | stays the source", @"C:\x|y", GameFolders.ResolveSource(null, @"C:\x|y"));
                r.Check("launcher-state.json in such a folder: not found", !LauncherStateFile.Load(GameFolders.Join("C:\\a\"b", "launcher-state.json")).Found);
                string dev = GameFolders.ResolveModded(new ModdedInputs { DevMode = true, Src = @"C:\x<y\Repo", DesktopDefault = root, LocalAppData = root });
                r.Equal("developer mode with such a Src: the Desktop copy", Path.Combine(root, "Among Us PocketRoles"), dev);
                string fakeDefault = Path.Combine(root, "NoSteam");
                r.Equal("Steam: launcher-state.json steamDir with quotes and a bad root are skipped", null, SteamLocator.Find(null, "\"D:\\Steam\"", new[] { @"C:\Steam|x", "\"E:\\S\"" }, fakeDefault));
                // Aegis keeps Aegis.ps1's Path.Combine on purpose: there such a folder is "Aegis failed" and never blocks the
                // game (pre-launch: "never blocks by failing")
                bool aegisThrows = false;
                try { AegisScanner.Build(modded, Path.Combine(root, "state"), new DefinitionsStore(null, Path.Combine(root, "state")).Load(), null); }
                catch (ArgumentException) { aegisThrows = true; }
                r.Check("Aegis's scan: same as Aegis.ps1 (Path.Combine throws -> \"Aegis failed\", the game is not blocked)", aegisThrows);
            });
        }

        static void SourceTests(SelfTestRunner r)
        {
            r.Section("launcher folder");
            r.Test("rules", () =>
            {
                string root = r.NewDir("src");
                string exe = Path.Combine(root, @"Repo\tools\client\bin");
                Directory.CreateDirectory(exe);
                // a developer's build: --source-dir is an option there, and it decides
                r.Equal("--source-dir first (a developer build)", @"Q:\S", GameFolders.ResolveSource(@"Q:\S", exe, true));
                r.Equal("nothing -> the exe folder", exe, GameFolders.ResolveSource(null, exe, true));
                SelfTestRunner.Touch(Path.Combine(root, @"Repo\PocketRoles.csproj"));
                r.Check("developer mode when the project file is the source", GameFolders.IsDevMode(false, Path.Combine(root, "Repo")));
                r.Check("--friend turns developer mode off", !GameFolders.IsDevMode(true, Path.Combine(root, "Repo")));

                // v0.4 review, the published exe. "Developer mode is the author's PC only" used to be untrue twice
                // over: --source-dir worked in a Release build, and the app also looked four folders ABOVE itself for
                // a PocketRoles.csproj. Either one turned a signed exe into one that shows a 「再ビルド」 button and
                // hands a folder of someone else's choosing to the compiler.
                r.Equal("a published build ignores --source-dir", exe, GameFolders.ResolveSource(@"Q:\S", exe, false));
                r.Equal("... and a project file 3 folders up is no longer looked for", exe, GameFolders.ResolveSource(null, exe, false));
                r.Check("... so a folder full of csproj files cannot make it a developer", !GameFolders.IsDevMode(false, GameFolders.ResolveSource(@"Q:\S", exe, false)));
                string beside = Path.Combine(root, "beside");
                SelfTestRunner.Touch(Path.Combine(beside, "PocketRoles.csproj"));
                r.Check("the only source left is the exe's own folder", GameFolders.IsDevMode(false, GameFolders.ResolveSource(null, beside, false)));
                // and the option itself: present for the author, absent from what is published
                bool optionExists = false;
                foreach (var o in CommandLine.KnownOptions) if (o == "--source-dir") optionExists = true;
                r.Equal("--source-dir is an option of a developer build only", GameFolders.DeveloperBuild, optionExists);
                var cli = CommandLine.Parse(new[] { "--source-dir", @"Q:\S" });
                if (GameFolders.DeveloperBuild) r.Equal("a developer build reads it", @"Q:\S", cli.SourceDir);
                else r.Check("a published build says it does not know the word, and stops",
                    cli.SourceDir == null && cli.Unknown.Count == 1 && Startup.Plan(cli).Mode == StartupMode.Usage && Startup.Plan(cli).ExitCode == 1,
                    string.Join(" ", cli.Unknown.ToArray()));
                SelfTestRunner.Touch(Path.Combine(exe, "launcher-state.json"), "{}");
                r.Equal("launcher-state.json next to the exe", exe, GameFolders.ResolveSource(null, exe, true));

                string st = SelfTestRunner.Touch(Path.Combine(root, "state.json"), "");
                File.WriteAllBytes(st, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"steamDir\":\"D:\\\\Steam\\\\Among Us\",\"lang\":\"en\",\"lastBuiltGameVersion\":\"2026.8.18\"}")).ToArray());
                var s = LauncherStateFile.Load(st);
                r.Check("launcher-state.json with a BOM", s.Found && s.SteamDir == @"D:\Steam\Among Us" && s.Lang == "en" && s.LastBuiltGameVersion == "2026.8.18", s.SteamDir + "|" + s.Lang);
                File.WriteAllText(st, "{broken", Encoding.UTF8);
                r.Check("a broken launcher-state.json is ignored", !LauncherStateFile.Load(st).Found);
            });

            // v1.1: the developer switch (the owner, 2026-09-24 「ランチャーじゃなくてクライアントからがいいな。開発用もそこで
            // 何とかしてよ」). The working copy is found by a fixed rule on the Desktop, never named on the command line;
            // the switch and the folder together are developer mode, either one alone is friend mode (src\Core\DevSource.cs).
            r.Test("the developer switch (v1.1)", () =>
            {
                string root = r.NewDir("devswitch");
                string desk = Path.Combine(root, "Desktop");
                Directory.CreateDirectory(desk);
                Func<string, string> noLnk = p => null;
                r.Check("an ordinary PC: no folder, no error", DevSource.Find(desk, noLnk) == null);
                r.Check("no Desktop at all: no folder, no error", DevSource.Find(null, noLnk) == null && DevSource.Find("", noLnk) == null);
                string hr = Path.Combine(desk, "HostRoles");
                SelfTestRunner.Touch(Path.Combine(hr, "PocketRoles.csproj"));
                r.Check("a project file alone is not the working copy (the v0.4 shape)", DevSource.Find(desk, noLnk) == null && !DevSource.IsDevFolder(hr));
                SelfTestRunner.Touch(Path.Combine(hr, "PocketRolesLauncher.ps1"));
                r.Equal("<Desktop>\\HostRoles with the project file AND the launcher script", hr, DevSource.Find(desk, noLnk));
                // the old launcher's Desktop shortcut comes first, and its target's FOLDER is what counts
                string repo = Path.Combine(root, @"elsewhere\Repo");
                SelfTestRunner.Touch(Path.Combine(repo, "PocketRoles.csproj"));
                SelfTestRunner.Touch(Path.Combine(repo, "PocketRolesLauncher.ps1"));
                SelfTestRunner.Touch(Path.Combine(desk, "PocketRoles.lnk"), "not really a shortcut");
                Func<string, string> lnk = p => p.EndsWith("PocketRoles.lnk", StringComparison.Ordinal) ? Path.Combine(repo, "PocketRoles Launcher.cmd") : null;
                r.Equal("the Desktop shortcut's folder first", repo, DevSource.Find(desk, lnk));
                r.Equal("a shortcut file that is not on the Desktop is never asked about", hr, DevSource.Find(desk, p => p.EndsWith("PocketRoles Launcher.lnk", StringComparison.Ordinal) ? Path.Combine(repo, "x.cmd") : null));
                SelfTestRunner.Touch(Path.Combine(desk, "PocketRoles Launcher.lnk"), "not really a shortcut");
                r.Equal("the older shortcut name too", repo, DevSource.Find(desk, p => p.EndsWith("PocketRoles Launcher.lnk", StringComparison.Ordinal) ? Path.Combine(repo, "x.cmd") : null));
                Func<string, string> broken = p => { throw new IOException("cannot read"); };
                r.Equal("a shortcut that cannot be read is skipped, not an error", hr, DevSource.Find(desk, broken));
                string elsewhere = Path.Combine(root, "Other");
                SelfTestRunner.Touch(Path.Combine(elsewhere, "PocketRoles.csproj"));
                r.Equal("a shortcut into a folder with only a project file is passed over", hr, DevSource.Find(desk, p => Path.Combine(elsewhere, "a.cmd")));
                r.Check("a shortcut that points nowhere is passed over", DevSource.Find(desk, p => "") == hr);

                // the one decision, with nothing on disk
                string exe = Path.Combine(root, "exe");
                DevChoice C(bool exeIsSource, bool friend, bool setting, string folder) => DevSource.Choose(exe, exeIsSource, friend, setting, folder);
                var c = C(false, false, false, hr);
                r.Check("switch off: friend mode, the exe folder is the source", !c.DevMode && !c.FromSetting && c.Src == exe);
                c = C(false, false, true, hr);
                r.Check("switch on + the working copy: developer mode against that folder", c.DevMode && c.FromSetting && c.Src == hr);
                c = C(false, false, true, null);
                r.Check("switch on, no working copy on this PC: friend mode, nothing happens", !c.DevMode && !c.FromSetting && c.Src == exe);
                c = C(false, true, true, hr);
                r.Check("--friend turns the switch off too", !c.DevMode && c.Src == exe);
                c = C(true, false, false, hr);
                r.Check("the exe's own folder holds the project file: developer mode as before (not from the switch)", c.DevMode && !c.FromSetting && c.Src == exe);
                c = C(true, false, true, hr);
                r.Check("... and the exe's own folder wins over the switch", c.DevMode && !c.FromSetting && c.Src == exe);
                c = C(true, true, false, null);
                r.Check("--friend still turns the exe folder's developer mode off", !c.DevMode && c.Src == exe);
                // what the page is told: the switch is drawn only where there is something to switch
                r.Check("the switch is for the working copy found on this PC", new[] { "PocketRoles.lnk", "PocketRoles Launcher.lnk" }.SequenceEqual(DevSource.ShortcutNames) && DevSource.DefaultFolderName == "HostRoles");
                r.Check("when the switch is flipped: on needs the folder, off never does, a running game refuses both",
                    ClientApp.DevSwitchRefusal(true, false, false) == "dev_nofolder" && ClientApp.DevSwitchRefusal(false, false, false) == null
                    && ClientApp.DevSwitchRefusal(true, true, false) == null && ClientApp.DevSwitchRefusal(true, true, true) == "game_running"
                    && ClientApp.DevSwitchRefusal(false, true, true) == "game_running");
                foreach (var l in Lang.Codes)
                    r.Check("the switch's words are in " + l, S.T(l, "dev_nofolder") != "dev_nofolder" && S.T(l, "dev_switch_on") != "dev_switch_on" && S.T(l, "dev_switch_off") != "dev_switch_off");
            });
        }

        // ------------------------------------------------------------------ 2. Steam (PORT-MAP 3.3)
        static void SteamTests(SelfTestRunner r)
        {
            r.Section("Steam");
            r.Test("rules", () =>
            {
                string root = r.NewDir("steam");
                string fakeDefault = Path.Combine(root, @"Program Files (x86)\Steam");   // never the real C:\Program Files (x86)\Steam
                string Find(string state, params string[] roots) => SteamLocator.Find(null, state, roots, fakeDefault);
                r.Equal("override as it is (not checked)", @"Z:\nowhere", SteamLocator.Find(@"Z:\nowhere", null, new string[0], fakeDefault));

                string steam1 = Path.Combine(root, "Steam1");
                string lib = Path.Combine(root, "ライブラリ 2");   // Japanese path in a UTF-8 vdf
                string lib2 = Path.Combine(root, "Lib3");
                Directory.CreateDirectory(Path.Combine(steam1, "steamapps"));
                string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + steam1.Replace(@"\", @"\\") + "\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + lib.Replace(@"\", @"\\") + "\"\n\t}\n\t\"2\"\n\t{\n\t\t\"path\"\t\t\"" + lib2.Replace(@"\", @"\\") + "\"\n\t}\n}\n";
                File.WriteAllText(Path.Combine(steam1, @"steamapps\libraryfolders.vdf"), vdf, new UTF8Encoding(false));
                r.Equal("no Among Us anywhere -> null", null, Find(null, steam1));

                string game2 = Path.Combine(lib, @"steamapps\common\Among Us");
                string game3 = Path.Combine(lib2, @"steamapps\common\Among Us");
                SelfTestRunner.Touch(Path.Combine(game2, "Among Us.exe"));
                SelfTestRunner.Touch(Path.Combine(game3, "Among Us.exe"));
                r.Equal("library from libraryfolders.vdf (UTF-8, \\\\ -> \\), first in order", game2, Find(null, steam1));

                string gameDefault = Path.Combine(fakeDefault, @"steamapps\common\Among Us");
                SelfTestRunner.Touch(Path.Combine(gameDefault, "Among Us.exe"));
                r.Equal("the default Steam folder comes after the registry's", game2, Find(null, steam1));
                r.Equal("the default Steam folder alone", gameDefault, Find(null));

                string game1 = Path.Combine(steam1, @"steamapps\common\Among Us");
                SelfTestRunner.Touch(Path.Combine(game1, "Among Us.exe"));
                r.Equal("the root itself is the first library", game1, Find(null, steam1));

                string stateDir = Path.Combine(root, "Picked");
                r.Equal("launcher-state.json steamDir without Among Us.exe is skipped", game1, Find(stateDir, steam1));
                SelfTestRunner.Touch(Path.Combine(stateDir, "Among Us.exe"));
                r.Equal("launcher-state.json steamDir with Among Us.exe", stateDir, Find(stateDir, steam1));
            });
        }

        // ------------------------------------------------------------------ 3. game version
        static void GameVersionTests(SelfTestRunner r)
        {
            r.Section("game version");
            r.Equal("2026.8.18", "2026.8.18", GameVersion.FromText("xx 2026.8.18\0yy"));
            r.Equal("skips 2022.x (Unity)", "2026.8.18", GameVersion.FromText("2022.3.62f1\0\0 2022.3.62 \0 2026.8.18"));
            r.Equal("2026.8.18f1 is not a game version", null, GameVersion.FromText("2026.8.18f1"));
            r.Equal("letters after a-z count too (2026.8.18a)", null, GameVersion.FromText("2026.8.18a"));
            r.Equal("upper case after is fine (case-sensitive like the ps1)", "2026.8.18", GameVersion.FromText("2026.8.18F"));
            r.Equal("only 2022.x -> null", null, GameVersion.FromText("2022.1.1 "));
            r.Test("file", () =>
            {
                string d = r.NewDir("gv");
                r.Equal("no file -> null", null, GameVersion.Read(d));
                Directory.CreateDirectory(Path.Combine(d, "Among Us_Data"));
                var bytes = new List<byte> { 0, 0xFF, 0xE9 };
                bytes.AddRange(Encoding.ASCII.GetBytes("2022.3.62f1\0" + "2026.8.18\0"));
                File.WriteAllBytes(Path.Combine(d, @"Among Us_Data\globalgamemanagers"), bytes.ToArray());
                r.Equal("globalgamemanagers (Latin-1)", "2026.8.18", GameVersion.Read(d));
            });
            r.Equal("DLL version: ProductVersion before +", "0.5.5", GameVersion.DllVersionFrom("0.5.5+abc123", "0.5.5.0"));
            r.Equal("DLL version: FileVersion when ProductVersion is not numeric", "0.5.5.0", GameVersion.DllVersionFrom("v0.5.5", "0.5.5.0"));
            r.Equal("DLL version: FileVersion when ProductVersion is empty", "1.0.0.0", GameVersion.DllVersionFrom("", "1.0.0.0"));
        }

        // ------------------------------------------------------------------ 4. play-button state (PORT-MAP 3.4)
        static void StatusTests(SelfTestRunner r)
        {
            r.Section("status");
            InstallInfo Info(bool exe, bool bep, bool dll, string ver) => new InstallInfo { Exe = exe, Bep = bep, Dll = dll, GameVer = ver };
            var full = Info(true, true, true, "2026.8.18");
            LaunchStatus C(bool dev, InstallInfo i, string steam, string built = "2026.8.18", bool blocked = false) => LaunchStatus.Compute(dev, i, steam, built, true, false, blocked);

            // v0.2 (PORT-MAP 13.3): the game copy is there but the mod is not -> 修復 (the same steps, from where it stopped)
            var half = C(false, Info(true, true, false, "2026.8.18"), "2026.8.18");
            r.Equal("friend: the copy is there, the mod is not -> repair", "repair", half.PState);
            r.Equal("... and it says which repair", "files", half.Repair);
            r.Equal("... and it warns al_repair", "al_repair", half.WarnKey);
            r.Equal("friend: nothing copied yet -> install", "install", C(false, Info(false, false, false, null), null).PState);
            r.Equal("friend: not installed warns al_notinstalled", "al_notinstalled", C(false, Info(false, false, false, null), null).WarnKey);
            var repairMod = LaunchStatus.Compute(false, full, "2026.8.18", "2026.8.18", true, false, false, true);
            r.Equal("friend: PocketRoles.dll changed (R-41) -> repair", "repair", repairMod.PState);
            r.Equal("... named as the mod's repair", "mod", repairMod.Repair);
            r.Equal("... and it warns al_repair_mod", "al_repair_mod", repairMod.WarnKey);
            var sync = C(false, full, "2026.9.2");
            r.Equal("friend: Steam newer -> sync", "sync", sync.PState);
            r.Equal("friend: sync line", "Steam 版が更新されています (2026.8.18 → 2026.9.2)。「起動」を押すとコピーを更新します", sync.Warn("ja"));
            r.Equal("friend: ready", "ready", C(false, full, "2026.8.18").PState);
            r.Equal("friend: Steam not found -> no update", "ready", C(false, full, null).PState);
            r.Equal("friend: copy version unreadable -> no update", "ready", C(false, Info(true, true, true, null), "2026.9.2").PState);
            r.Equal("developer: update first", "devUpdate", C(true, full, "2026.9.2", "2026.1.1").PState);
            r.Equal("developer: rebuild when built for another version", "devRebuild", C(true, full, "2026.8.18", "2026.3.31").PState);
            r.Equal("developer: rebuild when never built", "devRebuild", C(true, full, "2026.8.18", null).PState);
            r.Equal("developer: rebuild when the DLL is missing", "devRebuild", C(true, Info(true, true, false, null), null, null).PState);
            r.Equal("developer: ready", "ready", C(true, full, "2026.8.18").PState);
            r.Equal("developer: not installed is not asked", "ready", C(true, Info(false, false, true, null), null).PState);
            r.Equal("blocked by the last pre-launch scan", "blocked", C(false, full, "2026.8.18", blocked: true).PState);
            r.Equal("blocked never hides install", "install", C(false, Info(false, true, true, null), null, blocked: true).PState);
            r.Test("install info from files", () =>
            {
                string g = r.NewDir("install");
                var p = ModPaths.For(g);
                r.Check("empty folder: not installed", !InstallInfo.Read(p).Installed);
                SelfTestRunner.Touch(p.GameExe);
                SelfTestRunner.Touch(Path.Combine(g, @"BepInEx\core\BepInEx.Core.dll"));
                SelfTestRunner.Touch(p.DllPath);
                var i = InstallInfo.Read(p);
                r.Check("exe + BepInEx core + PocketRoles.dll: installed", i.Installed && !i.Interop);
                SelfTestRunner.Touch(Path.Combine(g, @"BepInEx\interop\Assembly-CSharp.dll"));
                r.Check("interop", InstallInfo.Read(p).Interop);
            });
        }

        // ------------------------------------------------------------------ 11. language (PORT-MAP 3.14)
        static void LanguageTests(SelfTestRunner r)
        {
            r.Section("language");
            r.Equal("--language first", "en", Lang.Resolve("en", "ja", "zh-CN", "ja-JP"));
            r.Equal("--language any case", "zh-CN", Lang.Resolve("ZH-cn", "ja", null, "ja-JP"));
            r.Equal("unknown --language ignored", "ja", Lang.Resolve("fr", "ja", "en", "en-US"));
            r.Equal("settings.json zh -> zh-CN", "zh-CN", Lang.Resolve(null, "zh", "en", "ja-JP"));
            r.Equal("settings.json auto -> launcher-state.json", "en", Lang.Resolve(null, "auto", "en", "ja-JP"));
            r.Equal("launcher-state.json any case", "zh-CN", Lang.Resolve(null, "auto", "zh-cn", "en-US"));
            r.Equal("bad launcher-state.json lang -> Windows", "ja", Lang.Resolve(null, null, "xx", "ja-JP"));
            r.Equal("Windows ja-JP", "ja", Lang.Resolve(null, null, null, "ja-JP"));
            r.Equal("Windows zh-TW -> zh-CN (L-10)", "zh-CN", Lang.Resolve(null, null, null, "zh-TW"));
            r.Equal("Windows zh-CN", "zh-CN", Lang.Resolve(null, null, null, "zh-CN"));
            r.Equal("Windows en-US", "en", Lang.Resolve(null, null, null, "en-US"));
            r.Equal("Windows fr-FR -> en", "en", Lang.Resolve(null, null, null, "fr-FR"));
            r.Equal("UI tag zh", "zh", Lang.ToUi("zh-CN"));
            r.Equal("UI pref zh -> zh-CN", "zh-CN", Lang.PrefFromUi("zh"));
            r.Equal("UI pref auto", "auto", Lang.PrefFromUi("auto"));
            r.Equal("UI pref invalid", null, Lang.PrefFromUi("de"));
            r.Equal("Chinese UI font", "Microsoft YaHei UI", Lang.UiFontName("zh-CN"));
            r.Test("the viewer's choice beats --language (like the launcher's combo box over -Language)", () =>
            {
                var c = new ClientContext { Cli = CommandLine.Parse(new[] { "--language", "en" }), Settings = new ClientSettings(), State = LauncherStateFile.InMemory("lang", "zh-CN") };
                c.ResolveLanguage();
                r.Equal("start: --language", "en", c.Lang);
                c.ChooseLanguage("ja", "en-US");
                r.Check("chose ja: ja, stored, --language dropped", c.Lang == "ja" && c.Settings.Lang == "ja" && c.Cli.Language == null, c.Lang + "|" + c.Settings.Lang + "|" + c.Cli.Language);
                c.ResolveLanguage();
                r.Equal("stays ja when resolved again", "ja", c.Lang);
                c.ChooseLanguage("auto", "en-US");
                r.Equal("auto: launcher-state.json next", "zh-CN", c.Lang);
                c.State = LauncherStateFile.InMemory();
                c.ChooseLanguage("auto", "ja-JP");
                r.Equal("auto: then Windows", "ja", c.Lang);
            });
        }

        // ------------------------------------------------------------------ settings.json
        static void SettingsTests(SelfTestRunner r)
        {
            r.Section("settings.json");
            r.Test("read and write", () =>
            {
                string d = r.NewDir("settings");
                string p = Path.Combine(d, @"StarPocket\Client\settings.json");
                var s = ClientSettings.Load(p);
                r.Check("missing file: close=tray, lang=auto", s.Close == "tray" && s.Lang == "auto");
                r.Check("close=quit accepted", s.SetClose("quit"));
                r.Check("close=minimize refused", !s.SetClose("minimize") && s.Close == "quit");
                s.SetLang("zh");
                s.Save(p);
                byte[] b = File.ReadAllBytes(p);
                r.Check("written without a BOM", !(b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF));
                r.Equal("written JSON", "{\"close\":\"quit\",\"lang\":\"zh-CN\"}", Encoding.UTF8.GetString(b));
                File.WriteAllBytes(p, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"close\":\"tray\",\"lang\":\"en\",\"future\":{\"a\":1}}")).ToArray());
                var s2 = ClientSettings.Load(p);
                r.Check("read with a BOM", s2.Close == "tray" && s2.Lang == "en");
                s2.Save(p);
                r.Check("unknown keys kept", File.ReadAllText(p).Contains("\"future\":{\"a\":1}"));
                File.WriteAllText(p, "{\"close\":\"x\",\"lang\":\"de\"}");
                var s3 = ClientSettings.Load(p);
                r.Check("invalid values -> defaults", s3.Close == "tray" && s3.Lang == "auto");
                File.WriteAllText(p, "not json");
                r.Check("broken file -> defaults", ClientSettings.Load(p).Close == "tray");
                r.Check("no .tmp left", !File.Exists(p + ".tmp"));

                var s4 = new ClientSettings();
                r.Check("the tray notice: not shown yet by default", !s4.TrayHintShown);
                s4.TrayHintShown = true;
                s4.Save(p);
                r.Equal("written once shown", "{\"close\":\"tray\",\"lang\":\"auto\",\"trayHintShown\":true}", File.ReadAllText(p));
                r.Check("read back", ClientSettings.Load(p).TrayHintShown);
                File.WriteAllText(p, "{\"close\":\"tray\",\"trayHintShown\":\"yes\"}");
                r.Check("not a true -> not shown yet", !ClientSettings.Load(p).TrayHintShown);
            });
            // v0.1.1: Settings → PocketRoles → 起動するゲーム (the owner, 2026-09-23). Kept where the app keeps its settings
            // (settings.json in %LOCALAPPDATA%\StarPocket\Client, written by the app itself), not in the page's storage.
            r.Test("起動するゲーム (startGame)", () =>
            {
                string d = r.NewDir("startgame");
                string p = Path.Combine(d, @"StarPocket\Client\settings.json");
                var s = ClientSettings.Load(p);
                r.Check("missing file: PocketRoles", s.StartGame == "pocketroles" && !s.PlaysVanilla);
                r.Check("vanilla accepted", s.SetStartGame("vanilla") && s.PlaysVanilla);
                r.Check("other values refused, nothing changes", !s.SetStartGame("steam") && !s.SetStartGame(null) && !s.SetStartGame("VANILLA") && s.StartGame == "vanilla");
                s.Save(p);
                r.Equal("written only while it is not the default", "{\"close\":\"tray\",\"lang\":\"auto\",\"startGame\":\"vanilla\"}", File.ReadAllText(p));
                r.Check("read back", ClientSettings.Load(p).PlaysVanilla);
                var back = ClientSettings.Load(p);
                back.SetStartGame("pocketroles");
                back.Save(p);
                r.Equal("back to PocketRoles: the key goes (the file of a v0.1.0 user)", "{\"close\":\"tray\",\"lang\":\"auto\"}", File.ReadAllText(p));
                File.WriteAllText(p, "{\"startGame\":\"plain\",\"close\":\"quit\"}");
                var bad = ClientSettings.Load(p);
                r.Check("an unknown value -> PocketRoles (the other settings still read)", bad.StartGame == "pocketroles" && bad.Close == "quit");
                File.WriteAllText(p, "{\"startGame\":true}");
                r.Check("not a string -> PocketRoles", ClientSettings.Load(p).StartGame == "pocketroles");

                // the page's settings.set goes through Bridge.SetSetting: the choice persists in settings.json
                File.Delete(p);
                var live = ClientSettings.Load(p);
                int saves = 0;
                Func<Dictionary<string, object>> save = () => { saves++; live.Save(p); return Bridge.Ok(); };
                Dictionary<string, object> Set(string key, object value) => Bridge.SetSetting(live, new Dictionary<string, object> { ["key"] = key, ["value"] = value }, save);
                var ok = Set("startGame", "vanilla");
                r.Check("settings.set startGame vanilla -> ok, saved once", Equals(ok["ok"], true) && saves == 1);
                r.Check("... and a new start reads it (it persists)", ClientSettings.Load(p).StartGame == "vanilla");
                var no = Set("startGame", "epic");
                r.Check("an unknown value -> invalid value, not saved, nothing changed", Equals(no["ok"], false) && (string)no["error"] == "invalid value" && saves == 1 && live.PlaysVanilla && ClientSettings.Load(p).PlaysVanilla);
                r.Check("a number -> invalid value", Equals(Set("startGame", 1)["ok"], false) && saves == 1);
                r.Check("settings.set startGame pocketroles -> saved", Equals(Set("startGame", "pocketroles")["ok"], true) && saves == 2 && ClientSettings.Load(p).StartGame == "pocketroles");
                r.Check("close still kept", Equals(Set("close", "quit")["ok"], true) && ClientSettings.Load(p).Close == "quit");
                var later = Set("autostart", true);
                r.Check("a key this version does not keep -> not in this version (named)", Equals(later["unsupported"], true) && (string)((Dictionary<string, object>)later["data"])["key"] == "autostart");
                r.Check("an unknown key -> unknown setting", (string)Set("nope", "x")["error"] == "unknown setting" && (string)Bridge.SetSetting(live, new Dictionary<string, object>(), save)["error"] == "unknown setting");
                var failing = Bridge.SetSetting(new ClientSettings(), new Dictionary<string, object> { ["key"] = "startGame", ["value"] = "vanilla" }, () => Bridge.Fail("disk full"));
                r.Check("a failed save is the reply", Equals(failing["ok"], false) && (string)failing["error"] == "disk full");
            });
            // v1.1: Settings → PocketRoles → 開発 (the owner, 2026-09-24 「開発用もそこで何とかしてよ」): the author's switch, kept in
            // settings.json like the others. Only a real true is on; on is the only value ever written.
            r.Test("the developer switch's setting (devBuild)", () =>
            {
                string d = r.NewDir("devbuild");
                string p = Path.Combine(d, @"StarPocket\Client\settings.json");
                var s = ClientSettings.Load(p);
                r.Check("missing file: off", !s.DevBuild);
                s.Save(p);
                r.Equal("off is never written", "{\"close\":\"tray\",\"lang\":\"auto\"}", File.ReadAllText(p));
                s.SetDevBuild(true);
                s.Save(p);
                r.Equal("on is written", "{\"close\":\"tray\",\"lang\":\"auto\",\"devBuild\":true}", File.ReadAllText(p));
                r.Check("read back", ClientSettings.Load(p).DevBuild);
                File.WriteAllText(p, "{\"devBuild\":\"on\",\"close\":\"quit\"}");
                var word = ClientSettings.Load(p);
                r.Check("only a real true turns it on (the other settings still read)", !word.DevBuild && word.Close == "quit");
                File.WriteAllText(p, "{\"devBuild\":1}");
                r.Check("a number is not a true either", !ClientSettings.Load(p).DevBuild);

                // the page's toggle (settings.set): the save itself; ClientApp adds the check for the folder and the restart
                File.Delete(p);
                var live = ClientSettings.Load(p);
                int saves = 0;
                Func<Dictionary<string, object>> save = () => { saves++; live.Save(p); return Bridge.Ok(); };
                Dictionary<string, object> Set(object value) => Bridge.SetSetting(live, new Dictionary<string, object> { ["key"] = "devBuild", ["value"] = value }, save);
                r.Check("the page may set it", Bridge.SettingKeys.Contains("devBuild"));
                r.Check("settings.set devBuild true -> ok, saved, persists", Equals(Set(true)["ok"], true) && saves == 1 && ClientSettings.Load(p).DevBuild);
                r.Check("settings.set devBuild \"off\" -> ok, back off, the key goes", Equals(Set("off")["ok"], true) && saves == 2 && !ClientSettings.Load(p).DevBuild && !File.ReadAllText(p).Contains("devBuild"));
                var no = Set("maybe");
                r.Check("a word that is neither -> invalid value, nothing saved", Equals(no["ok"], false) && (string)no["error"] == "invalid value" && saves == 2 && !live.DevBuild);
            });
            // v1.1: Settings → 全般 → 起動時の動作 → 起動のたびに Aegis のスキャンを出す (the owner, 2026-09-23 「起動のたびに出す形に変えて」)
            r.Test("the start scan's card (startScan)", () =>
            {
                string d = r.NewDir("startscan");
                string p = Path.Combine(d, @"StarPocket\Client\settings.json");
                var s = ClientSettings.Load(p);
                r.Check("missing file: on (the owner's default)", s.StartScan);
                s.Save(p);
                r.Equal("on is never written", "{\"close\":\"tray\",\"lang\":\"auto\"}", File.ReadAllText(p));
                s.SetStartScan(false);
                s.Save(p);
                r.Equal("off is written", "{\"close\":\"tray\",\"lang\":\"auto\",\"startScan\":false}", File.ReadAllText(p));
                r.Check("read back", !ClientSettings.Load(p).StartScan);
                File.WriteAllText(p, "{\"startScan\":\"off\",\"close\":\"quit\"}");
                var word = ClientSettings.Load(p);
                r.Check("only a real false turns it off (the other settings still read)", word.StartScan && word.Close == "quit");
                File.WriteAllText(p, "{\"startScan\":0}");
                r.Check("a number is not a false either", ClientSettings.Load(p).StartScan);

                // the page's toggle sends a real JSON false / true (settings.set); a script may send the words
                File.Delete(p);
                var live = ClientSettings.Load(p);
                int saves = 0;
                Func<Dictionary<string, object>> save = () => { saves++; live.Save(p); return Bridge.Ok(); };
                Dictionary<string, object> Set(object value) => Bridge.SetSetting(live, new Dictionary<string, object> { ["key"] = "startScan", ["value"] = value }, save);
                r.Check("the page may set it", Bridge.SettingKeys.Contains("startScan"));
                r.Check("settings.set startScan false -> ok, saved, persists", Equals(Set(false)["ok"], true) && saves == 1 && !ClientSettings.Load(p).StartScan);
                r.Check("settings.set startScan \"on\" -> ok, back on", Equals(Set("on")["ok"], true) && saves == 2 && ClientSettings.Load(p).StartScan);
                r.Check("settings.set startScan true (JSON) -> ok", Equals(Set(true)["ok"], true) && saves == 3 && live.StartScan);
                var no = Set("maybe");
                r.Check("a word that is neither -> invalid value, nothing saved", Equals(no["ok"], false) && (string)no["error"] == "invalid value" && saves == 3 && live.StartScan);
                r.Check("a number -> invalid value", Equals(Set(1)["ok"], false) && saves == 3);
                r.Check("OnOffValue: JSON true / false and the words; nothing else",
                    Bridge.OnOffValue(true) == true && Bridge.OnOffValue(false) == false && Bridge.OnOffValue("off") == false
                    && Bridge.OnOffValue("1") == true && Bridge.OnOffValue(2) == null && Bridge.OnOffValue(null) == null);

                // where the card goes (ClientApp.ScanCardPlan): the window away -> the card, live, as v0.4; the start scan
                // with the window up -> the card too, waiting for the window while it is not there yet; the rest -> the panel
                r.Equal("window away: the card, live (v0.4), whatever the setting", "live|live", ClientApp.ScanCardPlan("start", true, false, false) + "|" + ClientApp.ScanCardPlan("prelaunch", true, true, true));
                r.Equal("a normal start, the window not up yet: the card waits for the window", "wait", ClientApp.ScanCardPlan("start", false, false, true));
                r.Equal("the start scan runs with the window up (the old tray quit): live", "live", ClientApp.ScanCardPlan("start", false, true, true));
                r.Equal("the setting off: no card while the window is on screen (v0.4)", "none|none", ClientApp.ScanCardPlan("start", false, false, false) + "|" + ClientApp.ScanCardPlan("start", false, true, false));
                r.Equal("scan again / scan only / pre-launch with the window up: the panel shows them", "none|none|none", ClientApp.ScanCardPlan("rescan", false, true, true) + "|" + ClientApp.ScanCardPlan("scanOnly", false, true, true) + "|" + ClientApp.ScanCardPlan("prelaunch", false, true, true));
                // the headline of a scan that is not the pre-launch one and found a red row
                r.Equal("headline: found (ja)", "見つかりました — 赤い項目 2 件を直してください", AegisText.Get("ja", "found", 2));
                r.Equal("headline: found (zh-CN / en)", "有发现 — 请处理 2 个红色项目|Found — fix the 2 red row(s)", AegisText.Get("zh-CN", "found", 2) + "|" + AegisText.Get("en", "found", 2));
                r.Equal("the card that stays says how to close it", "クリックで閉じる|点击关闭|Click to close", AegisText.Get("ja", "card.close") + "|" + AegisText.Get("zh-CN", "card.close") + "|" + AegisText.Get("en", "card.close"));
            });
        }

        // ------------------------------------------------------------------ 12. log rules (Test-LogArchiveExpired)
        static void LogRuleTests(SelfTestRunner r)
        {
            r.Section("log rules");
            var now = new DateTime(2026, 9, 22, 12, 0, 0);
            Func<string, bool> E = n => GameLogs.ArchiveExpired(n, now, now);
            r.Equal("archive name", "LogOutput-2026-09-22_120000.log", GameLogs.ArchiveName(now));
            r.Check("log 30 days old", E("LogOutput-2026-08-23_120000.log"));
            r.Check("log 29 days old", !E("LogOutput-2026-08-24_120000.log"));
            r.Check("log name any case", E("logoutput-2026-01-01_000000.LOG"));
            r.Check("bad time in the name", !E("LogOutput-2026-13-01_000000.log"));
            r.Check(".part after a day", GameLogs.ArchiveExpired("LogOutput-x.log.123.part", now.AddDays(-1), now));
            r.Check(".part within a day", !GameLogs.ArchiveExpired("LogOutput-x.log.123.part", now.AddHours(-23), now));
            r.Check("day zip: the day ended 30 days ago", E("logs-20260822.zip"));
            r.Check("day zip: the day ended 29.5 days ago", !E("logs-20260823.zip"));
            r.Check("day zip -2", E("logs-20260801-2.zip"));
            r.Check("month zip ended 30 days ago", E("logs-2026-07.zip"));
            r.Check("month zip of last month", !E("logs-2026-08.zip"));
            r.Check("year 9999 day zip never", !E("logs-99991231.zip"));
            r.Check("year 9999 month zip never", !E("logs-9999-12.zip"));
            r.Check("other names never", !E("notes.txt") && !E("logs-2026.zip") && !E("LogOutput.log"));
        }

        static GameLogs NewLogs(SelfTestRunner r, string g, Func<bool> running, List<string> log)
        {
            return new GameLogs
            {
                LogPath = Path.Combine(g, @"BepInEx\LogOutput.log"),
                LogArchiveDir = Path.Combine(g, @"BepInEx\PocketRoles\logs"),
                Desktop = Path.Combine(g, "Desktop"),
                CacheDir = Path.Combine(g, "Cache"),
                MutexName = @"Local\StarPocketClient.SelfTest." + Guid.NewGuid().ToString("N"),
                LockWaitMs = 2000,
                IsModdedGameRunning = running,
                Log = s => log.Add(s),
                LogQuiet = s => log.Add("quiet: " + s),
                Lang = "en",
            };
        }

        static void SaveGameLogTests(SelfTestRunner r)
        {
            r.Section("Save-GameLog");
            r.Test("copies", () =>
            {
                string g = r.NewDir("savelog");
                var log = new List<string>();
                var gl = NewLogs(r, g, () => false, log);
                r.Check("no log: nothing to keep", gl.SaveGameLog() && !Directory.Exists(gl.LogArchiveDir));
                SelfTestRunner.Touch(gl.LogPath, "");
                r.Check("empty log: nothing to keep", gl.SaveGameLog() && !Directory.Exists(gl.LogArchiveDir));
                SelfTestRunner.Touch(gl.LogPath, "[Info] PocketRoles v0.5.5 loaded\r\n");
                var t = new DateTime(2026, 9, 20, 21, 4, 5);
                File.SetLastWriteTime(gl.LogPath, t);
                r.Check("game running: skipped", !NewLogs(r, g, () => true, log).SaveGameLog() && !Directory.Exists(gl.LogArchiveDir));
                r.Check("saved", gl.SaveGameLog());
                string dest = Path.Combine(gl.LogArchiveDir, "LogOutput-2026-09-20_210405.log");
                r.Check("name from the last write time", File.Exists(dest));
                r.Check("same time as the log", File.Exists(dest) && File.GetLastWriteTime(dest) == t);
                r.Check("same bytes", File.Exists(dest) && File.ReadAllText(dest) == File.ReadAllText(gl.LogPath));
                r.Check("no .part left", Directory.GetFiles(gl.LogArchiveDir, "*.part").Length == 0);
                r.Check("lg_saved logged", log.Contains("Saved the log of the previous game: LogOutput-2026-09-20_210405.log"), string.Join(" / ", log));
                File.WriteAllText(dest, "keep");
                r.Check("already saved: true, not copied again", gl.SaveGameLog() && File.ReadAllText(dest) == "keep");
                File.Delete(dest);
                string zip = Path.Combine(gl.LogArchiveDir, "logs-20260920-2.zip");
                using (var za = ZipFile.Open(zip, ZipArchiveMode.Create)) za.CreateEntry("LogOutput-2026-09-20_210405.log");
                r.Check("already in a day zip: true, no loose copy", gl.SaveGameLog() && !File.Exists(dest));
                File.Delete(zip);
                using (var fs = new FileStream(gl.LogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                    r.Check("log open for writing (game still writes): false", !gl.SaveGameLog() && !File.Exists(dest));
                r.Check("month zip counts too", MonthZipCounts(gl, t));
            });
        }

        static bool MonthZipCounts(GameLogs gl, DateTime t)
        {
            string zip = Path.Combine(gl.LogArchiveDir, "logs-2026-09.zip");
            using (var za = ZipFile.Open(zip, ZipArchiveMode.Create)) za.CreateEntry("logoutput-2026-09-20_210405.LOG");
            return gl.InZips(t, GameLogs.ArchiveName(t));
        }

        static void ExpiryTests(SelfTestRunner r)
        {
            r.Section("30-day expiry");
            r.Test("deletes what is due", () =>
            {
                string g = r.NewDir("expiry");
                var log = new List<string>();
                var now = DateTime.Now;
                var gl = NewLogs(r, g, () => false, log);
                gl.Now = () => now;
                SelfTestRunner.Touch(gl.LogPath);
                File.SetLastWriteTime(gl.LogPath, now.AddDays(-31));
                string bep = Path.GetDirectoryName(gl.LogPath);
                string asideOld = SelfTestRunner.Touch(Path.Combine(bep, GameLogs.ArchiveName(now.AddDays(-40))));
                string asideNew = SelfTestRunner.Touch(Path.Combine(bep, GameLogs.ArchiveName(now.AddDays(-2))));
                string a = gl.LogArchiveDir;
                string oldLog = SelfTestRunner.Touch(Path.Combine(a, GameLogs.ArchiveName(now.AddDays(-30).AddMinutes(-1))));
                string newLog = SelfTestRunner.Touch(Path.Combine(a, GameLogs.ArchiveName(now.AddDays(-5))));
                string oldZip = SelfTestRunner.Touch(Path.Combine(a, "logs-" + now.AddDays(-32).ToString("yyyyMMdd") + ".zip"));
                string keep9999 = SelfTestRunner.Touch(Path.Combine(a, "logs-99991231.zip"));
                string other = SelfTestRunner.Touch(Path.Combine(a, "readme.txt"));
                string part = SelfTestRunner.Touch(Path.Combine(a, "LogOutput-x.log.1.part"));
                File.SetLastWriteTime(part, now.AddDays(-2));
                string oldReport = SelfTestRunner.Touch(Path.Combine(gl.Desktop, "PocketRoles-report-" + now.AddDays(-31).ToString("yyyyMMdd-HHmm") + ".zip"));
                string newReport = SelfTestRunner.Touch(Path.Combine(gl.Desktop, "PocketRoles-report-" + now.AddDays(-1).ToString("yyyyMMdd-HHmm") + ".zip"));
                string oddReport = SelfTestRunner.Touch(Path.Combine(gl.Desktop, "PocketRoles-report-old.zip"));
                string oldWork = Path.Combine(gl.CacheDir, "report-" + now.AddDays(-2).ToString("yyyyMMdd-HHmm"));
                SelfTestRunner.Touch(Path.Combine(oldWork, "a.txt"));
                string newWork = Path.Combine(gl.CacheDir, "report-" + now.ToString("yyyyMMdd-HHmm"));
                Directory.CreateDirectory(newWork);

                int n = gl.RemoveExpired();
                r.Equal("count (the cache folder is not counted)", 6, n);
                r.Check("LogOutput.log 31 days old", !File.Exists(gl.LogPath));
                r.Check("moved-aside log 40 days old", !File.Exists(asideOld) && File.Exists(asideNew));
                r.Check("archive: old log, day zip, .part", !File.Exists(oldLog) && !File.Exists(oldZip) && !File.Exists(part));
                r.Check("archive: new log, year 9999, other names kept", File.Exists(newLog) && File.Exists(keep9999) && File.Exists(other));
                r.Check("Desktop report zips by the name's time only", !File.Exists(oldReport) && File.Exists(newReport) && File.Exists(oddReport));
                r.Check("report work folder after a day", !Directory.Exists(oldWork) && Directory.Exists(newWork));
                r.Check("lg_expired logged", log.Contains("Deleted 6 past log(s) / report zip(s) older than 30 days"), string.Join(" / ", log));

                SelfTestRunner.Touch(gl.LogPath);
                File.SetLastWriteTime(gl.LogPath, now.AddDays(-31));
                var running = NewLogs(r, g, () => true, log);
                running.Now = () => now;
                running.RemoveExpired();
                r.Check("LogOutput.log kept while the copy's game runs", File.Exists(gl.LogPath));
            });
            r.Test("waits for the log lock", () =>
            {
                string g = r.NewDir("lock");
                var log = new List<string>();
                var gl = NewLogs(r, g, () => false, log);
                gl.LockWaitMs = 200;
                SelfTestRunner.Touch(gl.LogPath, "abc");
                using (var held = new Mutex(false, gl.MutexName))
                {
                    var holder = new Thread(() => { held.WaitOne(); Thread.Sleep(800); held.ReleaseMutex(); });
                    holder.Start();
                    Thread.Sleep(100);
                    bool saved = gl.SaveGameLog();
                    holder.Join();
                    r.Check("busy lock: skipped (false), nothing written", !saved && !Directory.Exists(gl.LogArchiveDir));
                    r.Check("busy lock: logged quietly", log.Any(l => l.Contains("busy with the logs")));
                }
            });
        }

        // ------------------------------------------------------------------ launch flow (PORT-MAP 3.5 / 3.6)
        sealed class FakeGame
        {
            public bool GameRunning, SteamRunning = true, DevMode;
            public LaunchStatus Status;
            public PreLaunchResult Pre = new PreLaunchResult { Ran = true };
            public int PreDelayMs;
            public bool PreThrows;
            public readonly List<string> Calls = new List<string>();
            public readonly List<string> Log = new List<string>();
            public ProcessStartInfo Started;
            public int StatusCalls, SteamChecks;

            public GameLauncher Launcher(ModPaths p) => new GameLauncher
            {
                Paths = p,
                DevMode = DevMode,
                Lang = () => "en",
                GameRunning = () => GameRunning,
                SteamRunning = () => { SteamChecks++; return SteamRunning; },
                ComputeStatus = () => { StatusCalls++; return Status; },
                PreLaunchScan = (progress, cancel) =>
                {
                    Calls.Add("scan");
                    progress(new ScanProgress { Step = 1 });
                    if (PreThrows) throw new InvalidOperationException("scan broke");
                    if (PreDelayMs > 0) Thread.Sleep(PreDelayMs);
                    return Pre;
                },
                PreLaunchTimeoutMs = 60000,
                SaveGameLog = () => { Calls.Add("savelog"); return true; },
                StartProcess = psi => { Calls.Add("start"); Started = psi; },
                Log = s => Log.Add(s),
            };
        }

        static void LaunchTests(SelfTestRunner r)
        {
            r.Section("launch");
            var ok = new InstallInfo { Exe = true, Bep = true, Dll = true, GameVer = "2026.8.18" };
            LaunchStatus St(bool dev, InstallInfo i, string steam, string built = "2026.8.18") => LaunchStatus.Compute(dev, i, steam, built, true, false, false);
            var paths = ModPaths.For(@"C:\Games\Among Us PocketRoles");

            r.Test("checks in the launcher's order", () =>
            {
                var f = new FakeGame { GameRunning = true, Status = St(false, ok, "2026.8.18") };
                var o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("game running -> la_running, nothing started", !o.Ok && o.Error == "Among Us is already running." && f.Started == null && f.Calls.Count == 0);

                f = new FakeGame { Status = St(false, new InstallInfo { Exe = true }, null) };
                o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("friend not installed -> la_notinstalled, needs install", !o.Ok && o.Needs == "install" && o.Error == "Not installed yet. Press \"Install\"." && f.Started == null);

                f = new FakeGame { SteamRunning = false, Status = St(false, ok, "2026.8.18") };
                o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("Steam not running -> la_steam (no dialog)", !o.Ok && o.Error == "Please start Steam first." && f.Started == null && f.Calls.Count == 0);

                f = new FakeGame { Status = St(false, ok, "2026.9.2") };
                o = f.Launcher(paths).Launch(false, _ => { });
                // v0.2: the app can sync, so the answer asks for it in words instead of "not in this version"
                r.Check("friend: Steam updated -> asks to sync first (syncSteam)", !o.Ok && !o.Unsupported && o.Needs == "syncSteam" &&
                    o.Error == "The Steam version was updated (2026.8.18 → 2026.9.2). \"Update and play\" refreshes the mod copy first, then starts the game." &&
                    f.Started == null && f.Calls.Count == 0, o.Error);

                f = new FakeGame { DevMode = true, Status = St(true, ok, "2026.9.2", "2026.1.1") };
                o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("developer: update -> not in v0.1 (devUpdate)", o.Unsupported && o.Needs == "devUpdate");
                f = new FakeGame { DevMode = true, Status = St(true, ok, "2026.8.18", "2026.1.1") };
                o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("developer: rebuild -> not in v0.1 (rebuild)", o.Unsupported && o.Needs == "rebuild");
                f = new FakeGame { DevMode = true, Status = St(true, new InstallInfo { Dll = true, GameVer = "2026.8.18" }, null) };
                o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("developer: not installed is not checked", o.Ok && f.Started != null);
            });

            r.Test("Aegis stops the start", () =>
            {
                var f = new FakeGame { Status = St(false, ok, "2026.8.18"), Pre = new PreLaunchResult { Ran = true, Blocked = true, Lines = new[] { "Other plugins: Evil.dll → Remove it", "", "Kernel protection: TESTSIGNING" } } };
                var o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("blocked, not started, log not saved", o.Blocked && f.Started == null && !f.Calls.Contains("savelog"));
                r.Equal("the launcher's message (la_aegis_block + \\n・ lines)", "Aegis found the problems below and stopped the start. Fix them, then start again.\n・Other plugins: Evil.dll → Remove it\n・Kernel protection: TESTSIGNING", o.Text);
                r.Equal("empty lines dropped", 2, o.Reasons.Length);
                var res = o.ToResult("launch");
                r.Check("result {ok:false, blocked:true, data:{reasons, text}}", Equals(res["ok"], false) && Equals(res["blocked"], true) && ((Dictionary<string, object>)res["data"]).ContainsKey("reasons"));
            });

            r.Test("start like the launcher", () =>
            {
                var f = new FakeGame { Status = St(false, ok, "2026.8.18") };
                int progress = 0;
                var o = f.Launcher(paths).Launch(false, p => progress += p.Step);
                r.Check("ok with la_started", o.Ok && o.Text == "Launched. The mod is active when \"PocketRoles v...\" appears at the top-left.");
                r.Equal("order: scan, save the log, start", "scan,savelog,start", string.Join(",", f.Calls));
                r.Equal("progress reached the caller", 1, progress);
                r.Equal("file", @"C:\Games\Among Us PocketRoles\Among Us.exe", f.Started.FileName);
                r.Equal("working folder", @"C:\Games\Among Us PocketRoles", f.Started.WorkingDirectory);
                r.Equal("no arguments", "", f.Started.Arguments);
                r.Check("ShellExecute like Start-Process (the environment is passed on as it is)", f.Started.UseShellExecute);
                r.Check("logs in order: in_firstrun, la_start, la_started", f.Log.Count == 3 && f.Log[0].StartsWith("The first launch") && f.Log[1] == "Launching Among Us with the mod..." && f.Log[2].StartsWith("Launched."), string.Join(" / ", f.Log));

                f = new FakeGame { Status = St(false, ok, "2026.8.18") };
                o = f.Launcher(paths).Launch(true, _ => { });
                r.Equal("windowed arguments", "-screen-fullscreen 0 -screen-width 1600 -screen-height 900", f.Started.Arguments);
                r.Check("windowed log line (Japanese in every language, like the ps1)", f.Log.Contains("ウィンドウモード (1600x900) で起動しました"));
            });

            r.Test("Aegis never blocks by failing", () =>
            {
                var f = new FakeGame { Status = St(false, ok, "2026.8.18"), PreThrows = true };
                var o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("scan throws -> starts", o.Ok && f.Started != null && f.Log.Any(l => l.StartsWith("Aegis: ")));
                f = new FakeGame { Status = St(false, ok, "2026.8.18"), PreDelayMs = 1500, Pre = new PreLaunchResult { Ran = true, Blocked = true, Lines = new[] { "late" } } };
                var gl = f.Launcher(paths);
                gl.PreLaunchTimeoutMs = 300;
                o = gl.Launch(false, _ => { });
                r.Check("no answer in time -> starts (L-1)", o.Ok && f.Started != null && f.Log.Any(l => l.Contains("did not finish")));
                f = new FakeGame { Status = St(false, ok, "2026.8.18"), Pre = new PreLaunchResult { Ran = false } };
                o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("no Aegis (stub) -> starts", o.Ok && f.Started != null);
                f = new FakeGame { Status = St(false, ok, "2026.8.18") };
                var l2 = f.Launcher(paths);
                l2.StartProcess = psi => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified");
                o = l2.Launch(false, _ => { });
                r.Check("start fails -> err line", !o.Ok && o.Error == "Error: The system cannot find the file specified" && f.Log.Contains(o.Error));
            });

            // v0.1.1: PLAY while Settings → 起動するゲーム is plain Among Us (the page sends launchVanilla instead of launch)
            r.Test("plain Among Us (起動するゲーム)", () =>
            {
                var notInstalled = St(false, new InstallInfo { Exe = true }, null);
                var f = new FakeGame { Status = notInstalled, SteamRunning = false, Pre = new PreLaunchResult { Ran = true, Blocked = true, Lines = new[] { "red" } } };
                var o = f.Launcher(paths).LaunchVanilla();
                r.Check("starts even when the mod copy is not installed, Steam is closed and Aegis would block the mod", o.Ok && f.Started != null);
                r.Equal("Steam's URL", "steam://rungameid/945360", f.Started.FileName);
                r.Check("ShellExecute like the launcher's Start-Process, no arguments, no working folder", f.Started.UseShellExecute && f.Started.Arguments == "" && f.Started.WorkingDirectory == "");
                r.Equal("the vanilla path only: one start, no scan, no log copy", "start", string.Join(",", f.Calls));
                r.Check("no status check and no \"start Steam first\" (Steam starts itself)", f.StatusCalls == 0 && f.SteamChecks == 0);
                r.Check("the launcher's words (la_vanilla)", o.Text == "Launched the Steam version (no mod)." && f.Log.Count == 1 && f.Log[0] == o.Text, string.Join(" / ", f.Log));
                var res = o.ToResult("launchVanilla");
                r.Check("result {ok:true, data:{text}}", Equals(res["ok"], true) && (string)((Dictionary<string, object>)res["data"])["text"] == o.Text);

                // nothing of the mod copy is used at all: no folder, no status, no scan, no log copy
                var bare = new GameLauncher
                {
                    Paths = null, Lang = () => "ja", GameRunning = () => false, SteamRunning = () => throw new InvalidOperationException("Steam checked"),
                    ComputeStatus = () => throw new InvalidOperationException("status read"), PreLaunchScan = (p, c) => throw new InvalidOperationException("scanned"),
                    SaveGameLog = () => throw new InvalidOperationException("log copied"),
                };
                ProcessStartInfo started = null;
                bare.StartProcess = psi => started = psi;
                o = bare.LaunchVanilla();
                r.Check("without a mod copy (Paths null) and with every mod step throwing: still ok", o.Ok && started != null && started.FileName == "steam://rungameid/945360", o.Error);
                r.Equal("ja words", "Steam 版 (mod なし) を起動しました。", o.Text);

                f = new FakeGame { GameRunning = true, Status = notInstalled };
                o = f.Launcher(paths).LaunchVanilla();
                r.Check("Among Us already running -> la_running, nothing started", !o.Ok && o.Error == "Among Us is already running." && f.Started == null && f.Calls.Count == 0);
                f = new FakeGame { Status = notInstalled };
                var gl = f.Launcher(paths);
                gl.StartProcess = psi => throw new System.ComponentModel.Win32Exception(1155, "No application is associated with the specified file for this operation");
                o = gl.LaunchVanilla();
                r.Check("no Steam on the PC (steam:// has no handler) -> err line, never a crash", !o.Ok && o.Error == "Error: No application is associated with the specified file for this operation" && f.Log.Contains(o.Error));

                // PLAY with PocketRoles is unchanged: the mod copy's file, the scan and the log copy, never Steam's URL
                f = new FakeGame { Status = St(false, ok, "2026.8.18") };
                o = f.Launcher(paths).Launch(false, _ => { });
                r.Check("PocketRoles: scan, log copy, <Modded>\\Among Us.exe (not steam://)", o.Ok && string.Join(",", f.Calls) == "scan,savelog,start" && f.Started.FileName == @"C:\Games\Among Us PocketRoles\Among Us.exe" && f.StatusCalls == 1 && f.SteamChecks == 1);
            });

            r.Test("the tray's play (起動するゲーム)", () =>
            {
                var notInstalled = St(false, new InstallInfo { Exe = true }, null);
                var installed = St(false, ok, "2026.8.18");
                r.Check("PocketRoles, installed: yes", ClientApp.TrayPlayable(false, false, false, false, installed));
                r.Check("PocketRoles, friend not installed: no", !ClientApp.TrayPlayable(false, false, false, false, notInstalled));
                r.Check("PocketRoles, developer not installed: yes (as before)", ClientApp.TrayPlayable(false, false, false, true, notInstalled));
                r.Check("plain Among Us, mod copy not installed: yes (it needs no mod copy)", ClientApp.TrayPlayable(false, false, true, false, notInstalled));
                r.Check("no status yet: yes", ClientApp.TrayPlayable(false, false, false, false, null) && ClientApp.TrayPlayable(false, false, true, false, null));
                r.Check("during a task or a game: no, whichever game", !ClientApp.TrayPlayable(true, false, true, false, installed) && !ClientApp.TrayPlayable(false, true, true, false, installed) && !ClientApp.TrayPlayable(false, true, false, false, installed));
            });

            r.Test("the stub", () =>
            {
                r.Check("AegisFactory gives the Aegis port", AegisFactory.Create(new AegisContext { StateDir = @"C:\S" }) is AegisService);
                var stub = new AegisStub(new AegisContext { StateDir = @"C:\S" });
                r.Check("the stub (the fallback) is not the port", !stub.IsPorted);
                r.Check("stub: pre-launch did not run (never blocks)", !stub.PreLaunchScan(_ => { }, CancellationToken.None).Ran);
                r.Check("stub: scans are not available", stub.Rescan(_ => { }).NotAvailable && stub.ScanOnly(_ => { }).NotAvailable);
                r.Equal("events.log path", @"C:\S\events.log", stub.EventsLogPath);
                r.Equal("stub state", "off", stub.GetSnapshot().State);
            });
        }

        // ------------------------------------------------------------------ bridge (PORT-MAP 7)
        static void BridgeTests(SelfTestRunner r)
        {
            r.Section("bridge");
            string[] v01 = { "launch", "launchWindowed", "launchVanilla", "aegis.rescan", "aegis.scanOnly", "aegis.events", "openModFolder", "openLogsFolder", "openConfig", "openLog", "openReadme", "window.minimize", "window.close", "window.drag", "app.quit", "settings.set", "setLang",
                "install", "checkUpdate", "syncSteam", "pickSteam",
                "makeReport", "exportOne", "mailBug", "mailRequest", "openReportFolder", "shortcut.create", "uninstall",
                "showLog", "rebuild", "devUpdate", "profile.set", "profile.pickImage", "profile.clearImage" };
            string[] later = { "aegis.banConsole", "openExternal", "recent.clear", "player.vip", "player.restrict", "setOption", "status" };
            r.Check("the app's commands", v01.All(c => Bridge.Classify(c) == Bridge.Kind.Supported), string.Join(",", v01.Where(c => Bridge.Classify(c) != Bridge.Kind.Supported)));
            r.Check("later commands", later.All(c => Bridge.Classify(c) == Bridge.Kind.Later), string.Join(",", later.Where(c => Bridge.Classify(c) != Bridge.Kind.Later)));
            r.Check("UI-only commands are unknown to the app", new[] { "media.new", "soon", "aegis.status", "headless", "cliHelp", "aegis.trayOnly", "legal.third", "", null, "LAUNCH" }.All(c => Bridge.Classify(c) == Bridge.Kind.Unknown));
            r.Equal("unsupported answer", "{\"type\":\"result\",\"id\":\"a1\",\"ok\":false,\"unsupported\":true,\"data\":{\"cmd\":\"aegis.banConsole\",\"version\":\"" + AppInfo.UiVersion + "\",\"arrives\":\"later\"}}", Bridge.ResultJson("a1", Bridge.Unsupported("aegis.banConsole")));
            // v0.4: the last three launcher-only commands are the app's now. The log page is NOT busy-gated: the
            // launcher's log box stayed readable while a task ran, and watching a long task is what it is for.
            r.Check("v0.4: the log page and the developer tools are the app's",
                Bridge.Classify("showLog") == Bridge.Kind.Supported && Bridge.Classify("rebuild") == Bridge.Kind.Supported && Bridge.Classify("devUpdate") == Bridge.Kind.Supported);
            r.Check("v0.4: the rebuild and the update wait while a long task runs; the log page does not",
                Bridge.BusyGated.Contains("rebuild") && Bridge.BusyGated.Contains("devUpdate") && !Bridge.BusyGated.Contains("showLog"));
            // v1.3: the profile picture. Picking a file must work while an install waits for Steam (like pickSteam), so
            // none of the three is busy-gated; the app stops a second dialog itself (ClientApp.avatarBusy).
            r.Check("v1.3: the profile picture is the app's; picking one does not wait for a long task",
                Bridge.Classify("profile.set") == Bridge.Kind.Supported && Bridge.Classify("profile.pickImage") == Bridge.Kind.Supported && Bridge.Classify("profile.clearImage") == Bridge.Kind.Supported
                && !Bridge.BusyGated.Contains("profile.set") && !Bridge.BusyGated.Contains("profile.pickImage") && !Bridge.BusyGated.Contains("profile.clearImage"));
            r.Check("v0.3: the report zip and the uninstall wait while a long task runs; a mail link does not",
                Bridge.BusyGated.Contains("makeReport") && Bridge.BusyGated.Contains("exportOne") && Bridge.BusyGated.Contains("uninstall") &&
                !Bridge.BusyGated.Contains("mailBug") && !Bridge.BusyGated.Contains("shortcut.create"));
            r.Equal("settings.set unsupported names the key", "autostart", ((Dictionary<string, object>)Bridge.Unsupported("settings.set", "autostart")["data"])["key"]);
            r.Equal("busy answer", "{\"type\":\"result\",\"id\":\"b\",\"ok\":false,\"busy\":true}", Bridge.ResultJson("b", Bridge.Busy()));
            r.Equal("event", "{\"type\":\"event\",\"name\":\"game\",\"data\":{\"running\":true}}", Bridge.EventJson("game", new Dictionary<string, object> { ["running"] = true }));
            var inv = Invoke.Parse("{\"type\":\"invoke\",\"id\":\"x-1\",\"cmd\":\"settings.set\",\"args\":{\"key\":\"close\",\"value\":\"quit\"}}");
            r.Check("invoke parsed", inv != null && inv.Cmd == "settings.set" && Json.Str(inv.Args, "value") == "quit");
            r.Check("no id -> ignored", Invoke.Parse("{\"type\":\"invoke\",\"cmd\":\"launch\"}") == null);
            r.Check("not an invoke -> ignored", Invoke.Parse("{\"type\":\"result\",\"id\":\"1\"}") == null && Invoke.Parse("[1,2]") == null && Invoke.Parse("nonsense") == null);
            r.Check("missing args -> empty", Invoke.Parse("{\"type\":\"invoke\",\"id\":\"2\",\"cmd\":\"launch\"}").Args.Count == 0);
            r.Check("launcher buttons wait while busy; window and settings do not", Bridge.BusyGated.Contains("openConfig") && !Bridge.BusyGated.Contains("window.minimize") && !Bridge.BusyGated.Contains("settings.set"));
            r.Check("v0.1.1: launchVanilla is a launch (waits while busy); startGame is a settings.set key", Bridge.BusyGated.Contains("launchVanilla") && !Bridge.Later.ContainsKey("launchVanilla") && Bridge.SettingKeys.Contains("startGame"));
            r.Check("our origin only", MainForm.IsOurUri("https://app.starpocket.local/index.html") && !MainForm.IsOurUri("http://app.starpocket.local/") && !MainForm.IsOurUri("https://app.starpocket.local:444/") && !MainForm.IsOurUri("https://fonts.googleapis.com/css2") && !MainForm.IsOurUri("https://wakayamachannel.github.io/x.mp4") && !MainForm.IsOurUri("file:///C:/x"));
            var snap = new AegisSnapshot { State = "watching", Detected = 2, Rows = { new AegisRow { Key = "mod", Title = "t", State = 4 } }, Events = { "21:04:12  x" } };
            string ev = Bridge.EventJson("aegis", snap.ToEvent());
            r.Check("aegis event shape", ev.Contains("\"state\":\"watching\"") && ev.Contains("\"rows\":[{\"key\":\"mod\"") && ev.Contains("\"events\":[\"21:04:12  x\"]") && ev.Contains("\"defs\":{\"version\":0,\"sig\":1}"), ev);
            var lo = new LaunchOutcome { Unsupported = true, Needs = "syncSteam" }.ToResult("launch");
            r.Equal("launch unsupported names what is needed", "syncSteam", lo["needs"]);

            // v0.2: which Install mode an install invoke means (the page's args first, then the state the app last sent it)
            Dictionary<string, object> A(params object[] kv)
            {
                var d = new Dictionary<string, object>();
                for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
                return d;
            }
            LaunchStatus Rep(string kind) => new LaunchStatus { PState = "repair", Repair = kind };
            r.Equal("install {}: first install", "", ClientApp.InstallMode(A(), null));
            r.Equal("install {resume:true}: carry on", "resume", ClientApp.InstallMode(A("resume", true), null));
            r.Equal("install {resume:\"true\"} too", "resume", ClientApp.InstallMode(A("resume", "true"), null));
            r.Equal("install {handoff:\"steam\"}: wait for Steam", "steam", ClientApp.InstallMode(A("handoff", "steam"), null));
            r.Equal("install {repair:\"mod\"}: put the mod back", "repairMod", ClientApp.InstallMode(A("repair", "mod"), null));
            r.Equal("install {} while the copy is not whole: repair", "repair", ClientApp.InstallMode(A(), Rep("files")));
            r.Equal("install {} while PocketRoles.dll changed: repairMod", "repairMod", ClientApp.InstallMode(A(), Rep("mod")));
            r.Equal("install {} while ready: first install", "", ClientApp.InstallMode(A(), new LaunchStatus { PState = "install" }));
            r.Equal("the page's args win over the state", "steam", ClientApp.InstallMode(A("handoff", "steam"), Rep("mod")));
        }

        // ------------------------------------------------------------------ 13. tray tooltip
        // ------------------------------------------------------------------ what the app may hand to the shell (v0.2)
        // Everything the app "opens" goes through ShellOpen: the game copy's exe, the two steam:// URLs it knows, the one
        // Microsoft page, a folder, and a file that is not a program. The recorder catches the start, so nothing runs here.
        static void ShellOpenTests(SelfTestRunner r)
        {
            r.Section("what may be opened");
            r.Test("the allow-list", () =>
            {
                r.Check("the game copy's exe", ShellOpen.Allowed(OpenKind.Game, @"D:\Among Us PocketRoles\Among Us.exe"));
                r.Check("another exe is refused", !ShellOpen.Allowed(OpenKind.Game, @"D:\x\evil.exe"));
                r.Check("a relative path is refused", !ShellOpen.Allowed(OpenKind.Game, @"Among Us.exe"));
                r.Check("the two steam:// URLs", ShellOpen.Allowed(OpenKind.SteamUrl, AppInfo.SteamRunGameUrl) && ShellOpen.Allowed(OpenKind.SteamUrl, AppInfo.SteamInstallUrl));
                r.Check("another steam:// URL is refused", !ShellOpen.Allowed(OpenKind.SteamUrl, "steam://openurl/https://x.example"));
                r.Check("the WebView2 page", ShellOpen.Allowed(OpenKind.Web, AppInfo.WebView2DownloadPage));
                r.Check("another page is refused", !ShellOpen.Allowed(OpenKind.Web, "https://example.invalid/"));
                r.Check("a folder", ShellOpen.Allowed(OpenKind.Folder, @"C:\Users\x\Desktop"));
                r.Check("a text file", ShellOpen.Allowed(OpenKind.TextFile, @"C:\x\BepInEx\config\PocketRoles.cfg") && ShellOpen.Allowed(OpenKind.TextFile, @"C:\x\events.log"));
                r.Check("a program is refused as a file", !ShellOpen.Allowed(OpenKind.TextFile, @"C:\x\setup.exe") && !ShellOpen.Allowed(OpenKind.TextFile, @"C:\x\a.ps1") && !ShellOpen.Allowed(OpenKind.TextFile, @"C:\x\a.lnk"));
                // v1.1: the developer switch's restart - this very exe, by its own full path, and nothing else
                string self = AppInfo.ExePath();
                r.Check("itself, by its own full path", !string.IsNullOrEmpty(self) && ShellOpen.Allowed(OpenKind.Self, self));
                r.Check("another exe of the same name is refused", !ShellOpen.Allowed(OpenKind.Self, @"C:\x\StarPocket Client.exe"));
                r.Check("a relative path, nothing, and another program are refused", !ShellOpen.Allowed(OpenKind.Self, "StarPocket Client.exe") && !ShellOpen.Allowed(OpenKind.Self, null) && !ShellOpen.Allowed(OpenKind.Self, @"C:\Windows\System32\anything.exe"));
                r.Check("nothing and an empty name are refused", !ShellOpen.Allowed(OpenKind.Folder, null) && !ShellOpen.Allowed(OpenKind.TextFile, ""));
                // v0.3: the report's mail links. The app fills a mailto: in; it never sends anything itself.
                r.Check("the project's three addresses", ShellOpen.Allowed(OpenKind.Mail, ShellOpen.MailUri(AppInfo.MailBug, "s", "b")) &&
                    ShellOpen.Allowed(OpenKind.Mail, ShellOpen.MailUri(AppInfo.MailRequest, "s", "b")) &&
                    ShellOpen.Allowed(OpenKind.Mail, ShellOpen.MailUri(AppInfo.MailHost, "s", "b")));
                r.Check("any other address is refused", !ShellOpen.Allowed(OpenKind.Mail, ShellOpen.MailUri("someone@example.invalid", "s", "b")));
                r.Check("a second address behind &to= is refused", !ShellOpen.Allowed(OpenKind.Mail, "mailto:" + AppInfo.MailBug + "?subject=x&to=someone@example.invalid"));
                r.Check("a newline in the link is refused", !ShellOpen.Allowed(OpenKind.Mail, "mailto:" + AppInfo.MailBug + "?subject=a\nbcc:x@y"));
                r.Check("a subject and a body are escaped, not dropped", ShellOpen.MailUri(AppInfo.MailBug, "a b", "c&d").EndsWith("?subject=a%20b&body=c%26d", StringComparison.Ordinal),
                    ShellOpen.MailUri(AppInfo.MailBug, "a b", "c&d"));
            });
            r.Test("nothing is started when it is not on the list", () =>
            {
                var seen = new List<string>();
                ShellOpen.Recorder = (kind, psi) => seen.Add(kind + " " + psi.FileName);
                try
                {
                    ShellOpen.SteamUrl(AppInfo.SteamRunGameUrl);
                    ShellOpen.Folder(@"C:\Users\x\Desktop");
                    r.Equal("the allowed ones are handed over", "SteamUrl " + AppInfo.SteamRunGameUrl + "|Folder C:\\Users\\x\\Desktop", string.Join("|", seen));
                    bool refused = false;
                    try { ShellOpen.WebPage("https://example.invalid/"); } catch (InvalidOperationException) { refused = true; }
                    r.Check("a page that is not ours throws instead", refused && seen.Count == 2);
                    refused = false;
                    try { ShellOpen.TextFile(@"C:\x\evil.cmd"); } catch (InvalidOperationException) { refused = true; }
                    r.Check("a program as a text file throws instead", refused && seen.Count == 2);
                }
                finally { ShellOpen.Recorder = null; }
            });
        }

        static void TooltipTests(SelfTestRunner r)
        {
            r.Section("tray");
            foreach (var l in Lang.Codes)
            {
                string w = TrayText.Tooltip(l, new AegisSnapshot { State = "watching", Detected = int.MaxValue, Kicked = int.MaxValue });
                r.Check("watching tooltip <= 63 (" + l + ")", w.Length <= 63, w.Length + ": " + w);
                r.Check("idle / off tooltip <= 63 (" + l + ")", TrayText.Tooltip(l, new AegisSnapshot { State = "idle" }).Length <= 63 && TrayText.Tooltip(l, null).Length <= 63);
            }
            r.Equal("idle (ja)", "StarPocket Client — Aegis 待機中", TrayText.Tooltip("ja", new AegisSnapshot { State = "idle" }));
            r.Equal("watching (en)", "StarPocket Client — Aegis watching · 3 flagged · 1 removed", TrayText.Tooltip("en", new AegisSnapshot { State = "kicked", Detected = 3, Kicked = 1 }));
            r.Equal("clip ends with …", 63, TrayText.Clip(new string('a', 80)).Length);
            foreach (var l in Lang.Codes)
            {
                string red = TrayText.Tooltip(l, new AegisSnapshot { State = "watching", Detected = 1, LastScanSerious = 2 });
                r.Check("red last scan wins over watching (" + l + ")", red == S.T(l, "tip.serious") && red.Length <= 63, red);
                string old = TrayText.Tooltip(l, new AegisSnapshot { State = "off", OldTray = true });
                r.Check("old Aegis tray running: says so, not \"off\" (" + l + ")", old == S.T(l, "tip.oldtray") && old.Length <= 63, old);
            }
            r.Equal("red (ja)", "StarPocket Client — Aegis が問題を見つけました", TrayText.Tooltip("ja", new AegisSnapshot { State = "idle", LastScanSerious = 1 }));
            r.Equal("old tray (en)", "StarPocket Client — the old Aegis tray is watching", TrayText.Tooltip("en", new AegisSnapshot { State = "off", OldTray = true }));
            r.Equal("plain off (zh-CN)", "StarPocket Client — Aegis 已停止", TrayText.Tooltip("zh-CN", new AegisSnapshot { State = "off" }));
            r.Equal("quit during a game (en)", "Quitting also stops Aegis from watching the rest of this game. Quit?", S.T("en", "quit.confirm"));
            r.Equal("quit during a game (zh-CN)", "退出后，Aegis 将不再监视本局游戏。要退出吗？", S.T("zh-CN", "quit.confirm"));
            r.Check("menu words", S.T("ja", "tray.open") == "開く" && S.T("zh-CN", "tray.play") == "开始游戏" && S.T("en", "tray.rescan") == "Scan again" && S.T("ja", "tray.quit") == "終了");
        }

        // ------------------------------------------------------------------ 優しく閉じる (v1.2)
        /// <summary>持ち主 2026-09-24「× 押したときの挙動が落ちた感じ」: 窓は 140 ms 薄くなってから消える（src\Shell\CloseFade.cs）。
        /// 窓を出さないと目で見られないので、ここで言い切る: 演出が失敗しても必ず閉じる（then は 1 回、reset は必ず）、上限のミリ秒、
        /// 「動きを減らす」（Windows のアニメーション効果 OFF）や窓が見えていない時は演出しない。窓もタイマーも作らず、偽の時計で回す。</summary>
        static void CloseFadeTests(SelfTestRunner r)
        {
            r.Section("close fade");
            // (a) the numbers
            r.Check("140 ms is inside the 120-160 window, and under the cap", CloseFade.DurationMs >= 120 && CloseFade.DurationMs <= 160 && CloseFade.DurationMs <= CloseFade.MaxMs);
            r.Check("the tick is 20 ms or finer", CloseFade.TickMs > 0 && CloseFade.TickMs <= 20);
            // (b) when there is no fade at all
            r.Equal("animations off (動きを減らす): no fade", 0, CloseFade.PlannedMs(true, false));
            r.Equal("animations unknown: no fade", 0, CloseFade.PlannedMs(true, null));
            r.Equal("window not on screen: no fade", 0, CloseFade.PlannedMs(false, true));
            r.Equal("visible and animations on: the full fade", CloseFade.DurationMs, CloseFade.PlannedMs(true, true));
            // (c) the curve
            r.Equal("alpha at 0 ms", 1.0, CloseFade.AlphaAt(0, 140));
            r.Check("alpha half-way is 0.5", Math.Abs(CloseFade.AlphaAt(70, 140) - 0.5) < 0.001, CloseFade.AlphaAt(70, 140).ToString());
            r.Equal("alpha at the end", 0.0, CloseFade.AlphaAt(140, 140));
            r.Equal("alpha past the end", 0.0, CloseFade.AlphaAt(9999, 140));
            r.Equal("alpha before the start", 1.0, CloseFade.AlphaAt(-5, 140));
            r.Equal("no duration: gone at once", 0.0, CloseFade.AlphaAt(0, 0));
            bool mono = true; double prev = 1.0;
            for (int t = 0; t <= 140; t++) { double a = CloseFade.AlphaAt(t, 140); if (a > prev + 1e-12) mono = false; prev = a; }
            r.Check("alpha never goes back up (0..140 ms, 1 ms steps)", mono);
            // (d) the fade with a fake clock
            r.Test("ms = 0: then at once, no alpha, reset once, synchronous", () =>
            {
                int then = 0, alpha = 0, reset = 0;
                var f = new CloseFade(0, () => 0, _ => alpha++, () => then++, () => reset++, _ => { });
                f.Start();
                r.Check("then 1, setAlpha 0, reset 1, finished", then == 1 && alpha == 0 && reset == 1 && f.Finished && !f.Running, then + "/" + alpha + "/" + reset);
            });
            r.Test("ms = 140, ticks at 0, 16, ..., 160", () =>
            {
                double now = 0; int then = 0, reset = 0, thenAt = -1, resetAfterThen = -1;
                var alphas = new List<double>();
                var f = new CloseFade(140, () => now, a => alphas.Add(a), () => { then++; thenAt = (int)now; }, () => { reset++; resetAfterThen = then; }, _ => { });
                f.Start();
                r.Check("running after Start, alpha 1.0 first, then not yet", f.Running && alphas.Count == 1 && alphas[0] == 1.0 && then == 0);
                for (now = 16; now <= 160; now += 16) f.Tick();
                r.Check("then exactly once, on the tick at or past 140 ms", then == 1 && thenAt >= 140, "then=" + then + " at " + thenAt);
                bool down = true;
                for (int i = 1; i < alphas.Count; i++) if (alphas[i] > alphas[i - 1] + 1e-12) down = false;
                r.Check("alpha never rose and ended at 0", down && alphas[alphas.Count - 1] == 0.0, string.Join(",", alphas.Select(a => a.ToString("0.00"))));
                r.Check("reset once, after then", reset == 1 && resetAfterThen == 1);
                r.Check("finished, not running", f.Finished && !f.Running);
                int before = alphas.Count;
                now = 500; f.Tick();
                r.Check("a tick after the end does nothing", alphas.Count == before && then == 1 && reset == 1);
            });
            r.Test("setAlpha throws on the second tick: no exception, still closes", () =>
            {
                int calls = 0, then = 0, reset = 0; var log = new List<string>();
                var f = new CloseFade(140, () => calls * 16.0, _ => { if (++calls == 2) throw new InvalidOperationException("no window"); }, () => then++, () => reset++, log.Add);
                f.Start();
                f.Tick();
                r.Check("then 1, reset 1, finished, one log line", then == 1 && reset == 1 && f.Finished && !f.Running && log.Count == 1 && log[0].Contains("no window"), then + "/" + reset + "/" + log.Count);
            });
            r.Test("then throws: reset still runs and it is finished", () =>
            {
                int reset = 0; var log = new List<string>();
                var f = new CloseFade(0, () => 0, _ => { }, () => { throw new InvalidOperationException("boom"); }, () => reset++, log.Add);
                f.Start();
                r.Check("reset 1, finished, logged", reset == 1 && f.Finished && !f.Running && log.Count == 1);
            });
            r.Test("the clock jumps from 0 to 5000 ms", () =>
            {
                double now = 0; int then = 0; double last = -1;
                var f = new CloseFade(140, () => now, a => last = a, () => then++, null, null);
                f.Start();
                now = 5000; f.Tick();
                r.Check("then once, the last alpha is 0", then == 1 && last == 0.0 && f.Finished);
            });
            r.Test("Then replaced half-way: the later one wins", () =>
            {
                double now = 0; int a = 0, b = 0;
                var f = new CloseFade(140, () => now, _ => { }, () => a++, null, null);
                f.Start();
                now = 50; f.Tick();
                f.Then = () => b++;
                now = 200; f.Tick();
                r.Check("b once, a never", a == 0 && b == 1);
            });
            r.Test("Finish twice: then once", () =>
            {
                int then = 0;
                var f = new CloseFade(140, () => 0, _ => { }, () => then++, null, null);
                f.Start();
                f.Finish(); f.Finish();
                r.Check("once, finished, not running", then == 1 && f.Finished && !f.Running);
            });
            // (e) the window has the two doors ClientApp uses (no window is made here)
            r.Check("MainForm has FadeOut and EnsureOpaque", typeof(MainForm).GetMethod("FadeOut") != null && typeof(MainForm).GetMethod("EnsureOpaque") != null);
            // (f) the OS setting both sides read
            r.Test("Native.TryClientAreaAnimation does not throw", () =>
            {
                bool? v = Native.TryClientAreaAnimation();
                r.Check("answered: " + (v.HasValue ? v.Value.ToString() : "null"), true);
            });
            // (g) what the page is told
            r.Equal("the closing event's JSON", "{\"type\":\"event\",\"name\":\"window\",\"data\":{\"closing\":true,\"ms\":140}}",
                Bridge.EventJson("window", new Dictionary<string, object> { ["closing"] = true, ["ms"] = 140 }));
        }

        // ------------------------------------------------------------------ strings
        static void StringTests(SelfTestRunner r)
        {
            r.Section("strings");
            var ja = S.Table["ja"];
            foreach (var l in new[] { "zh-CN", "en" })
            {
                var missing = ja.Keys.Where(k => !S.Table[l].ContainsKey(k)).ToArray();
                r.Check("every key in " + l, missing.Length == 0, string.Join(",", missing));
            }
            // official Among Us terms (PORT-MAP 9.3)
            string[] unofficial = { "内鬼", "通风管", "举报" };   // terms-ok (the words to catch)
            var bad = S.Table["zh-CN"].Where(kv => unofficial.Any(u => kv.Value.Contains(u))).Select(kv => kv.Key).ToArray();
            r.Check("Chinese uses the official terms", bad.Length == 0, string.Join(",", bad));
            r.Equal("fallback to Japanese", "開く", S.T("fr", "tray.open"));
            r.Equal("unknown key", "no.such.key", S.T("en", "no.such.key"));
            r.Equal("bad format gives the raw text", "{0} not found: {1}", S.T("en", "op_notfound", "only one"));
            r.Equal("op_notfound", "設定ファイル が見つかりません: C:\\x.cfg", S.T("ja", "op_notfound", S.T("ja", "f_cfg"), @"C:\x.cfg"));
            r.Check("la_vanilla: the launcher's words in 3 languages (ps1 v0.5.5)", S.T("ja", "la_vanilla") == "Steam 版 (mod なし) を起動しました。" && S.T("zh-CN", "la_vanilla") == "已启动 Steam 原版 (无 mod)。" && S.T("en", "la_vanilla") == "Launched the Steam version (no mod).");
            r.Test("readme", () =>
            {
                string d = r.NewDir("readme");
                string src = Path.Combine(d, "src"), game = Path.Combine(d, "game");
                SelfTestRunner.Touch(Path.Combine(game, "README.md"));
                SelfTestRunner.Touch(Path.Combine(game, "README.en.md"));
                r.Equal("friend en", Path.Combine(game, "README.en.md"), LauncherFiles.ReadmePath(false, src, game, "en"));
                r.Equal("friend zh-CN falls back to README.md", Path.Combine(game, "README.md"), LauncherFiles.ReadmePath(false, src, game, "zh-CN"));
                r.Equal("developer: in Src (missing -> README.md there)", Path.Combine(src, "README.md"), LauncherFiles.ReadmePath(true, src, game, "ja"));
            });
        }

        // ------------------------------------------------------------------ the package next to the exe
        static void PackageTests(SelfTestRunner r)
        {
            r.Section("package");
            string exePath = Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);
            var vi = FileVersionInfo.GetVersionInfo(exePath);
            r.Equal("FileDescription", "StarPocket Client", vi.FileDescription);
            r.Equal("ProductName", "StarPocket Client", vi.ProductName);
            r.Equal("CompanyName", "StarPocket Games", vi.CompanyName);
            r.Test("icon A embedded", () =>
            {
                byte[] ico = Icons.IcoBytes;
                r.Check("resource is an .ico with 9 images", ico.Length > 6 && ico[2] == 1 && ico[4] == 9, "images: " + (ico.Length > 4 ? ico[4] : 0));
                var sizes = new List<int>();
                for (int k = 0; k < ico[4]; k++) sizes.Add(ico[6 + 16 * k] == 0 ? 256 : ico[6 + 16 * k]);
                r.Equal("sizes 16..256", "16,20,24,32,40,48,64,128,256", string.Join(",", sizes));
                using (var small = Icons.Brand(16)) using (var big = Icons.Brand(128)) r.Check("16 and 128 px load (System.Drawing skips the 256 PNG; Windows uses it)", small.Width == 16 && big.Width == 128, small.Width + "/" + big.Width);
                using (var ex = System.Drawing.Icon.ExtractAssociatedIcon(exePath)) r.Check("the exe has an application icon", ex != null && ex.Width > 0);
            });
            string ui = Path.Combine(exeDir, AppInfo.UiFolderName);
            r.Test("ui folder", () =>
            {
                string index = Path.Combine(ui, "index.html");
                r.Check("ui\\index.html", File.Exists(index), index);
                if (!File.Exists(index)) return;
                string html = File.ReadAllText(index, Encoding.UTF8);
                r.Check("starts as a standards-mode UTF-8 page", html.StartsWith("<!doctype html>", StringComparison.OrdinalIgnoreCase) && html.Contains("<meta charset=\"utf-8\">"));
                r.Check("has a CSP", html.Contains("http-equiv=\"Content-Security-Policy\""));
                r.Check("no Google Fonts", !html.Contains("fonts.googleapis.com") && !html.Contains("fonts.gstatic.com"));
                r.Check("no embedded crewmate image", !html.Contains("data:image/png;base64"));
                r.Check("crewmate icon is a separate file under ui\\img", File.Exists(Path.Combine(ui, @"img\pocketroles-128.png")) && html.Contains("href=\"img/pocketroles-128.png\""));
                r.Check("the v0.1 glue is loaded", html.Contains("<script src=\"host-v01.js\"></script>") && File.Exists(Path.Combine(ui, "host-v01.js")));
                r.Check("hooks for the glue", html.Contains("window.spHost = {"));
                r.Check("official Chinese term (伪装者, not 内鬼)", !html.Contains("内鬼") && html.Contains("按设置补足伪装者人数"));   // terms-ok
            });
            r.Test("no crewmate art inside the exe", () =>
            {
                string png = Path.Combine(ui, @"img\pocketroles-128.png");
                if (!File.Exists(png)) { r.Fail("crewmate check", "ui\\img\\pocketroles-128.png missing"); return; }
                byte[] needle = File.ReadAllBytes(png).Skip(64).Take(64).ToArray();
                byte[] exe = File.ReadAllBytes(exePath);
                r.Check("the PNG's bytes are not in the exe", IndexOf(exe, needle) < 0);
                var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                r.Check("the exe's only resource is icon A", names.Length == 1 && names[0] == Icons.ResourceName, string.Join(",", names));
            });
            r.Check("WebView2 files next to the exe", File.Exists(Path.Combine(exeDir, "Microsoft.Web.WebView2.Core.dll")) && File.Exists(Path.Combine(exeDir, "Microsoft.Web.WebView2.WinForms.dll")) && File.Exists(Path.Combine(exeDir, "WebView2Loader.dll")));
            r.Check("LICENSE and NOTICE next to the exe", File.Exists(Path.Combine(exeDir, "LICENSE")) && File.Exists(Path.Combine(exeDir, "NOTICE")));
            r.Test("WebView2's licence texts next to the exe", () =>
            {
                string lic = Path.Combine(exeDir, @"licenses\Microsoft.Web.WebView2-LICENSE.txt");
                string not = Path.Combine(exeDir, @"licenses\Microsoft.Web.WebView2-NOTICE.txt");
                r.Check("licenses\\Microsoft.Web.WebView2-LICENSE.txt (BSD 3-clause text)", File.Exists(lic) && File.ReadAllText(lic).Contains("Redistribution and use in source and binary forms"));
                r.Check("licenses\\Microsoft.Web.WebView2-NOTICE.txt", File.Exists(not) && new FileInfo(not).Length > 0);
                string notice = File.Exists(Path.Combine(exeDir, "NOTICE")) ? File.ReadAllText(Path.Combine(exeDir, "NOTICE")) : "";
                r.Check("NOTICE points to both", notice.Contains(@"licenses\Microsoft.Web.WebView2-LICENSE.txt") && notice.Contains(@"licenses\Microsoft.Web.WebView2-NOTICE.txt"));
            });
            r.Test("Feather's licence text next to the exe (the gear icon)", () =>
            {
                // ui\index.html's <symbol id="i-gear"> is the "settings" icon of Feather Icons (MIT): the copyright notice and
                // the permission notice go with every copy, so the text is shipped and NOTICE and the page itself point to it
                string lic = Path.Combine(exeDir, @"licenses\feather-LICENSE.txt");
                string text = File.Exists(lic) ? File.ReadAllText(lic) : "";
                r.Check("licenses\\feather-LICENSE.txt (the MIT text with Feather's copyright line)", text.Contains("Copyright (c) 2013-2017 Cole Bemis") && text.Contains("Permission is hereby granted, free of charge") && text.Contains("The above copyright notice and this permission notice shall be included"));
                string notice = File.Exists(Path.Combine(exeDir, "NOTICE")) ? File.ReadAllText(Path.Combine(exeDir, "NOTICE")) : "";
                r.Check("NOTICE names Feather and points to the file", notice.Contains("Feather Icons") && notice.Contains("Cole Bemis") && notice.Contains(@"licenses\feather-LICENSE.txt"));
                string index = Path.Combine(ui, "index.html");
                string html = File.Exists(index) ? File.ReadAllText(index, Encoding.UTF8) : "";
                r.Check("the page still has the gear and says where it comes from", html.Contains("<symbol id=\"i-gear\"") && html.Contains(@"licenses\feather-LICENSE.txt"));
            });
            r.Test("the files the exe needs next to it", () =>
            {
                r.Check("all there in the build output", PackageFiles.Missing(exeDir).Count == 0, string.Join(",", PackageFiles.Missing(exeDir)));
                string alone = r.NewDir("exe-alone");
                SelfTestRunner.Touch(Path.Combine(alone, "StarPocket Client.exe"));
                r.Equal("an exe copied alone: the 3 WebView2 DLLs and ui\\index.html are missing", "Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.WinForms.dll,WebView2Loader.dll,ui\\index.html", string.Join(",", PackageFiles.Missing(alone)));
                r.Check("a missing WebView2Loader.dll / wrong DLL is \"files missing\", not \"install the runtime\"", WebView2Gate.IsFileProblem(new DllNotFoundException()) && WebView2Gate.IsFileProblem(new BadImageFormatException()) && WebView2Gate.IsFileProblem(new FileNotFoundException()) && !WebView2Gate.IsFileProblem(new InvalidOperationException()));
                foreach (var l in Lang.Codes) r.Check("files.missing says to extract the whole zip (" + l + ")", S.T(l, "files.missing").Contains("zip"));
            });
            r.Test("the self-test's own work folder", () =>
            {
                string d = r.NewDir("workfolder");
                string foreign = Path.Combine(d, @"someone\work");
                SelfTestRunner.Touch(Path.Combine(foreign, "keep.txt"), "not ours");
                string why = SelfTestRunner.PrepareWork(foreign);
                r.Check("a \"work\" folder without the marker is refused and left alone", why != null && why.Contains("not made by the self-test") && File.Exists(Path.Combine(foreign, "keep.txt")), why);
                string ours = Path.Combine(d, @"ours\work");
                r.Check("a new one is made with the marker", SelfTestRunner.PrepareWork(ours) == null && File.Exists(Path.Combine(ours, SelfTestRunner.MarkerName)));
                SelfTestRunner.Touch(Path.Combine(ours, "old.txt"));
                r.Check("one with the marker is emptied and made again", SelfTestRunner.PrepareWork(ours) == null && !File.Exists(Path.Combine(ours, "old.txt")) && File.Exists(Path.Combine(ours, SelfTestRunner.MarkerName)));
            });
            r.Test("the UI glue (v0.1 review)", () =>
            {
                string js = Path.Combine(ui, "host-v01.js");
                string index = Path.Combine(ui, "index.html");
                if (!File.Exists(js) || !File.Exists(index)) { r.Fail("ui files", "host-v01.js or index.html missing"); return; }
                string g = File.ReadAllText(js, Encoding.UTF8), html = File.ReadAllText(index, Encoding.UTF8);
                r.Check("the tray's \"scan again\" runs the page's own scan (nav run aegis.rescan)", g.Contains("d.run === 'aegis.rescan'"));
                r.Check("the blocked button says it also starts the game", g.Contains("'ps.b.blocked': '確かめて起動'") && g.Contains("'ps.b.blocked': '重新检查并启动'") && g.Contains("'ps.b.blocked': 'Check and play'"));
                r.Check("settings v0.1 does not store go back to their defaults (PREF_DEFAULTS from the page)", html.Contains("AEGIS_LOG, PREF_DEFAULTS, prefs,") && g.Contains("APP_SIDE"));
            });
            // v0.1.1: 起動するゲーム in Settings → PocketRoles; PLAY follows it (the page decides which command PLAY sends)
            r.Test("the UI: 起動するゲーム (v0.1.1)", () =>
            {
                string js = Path.Combine(ui, "host-v01.js");
                string index = Path.Combine(ui, "index.html");
                if (!File.Exists(js) || !File.Exists(index)) { r.Fail("ui files", "host-v01.js or index.html missing"); return; }
                string g = File.ReadAllText(js, Encoding.UTF8), html = File.ReadAllText(index, Encoding.UTF8);
                r.Check("PLAY with plain Among Us sends launchVanilla, before any of the mod's states", html.Contains("  if (plainOn()) return launchPlain();\n  const s = PSTATE[pstate];") && html.Contains("const r = await bridge.invoke('launchVanilla');"));
                r.Check("PLAY with PocketRoles still sends launch (with the pre-launch steps)", html.Contains("const result = bridge.invoke('launch');") && html.Contains("  if (!s.task) return launch();"));
                r.Check("the tray's play runs the page's PLAY (nav run play)", html.Contains("'play': () => { closeAll(); show('game'); if (plainOn()) launchPlain(); else launch(); },") && g.Contains("if (d.run === 'play' || d.run === 'launch') H.run('play');"));
                r.Check("plain: the real game state after the start (import-ui hook)", html.Contains("running = !!(window.spHostGameRunning && window.spHostGameRunning()); renderPlay();   /* v0.1.1: the real game state */"));
                r.Check("the app's settings.json wins (shell event startGame)", g.Contains("d.startGame === 'pocketroles' || d.startGame === 'vanilla'") && html.Contains("startGame:'pocketroles',") && html.Contains("startGame:['pocketroles', 'vanilla']"));
                r.Check("the setting is a radio group in Settings → PocketRoles", html.Contains("h('div', { class:'set-block', id:'set-game', role:'radiogroup', 'aria-labelledby':'set-game-cap' }"));
                r.Check("the status line under PLAY names the game, with 「変更」", html.Contains("id=\"gameLink\" data-cmd=\"settings.game\"") && html.Contains("'settings.game': () => openGameSetting(),"));
                string[][] words =
                {
                    new[] { "'set.gameCap':'起動するゲーム'", "'set.gameCap':'启动的游戏'", "'set.gameCap':'Game to start'" },
                    new[] { "'set.gameVan':'ふつうの Among Us（Steam）'", "'set.gameVan':'原版 Among Us（Steam）'", "'set.gameVan':'Plain Among Us (Steam)'" },
                    new[] { "'ps.vanilla':'ふつうの Among Us（Steam）を起動します'", "'ps.vanilla':'将启动原版 Among Us（Steam）'", "'ps.vanilla':'Starts plain Among Us (Steam)'" },
                };
                foreach (var w in words) r.Check("texts in ja / zh-CN / en: " + w[2], w.All(html.Contains), string.Join(" ", w.Where(x => !html.Contains(x))));
                foreach (var key in new[] { "set.gamePr", "set.gamePrD", "set.gameVanD", "toast.gamePr", "toast.gameVan", "ps.m.vanilla", "ps.change", "toast.launchedPlain" })
                {
                    int n = 0, at = 0;
                    while ((at = html.IndexOf("'" + key + "':'", at, StringComparison.Ordinal)) >= 0) { n++; at++; }
                    r.Check("'" + key + "' in all 3 languages", n == 3, n + " found");
                }
                // 修復: a download sign with the arrow going down into a tray (owner, 2026-09-23 evening); install / update keep i-download
                const string trayIcon = "<symbol id=\"i-download-tray\" viewBox=\"0 0 24 24\"><path d=\"M12 3.5V15M7.5 10.5L12 15l4.5-4.5\"/><path d=\"M4 13v5a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-5\"/></symbol>";
                r.Check("修復 has the download-into-a-tray icon, not a wrench (owner, 2026-09-23)", html.Contains("svgIcon('i-download-tray'), t('set.repairBtn')") && !html.Contains("svgIcon('i-wrench'), t('set.repairBtn')") && !html.Contains("svgIcon('i-download'), t('set.repairBtn')"));
                r.Check("... the tray icon is drawn: the arrow ends inside the tray (tip y 15, the tray's sides from y 13)", html.Contains(trayIcon));
                r.Check("... install and update keep i-download", html.Contains("install:    { icon:'i-download',") && html.Contains("update:     { icon:'i-download',"));
                // v0.1.1 review
                r.Check("\"update & play\" starts the game 起動するゲーム names when the task ends (never the mod while plain is chosen)", html.Contains("  if (ok && s.thenLaunch) { if (plainOn()) launchPlain(); else launch(); }") && !html.Contains("  if (ok && s.thenLaunch) launch();"));
                r.Check("the tools list's plain start is the PLAY flow (busy guard, 「プレイ中」, the toast)", html.Contains("'launchVanilla': () => { closeAll(); show('game'); launchPlain(); },"));
                r.Check("plain: PLAY shows the game's name under 「プレイ」", html.Contains("const sub = !busy && !running && (plain ? 'set.gameVan' : s.sub);"));
                r.Check("plain: 「プレイ中」 until the game runs, at most 30 s (the game / status events end the wait)", html.Contains("const PLAIN_HOLD_MS = 30000;") && html.Contains("await Promise.race([wait(bridge.native ? PLAIN_HOLD_MS : 3200), new Promise(res => { plainHold = res; })]);")
                    && html.Contains("plainStarted,") && html.Contains("get plainHolding(){ return !!plainHold; },")
                    && g.Contains("if (gameRunning) H.plainStarted();\n  if (!H.busy && !H.plainHolding) { H.running = gameRunning; H.renderPlay(); }") && g.Contains("if (!H.plainHolding) H.running = gameRunning;"));
                r.Check("en: no settings path that is not there; curly quotes", html.Contains("'ps.m.vanilla':'No mod · “Game to start” in Settings'") && !html.Contains("Settings › Game to start"));
            });
            // v0.3.1 review (2026-09-23): the things that live only in the shipped UI files, so this suite says
            // something if they are ever taken out again. What they DO is measured in a real browser engine by
            // tools\uitest (headless Edge, ja / zh / en, light, dark and Windows high contrast).
            r.Test("the UI: the colour review (v0.3.1)", () =>
            {
                string js = Path.Combine(ui, "host-v01.js");
                string index = Path.Combine(ui, "index.html");
                if (!File.Exists(js) || !File.Exists(index)) { r.Fail("ui files", "host-v01.js or index.html missing"); return; }
                string g = File.ReadAllText(js, Encoding.UTF8), html = File.ReadAllText(index, Encoding.UTF8);
                // PLAY's hover may not repaint it: every ratio the guard works out is measured on the resting colours
                string hover = Between(html, ".play:hover{", "}");
                r.Check("PLAY's hover does not repaint it (no filter, no background, no colour)",
                    hover.Length > 0 && !hover.Contains("filter") && !hover.Contains("background") && !hover.Contains("color:"),
                    hover);
                // the colour field: the app is told once the wheel has stopped, not on every pixel of a drag
                r.Check("a drag round the colour wheel is one message to the app, not a hundred",
                    html.Contains("const ACCENT_QUIET_MS = 250;") && html.Contains("setAccent(field.value, false, true)")
                    && !html.Contains("field.addEventListener('input', () => setAccent(field.value));"));
                r.Check("... and the settings page is patched, not built again (the colour dialog hangs off that field)",
                    html.Contains("function paintAccentRow()") && html.Contains("function accentRoving("));
                // the page puts the colour back on every shell event: WebView2 reloads the page by itself
                r.Check("a page WebView2 reloaded by itself gets its colour back from the app",
                    g.Contains("if (H.acc) H.acc.apply(d.accent, d.vars && d.accent !== 'default' ? d.vars : null);")
                    && html.Contains("acc: ACC,"));
                // 配信モード covers another player's code, the way it switches the community search off
                r.Check("配信モード covers up the evidence box and empties it",
                    html.Contains(":root[data-streamer] .tool-ask .ask-in{-webkit-text-security:disc}")
                    && html.Contains("function forgetAsked()"));
                // a zip takes minutes: the button is held and the app's own words are shown while it runs
                r.Check("making a zip says so on the screen and holds its button",
                    g.Contains("btn.setAttribute('aria-busy', 'true')") && g.Contains("'v03.exporting'")
                    && g.Contains("'v03.reporting'"));
                r.Check("... a mistyped code is shown in a window, not in a notice that fades",
                    g.Contains("showDialog({ title:H.cmdLabel(cmd), body:String(r.error)"));
                r.Check("... and an answer that comes late is shown, not dropped",
                    g.Contains("document.addEventListener('host:late'") && html.Contains("new CustomEvent('host:late'"));
                // the Aegis lamp must not tell the host by colour alone
                r.Check("the Aegis lamp has words beside it, and keeps its colour in high contrast",
                    html.Contains("id=\"aegisDotText\"") && html.Contains("aria-describedby=\"aegisDotText\"")
                    && html.Contains(".aegis-dot{forced-color-adjust:none"));
                // the swatches in Windows high contrast
                r.Check("the chosen colour is marked by more than a pixel of border in high contrast",
                    html.Contains(".sw[aria-checked=\"true\"]{outline:3px solid Highlight;outline-offset:2px}")
                    && !html.Contains(".sw-dot,.sw-custom{forced-color-adjust:none}"));
                foreach (var key in new[] { "set.accentUp", "set.accentDown", "set.accentRed", "set.accentPicked",
                    "set.accentUsed", "aegis.dotOk", "aegis.dotFound" })
                {
                    int n = 0, i = 0;
                    while ((i = html.IndexOf("'" + key + "':'", i, StringComparison.Ordinal)) >= 0) { n++; i++; }
                    r.Check("'" + key + "' in all 3 languages", n == 3, n + " found");
                }
            });
            // v1.1: the start scan's card on every start (the owner, 2026-09-23): the switch in Settings, the app's value winning
            r.Test("the UI: the start scan's card (v1.1)", () =>
            {
                string js = Path.Combine(ui, "host-v01.js");
                string index = Path.Combine(ui, "index.html");
                if (!File.Exists(js) || !File.Exists(index)) { r.Fail("ui files", "host-v01.js or index.html missing"); return; }
                string g = File.ReadAllText(js, Encoding.UTF8), html = File.ReadAllText(index, Encoding.UTF8);
                r.Check("the page's default is on, like the app's", html.Contains("startScan:true,"));
                r.Check("the switch sits under 起動時の動作, beside \"start with Windows\"", html.Contains("toggleRow('set-startScan', t('set.startScan'), t('set.startScanD'), prefs.startScan, v => setPref('startScan', v))"));
                r.Check("the app's settings.json wins (shell event startScan)", g.Contains("if (typeof d.startScan === 'boolean' && H.prefs.startScan !== d.startScan) { H.prefs.startScan = d.startScan; H.savePrefs(); H.refreshSettings(); }"));
                r.Check("a red row at start opens the panel: the page answers nav open d-aegis", g.Contains("if (d.open === 'd-aegis') { H.closeAll(); H.openLayer('d-aegis'); }"));
                foreach (var key in new[] { "set.startScan", "set.startScanD" })
                {
                    int n = 0, i = 0;
                    while ((i = html.IndexOf("'" + key + "':'", i, StringComparison.Ordinal)) >= 0) { n++; i++; }
                    r.Check("'" + key + "' in all 3 languages", n == 3, n + " found");
                }
            });
            // 2026-09-24（持ち主）: 「概要は概要の画面、パッチノートはパッチノートって分けないか」「動画はいいや、要らない」、音は Aegis が
            // 見つけた時の 1 つだけ。ここは「出荷する UI のファイルにそれが本当に入っているか」だけを見る（見た目は tools\uitest）。
            r.Test("the UI: タブの分け方・動画なし・音は 1 つ (v1.2, 2026-09-24)", () =>
            {
                string js = Path.Combine(ui, "host-v01.js");
                string index = Path.Combine(ui, "index.html");
                if (!File.Exists(js) || !File.Exists(index)) { r.Fail("ui files", "host-v01.js or index.html missing"); return; }
                string g = File.ReadAllText(js, Encoding.UTF8), html = File.ReadAllText(index, Encoding.UTF8);
                // A: パッチノートのタブでは hero が「プレイの帯」にたたまれ、売り文句とカードは出ない。プレイの行は両方のタブに残る
                r.Check("どのタブかは <section id=\"view-game\"> の data-tab に書き、変わった時は一番上へ戻す",
                    html.Contains("<section class=\"view\" id=\"view-game\" aria-labelledby=\"gameTitle\" data-tab=\"ov\">")
                    && html.Contains("if (view.dataset.tab !== tabKey) { view.dataset.tab = tabKey; view.scrollTop = 0; }"));
                r.Check("パッチノートでは売り文句とカードが消え、hero はたたまれる（CSS）",
                    html.Contains("#view-game[data-tab=\"pn\"] .hero-copy,#view-game[data-tab=\"pn\"] .media-col{display:none}")
                    && html.Contains("#view-game[data-tab=\"pn\"] .hero{min-height:0;"));
                r.Check("プレイの行はタブの外の hero の中に 1 つ（どちらのタブでも出る）",
                    html.Contains("<button class=\"play\" id=\"playBtn\"")
                    && html.IndexOf("id=\"playBtn\"", StringComparison.Ordinal) < html.IndexOf("id=\"panel-ov\"", StringComparison.Ordinal));
                r.Check("概要の「新しいこと」のカードはタブを切り替えるだけ（一番上へ戻すのは selectTab）",
                    html.Contains("'media.new': () => selectTab('tab-pn'),") && !html.Contains("$('#panel-pn').scrollIntoView("));
                // B: 動画は無い（背景は自作の動く絵のまま）
                foreach (var gone in new[] { "video.intro", "video.howto", "bgVideo", "id=\"videoBtn\"", "id=\"muteBtn\"", "hero.standin", "hero.watch", "hero.motion", "dur.howto", "dur.soon", "set.video", "toast.videoOff", "toast.intro", "toast.howto", "media.howto", "class=\"vctl\"", "v-001.mp4", "<video" })
                    r.Check("動画の名残が無い: " + gone, !html.Contains(gone));
                foreach (var gone in new[] { "video.intro", "video.howto", "bgVideo", "dur.soon" })
                    r.Check("グルーにも無い: " + gone, !g.Contains(gone));
                r.Check("背景は自作の動く絵（art-hero）のまま", html.Contains("<symbol id=\"art-hero\"") && html.Contains("<use href=\"#art-hero\""));
                r.Check("アプリ側の設定キーからも bgVideo が消えた", !Bridge.SettingKeys.Contains("bgVideo"));
                // 音の入切と音量は残る（設定 → 全般 → 音）。hero の消音ボタンが無くなっても、設定そのものは消さない
                r.Check("音の入切と音量は設定 → 全般 → 音に残っている",
                    html.Contains("toggleRow('set-snd', t('set.snd'), t('set.sndD'), prefs.sound, v => { setPref('sound', v); vol.disabled = !v; })")
                    && html.Contains("setPref('volume', +vol.value, '')") && html.Contains("sound:true, volume:40,")
                    && Bridge.SettingKeys.Contains("sound") && Bridge.SettingKeys.Contains("volume"));
                // 音は Aegis が見つけた時の 1 つだけ
                r.Check("音の表は found だけ（起動音とプレイの音は無い）",
                    html.Contains("const SP_SOUNDS = {\"found\":{") && !html.Contains("\"start\":{\"notes\"") && !html.Contains("\"play\":{\"notes\"")
                    && !html.Contains("sound.play('start')") && !html.Contains("sound.play('play')"));
                r.Check("鳴らすのはアプリ: aegis イベントの scan 番号が進んで赤だった時に 1 回",
                    g.Contains("window.spSound.play('found')") && g.Contains("if (scan > lastScan) {") && g.Contains("d.phase === 'done' && (d.serious || 0) > 0"));
                r.Check("入切と音量はアプリの settings.json が勝つ（shell イベント）",
                    g.Contains("if (typeof d.sound === 'boolean' && H.prefs.sound !== d.sound) { H.prefs.sound = d.sound; H.savePrefs(); H.refreshSettings(); }")
                    && g.Contains("H.prefs.volume = d.volume;"));
                foreach (var key in new[] { "set.snd", "set.sndD", "set.sndVol", "media.new", "media.aegis", "tab.overview", "tab.notes", "hero.pitch", "hero.desc" })
                {
                    int n = 0, i = 0;
                    while ((i = html.IndexOf("'" + key + "':'", i, StringComparison.Ordinal)) >= 0) { n++; i++; }
                    r.Check("'" + key + "' in all 3 languages", n == 3, n + " found");
                }
                string[] one = { "'set.sndD':'Aegis が見つけた時の 1 つだけ。", "'set.sndD':'只有 1 个：Aegis 有发现时。", "'set.sndD':'Only one: when Aegis finds something." };
                r.Check("音の説明は「1 つだけ」（ja / zh-CN / en）", one.All(html.Contains), string.Join(" ", one.Where(x => !html.Contains(x))));
            });
            // v1.2（持ち主 2026-09-24「× 押したときの挙動が落ちた感じ」）: 窓は MainForm.FadeOut で薄くなり、ページは host-v01.js で
            // ほんの少し縮む（"window" イベントの closing / ms）。ここは出荷する UI のファイルにその受け口があること、数がアプリの
            // CloseFade と同じこと、index.html の兄弟には書いていないこと（グルーだけの持ち物）を見る。
            r.Test("the UI: 優しく閉じる (v1.2)", () =>
            {
                string js = Path.Combine(ui, "host-v01.js");
                string index = Path.Combine(ui, "index.html");
                if (!File.Exists(js) || !File.Exists(index)) { r.Fail("ui files", "host-v01.js or index.html missing"); return; }
                string g = File.ReadAllText(js, Encoding.UTF8), html = File.ReadAllText(index, Encoding.UTF8);
                r.Check("the page listens to the window event's closing", g.Contains("on('window', d => {") && g.Contains("if (d.closing === true) {"));
                r.Check("動きを減らす (prefers-reduced-motion): nothing moves", g.Contains("if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;"));
                r.Check("the page clamps the app's ms to the same cap (CloseFade.MaxMs)", g.Contains("Math.min(" + CloseFade.MaxMs + ", Math.max(0, Math.round(Number(d.ms) || 0)))"));
                r.Check("the page sets the duration and the class", g.Contains("app.style.setProperty('--sp-leave-ms', ms + 'ms');") && g.Contains("app.classList.add('sp-leave');"));
                r.Check("visible:true takes it off again", g.Contains("} else if (d.visible === true) {") && g.Contains("app.classList.remove('sp-leave');"));
                r.Check("the CSS shrinks a little, with the app's DurationMs as its default", g.Contains("html.sp-live .app.sp-leave{") && g.Contains("var(--sp-leave-ms," + CloseFade.DurationMs + "ms)"));
                r.Check("index.html (the prototype's sibling) says nothing about it", !html.Contains("sp-leave"));
            });
            r.Check("the first-paint colour script can be replaced, not only set once when the app starts",
                typeof(MainForm).GetMethod("SetBootScriptAsync") != null);
            r.Check("x86 build", IntPtr.Size == 4, "pointer size " + IntPtr.Size);
        }

        /// <summary>What is between the first <paramref name="from"/> and the next <paramref name="to"/>, or "".</summary>
        static string Between(string s, string from, string to)
        {
            int a = s.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            a += from.Length;
            int b = s.IndexOf(to, a, StringComparison.Ordinal);
            return b < 0 ? "" : s.Substring(a, b - a);
        }

        static int IndexOf(byte[] hay, byte[] needle)
        {
            if (needle.Length == 0) return -1;
            for (int i = 0; i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
