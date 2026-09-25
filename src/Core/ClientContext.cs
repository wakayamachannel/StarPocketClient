// Everything the app decides once at start (PORT-MAP 5 step 4): folders, mode, launcher-state.json (read, written and
// taken over from the PowerShell launcher since v0.2), settings, language, the game copy and Steam's copy.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;

namespace Starpocket.Client.Core
{
    internal sealed class ClientContext
    {
        public CommandLine Cli;
        public string ExeDir;
        public string Src;
        public bool DevMode;
        /// <summary>v1.1: the author's working copy of the mod on this PC as <see cref="DevSource.Find"/> found it, whether or
        /// not it is in use (null on the ordinary PC: then the switch in Settings is not drawn at all).</summary>
        public string DevFolder;
        /// <summary>v1.1: developer mode came from the switch (Settings → PocketRoles → 開発), not from the exe's own folder.</summary>
        public bool DevFromSetting;
        public string Desktop;
        public string CacheDir;
        public ModPaths Paths;
        public string SteamOverride;
        /// <summary>Steam's Among Us folder found at start (null when not found), like $script:Steam.</summary>
        public string SteamDir;
        /// <summary>launcher-state.json: the launcher's own file when the app runs from its folder (or in developer mode),
        /// else the app's own in %LOCALAPPDATA%\StarPocket\Client, filled once from the old launcher's (PORT-MAP 13.1).</summary>
        public LauncherStateFile State;
        /// <summary>%LOCALAPPDATA% as the whole app sees it - one source of truth. DataDir, the Aegis folder and the
        /// uninstall's "never touch this" guards are all built from this same value (v0.3 review: the uninstall used to
        /// take its own from Environment.GetFolderPath, so its guards could be judged against another root).</summary>
        public string LocalAppData;
        public string DataDir;
        public string SettingsPath;
        /// <summary>v1.3: 自分のプロフィールの絵、アプリの写し（256×256 の PNG）。settings.json と同じ DataDir の下。他は誰も読まない。</summary>
        public string AvatarPath => Path.Combine(DataDir, ProfileImage.FolderName, ProfileImage.FileName);
        public ClientSettings Settings;
        public string AegisStateDir;
        public ClientLog Log;
        /// <summary>ja / zh-CN / en.</summary>
        public string Lang;

        internal static string LocalAppDataDir()
        {
            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrEmpty(localAppData)) localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return localAppData;
        }

        /// <summary>%LOCALAPPDATA%\StarPocket\Client\client.log, opened before anything else (the crash handler writes to it).
        /// Not rotated here: only the first instance rotates it (<see cref="ClientLog.RotateAtStart"/>).</summary>
        public static ClientLog OpenLog()
        {
            try { return new ClientLog(Path.Combine(LocalAppDataDir(), AppInfo.DataFolderRelative, "client.log")); }
            catch (Exception) { return ClientLog.None; }
        }

