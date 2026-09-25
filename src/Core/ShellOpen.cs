// The ONLY place in StarPocket Client that starts anything outside this process (owner, 2026-09-23: the app must be a
// real app in Task Manager, not a shell wrapper; antivirus flags exes that host PowerShell).
//
// What may be started, and nothing else (Kind):
//   Game     <the mod's game copy>\Among Us.exe with its own folder as the working folder (ShellExecute, like the
//            launcher's Start-Process), or the windowed arguments.
//   SteamUrl steam://rungameid/945360 (plain Among Us) and steam://install/945360 (the viewer pressed "install it in
//            Steam"): opened by the shell's URL handler, which hands them to Steam.
//   Web      an https:// page from the app's own list, opened in the viewer's browser (the WebView2 page, the support
//            site later): never a page a web page or a downloaded file chose.
//   Folder   a folder of the viewer's, opened by the shell's folder handler (their file manager).
//   TextFile a text file of the viewer's (the mod's config, a log, the README, events.log), opened with the handler of
//            the ".txt" class - the viewer's own text editor. The app never names notepad.exe.
//   Mail     a mailto: for ONE of the project's three addresses, opened in the viewer's mail app with the subject and
//            a template filled in (v0.3, the report zip). The app sends nothing: the person attaches the zip and
//            presses send themselves, and no address a page or a file chose can ever be used.
//   Build    (v0.4, developer mode only) the .NET SDK's own dotnet.exe, to compile PocketRoles.dll from the source
//            next to the app. This is the one thing the app cannot do inside itself - compiling is what a compiler
//            does - so it is the narrowest entry on this list, and it is still not a shell:
//              * the file must be named dotnet.exe and its path must be absolute (Allowed below);
//              * the arguments are written HERE, as three fixed words (build -c Release). No caller, no page, no file
//                and no setting can add, change or reorder them, so there is no command line to smuggle anything into;
//              * UseShellExecute is false: no cmd.exe, no shell parsing, no console window (CreateNoWindow), and the
//                output is read back through pipes rather than by running something else;
//              * DOTNET_ROOT and PATH are set ON THE CHILD ONLY (psi.EnvironmentVariables). The PowerShell launcher
//                set them on itself, so every program it started afterwards - the game included - inherited them;
//                PORT-MAP 3.5 forbids this app to change its own environment for exactly that reason.
//            Friend mode never reaches it: RebuildTools only exist while PocketRoles.csproj sits next to the app.
//   Self     (v1.1) this very exe once more, with no arguments. The developer switch in Settings changes what the app
//            IS at start (ClientContext.Detect decides the mode once, and everything is built from it), so the app
//            closes and starts itself again. Only the file that is running right now (AppInfo.ExePath), never another
//            exe of the same name, and nothing on its command line: what it is to be is in settings.json.
// Everything else - copying the game, unpacking zips, the registry, shortcuts - is done in-process by the app itself.
// Nothing here ever passes a command line to a shell: no cmd.exe, powershell.exe, robocopy, wscript, schtasks, reg.exe.
// Three things keep that true instead of it being a promise in a comment:
//   - the guard below (Allowed) refuses at run time whatever is not on the lists above;
//   - SelfTest\IdentitySelfTests.cs checks the built exe: its name, its icon, its manifest, and that it carries none of
//     those program names as text at all;
//   - .github\workflows\build.yml reads every .cs file and fails the build when Process.Start, ShellExecute,
//     CreateProcess or WinExec appears anywhere but this file (v0.2 had exactly that leak: notepad.exe and
//     explorer.exe were still being started by name from ClientApp).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Starpocket.Client.Core
{
    internal enum OpenKind { Game, SteamUrl, Web, Folder, TextFile, Mail, Build, Self }

    /// <summary>What <see cref="ShellOpen.RunBuild"/> gives back: what the compiler printed and how it ended.
    /// <see cref="Error"/> is set only when the build could not be started at all.</summary>
    internal sealed class BuildRun
    {
        public int ExitCode = -1;
        public string Output = "";
        public string Error;
        /// <summary>The compiler was still running after <see cref="ShellOpen.BuildTimeoutMs"/> and was closed. Without
        /// this the app waited for ever on a compiler that had stopped making progress (v0.4 review), and the window
        /// stayed on 「再ビルド」 with no way out but Task Manager.</summary>
        public bool TimedOut;
    }

    /// <summary>The game started for the interop run of the developer update: the only place this app ever waits for
    /// the game and then closes it. An interface so the self-test can play the whole wait without a game.</summary>
    internal interface IGameRun : IDisposable
    {
        bool HasExited { get; }
        /// <summary>Stop-Process -Force on the run this app started itself (never on a game someone else started).</summary>
        void Stop();
    }

    internal static class ShellOpen
    {
        /// <summary>The only program name the app may compile with, and the only arguments it may pass it.</summary>
        public const string BuildProgram = "dotnet.exe";
        static readonly string[] BuildArgs = { "build", "-c", "Release" };
        /// <summary>How long the app waits for the compiler before closing it. A first build of this mod takes one or
        /// two minutes, so 20 is generous; what it rules out is waiting for ever on a compiler that has stopped
        /// (v0.4 review). <see cref="BuildRun.TimedOut"/> then says so, in words, in all three languages.</summary>
        public const int BuildTimeoutMs = 20 * 60 * 1000;
        /// <summary>The only steam:// URLs the app ever opens (the app id is the app's own constant).</summary>
        public static readonly string[] AllowedSteamUrls = { AppInfo.SteamRunGameUrl, AppInfo.SteamInstallUrl };

        /// <summary>The only web pages the app ever opens in the browser.</summary>
        public static readonly string[] AllowedWebPages = { AppInfo.WebView2DownloadPage };

        /// <summary>The only addresses the app ever puts in a mailto:.</summary>
        public static readonly string[] AllowedMail = { AppInfo.MailBug, AppInfo.MailRequest, AppInfo.MailHost };

        /// <summary>Set by the self-test: what would have been started, instead of starting it.</summary>
        internal static Action<OpenKind, ProcessStartInfo> Recorder;

        /// <summary>true when <paramref name="target"/> may be started as <paramref name="kind"/>.</summary>
        public static bool Allowed(OpenKind kind, string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            switch (kind)
            {
                case OpenKind.Game:
                    // only a file named "Among Us.exe" (the mod's copy; Steam's own copy is started by Steam)
                    try { return string.Equals(Path.GetFileName(target), "Among Us.exe", StringComparison.OrdinalIgnoreCase) && Path.IsPathRooted(target); }
                    catch (ArgumentException) { return false; }
                case OpenKind.SteamUrl:
                    return Array.IndexOf(AllowedSteamUrls, target) >= 0;
                case OpenKind.Web:
                    return Array.IndexOf(AllowedWebPages, target) >= 0;
                case OpenKind.Mail:
                    // the whole mailto:, so a second address cannot be smuggled in behind a "&to=" or a line break
                    if (target.IndexOf('\0') >= 0 || target.IndexOf('\r') >= 0 || target.IndexOf('\n') >= 0) return false;
                    if (!target.StartsWith("mailto:", StringComparison.Ordinal)) return false;
                    int q = target.IndexOf('?');
                    string to = q < 0 ? target.Substring(7) : target.Substring(7, q - 7);
                    if (Array.IndexOf(AllowedMail, to) < 0) return false;
                    return q < 0 || target.IndexOf("&to=", q, StringComparison.OrdinalIgnoreCase) < 0;
                case OpenKind.Build:
                    // ONLY the SDK's dotnet.exe, by an absolute path. Not "a program called dotnet", not a .bat or a
                    // .cmd wrapper of that name, and nothing the viewer or a file could point somewhere else.
                    try { return Path.IsPathRooted(target) && string.Equals(Path.GetFileName(target), BuildProgram, StringComparison.OrdinalIgnoreCase); }
                    catch (ArgumentException) { return false; }
                case OpenKind.Self:
                    // ONLY the exe that is running now, by its own full path (v1.1, the developer switch's restart)
                    try { return Path.IsPathRooted(target) && string.Equals(Processes.LongPath(target), Processes.LongPath(AppInfo.ExePath()), StringComparison.OrdinalIgnoreCase); }
                    catch (ArgumentException) { return false; }
                case OpenKind.Folder:
                case OpenKind.TextFile:
                    // a path of the viewer's, never a command line and never an exe
                    if (target.IndexOf('\0') >= 0 || !Path.IsPathRooted(target)) return false;
                    string ext;
                    try { ext = Path.GetExtension(target); } catch (ArgumentException) { return false; }
                    if (kind == OpenKind.Folder) return true;
                    return !IsProgram(ext);
                default:
                    return false;
            }
        }

        static readonly HashSet<string> ProgramExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".msi", ".msp",
            ".scr", ".cpl", ".hta", ".reg", ".lnk", ".url", ".pif", ".dll", ".jar", ".msc",
        };

        static bool IsProgram(string extension) => !string.IsNullOrEmpty(extension) && ProgramExtensions.Contains(extension);

        /// <summary>The game (the launcher's Start-Process: ShellExecute, the copy's folder as the working folder, no
        /// arguments unless "launch in a window"). Throws like Process.Start when it cannot be started.</summary>
        public static void StartGame(ProcessStartInfo psi)
        {
            if (psi == null) throw new ArgumentNullException(nameof(psi));
            Start(OpenKind.Game, psi);
        }

        /// <summary>What GameLauncher starts: the game copy's exe, or Steam's URL for plain Among Us.</summary>
        public static void StartLaunch(ProcessStartInfo psi)
        {
            if (psi == null) throw new ArgumentNullException(nameof(psi));
            Start((psi.FileName ?? "").StartsWith("steam://", StringComparison.OrdinalIgnoreCase) ? OpenKind.SteamUrl : OpenKind.Game, psi);
        }

        /// <summary>steam://rungameid/945360 or steam://install/945360 through the shell's URL handler.</summary>
        public static void SteamUrl(string url) => Start(OpenKind.SteamUrl, new ProcessStartInfo(url) { UseShellExecute = true });

        /// <summary>One of the app's own pages in the viewer's browser.</summary>
        public static void WebPage(string url) => Start(OpenKind.Web, new ProcessStartInfo(url) { UseShellExecute = true });

        /// <summary>The viewer's mail app with one of the project's addresses, a subject and a template (ps1 Open-Mailto).
        /// The app does not send it: the person attaches the zip and presses send.</summary>
        public static void MailTo(string to, string subject, string body) =>
            Start(OpenKind.Mail, new ProcessStartInfo(MailUri(to, subject, body)) { UseShellExecute = true });

        public static string MailUri(string to, string subject, string body) =>
            "mailto:" + to + "?subject=" + Uri.EscapeDataString(subject ?? "") + "&body=" + Uri.EscapeDataString(body ?? "");

        /// <summary>A folder in the viewer's file manager (the shell's own "open" of a folder; the app never runs explorer.exe).</summary>
        public static void Folder(string path) => Start(OpenKind.Folder, new ProcessStartInfo(path) { UseShellExecute = true, Verb = "open" });

        /// <summary>A text file in the viewer's text editor: ShellExecuteEx with the ".txt" class, so a file whose own
        /// extension has no program (the mod's .cfg, events.log) still opens in their editor instead of "Open with".</summary>
        public static void TextFile(string path) => Start(OpenKind.TextFile, new ProcessStartInfo(path) { UseShellExecute = true, Verb = "open" });

        /// <summary>v1.1: this exe once more, no arguments, its own folder as the working folder - after the developer
        /// switch, once the window, WebView2 and the single-instance mutex are gone (Program). Throws like Process.Start.</summary>
        public static void Restart()
        {
            string exe = AppInfo.ExePath();
            string dir = null;
            try { dir = Path.GetDirectoryName(exe); } catch (ArgumentException) { }
            Start(OpenKind.Self, new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = dir ?? "" });
        }

        /// <summary>The game copy's exe for the developer update's interop run, kept so the app can wait for it and
        /// close it again (the launcher's Start-Process -PassThru). Everything else starts the game and forgets it.</summary>
        public static IGameRun StartGameRun(ProcessStartInfo psi)
        {
            if (psi == null) throw new ArgumentNullException(nameof(psi));
            if (!Allowed(OpenKind.Game, psi.FileName)) throw new InvalidOperationException("ShellOpen refused Game: " + psi.FileName);
            if (psi.UseShellExecute != true) throw new InvalidOperationException("ShellOpen always uses the shell");
            var recorder = Recorder;
            if (recorder != null) { recorder(OpenKind.Game, psi); return new NoRun(); }
            return new GameRun(Process.Start(psi));
        }

        sealed class NoRun : IGameRun
        {
            public bool HasExited => true;
            public void Stop() { }
            public void Dispose() { }
        }

        sealed class GameRun : IGameRun
        {
            readonly Process p;
            public GameRun(Process p) { this.p = p; }
            public bool HasExited { get { try { return p == null || p.HasExited; } catch (Exception) { return true; } } }
            public void Stop() { try { if (p != null && !p.HasExited) p.Kill(); } catch (Exception) { } }
            public void Dispose() { try { if (p != null) p.Dispose(); } catch (Exception) { } }
        }

        /// <summary>Runs "&lt;dotnet.exe&gt; build -c Release" in <paramref name="workingDir"/> and reads back what it
        /// printed. No shell, no window, no change to this app's own environment (see the head of this file).
        /// It waits up to <see cref="BuildTimeoutMs"/> and then closes the compiler: the launcher's Wait-Proc had no
        /// timeout at all, so a compiler that stopped making progress held the window for ever (v0.4 review).</summary>
        public static BuildRun RunBuild(string dotnetExe, string workingDir)
        {
            var result = new BuildRun();
            if (!Allowed(OpenKind.Build, dotnetExe)) { result.Error = "ShellOpen refused Build: " + dotnetExe; return result; }
            string root = null;
            try { root = Path.GetDirectoryName(dotnetExe); } catch (ArgumentException) { }
            var psi = new ProcessStartInfo
            {
                FileName = dotnetExe,
                WorkingDirectory = workingDir ?? "",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            foreach (var a in BuildArgs) psi.Arguments += (psi.Arguments.Length > 0 ? " " : "") + a;
            if (!string.IsNullOrEmpty(root))
            {
                // the child's environment only: this app never sets DOTNET_ROOT or PATH on itself (PORT-MAP 3.5)
                psi.EnvironmentVariables["DOTNET_ROOT"] = root;
                string path = psi.EnvironmentVariables["PATH"];
                psi.EnvironmentVariables["PATH"] = root + ";" + (path ?? "");
            }
            var recorder = Recorder;
            if (recorder != null) { recorder(OpenKind.Build, psi); result.ExitCode = 0; return result; }
            var text = new StringBuilder();
            try
            {
                using (var p = new Process { StartInfo = psi })
                {
                    // both pipes are drained while it runs: a compiler that fills one and waits would otherwise never end
                    DataReceivedEventHandler take = (s, e) => { if (e.Data != null) lock (text) text.Append(e.Data).Append("\r\n"); };
                    p.OutputDataReceived += take;
                    p.ErrorDataReceived += take;
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(BuildTimeoutMs))
                    {
                        // only the compiler this app started itself is closed, and only after it has had its 20 minutes
                        result.TimedOut = true;
                        try { p.Kill(); } catch (Exception) { }
                        try { p.WaitForExit(5000); } catch (Exception) { }
                    }
                    else result.ExitCode = p.ExitCode;
                }
            }
            catch (Exception ex) { result.Error = ex.GetBaseException().Message; }
            lock (text) result.Output = text.ToString();
            return result;
        }

        static void Start(OpenKind kind, ProcessStartInfo psi)
        {
            if (!Allowed(kind, psi.FileName)) throw new InvalidOperationException("ShellOpen refused " + kind + ": " + psi.FileName);
            if (psi.UseShellExecute != true) throw new InvalidOperationException("ShellOpen always uses the shell");
            var recorder = Recorder;
            if (recorder != null) { recorder(kind, psi); return; }
            if (kind == OpenKind.TextFile) { OpenWithTextClass(psi.FileName); return; }
            using (Process.Start(psi)) { }
        }

        // ---- ShellExecuteEx with SEE_MASK_CLASSNAME: "open this file with what .txt opens with"
        const int SEE_MASK_CLASSNAME = 0x00000001;
        const int SEE_MASK_NOASYNC = 0x00000100;
        const int SEE_MASK_FLAG_NO_UI = 0x00000400;
        const int SW_SHOWNORMAL = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public int fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
            public IntPtr hkeyClass;
            public int dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);

        static void OpenWithTextClass(string path)
        {
            var info = new SHELLEXECUTEINFO
            {
                cbSize = Marshal.SizeOf(typeof(SHELLEXECUTEINFO)),
                fMask = SEE_MASK_CLASSNAME | SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI,
                lpVerb = "open",
                lpFile = path,
                lpClass = ".txt",
                nShow = SW_SHOWNORMAL,
            };
            if (!ShellExecuteEx(ref info)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
