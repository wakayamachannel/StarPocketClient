// Which PocketRoles.dll is in the game copy right now: the one this app installed from a release (配布用), the one it
// built from the author's source (開発用), or one something else put there (v1.1).
//
// Why: on the author's PC the released mod and the locally built one go into the SAME game copy (the mod's own
// project file writes to ..\Among Us PocketRoles, which is also the copy the installer fills), so 「どちらを入れたか」 is
// a real question - on 2026-09-23 the client had installed 0.5.4 and the DLL on disk was a 0.5.5 developer build the
// old launcher had put there afterwards, and nothing on the screen said so.
//
// The app writes one line, <DataDir>\mod-origin.txt, each time IT puts a DLL there: "<sha256>|<kind>|<version>|<when>".
// Reading it back is a comparison, never a belief: the DLL's SHA-256 has to be the one written down, or the answer is
// "unknown" (changed by someone else: the old launcher's build, a copy by hand). No DLL at all is null. Nothing here
// ever touches the game copy or the source folder - it reads the DLL and writes one small file of the app's own.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;

namespace Starpocket.Client.Core
{
    internal sealed class ModOrigin
    {
        public const string Dev = "dev";
        public const string Release = "release";
        public const string Unknown = "unknown";

        /// <summary>dev / release / unknown.</summary>
        public string Kind;
        /// <summary>The DLL's version as it was written down (unknown: as the file says now).</summary>
        public string Version;
        /// <summary>When the app put it there (yyyy-MM-dd HH:mm:ss); null when unknown.</summary>
        public string At;

        /// <summary>The words for the page and the status line, in the viewer's language.</summary>
        public string Text(string lang)
        {
            switch (Kind)
            {
                case Dev: return S.T(lang, "origin_dev", At ?? "?");
                case Release: return S.T(lang, "origin_release", Version ?? "?", At ?? "?");
                default: return S.T(lang, "origin_unknown");
            }
        }
    }

    internal static class ModOriginFile
    {
        public const string FileName = "mod-origin.txt";

        /// <summary>The app has just put <paramref name="dllPath"/> in place: write down what it is. A failure is logged
        /// and nothing else - an install or a build that worked is not undone by a note that could not be written.</summary>
        public static void Record(string dataDir, string dllPath, string kind, string version, DateTime now, Action<string> log)
        {
            if (string.IsNullOrEmpty(dataDir) || !GameFolders.PathExists(dllPath)) return;
            try
            {
                Directory.CreateDirectory(dataDir);
                string line = FileHash.Sha256(dllPath) + "|" + kind + "|" + (version ?? "") + "|" + LauncherStateFile.Stamp(now);
                File.WriteAllText(Path.Combine(dataDir, FileName), line, new UTF8Encoding(false));
            }
            catch (Exception ex) { if (log != null) log("mod-origin: " + ex.Message); }
        }

        /// <summary>What is in the game copy now, judged against the note. null when there is no DLL.</summary>
        public static ModOrigin Read(string dataDir, string dllPath)
        {
            if (!GameFolders.PathExists(dllPath)) return null;
            var o = new ModOrigin { Kind = ModOrigin.Unknown, Version = GameVersion.DllVersionString(dllPath) };
            try
            {
                string sha = FileHash.Sha256(dllPath);
                string path = string.IsNullOrEmpty(dataDir) ? null : Path.Combine(dataDir, FileName);
                if (path == null || !File.Exists(path)) return o;
                var parts = File.ReadAllText(path, Encoding.UTF8).Trim().Split('|');
                if (parts.Length < 4 || !FileHash.Same(parts[0], sha)) return o;
                if (parts[1] != ModOrigin.Dev && parts[1] != ModOrigin.Release) return o;
                o.Kind = parts[1];
                if (parts[2].Length > 0) o.Version = parts[2];
                o.At = parts[3];
            }
            catch (Exception) { }
            return o;
        }
    }
}
