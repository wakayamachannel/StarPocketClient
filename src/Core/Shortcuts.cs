// Shortcuts, the "start with Windows" entry and the Apps & Features entry (v0.3).
//
// SignPath's conditions, and what this file does about each:
//  - "no shortcut without telling the user": a shortcut is written ONLY by Create(), which only the page's
//    shortcut.create invoke reaches - the button in Settings the viewer presses. Nothing here runs at start, after an
//    install or after an update, and the app never writes a file association at all.
//  - "no startup entry without asking": AutoStart writes one HKCU Run value, and only when the viewer turns on the
//    setting whose own words say the app will start with Windows. Turning it off removes the value.
//  - "an uninstall exists": Registered() / Register() / Unregister() are the Apps & Features entry, under HKCU (a
//    per-user install needs no administrator). The app itself never writes the entry at start: the setup exe writes it
//    when it installs, and Uninstaller removes it. Register() is here so the setup and the uninstall agree on one
//    shape, and so the self-test can check it without a setup.
// Everything is done in-process: no reg.exe, no schtasks, no PowerShell.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Starpocket.Client.Core
{
    /// <summary>Where a shortcut may go. Nothing else is ever written.</summary>
    internal enum ShortcutPlace { Desktop, StartMenu }

    internal static class Shortcuts
    {
        /// <summary>The file name of our shortcut, in both places.</summary>
        public const string LinkName = AppInfo.Name + ".lnk";

        public static string DesktopDir() => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        /// <summary>The viewer's own Start menu (never the machine-wide one: that needs administrator).</summary>
        public static string StartMenuDir() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppInfo.Company);

        /// <summary>The Start-menu folder, or the one a test hands in.</summary>
        public static string StartMenuDirOr(string startMenuDir) => startMenuDir ?? StartMenuDir();

        public static string PathFor(ShortcutPlace place, string desktopDir = null, string startMenuDir = null) =>
            place == ShortcutPlace.Desktop
                ? GameFolders.Join(desktopDir ?? DesktopDir(), LinkName)
                : GameFolders.Join(startMenuDir ?? StartMenuDir(), LinkName);

        /// <summary>Which of the two we have written (the page ticks the boxes from this).</summary>
        public static bool Exists(ShortcutPlace place, string desktopDir = null, string startMenuDir = null) =>
            GameFolders.PathExists(PathFor(place, desktopDir, startMenuDir));

        /// <summary>Writes the shortcut. Called only from the viewer's button press. Returns its path; throws with the
        /// reason when it cannot be written.</summary>
        public static string Create(ShortcutPlace place, string exePath, string desktopDir = null, string startMenuDir = null)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) throw new FileNotFoundException(exePath ?? "");
            string lnk = PathFor(place, desktopDir, startMenuDir);
            string dir = GameFolders.Parent(lnk);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            ShellLink.Save(lnk, exePath, GameFolders.Parent(exePath), AppInfo.Name, exePath);
            return lnk;
        }

        /// <summary>Removes the shortcut when it is there and points at this app (the uninstall; never another
        /// program's shortcut that happens to share the name). Returns true when a file was deleted.</summary>
        public static bool Remove(ShortcutPlace place, string exePath, string desktopDir = null, string startMenuDir = null)
        {
            string lnk = PathFor(place, desktopDir, startMenuDir);
            if (!GameFolders.PathExists(lnk)) return false;
            if (!PointsAtUs(lnk, exePath)) return false;
            try
            {
                File.Delete(lnk);
                // the Start menu folder is ours: take it away when it is empty, leave it when it is not
                if (place == ShortcutPlace.StartMenu)
                {
                    string dir = GameFolders.Parent(lnk);
                    try { if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir); }
                    catch (Exception) { }
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>true when the .lnk points at exactly this exe: what makes it ours. A shortcut written before the app
        /// was moved points somewhere else and is left alone on purpose - it is not ours to remove any more, and it may
        /// be the one the person keeps for another copy.</summary>
        public static bool PointsAtUs(string lnkPath, string exePath)
        {
            string target = ShellLink.TargetPath(lnkPath);
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(exePath)) return false;
            return string.Equals(Uninstaller.Full(target), Uninstaller.Full(exePath), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The one "start with Windows" value. Off unless the viewer turns the setting on.</summary>
    internal static class AutoStart
    {
        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        /// <summary>The value name: the app's own, so nothing else is ever touched.</summary>
        public const string ValueName = "StarPocketClient";

        /// <summary>The registry root to work in (HKCU). The self-test hands in a key of its own.</summary>
        public static Func<RegistryKey> OpenUserRoot = () => Registry.CurrentUser;

        /// <summary>true when our value is there now.</summary>
        public static bool IsOn()
        {
            try
            {
                using (var root = OpenUserRoot())
                using (var k = root?.OpenSubKey(RunKey, false))
                    return k?.GetValue(ValueName) != null;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Writes or removes the value. Only the setting whose words say "start with Windows" calls this.
        /// Returns true when the registry now says what was asked for.</summary>
        public static bool Set(bool on, string exePath)
        {
            try
            {
                using (var root = OpenUserRoot())
                using (var k = root?.CreateSubKey(RunKey))
                {
                    if (k == null) return false;
                    if (on)
                    {
                        if (string.IsNullOrEmpty(exePath)) return false;
                        k.SetValue(ValueName, "\"" + exePath + "\"", RegistryValueKind.String);
                    }
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                    return true;
                }
            }
            catch (Exception) { return false; }
        }
    }

    /// <summary>The Apps &amp; Features entry (HKCU, per user: no administrator).</summary>
    internal static class AppsAndFeatures
    {
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\StarPocketClient";

        public static Func<RegistryKey> OpenUserRoot = () => Registry.CurrentUser;

        /// <summary>The entry's values when it is there, else null.</summary>
        public static Dictionary<string, string> Read()
        {
            try
            {
                using (var root = OpenUserRoot())
                using (var k = root?.OpenSubKey(UninstallKey, false))
                {
                    if (k == null) return null;
                    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var n in k.GetValueNames()) d[n] = Convert.ToString(k.GetValue(n));
                    return d;
                }
            }
            catch (Exception) { return null; }
        }

        /// <summary>Writes the entry (the setup exe does this when it installs; the app never does it by itself, so a
        /// copy someone unpacked into a folder adds nothing to their system).</summary>
        public static bool Register(string exePath, string installDir)
        {
            try
            {
                using (var root = OpenUserRoot())
                using (var k = root?.CreateSubKey(UninstallKey))
                {
                    if (k == null) return false;
                    k.SetValue("DisplayName", AppInfo.Name);
                    k.SetValue("DisplayVersion", AppInfo.Version);
                    k.SetValue("Publisher", AppInfo.Company);
                    k.SetValue("DisplayIcon", exePath ?? "");
                    k.SetValue("InstallLocation", installDir ?? "");
                    // "Settings → アンインストール" of the app itself: one flow, one confirm, one set of words
                    k.SetValue("UninstallString", "\"" + (exePath ?? "") + "\" --uninstall");
                    k.SetValue("QuietUninstallString", "\"" + (exePath ?? "") + "\" --uninstall --quiet");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    // "アプリと機能" の「詳細情報」: この app のリポジトリへ（MOD の方ではありません）
                    k.SetValue("URLInfoAbout", AppInfo.ClientRepoUrl);
                    return true;
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>Takes the entry away (the uninstall). true when there is none left.</summary>
        public static bool Unregister()
        {
            try
            {
                using (var root = OpenUserRoot())
                {
                    if (root == null) return false;
                    if (root.OpenSubKey(UninstallKey, false) == null) return true;
                    root.DeleteSubKeyTree(UninstallKey, false);
                    return true;
                }
            }
            catch (Exception) { return false; }
        }
    }
}
