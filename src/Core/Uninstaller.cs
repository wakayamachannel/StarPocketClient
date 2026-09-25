// The uninstall (v0.3). SignPath's first condition is that an uninstall exists; this is it, reachable from
// Settings → アンインストール and from the Apps & Features entry ("<exe>" --uninstall, and --uninstall --quiet).
//
// WHEN it runs (this is the whole trick): never while the window is open. The app holds its own data folder open -
// the WebView2 profile (EBWebView\...\LOCK, leveldb) and client.log - so a delete from inside the running app is
// guaranteed to stop half-way. So:
//   - Settings → アンインストール only asks, remembers the answer and closes the app. Program.Main runs this class
//     AFTER the message loop has ended and the WebView2 control has been disposed (Program.FinishUninstall).
//   - "<exe>" --uninstall runs it before anything is opened at all, and refuses while another Client is running.
// The app's own data folder goes FIRST. When that fails, nothing else is touched: the app is never left registered
// with its files gone, or unregistered with its files there. Pressing again simply works.
//
// What it takes away:
//   - the app's own data folder %LOCALAPPDATA%\StarPocket\Client (settings.json, client.log, the WebView2 profile,
//     profile\avatar.png (v1.3), and the app's own launcher-state.json when it has one of its own)
//   - the shortcuts the app wrote, and only those: a .lnk is deleted only when it points at this exe
//   - the "start with Windows" value, when the viewer had turned it on
//   - the Apps & Features entry
//   - the mod's game copy ONLY when the viewer ticks the box, AND only when Among Us.exe is really in that folder.
//     It is off by default: it is about 1 GB the person would have to download again, and their own settings and
//     records are in it. Where the folder is can be said by --game-dir or by POCKETROLES_GAMEDIR (shared with the
//     PowerShell launcher), so "there is a game in it" is what tells the copy from a folder of somebody's own that an
//     old setting happens to point at. A folder that fails that question is kept and named, never emptied.
//
// What it NEVER touches, whatever is ticked:
//   - Steam, Steam's copy of Among Us, and anything under Steam's folders
//   - the Aegis data shared with the PowerShell tools (%LOCALAPPDATA%\PocketRoles\Aegis) and that shared folder
//     itself: the PowerShell launcher and tray must keep working after the Client is gone. (The guard is that folder,
//     not the whole PocketRoles tree: on a PC whose Desktop is inside OneDrive the mod's own game copy lives at
//     %LOCALAPPDATA%\PocketRoles\Among Us PocketRoles, and the checkbox has to be able to remove it.)
//   - the program folder: a running exe cannot delete itself, so it is always KEPT and named in the answer. A copy
//     someone unpacked into a folder of their own is kept too, and said so in different words.
//   - report zips and evidence zips already on the Desktop (the person's own files; they expire by themselves)
//   - anything outside the folders named here. Every delete is checked against that list first (Allowed), and Plan
//     itself only lists what Allowed lets through, so the dialog can never promise something Run will refuse.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Starpocket.Client.Core
{
    /// <summary>What an uninstall would do, worked out before anything is deleted so the viewer can be shown it (and so
    /// the self-test can check it without deleting anything).</summary>
    internal sealed class UninstallPlan
    {
        /// <summary>Folders that go completely.</summary>
        public List<string> Folders = new List<string>();
        /// <summary>Single files that go (the shortcuts).</summary>
        public List<string> Files = new List<string>();
        /// <summary>Registry values and keys that go, as words for the log.</summary>
        public List<string> Registry = new List<string>();
        /// <summary>What stays, and why: shown to the viewer so nothing is a surprise.</summary>
        public List<string> Kept = new List<string>();
        /// <summary>The mod copy, when the viewer ticked the box.</summary>
        public string ModCopy;
        /// <summary>The app's own folder. Always kept (a running exe cannot delete itself); here so the answer can name it.</summary>
        public string ProgramFolder;
        /// <summary>true when the program folder is one someone unpacked themselves, not a %LOCALAPPDATA%\Programs install
        /// (different words: we cannot tell what else of theirs is in it).</summary>
        public bool ProgramFolderIsTheirs;
    }

    /// <summary>What the viewer confirmed in the window, carried out of the app to <see cref="Program"/>.</summary>
    internal sealed class UninstallRequest
    {
        public bool ModCopy;
    }

    internal sealed class UninstallResult
    {
        public bool Ok;
        public string Error;
        public List<string> Removed = new List<string>();
        public List<string> Failed = new List<string>();
        public List<string> Kept = new List<string>();
        /// <summary>The app's own folder is still there and the person has to remove it themselves.</summary>
        public bool RestartNeeded;
        /// <summary>That folder.</summary>
        public string LeftFolder;
        /// <summary>The left folder is one they unpacked themselves (not our per-user install).</summary>
        public bool LeftFolderIsTheirs;
        /// <summary>The app's own data folder could not go, so nothing else was touched.</summary>
        public bool StoppedAtDataDir;
    }

    internal sealed class Uninstaller
    {
        public string DataDir;          // %LOCALAPPDATA%\StarPocket\Client
        public string ExePath;
        public string ExeDir;
        public string LocalAppData;
        public ModPaths Paths;          // the mod's game copy
        public string SteamDir;         // never touched; here only to be sure of it
        public string DesktopDir;
        public string StartMenuDir;
        public Action<string> Log = _ => { };
        /// <summary>How often a folder delete is tried. More than once after the window closed: WebView2's browser
        /// process lets go of the profile a moment after the control is disposed.</summary>
        public int DeleteAttempts = 1;
        public int DeleteWaitMs = 400;
        public Action<int> Sleep = ms => Thread.Sleep(ms);
        /// <summary>The self-test hands in its own, so nothing is really deleted.</summary>
        public Action<string> DeleteFolder = DeleteTree;
        public Action<string> DeleteFile = File.Delete;

        /// <summary>The Aegis records the PowerShell launcher and tray share with us: never ours to remove.</summary>
        public string AegisSharedDir => GameFolders.Join(LocalAppData, @"PocketRoles\Aegis");

        /// <summary>The folder those tools and (on a OneDrive Desktop) the mod's game copy live in. The folder itself is
        /// never removed; what is inside it is judged one by one.</summary>
        public string SharedRoot => GameFolders.Join(LocalAppData, "PocketRoles");

        /// <summary>Is <paramref name="path"/> inside <paramref name="root"/> (or the folder itself)? Both sides are
        /// resolved first, so the same folder reached through an 8.3 short name (C:\PROGRA~2\...) is still recognised.</summary>
        public static bool Inside(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;
            string a = Full(path), b = Full(root);
            if (a == null || b == null) return false;
            if (!b.EndsWith("\\", StringComparison.Ordinal)) b += "\\";
            return (a + "\\").StartsWith(b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The same folder, by any of its names.</summary>
        public static bool Same(string a, string b)
        {
            string x = Full(a), y = Full(b);
            return x != null && y != null && string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The full path with long folder names where Windows knows them (SOMEON~1 -> someone). A path that is
        /// not there cannot be expanded, so it is kept as it is - which is fine here: every root of a guard, and every
        /// path that is really deleted, exists.</summary>
        internal static string Full(string p)
        {
            try
            {
                string full = Path.GetFullPath(p).TrimEnd('\\');
                string lng = Native.TryGetLongPath(full);
                return string.IsNullOrEmpty(lng) ? full : lng.TrimEnd('\\');
            }
            catch (Exception) { return null; }
        }

        /// <summary>What the app is allowed to delete at all: its own data folder, its own program folder, its two
        /// shortcuts, and (only when it is asked) the mod copy. Nothing else, ever.</summary>
        public bool Allowed(string path, bool withModCopy)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // the Steam copy of the game and Steam itself are out of bounds whatever else matches
            if (Inside(path, SteamDir)) return false;
            // the records the PowerShell tray and launcher share stay: they must keep working
            if (Inside(path, AegisSharedDir)) return false;
            // ... and the folder that holds them is never removed either (what else is in it is judged below)
            if (Same(path, SharedRoot)) return false;
            if (Inside(path, DataDir)) return true;
            if (IsOurProgramFolder(ExeDir) && Inside(path, ExeDir)) return true;
            if (string.Equals(path, Shortcuts.PathFor(ShortcutPlace.Desktop, DesktopDir, StartMenuDir), StringComparison.OrdinalIgnoreCase)) return true;
            if (Inside(path, Shortcuts.StartMenuDirOr(StartMenuDir))) return true;
            // the mod's game copy, wherever it is - also at %LOCALAPPDATA%\PocketRoles\Among Us PocketRoles, which is
            // where it lands when the Desktop is inside OneDrive (GameFolders.ResolveModded).
            // v0.4 review: and only when it really IS a copy of the game. Where that folder is can be said by
            // --game-dir or by POCKETROLES_GAMEDIR, the same variable the PowerShell launcher reads, so a stale
            // setting on somebody's PC could point this at a folder of theirs - and the ticked box would then hand
            // that whole folder to DeleteTree, read-only files and all. "Among Us.exe is in it" is the one question
            // that tells the game's copy from anything else; when the answer is no the folder is kept and named.
            if (withModCopy && Paths != null && Inside(path, Paths.Modded) && LooksLikeGameCopy()) return true;
            return false;
        }

        /// <summary>Is the folder the mod copy is said to be in really a copy of the game? (Among Us.exe is in it.)</summary>
        public bool LooksLikeGameCopy() => Paths != null && GameFolders.PathExists(Paths.GameExe);

        /// <summary>The program folder was installed the way the setup installs it (%LOCALAPPDATA%\Programs\...), rather
        /// than unpacked into a folder of the person's own. Either way it is kept - this only decides the words.</summary>
        public bool IsOurProgramFolder(string dir) =>
            !string.IsNullOrEmpty(dir) && Inside(dir, GameFolders.Join(LocalAppData, "Programs")) &&
            !Same(dir, GameFolders.Join(LocalAppData, "Programs"));

        /// <summary>Works out what would go, without touching anything. Everything listed has already been through
        /// <see cref="Allowed"/>, so the confirm dialog cannot promise something Run() would refuse.</summary>
        public UninstallPlan Plan(bool withModCopy)
        {
            var p = new UninstallPlan();
            if (GameFolders.PathExists(DataDir) && Allowed(DataDir, withModCopy)) p.Folders.Add(DataDir);
            foreach (var place in new[] { ShortcutPlace.Desktop, ShortcutPlace.StartMenu })
            {
                string lnk = Shortcuts.PathFor(place, DesktopDir, StartMenuDir);
                if (GameFolders.PathExists(lnk) && Shortcuts.PointsAtUs(lnk, ExePath) && Allowed(lnk, withModCopy)) p.Files.Add(lnk);
            }
            if (AutoStart.IsOn()) p.Registry.Add(@"HKCU\" + AutoStart.RunKey + "\\" + AutoStart.ValueName);
            if (AppsAndFeatures.Read() != null) p.Registry.Add(@"HKCU\" + AppsAndFeatures.UninstallKey);
            if (Paths != null && GameFolders.PathExists(Paths.Modded))
            {
                if (withModCopy && Allowed(Paths.Modded, true)) p.ModCopy = Paths.Modded;
                else p.Kept.Add(Paths.Modded);
            }
            // the running exe cannot delete itself, so its folder is always kept and always named
            if (!string.IsNullOrEmpty(ExeDir))
            {
                p.ProgramFolder = ExeDir;
                p.ProgramFolderIsTheirs = !IsOurProgramFolder(ExeDir);
                p.Kept.Add(ExeDir);
            }
            // always named, so the person knows these are not ours to remove
            if (GameFolders.PathExists(AegisSharedDir)) p.Kept.Add(AegisSharedDir);
            if (!string.IsNullOrEmpty(SteamDir) && GameFolders.PathExists(SteamDir)) p.Kept.Add(SteamDir);
            return p;
        }

        /// <summary>Does it. Everything is checked against <see cref="Allowed"/> first, so a wrong path in the plan
        /// cannot delete something else. Run this only when the app's window is closed (see the file's header).</summary>
        public UninstallResult Run(bool withModCopy)
        {
            var r = new UninstallResult();
            try
            {
                var plan = Plan(withModCopy);
                // the app's own data folder first. If it will not go, the app is still installed: leave the registry,
                // the shortcuts and the mod copy exactly as they are, so pressing again does the whole thing.
                foreach (var d in plan.Folders) Try(r, d, true, withModCopy);
                if (r.Failed.Count > 0)
                {
                    r.StoppedAtDataDir = true;
                    r.Error = string.Join(", ", r.Failed.ToArray());
                    r.Kept.AddRange(plan.Kept);
                    Log("uninstall: stopped - the app's own data folder is still in use (" + r.Error + "); nothing else was touched");
                    return r;
                }
                foreach (var f in plan.Files) Try(r, f, false, withModCopy);
                if (plan.ModCopy != null) Try(r, plan.ModCopy, true, true);
                if (AutoStart.IsOn() && AutoStart.Set(false, ExePath)) r.Removed.Add("start with Windows");
                if (AppsAndFeatures.Read() != null && AppsAndFeatures.Unregister()) r.Removed.Add("Apps & Features entry");
                if (plan.ProgramFolder != null)
                {
                    r.RestartNeeded = true;
                    r.LeftFolder = plan.ProgramFolder;
                    r.LeftFolderIsTheirs = plan.ProgramFolderIsTheirs;
                }
                r.Kept.AddRange(plan.Kept);
                r.Ok = r.Failed.Count == 0;
                if (!r.Ok) r.Error = string.Join(", ", r.Failed.ToArray());
                Log("uninstall: removed " + r.Removed.Count + ", failed " + r.Failed.Count + (withModCopy ? ", with the mod copy" : ", the mod copy kept"));
                return r;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                Log("uninstall: " + ex);
                return r;
            }
        }

        void Try(UninstallResult r, string path, bool folder, bool withModCopy)
        {
            if (!Allowed(path, withModCopy)) { Log("uninstall: refused " + path); r.Failed.Add(path); return; }
            string last = null;
            for (int attempt = 1; attempt <= Math.Max(1, DeleteAttempts); attempt++)
            {
                try
                {
                    if (folder) DeleteFolder(path); else DeleteFile(path);
                    r.Removed.Add(path);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex.Message;
                    if (attempt < Math.Max(1, DeleteAttempts)) Sleep(DeleteWaitMs);
                }
            }
            Log("uninstall: " + path + ": " + last);
            r.Failed.Add(path);
        }

        /// <summary>Removes a folder and everything in it. Directory.Delete(path, true) stops half-way with "access
        /// denied" as soon as it meets a junction or a symbolic link inside the tree, and leaves the rest; this unlinks
        /// such a link instead (its target is never followed and never touched) and carries on.</summary>
        public static void DeleteTree(string dir)
        {
            var di = new DirectoryInfo(dir);
            if (!di.Exists) return;
            if (IsLink(di)) { Directory.Delete(dir); return; }   // the link only, never what it points at
            foreach (var sub in di.GetDirectories()) DeleteTree(sub.FullName);
            foreach (var f in di.GetFiles())
            {
                try { if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal; }
                catch (Exception) { }
                f.Delete();
            }
            Directory.Delete(dir, false);
        }

        static bool IsLink(DirectoryInfo di)
        {
            try { return (di.Attributes & FileAttributes.ReparsePoint) != 0; }
            catch (Exception) { return false; }
        }
    }
}
