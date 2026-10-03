// Everything the app decides once at start (PORT-MAP 5 step 4): folders, mode, launcher-state.json (read, written and
// taken over from the PowerShell launcher since v0.2), settings, language, the game copy and Steam's copy.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
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
        /// <summary>2026-10-01: どの決まりで Paths.Modded になったか（"arg" / "env" / "setting" / "default"、CopyPlace.Source）。
        /// "arg" と "env" の時は、設定画面の「場所を変える」は理由を言って断る。</summary>
        public string CopySource = "default";
        public string SteamOverride;
        /// <summary>Steam's Among Us folder found at start (null when not found), like $script:Steam.</summary>
        public string SteamDir;
        /// <summary>launcher-state.json: the launcher's own file when the app runs from its folder (or when a developer
        /// build runs from the repository), else the app's own in %LOCALAPPDATA%\StarPocket\Client, filled once from the
        /// old launcher's (PORT-MAP 13.1). v1.5: the switch in Settings no longer changes which file this is - see
        /// <see cref="LoadState"/>.</summary>
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
            string envGameDir = Environment.GetEnvironmentVariable("POCKETROLES_GAMEDIR");
            c.CopySource = CopyPlace.Source(cli.GameDir, envGameDir, c.Settings.CopyDir, c.DevMode);
            string modded = GameFolders.ResolveModded(new ModdedInputs
            {
                GameDirArg = cli.GameDir,
                EnvGameDir = envGameDir,
                SettingCopyDir = c.Settings.CopyDir,
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

        /// <summary>PORT-MAP 13.1: the launcher's own file when the app sits in the launcher's folder (or in a developer
        /// build running from the repository, where the launcher writes lastBuiltGameVersion); otherwise the app's own
        /// file, filled once from the old launcher's - found through the Desktop shortcut "PocketRoles Launcher.lnk" -
        /// so settings and records carry over.
        /// <para>v1.5: WHICH file is the file may not depend on the switch in Settings, and until now it did. 「shared」
        /// was judged from Src, and Src is the author's working copy in developer mode (DevSource.Choose), so 開発 on
        /// read &lt;working copy&gt;\launcher-state.json while 開発 off read the app's own in %LOCALAPPDATA%: two files of
        /// installedVersion / modSource / gameVersion / lastCheck / lastBuiltGameVersion for ONE game copy (both modes
        /// install into the same ..\Among Us PocketRoles). 「更新を確認」 answers from modSource
        /// (ReleaseInfo.ModCurrent), so with the very same DLL installed one mode said 「最新です」 and the other offered
        /// the version that was already there. Now only where the EXE is decides, which no switch can move:
        /// a developer build from the repository keeps the repository's file; an exe placed in the old launcher's own
        /// folder keeps that folder's file (judged on ExeDir, not Src); every published exe uses the app's own file in
        /// both modes. Records left in the working copy's file by the older behaviour are carried over once, by
        /// <see cref="TakeOverDevState"/>, so nobody loses what is written there.</para></summary>
        internal void LoadState(Func<string, string> readShortcut)
        {
            bool devFromExe = DevMode && !DevFromSetting;   // the project file sits beside the exe (or --source-dir in a developer build)
            // (an empty ExeDir is never asked about: Join would make "PocketRolesLauncher.ps1" a RELATIVE path and
            // Test-Path would answer about whatever folder the app happens to be started in)
            bool shared = devFromExe || (!string.IsNullOrEmpty(ExeDir) && LegacyLauncher.IsLauncherFolder(ExeDir));
            string path = shared
                ? GameFolders.Join(devFromExe ? Src : ExeDir, AppInfo.StateFileName)
                : Path.Combine(DataDir, AppInfo.StateFileName);
            State = LauncherStateFile.Load(path);
            State.Log = Log.Write;
            // Unreadable: the app's own file IS there, it just could not be read this moment. Taking the old launcher's
            // over now would throw the viewer's own settings and records away (PORT-MAP 13.1).
            if (shared || State.Unreadable) return;
            if (!State.Found)
            {
                string old = null;
                try { old = LegacyLauncher.FindStateFile(Desktop, readShortcut) ?? DevStateFile(); }
                catch (Exception ex) { Log.Write("launcher-state.json: " + ex.Message); }
                if (old != null) State.MigrateFrom(old, DateTime.Now);
            }
            TakeOverDevState();
        }

        /// <summary>The working copy's own launcher-state.json (null when there is no working copy on this PC).</summary>
        string DevStateFile()
        {
            if (DevFolder == null) return null;
            string p = GameFolders.Join(DevFolder, AppInfo.StateFileName);
            return GameFolders.PathExists(p) ? p : null;
        }

        /// <summary>Written into this file when the carry-over below has been done, so it happens once and a report says
        /// where the values came from.</summary>
        const string DevStateMergedFrom = "devStateMergedFrom";
        const string DevStateMergedAt = "devStateMergedAt";

        /// <summary>Keys that describe where a file's OWN contents came from: carrying them over would make this file
        /// claim the other file's history.</summary>
        static readonly string[] NotCarriedOver = { "migratedFrom", "migratedAt", DevStateMergedFrom, DevStateMergedAt };

        /// <summary>v1.5, the other half of the fix above: what the app wrote into the working copy's launcher-state.json
        /// while developer mode read that file instead of this one is brought over ONCE - only keys this file does not
        /// have yet, never on top of a value that is already here, and the working copy's file is not changed or
        /// deleted (the PowerShell launcher keeps reading it). Nothing is written when there is nothing to carry over.
        /// <para>Why it is needed and not just tidy: lastBuiltGameVersion and modSource live there. Without this, the
        /// first start after the fix would say 「再ビルドが必要」 for a build that is already in the copy and offer
        /// 「更新」 for the version that is already installed - the same lie, from the other side.</para>
        /// <para>Why carrying a record over cannot make the app lie: both modes install into the same folder on this PC
        /// (developer mode's copy is &lt;working copy&gt;\..\Among Us PocketRoles - the Desktop copy friend mode uses -
        /// GameFolders.ResolveModded), a key is only ever filled where this file had NO answer at all, and what the page
        /// shows about the copy is read from the copy itself (InstallInfo.Read), never from these records. The records
        /// decide one thing each: modSource whether a re-published zip of the same number counts as installed,
        /// copiedGameVersion whether the game files may be left alone (and only when the copy on disk already has that
        /// very version), lastBuiltGameVersion whether a rebuild is needed.</para></summary>
        void TakeOverDevState()
        {
            try
            {
                string devPath = DevStateFile();
                if (devPath == null || State.Path == null || State.Str(DevStateMergedFrom) != null) return;
                if (string.Equals(devPath, State.Path, StringComparison.OrdinalIgnoreCase)) return;
                var dev = LauncherStateFile.Load(devPath);
                if (!dev.Found) return;   // there but unreadable this moment: nothing is guessed, another start may read it
                var have = new HashSet<string>(StringComparer.Ordinal);
                foreach (var kv in State.All) have.Add(kv.Key);
                var carry = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var kv in dev.All)
                    if (!have.Contains(kv.Key) && Array.IndexOf(NotCarriedOver, kv.Key) < 0) carry[kv.Key] = kv.Value;
                if (carry.Count == 0) return;
                int taken = carry.Count;
                carry[DevStateMergedFrom] = devPath;
                carry[DevStateMergedAt] = LauncherStateFile.Stamp(DateTime.Now);
                if (State.Update(carry)) Log.Write("launcher-state.json: " + taken + " record(s) taken over from " + devPath);
            }
            catch (Exception ex) { Log.Write("launcher-state.json: " + ex.Message); }
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
