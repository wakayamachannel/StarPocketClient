// Which PocketRoles.dll is in the game copy right now: the one this app installed from a release (配布用), the one it
// built from the author's source (開発用), or one something else put there (v1.1).
//
// Why: on the author's PC the released mod and the locally built one go into the SAME game copy (the mod's own
// project file writes to ..\Among Us PocketRoles, which is also the copy the installer fills), so 「どちらを入れたか」 is
// a real question - on 2026-09-23 the client had installed 0.5.4 and the DLL on disk was a 0.5.5 developer build the
// old launcher had put there afterwards, and nothing on the screen said so.
//
// The app writes one line, <DataDir>\mod-origin.txt, each time IT puts a DLL there: "<sha256>|<kind>|<version>|<when>".
// Reading it back is a comparison, never a belief: the DLL's SHA-256 has to be the one written down. No DLL at all is
// null. Nothing here ever touches the game copy or the source folder - it reads the DLL and writes one small file of
// the app's own.
//
// v1.5: there are FOUR answers, because "we have no note" and "the note says another file" are not the same thing and
// only the second one is evidence:
//   release / dev - the note is there, the DLL is the file it names: this app installed it / built it.
//   replaced      - the note is there and the DLL is NOT the file it names (the old launcher rebuilt over it, a copy
//                   by hand, the file was changed): something other than this app put what is there now.
//   unknown       - there is no note, it cannot be read, or it is broken: we do not KNOW who put the DLL there.
// Until v1.5 both of the last two were "unknown" and the screen said 「このクライアント以外が入れたもの」 for both, so a
// copy installed before this version - which has no note at all - was announced as somebody else's. The owner read
// that line and was worried by it. A thing we have not measured is now said as "we do not know".
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
        /// <summary>We do not know: there is no note, it could not be read, or it is broken. NOT a finding about the DLL -
        /// a DLL installed before v1.1 (when the note was invented) is this, and so is every start on a PC whose
        /// %LOCALAPPDATA% was cleared.</summary>
        public const string Unknown = "unknown";
        /// <summary>v1.5: the note is there and the DLL is not the file it names - measured, so it may be said plainly.</summary>
        public const string Replaced = "replaced";

        /// <summary>dev / release / replaced / unknown.</summary>
        public string Kind;
        /// <summary>The DLL's version as it was written down (replaced / unknown: as the file itself says now).</summary>
        public string Version;
        /// <summary>When the app put it there (yyyy-MM-dd HH:mm:ss); null unless Kind is dev or release.</summary>
        public string At;

        /// <summary>The words for the page and the status line, in the viewer's language.</summary>
        public string Text(string lang)
        {
            switch (Kind)
            {
                case Dev: return S.T(lang, "origin_dev", At ?? "?");
                case Release: return S.T(lang, "origin_release", Version ?? "?", At ?? "?");
                case Replaced: return S.T(lang, "origin_replaced");
                default: return S.T(lang, "origin_unknown");   // 分からない
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

        /// <summary>What is in the game copy now, judged against the note. null when there is no DLL.
        /// <para>v1.5: the answer starts at "unknown" (分からない) and only becomes something else when a note was actually
        /// read. "replaced" needs a readable note AND a fingerprint in it that is a fingerprint AND a DLL whose
        /// fingerprint is a different one - all three measured. Everything else (no note, no data folder, unreadable,
        /// too few fields, a kind nobody writes, a hash that is not 64 hex characters, an exception) stays "unknown":
        /// the app says it does not know instead of blaming the viewer's copy.</para></summary>
        public static ModOrigin Read(string dataDir, string dllPath)
        {
            if (!GameFolders.PathExists(dllPath)) return null;
            var o = new ModOrigin { Kind = ModOrigin.Unknown, Version = GameVersion.DllVersionString(dllPath) };
            try
            {
                string path = string.IsNullOrEmpty(dataDir) ? null : Path.Combine(dataDir, FileName);
                if (path == null || !File.Exists(path)) return o;                               // 記録が無い = 分からない
                var parts = File.ReadAllText(path, Encoding.UTF8).Trim().Split('|');
                if (parts.Length < 4) return o;                                                 // 記録が壊れている = 分からない
                if (parts[1] != ModOrigin.Dev && parts[1] != ModOrigin.Release) return o;
                // A note whose first field is not a SHA-256 at all tells us nothing, so it cannot tell us the DLL was
                // replaced either (case is ignored here exactly as FileHash.Same ignores it below).
                if (!FileHash.LooksLikeSha256((parts[0] ?? "").ToLowerInvariant())) return o;
                // Only now is the DLL hashed: a note we could not use is not worth reading 1 MB for, and a hash that
                // throws (the game is holding the file) must stay "unknown", never "replaced".
                if (!FileHash.Same(parts[0], FileHash.Sha256(dllPath)))
                {
                    o.Kind = ModOrigin.Replaced;                                                // 記録はある、でも別のファイル
                    return o;
                }
                o.Kind = parts[1];
                if (parts[2].Length > 0) o.Version = parts[2];
                o.At = parts[3];
            }
            catch (Exception) { o.Kind = ModOrigin.Unknown; o.At = null; }
            return o;
        }
    }
}
