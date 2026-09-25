// What is installed and what the play button shows (PORT-MAP 3.4; ps1:533-567, 869-882, 1707-1753).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Starpocket.Client.Core
{
    internal static class GameVersion
    {
        // case-sensitive like [regex]::Matches in the ps1: "2026.8.18f1" is not a version, "2026.8.18" is
        static readonly Regex VersionPattern = new Regex(@"20\d\d\.\d{1,2}\.\d{1,2}(?![\dfa-z])");

        /// <summary>The game version in &lt;dir&gt;\Among Us_Data\globalgamemanagers (read as Latin-1): the first match not
        /// starting with 2022. (Unity's own version); null when there is none or the file is missing.</summary>
        public static string Read(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return null;
            try
            {
                string f = GameFolders.Join(dir, @"Among Us_Data\globalgamemanagers");
                if (!GameFolders.PathExists(f)) return null;
                return FromText(Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(f)));
            }
            catch (Exception) { return null; }
        }

        public static string FromText(string text)
        {
            foreach (Match m in VersionPattern.Matches(text ?? ""))
                if (!m.Value.StartsWith("2022.", StringComparison.Ordinal)) return m.Value;
            return null;
        }

        public static string ProductVersion(string path)
        {
            try { return FileVersionInfo.GetVersionInfo(path).ProductVersion; } catch (Exception) { return null; }
        }

        public static string FileVersion(string path)
        {
            try { return FileVersionInfo.GetVersionInfo(path).FileVersion; } catch (Exception) { return null; }
        }

        /// <summary>Get-DllVersionString: ProductVersion (the part before '+') when it starts with digits.digits, else FileVersion.</summary>
        public static string DllVersionString(string path)
        {
            if (!GameFolders.PathExists(path)) return null;
            return DllVersionFrom(ProductVersion(path), FileVersion(path));
        }

        public static string DllVersionFrom(string productVersion, string fileVersion)
        {
            if (!string.IsNullOrEmpty(productVersion) && Regex.IsMatch(productVersion, @"^\d+\.\d+")) return productVersion.Split('+')[0];
            return fileVersion;
        }
    }

    /// <summary>Get-InstallInfo (ps1:869-882).</summary>
    internal sealed class InstallInfo
    {
        public bool Exe, Bep, BepOk, Dll, Interop;
        public string GameVer, BepVer, DllVer;

        public bool Installed => Exe && Bep && Dll;

        public static InstallInfo Read(ModPaths p)
        {
            var i = new InstallInfo();
            i.Exe = GameFolders.PathExists(p.GameExe);
            i.GameVer = i.Exe ? GameVersion.Read(p.Modded) : null;
            string core = GameFolders.Join(p.Modded, @"BepInEx\core\BepInEx.Core.dll");
            i.Bep = GameFolders.PathExists(core);
            i.BepVer = i.Bep ? GameVersion.ProductVersion(core) : null;
            i.BepOk = !string.IsNullOrEmpty(i.BepVer) && i.BepVer.IndexOf(AppInfo.BepInExVersion, StringComparison.OrdinalIgnoreCase) >= 0;
            i.Dll = GameFolders.PathExists(p.DllPath);
            i.DllVer = i.Dll ? GameVersion.DllVersionString(p.DllPath) : null;
            i.Interop = GameFolders.PathExists(GameFolders.Join(p.Modded, @"BepInEx\interop\Assembly-CSharp.dll"));
            return i;
        }
    }

    /// <summary>The launcher's status and warning line, as the play button's state (PSTATE in the UI).</summary>
    internal sealed class LaunchStatus
    {
        public bool DevMode;
        public string SteamVer, ModVer;
        public bool Installed, NeedsUpdate, NeedsRebuild;
        public bool SteamRunning, GameRunning;
        public InstallInfo Info;
        /// <summary>ready / install / repair / sync / devUpdate / devRebuild (the launcher's warning line), or blocked (the
        /// last pre-launch scan stopped the start).</summary>
        public string PState;
        /// <summary>Why the play button says 「修復」 (the R state): "files" - the copy is there but not whole (P-14) - or
        /// "mod" - Aegis found PocketRoles.dll changed (R-41). null otherwise.</summary>
        public string Repair;
        public string WarnKey;
        public object[] WarnArgs = new object[0];
        /// <summary>v1.1: which PocketRoles.dll is in the copy - the release this app installed, the author's own build, or
        /// one something else put there (null: no DLL). Filled by ClientContext.ComputeStatus, not by Compute.</summary>
        public ModOrigin Origin;

        /// <summary>The same decisions as Get-StatusLines + Refresh-Status (PORT-MAP 3.4 table), plus the R state
        /// (PORT-MAP 13.3): a game copy that is there but not whole, and a changed PocketRoles.dll, are repaired in place
        /// instead of "not installed".</summary>
        public static LaunchStatus Compute(bool devMode, InstallInfo info, string steamVer, string lastBuiltGameVersion, bool steamRunning, bool gameRunning, bool blockedByAegis, bool repairMod = false)
        {
            var s = new LaunchStatus { DevMode = devMode, Info = info, SteamVer = steamVer, ModVer = info.GameVer, SteamRunning = steamRunning, GameRunning = gameRunning };
            s.Installed = info.Installed;
            // PowerShell -ne on strings ignores case
            s.NeedsUpdate = !string.IsNullOrEmpty(s.SteamVer) && !string.IsNullOrEmpty(s.ModVer) && !string.Equals(s.SteamVer, s.ModVer, StringComparison.OrdinalIgnoreCase);
            if (devMode)
            {
                s.NeedsRebuild = (!string.IsNullOrEmpty(s.ModVer) && !string.Equals(lastBuiltGameVersion, s.ModVer, StringComparison.OrdinalIgnoreCase)) || !info.Dll;
                if (s.NeedsUpdate) { s.PState = "devUpdate"; s.WarnKey = "al_update"; s.WarnArgs = new object[] { s.ModVer, s.SteamVer }; }
                else if (s.NeedsRebuild) { s.PState = "devRebuild"; s.WarnKey = "al_rebuild"; }
                else { s.PState = "ready"; s.WarnKey = "al_ok_dev"; }
            }
            else
            {
                s.NeedsRebuild = false;
                // the copy is there but BepInEx or the mod is missing: the same install steps, but the button says 修復
                if (!s.Installed && info.Exe) { s.PState = "repair"; s.Repair = "files"; s.WarnKey = "al_repair"; }
                else if (!s.Installed) { s.PState = "install"; s.WarnKey = "al_notinstalled"; }
                else if (repairMod) { s.PState = "repair"; s.Repair = "mod"; s.WarnKey = "al_repair_mod"; }
                else if (s.NeedsUpdate) { s.PState = "sync"; s.WarnKey = "al_gameupdated"; s.WarnArgs = new object[] { s.ModVer, s.SteamVer }; }
                else { s.PState = "ready"; s.WarnKey = "al_ok"; }
            }
            // only a start that got as far as the scan can have been stopped by it (the checks before it answer first)
            if (blockedByAegis && s.PState == "ready") s.PState = "blocked";
            return s;
        }

        public string Warn(string lang) => S.T(lang, WarnKey, WarnArgs);
    }
}
