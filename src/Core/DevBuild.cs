// Developer mode only: 「再ビルド」 and 「更新」 (PORT-MAP 14.5; ps1 Invoke-Build 1756-1778 and Invoke-Update 1780-1819).
// These are the author's own two buttons. They exist only while PocketRoles.csproj sits next to the app, they are the
// last thing the PowerShell launcher could do that this app could not, and they are the ONLY place the app starts a
// program that is not the game: the compiler. Why that is still not a shell is written at the head of ShellOpen.cs.
//
// 「再ビルド」  : dotnet build -c Release in Src -> BepInEx\plugins\PocketRoles.dll, then Aegis's fingerprint of that
//                new DLL and lastBuiltGameVersion in launcher-state.json (so the play button stops saying "rebuild").
// 「更新」      : copy Steam's game files over the mod copy -> delete the old interop/cache -> start the game once so
//                BepInEx writes a new interop (up to 420 s, then it is closed) -> rebuild.
//
// Everything that touches the world is a seam (RunBuild, StartInterop, Sleep, Now, GameRunning, SteamRunning), so the
// self-test plays both buttons - including a failed build, a game that never writes interop and a game that exits by
// itself - without a compiler, without Steam and without ever starting Among Us.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Starpocket.Client.Core
{
    internal sealed class DevBuild
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public ModPaths Paths;
        /// <summary>The folder with PocketRoles.csproj: what the compiler is pointed at.</summary>
        public string Src;
        public string AegisStateDir;
        /// <summary>v1.1: the app's own data folder, where mod-origin.txt says which DLL this app put in the copy
        /// (<see cref="ModOriginFile"/>). null (the self-test's fakes): nothing is written down.</summary>
        public string OriginDir;
        /// <summary>%USERPROFILE%\.dotnet\dotnet.exe, like the launcher's $script:Dotnet.</summary>
        public string DotnetPath = DefaultDotnet();
        /// <summary>%TEMP%\pocketroles-build.log: what the compiler printed, kept for the author to read.</summary>
        public string BuildLogPath = DefaultBuildLog();
        public LauncherStateFile State;
        public Func<string> Lang = () => "ja";
        public Action<string> Log = _ => { };
        public Action<TaskProgress> Progress = _ => { };
        public Func<bool> GameRunning = Processes.GameRunning;
        public Func<bool> SteamRunning = Processes.SteamRunning;
        /// <summary>Steam's Among Us as the app knows it (null when it was never found).</summary>
        public Func<string> SteamDir = () => null;
        public Func<DateTime> Now = () => DateTime.Now;
        public Action<int> Sleep = ms => Thread.Sleep(ms);
        /// <summary>The compiler. Replaced by the self-test, which never compiles anything.</summary>
        public Func<string, string, BuildRun> RunBuild = ShellOpen.RunBuild;
        /// <summary>Starts the mod copy's game for the interop run; null means "the real one".</summary>
        public Func<IGameRun> StartInterop;
        /// <summary>Copying the game files and clearing interop are the same code the Steam sync uses.</summary>
        public Installer Installer;
        /// <summary>The log housekeeping: the previous game's log is kept before this one overwrites it.</summary>
        public Func<GameLogs> NewGameLogs = () => null;
        /// <summary>ps1: 420 s for the interop run, then 5 more seconds and the game is closed.</summary>
        public int InteropTimeoutSec = 420;
        public int InteropPollMs = 500;
        public int InteropGraceMs = 5000;

        internal static string DefaultDotnet()
        {
            string home = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(home)) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return GameFolders.Join(home, @".dotnet\" + ShellOpen.BuildProgram);
        }

        internal static string DefaultBuildLog()
        {
            string temp = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrEmpty(temp)) temp = Path.GetTempPath();
            return GameFolders.Join(temp, "pocketroles-build.log");
        }

        string T(string key, params object[] args) => S.T(Lang(), key, args);

        // ------------------------------------------------------------------ 「再ビルド」 (Invoke-Build)
        /// <summary>The tools list's 「再ビルド」 and the last step of 「更新」.</summary>
        public TaskOutcome Rebuild()
        {
            try
            {
                if (!Build(1, 1)) return TaskOutcome.Bad(BuildFailedText());
                return TaskOutcome.Good(T("dev_build_ok"));
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return TaskOutcome.Bad(e);
            }
        }

        /// <summary>The last build was closed for taking too long, rather than failing to compile.</summary>
        internal bool LastBuildTimedOut;

        /// <summary>What the person is shown when the build did not produce a DLL: the ordinary "it failed", or the
        /// timeout in its own words, so 20 minutes of waiting are not reported as a compiler error (v0.4 review).</summary>
        string BuildFailedText() => LastBuildTimedOut ? T("dev_build_timeout", ShellOpen.BuildTimeoutMs / 60000) : T("dev_build_failed");

        /// <summary>Invoke-Build. true when the compiler ended with 0 AND the DLL is really in BepInEx\plugins (the
        /// launcher checks both: an "ok" with no file is still a failure).</summary>
        internal bool Build(int step, int of)
        {
            if (!GameFolders.PathExists(DotnetPath)) { Log(T("sdk_missing", DotnetPath)); return false; }
            Delete(BuildLogPath);
            Log(T("dev_building"));
            Progress(new TaskProgress { Task = "rebuild", Step = step, Of = of, Value = (double)(step - 1) / of });
            var run = RunBuild(DotnetPath, Src);
            if (run == null) run = new BuildRun { Error = "?" };
            LastBuildTimedOut = run.TimedOut;
            // the launcher wrote the compiler's output straight to this file; this app has it in hand already, so it
            // writes it as UTF-8 (the launcher read its own file back as the ANSI code page, which garbled 「エラー CS」)
            try { File.WriteAllText(BuildLogPath, run.Output ?? "", new UTF8Encoding(false)); }
            catch (Exception ex) { Log("build log: " + ex.Message); }
            if (run.Error != null) Log(T("err", run.Error));
            // the compiler was still going after its 20 minutes and was closed (v0.4 review). Said in its own words, so
            // the reason cannot be mistaken for one of the compiler errors listed below.
            if (run.TimedOut) Log(T("dev_build_timeout", ShellOpen.BuildTimeoutMs / 60000));
            if (run.ExitCode == 0 && GameFolders.PathExists(Paths.DllPath))
            {
                Log(T("dev_build_done"));
                if (Installer != null) Installer.SaveAegisFingerprint();
                ModOriginFile.Record(OriginDir, Paths.DllPath, ModOrigin.Dev, GameVersion.DllVersionString(Paths.DllPath), Now(), Log);   // v1.1: 開発用 is what is in the copy now
                string modVer = GameVersion.Read(Paths.Modded);
                if (!string.IsNullOrEmpty(modVer) && State != null)
                    State.Update(new Dictionary<string, object>
                    {
                        ["lastBuiltGameVersion"] = modVer,
                        ["lastBuiltAt"] = LauncherStateFile.Stamp(Now()),
                    });
                Progress(new TaskProgress { Task = "rebuild", Step = step, Of = of, Value = 1 });
                return true;
            }
            Log(T("dev_build_failed_exit", run.ExitCode));
            foreach (var line in ErrorLines(run.Output, 8)) Log("  " + line);
            Log(T("dev_build_ask"));
            return false;
        }

        static readonly Regex BuildError = new Regex(@"error CS|error MSB|エラー CS|エラー MSB");

        /// <summary>The launcher's filter: the lines that name a compiler error, each one only once, at most
        /// <paramref name="max"/> of them.</summary>
        internal static List<string> ErrorLines(string output, int max)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var found = new List<string>();
            foreach (var raw in (output ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (!BuildError.IsMatch(raw)) continue;
                if (!seen.Add(raw)) continue;
                found.Add(raw);
                if (found.Count >= max) break;
            }
            return found;
        }

        // ------------------------------------------------------------------ 「更新」 (Invoke-Update)
        /// <summary>Copy Steam's game files over the mod copy, throw the old interop away, let the game write a new one,
        /// then rebuild. The game IS started here - that is the only way BepInEx writes interop - and this app closes it
        /// again when it is done.</summary>
        public TaskOutcome DevUpdate()
        {
            try
            {
                if (GameRunning()) { Log(T("game_running")); return TaskOutcome.Bad(T("game_running")); }
                string steam = SteamDir();
                if (string.IsNullOrEmpty(steam) || !GameFolders.PathExists(GameFolders.Join(steam, "Among Us.exe")))
                {
                    Log(T("in_steam_notfound"));
                    return new TaskOutcome { NeedGame = true, Error = T("in_steam_notfound") };
                }
                if (!SteamRunning()) { Log(T("dev_need_steam")); return TaskOutcome.Bad(T("dev_need_steam")); }

                Log(T("dev_up_start"));
                Log(T("dev_up_1"));
                Progress(new TaskProgress { Task = "devUpdate", Step = 1, Of = 4, Value = 0 });
                if (Installer == null || !Installer.CopyGameFiles(steam, Paths.Modded, 1)) return TaskOutcome.Bad(T("in_copy_fail", "copy"));

                Log(T("dev_up_2"));
                Progress(new TaskProgress { Task = "devUpdate", Step = 2, Of = 4, Value = 0.25 });
                Installer.ClearInterop();

                Log(T("dev_up_3"));
                Progress(new TaskProgress { Task = "devUpdate", Step = 3, Of = 4, Value = 0.5 });
                if (!InteropRun()) { Log(T("dev_interop_failed")); return TaskOutcome.Bad(T("dev_interop_failed")); }
                Log(T("dev_interop_ok"));

                Log(T("dev_up_4"));
                Progress(new TaskProgress { Task = "devUpdate", Step = 4, Of = 4, Value = 0.75 });
                if (!Build(4, 4)) return TaskOutcome.Bad(BuildFailedText());
                Log(T("dev_up_done"));
                Progress(new TaskProgress { Task = "devUpdate", Step = 4, Of = 4, Value = 1 });
                return TaskOutcome.Good(T("dev_up_done"));
            }
            catch (Exception ex)
            {
                string e = T("err", ex.Message);
                Log(e);
                return TaskOutcome.Bad(e);
            }
        }

        string InteropDll => GameFolders.Join(Paths.Modded, @"BepInEx\interop\Assembly-CSharp.dll");

        /// <summary>The [3/4] step: keep the previous game's log, start the game, wait for the interop and for
        /// 「Chainloader startup complete」, then close it. true when interop is there afterwards.</summary>
        internal bool InteropRun()
        {
            // v0.5.5: a log that could not be archived is moved aside, never deleted - the game is about to overwrite it
            var logs = NewGameLogs();
            if (logs != null)
            {
                if (logs.SaveGameLog()) Delete(Paths.LogPath);
                else logs.MoveGameLogAside();
            }
            IGameRun game = null;
            try
            {
                game = StartInterop != null ? StartInterop() : StartGame();
                DateTime until = Now().AddSeconds(InteropTimeoutSec);
                bool done = false;
                while (Now() < until)
                {
                    Sleep(InteropPollMs);
                    if (game.HasExited) break;
                    if (GameFolders.PathExists(InteropDll) && GameFolders.PathExists(Paths.LogPath)
                        && Tail(Paths.LogPath, 30).IndexOf("Chainloader startup complete", StringComparison.Ordinal) >= 0)
                    {
                        done = true;
                        break;
                    }
                }
                if (!game.HasExited)
                {
                    Sleep(InteropGraceMs);
                    game.Stop();
                }
                // the launcher's rule: a game that closed itself is fine as long as the interop file is there
                return done || GameFolders.PathExists(InteropDll);
            }
            catch (Exception ex)
            {
                Log(T("err", ex.GetBaseException().Message));
                return false;
            }
            finally { if (game != null) game.Dispose(); }
        }

        IGameRun StartGame() => ShellOpen.StartGameRun(new ProcessStartInfo
        {
            FileName = Paths.GameExe,
            WorkingDirectory = Paths.Modded,
            UseShellExecute = true,
        });

        /// <summary>Get-Content -Tail: the last <paramref name="lines"/> lines, with the game still writing to the file.
        /// An unreadable log is simply "" - the wait then ends on the timer instead.</summary>
        internal static string Tail(string path, int lines)
        {
            try
            {
                var keep = new LinkedList<string>();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs, new UTF8Encoding(false), true))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        keep.AddLast(line);
                        if (keep.Count > lines) keep.RemoveFirst();
                    }
                }
                var sb = new StringBuilder();
                foreach (var l in keep) sb.Append(l).Append("\n");
                return sb.ToString();
            }
            catch (Exception) { return ""; }
        }

        static void Delete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }
    }
}
