// Where the author's working copy of the mod is, and whether THIS start runs in developer mode against it (v1.1; the
// owner, 2026-09-24: 「ランチャーじゃなくてクライアントからがいいな。開発用もそこで何とかしてよ」).
//
// Until now a published exe could be in developer mode in exactly one way: PocketRoles.csproj beside the exe (v0.4
// review, GameFolders.ResolveSource). That is still the first rule and nothing about it changes. This file adds the
// second, and it is the only other one: the switch in Settings → PocketRoles → 開発 (settings.json "devBuild": true)
// AND the author's folder found by a fixed rule on this PC. Nothing on the command line names that folder, no page
// and no file can point it somewhere else, and on a PC where the folder is not found the switch is not drawn at all -
// nothing happens and nothing complains.
//
// The fixed rule (Find): the folder the Desktop's "PocketRoles.lnk" or "PocketRoles Launcher.lnk" points into (the
// old launcher's own shortcut), then <Desktop>\HostRoles. A folder counts only when it holds BOTH PocketRoles.csproj
// and PocketRolesLauncher.ps1: the working copy of the mod, not just a project file of that name.
//
// Why this is not the door v0.4 closed. That door was an ARGUMENT (--source-dir) plus a search of the folders above
// the exe, so a shortcut, a URL handler or another program could hand the signed exe any folder at all and get a
// 「再ビルド」 button that runs whatever that folder's project file says. Here the person has to turn the switch on
// inside the app (or write "devBuild": true into settings.json in their own %LOCALAPPDATA%), and the folder has to be
// on their own Desktop, behind their own shortcut. Someone who can write those two places already runs programs as
// this user (the Startup folder is next door); a signed exe is not what lets them. docs\CODE-SIGNING.md 8. says the
// same in the reviewer's words.
//
// This file only ever READS the folder (two Test-Path). What the app then does in developer mode - the rebuild, the
// shared launcher-state.json - is exactly what developer mode did before (DevBuild.cs, ClientContext.LoadState).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Starpocket.Client.Core
{
    /// <summary>What ClientContext.Detect settles on: the source folder, and whether it is a developer start.</summary>
    internal sealed class DevChoice
    {
        /// <summary>The folder launcher-state.json and the README are read from (the exe's own folder in friend mode).</summary>
        public string Src;
        public bool DevMode;
        /// <summary>Developer mode came from the switch (Settings → PocketRoles → 開発), not from the exe's own folder.
        /// The page shows the switch as "on" only for this; a DEBUG build started from the repository has nothing to switch.</summary>
        public bool FromSetting;
    }

    internal static class DevSource
    {
        public const string ProjectFile = "PocketRoles.csproj";
        /// <summary>The Desktop shortcuts the old launcher is started from, newest spelling first.</summary>
        public static readonly string[] ShortcutNames = { "PocketRoles.lnk", LegacyLauncher.ShortcutName };
        /// <summary>The folder name the author's working copy has when there is no shortcut to follow.</summary>
        public const string DefaultFolderName = "HostRoles";

        /// <summary>The working copy of the mod: the project file AND the launcher script, both. A folder with only a
        /// PocketRoles.csproj in it is not one (that is the shape the v0.4 review was about).</summary>
        public static bool IsDevFolder(string dir) =>
            !string.IsNullOrEmpty(dir)
            && GameFolders.PathExists(GameFolders.Join(dir, ProjectFile))
            && GameFolders.PathExists(GameFolders.Join(dir, LegacyLauncher.ScriptName));

        /// <summary>
        /// The folders looked at, in order: the folder the author PICKED in Settings (v1.4), then each Desktop
        /// shortcut's target folder, then &lt;Desktop&gt;\HostRoles. A shortcut that cannot be read is skipped, never
        /// an error, and every candidate still has to pass <see cref="IsDevFolder"/>.
        /// <para>v1.4 added the picked folder because the two fixed places were too strict to live with: the working
        /// copy had to sit on the Desktop, and moving it turned developer mode off with no explanation (the owner,
        /// 2026-09-26: 「特定のフォルダからやらないといけないのめんどくさい」). It does NOT reopen what the v0.4 review
        /// closed. That door was an ARGUMENT (--source-dir) plus a search of the folders above the exe, so a shortcut,
        /// a URL handler or another program could hand the signed exe a folder. This one is written only by
        /// "pickModSource", which is a folder dialog the person opened themselves, and it is kept in their own
        /// settings.json - the same place, and the same threat model, as the switch that has to be on beside it.
        /// </para>
        /// </summary>
        public static IEnumerable<string> Candidates(string desktop, Func<string, string> readShortcut, string picked = null)
        {
            if (!string.IsNullOrEmpty(picked)) yield return picked;
            if (string.IsNullOrEmpty(desktop)) yield break;
            foreach (var name in ShortcutNames)
            {
                string lnk = GameFolders.Join(desktop, name);
                if (readShortcut == null || !GameFolders.PathExists(lnk)) continue;
                string target = null;
                try { target = readShortcut(lnk); } catch (Exception) { }
                if (string.IsNullOrEmpty(target)) continue;
                string dir = GameFolders.Parent(target);
                if (!string.IsNullOrEmpty(dir)) yield return dir;
            }
            yield return GameFolders.Join(desktop, DefaultFolderName);
        }

        /// <summary>The author's working copy on this PC, or null when there is none (the ordinary PC). Never throws.
        /// <paramref name="picked"/> is the folder chosen in Settings (settings.json "devSource"), looked at first.</summary>
        public static string Find(string desktop, Func<string, string> readShortcut, string picked = null)
        {
            try
            {
                foreach (var dir in Candidates(desktop, readShortcut, picked))
                    if (IsDevFolder(dir)) return dir;
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>The one decision, with nothing read from disk (the self-test plays every combination).
        /// 1. the exe's own folder holds the project file (or --source-dir in a developer build): developer mode as it
        ///    has always been; 2. else the switch is on AND the working copy was found: developer mode against that
        ///    folder; 3. else friend mode. --friend turns both off, as it always turned the first off.</summary>
        /// <param name="exeSource">GameFolders.ResolveSource: the exe's folder, or --source-dir in a developer build.</param>
        /// <param name="exeIsSource">GameFolders.IsDevMode(false, exeSource): the project file sits there.</param>
        /// <param name="devFolder">DevSource.Find's answer (null: not on this PC).</param>
        public static DevChoice Choose(string exeSource, bool exeIsSource, bool friend, bool settingOn, string devFolder)
        {
            if (!friend && exeIsSource) return new DevChoice { Src = exeSource, DevMode = true };
            if (!friend && settingOn && !string.IsNullOrEmpty(devFolder)) return new DevChoice { Src = devFolder, DevMode = true, FromSetting = true };
            return new DevChoice { Src = exeSource, DevMode = false };
        }
    }
}