        /// <summary>Paths built from arguments, environment variables and launcher-state.json never throw (GameFolders.Join):
        /// a character Windows does not allow in a path makes that folder "not found", like the ps1's Join-Path / Test-Path.</summary>
        public static ClientContext Detect(CommandLine cli, string exeDir, ClientLog log = null, Func<string, string> readShortcut = null)
        {
            var c = new ClientContext { Cli = cli, ExeDir = exeDir };
            string localAppData = LocalAppDataDir();
            c.LocalAppData = localAppData;

            c.DataDir = Path.Combine(localAppData, AppInfo.DataFolderRelative);
            c.Log = log ?? new ClientLog(Path.Combine(c.DataDir, "client.log"));
            c.SettingsPath = Path.Combine(c.DataDir, "settings.json");
            c.Settings = ClientSettings.Load(c.SettingsPath);
            c.AegisStateDir = Path.Combine(localAppData, AppInfo.AegisStateFolderRelative);

            string desktopDefault = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);   // [Environment]::GetFolderPath('Desktop')
            c.Desktop = GameFolders.Desktop(cli.DesktopDir, desktopDefault);
            Func<string, string> shortcut = readShortcut ?? ShellLink.TargetPath;
            // PORT-MAP 3.2 as before: the exe's own folder (or --source-dir in a developer build) - then, v1.1, the
            // switch in Settings together with the working copy found on this PC (src\Core\DevSource.cs)
            string exeSource = GameFolders.ResolveSource(cli.SourceDir, exeDir);
            // v1.4: the folder chosen in Settings comes first, then the two fixed places (DevSource.Candidates)
            c.DevFolder = DevSource.Find(c.Desktop, shortcut, c.Settings.DevSourcePath);
            var pick = DevSource.Choose(exeSource, GameFolders.IsDevMode(false, exeSource), cli.Friend, c.Settings.DevBuild, c.DevFolder);
            c.Src = pick.Src;
            c.DevMode = pick.DevMode;
            c.DevFromSetting = pick.FromSetting;
            c.LoadState(shortcut);
            string modded = GameFolders.ResolveModded(new ModdedInputs
            {
                GameDirArg = cli.GameDir,
                EnvGameDir = Environment.GetEnvironmentVariable("POCKETROLES_GAMEDIR"),
                DevMode = c.DevMode,
                Src = c.Src,
                DesktopDirArg = cli.DesktopDir,
                DesktopDefault = desktopDefault,
                EnvOneDrive = Environment.GetEnvironmentVariable("OneDrive"),
                LocalAppData = localAppData,
            });
            c.Paths = ModPaths.For(modded);
            string temp = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrEmpty(temp)) temp = Path.GetTempPath();
            c.CacheDir = Path.Combine(temp, "PocketRolesLauncher");
            c.SteamOverride = !string.IsNullOrEmpty(cli.SteamDir) ? cli.SteamDir : (Environment.GetEnvironmentVariable("POCKETROLES_STEAMDIR") ?? "");
            c.ResolveLanguage();
            c.SteamDir = SteamLocator.Find(c.SteamOverride, c.State.SteamDir, SteamLocator.RegistryRoots());
            return c;
        }

        /// <summary>v1.1: the page draws the developer switch only where there is something to switch: the working copy was
        /// found, and this is not a developer build already running from the repository (nothing to turn off there).</summary>
        public bool DevSwitchShown => DevFolder != null && !(DevMode && !DevFromSetting);

        /// <summary>PORT-MAP 13.1: the launcher's own file when the app sits in the launcher's folder (or in developer mode,
        /// where the launcher writes lastBuiltGameVersion); otherwise the app's own file, filled once from the old
        /// launcher's - found through the Desktop shortcut "PocketRoles Launcher.lnk" - so settings and records carry over.</summary>
        internal void LoadState(Func<string, string> readShortcut)
        {
            bool shared = DevMode || LegacyLauncher.IsLauncherFolder(Src);
            string path = shared ? GameFolders.Join(Src, AppInfo.StateFileName) : Path.Combine(DataDir, AppInfo.StateFileName);
            State = LauncherStateFile.Load(path);
            State.Log = Log.Write;
            // Unreadable: the app's own file IS there, it just could not be read this moment. Taking the old launcher's
            // over now would throw the viewer's own settings and records away (PORT-MAP 13.1).
            if (shared || State.Found || State.Unreadable) return;
            string old = null;
            try { old = LegacyLauncher.FindStateFile(Desktop, readShortcut); }
            catch (Exception ex) { Log.Write("launcher-state.json: " + ex.Message); }
            if (old != null) State.MigrateFrom(old, DateTime.Now);
        }

        /// <summary>PORT-MAP 3.14.</summary>
        public void ResolveLanguage() => Lang = Core.Lang.Resolve(Cli.Language, Settings.Lang, State.Lang, CultureInfo.CurrentUICulture.Name);

        /// <summary>The viewer chose a language in Settings (auto / ja / zh-CN / en). --language only decides the first language
        /// (like the launcher's -Language: its combo box overrides it), so from now on settings.json -> launcher-state.json ->
        /// Windows decide.</summary>
        public void ChooseLanguage(string pref, string uiCultureName = null)
        {
            Settings.SetLang(pref);
            Cli.Language = null;
            Lang = Core.Lang.Resolve(null, Settings.Lang, State.Lang, uiCultureName ?? CultureInfo.CurrentUICulture.Name);
        }

        /// <summary>The Aegis evidence records on this PC. The app only READS them: the mod writes them and deletes them
        /// at 90 days (evidence-90d 「A」).</summary>
        public EvidenceStore NewEvidenceStore() => new EvidenceStore { Paths = Paths, LogQuiet = Log.Write };

        /// <summary>The report zip and the one-player evidence zip (v0.3, PORT-MAP 14.1).</summary>
        public ReportBuilder NewReportBuilder() => new ReportBuilder
        {
            Paths = Paths,
            Desktop = Desktop,
            CacheDir = CacheDir,
            StatePath = State != null ? State.Path : null,
            ClientLogPath = Log.Path,
            SteamDir = SteamDir,
            DevMode = DevMode,
            Lang = () => Lang,
            Log = Log.Write,
            LogQuiet = Log.Write,
            Evidence = NewEvidenceStore(),
        };

        public GameLogs NewGameLogs() => new GameLogs
        {
            // evidence-90d: before a log 30 days old is deleted, the two lines that back a record still kept are copied
            // into evidence\<id>.log, so a record older than its log can still be checked
            Evidence = NewEvidenceStore(),
            LogPath = Paths.LogPath,
            LogArchiveDir = Paths.LogArchiveDir,
            Desktop = Desktop,
            CacheDir = CacheDir,
            IsModdedGameRunning = () => Processes.ModdedGameRunning(Paths.Modded),
            Log = Log.Write,
            LogQuiet = Log.Write,
            Lang = Lang,
        };

        /// <summary>Install, repair, Steam sync and the update check of v0.2 (PORT-MAP 13.2), wired to this app's folders.</summary>
        public Installer NewInstaller(Action<TaskProgress> progress) => new Installer
        {
            Paths = Paths,
            Src = Src,
            CacheDir = CacheDir,
            AegisStateDir = AegisStateDir,
            OriginDir = DataDir,
            DevMode = DevMode,
            State = State,
            Lang = () => Lang,
            Log = Log.Write,
            Progress = progress ?? (_ => { }),
            SteamDir = () => SteamDir,
            SetSteamDir = dir => SteamDir = dir,
            FindSteam = () => SteamLocator.Find(SteamOverride, State.SteamDir, SteamLocator.RegistryRoots()),
        };

        /// <summary>Developer mode's 「再ビルド」 and 「更新」 (v0.4, PORT-MAP 14.5). It shares the Installer, so the file
        /// copying and the interop clearing are the same code the Steam sync uses, and the same GameLogs, so the
        /// previous game's log is kept before the interop run overwrites it.</summary>
        public DevBuild NewDevBuild(Action<TaskProgress> progress) => new DevBuild
        {
            Paths = Paths,
            Src = Src,
            AegisStateDir = AegisStateDir,
            OriginDir = DataDir,
            State = State,
            Lang = () => Lang,
            Log = Log.Write,
            Progress = progress ?? (_ => { }),
            SteamDir = () => SteamDir,
            Installer = NewInstaller(progress),
            NewGameLogs = NewGameLogs,
        };

        /// <summary>The uninstall (v0.3, PORT-MAP 14.4). One place builds it, so the window's "アンインストール" and the
        /// Apps &amp; Features entry (--uninstall) judge the same folders by the same %LOCALAPPDATA%.</summary>
        public Uninstaller NewUninstaller(Action<string> log = null) => new Uninstaller
        {
            DataDir = DataDir,
            ExePath = AppInfo.ExePath(),
            ExeDir = ExeDir,
            LocalAppData = LocalAppData,
            Paths = Paths,
            SteamDir = SteamDir,
            Log = log ?? Log.Write,
        };

        /// <summary>Get-StatusLines (PORT-MAP 3.4) plus the R state (PORT-MAP 13.3), and (v1.1) which DLL is in the copy.</summary>
        public LaunchStatus ComputeStatus(bool blockedByAegis, bool repairMod = false)
        {
            var s = LaunchStatus.Compute(DevMode, InstallInfo.Read(Paths), GameVersion.Read(SteamDir), State.LastBuiltGameVersion,
                Processes.SteamRunning(), Processes.GameRunning(), blockedByAegis, repairMod);
            s.Origin = ModOriginFile.Read(DataDir, Paths.DllPath);
            return s;
        }
    }
}
