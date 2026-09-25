// One open handle on a downloaded zip, from the fingerprint all the way to the last unpacked file.
//
// Why it exists (v0.4 review of the pin): checking the file and unpacking it used to be three separate opens -
// FileHash.Sha256 opened and closed it, ZipFiles.Has opened and closed it again, ZipFiles.ExpandOver opened it a third
// time. In between, nobody held the file. The cache folder is %TEMP%\PocketRolesLauncher (ClientContext.CacheDir),
// which this app shares with the PowerShell launcher and with everything else running as the same Windows user, so
// another program could watch for the "checked" line in the log and swap the file before the unpacking started. What
// came out of that zip is copied into the game folder and loaded as code by the game, so that gap had to go.
//
// What this gives: the file is opened ONCE with FileShare.Read - no one else may write to it while we hold it - and
// the same handle answers all three questions. Nothing is re-opened, so there is no moment between "this is the file
// we pinned" and "these are the files we unpacked" in which the bytes could change.
//
// What it does not give: it is not a lock on the folder. A file can still be swapped BEFORE we open it (which is
// exactly what the fingerprint catches) and the file we refused can still be deleted and replaced afterwards (which is
// why a refused file is deleted and the next address is tried).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Starpocket.Client.Core
{
    internal sealed class VerifiedZip : IDisposable
    {
        readonly FileStream stream;
        ZipArchive archive;
        string sha;

        VerifiedZip(FileStream s) { stream = s; }

        /// <summary>Opens the file for reading and keeps others from writing to it until <see cref="Dispose"/>. Throws
        /// like File.OpenRead when it is not there, and when something else already holds it for writing - which is
        /// itself an answer: we do not unpack a file another program is still working on.</summary>
        public static VerifiedZip Open(string path)
            => new VerifiedZip(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536));

        /// <summary>The file's SHA-256 as 64 lowercase hex characters, read from the handle we keep holding.</summary>
        public string Sha256
        {
            get
            {
                if (sha == null)
                {
                    stream.Position = 0;
                    using (var alg = SHA256.Create()) sha = FileHash.Hex(alg.ComputeHash(stream));
                }
                return sha;
            }
        }

        /// <summary>Does the zip hold this entry (\ read as /, case ignored)? False for anything we cannot read as a
        /// zip at all - by the time this is asked the fingerprint has already matched, so that would be our own file
        /// being unreadable, and the caller refuses it either way.</summary>
        public bool Has(string entry)
        {
            try
            {
                foreach (var e in Archive.Entries)
                    if (string.Equals(e.FullName.Replace('\\', '/'), entry, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Unpacks over <paramref name="dest"/> exactly as <see cref="ZipFiles.ExpandOver(string, string, string[], Action{int})"/>
        /// does, from the handle already open. Returns how many files were written; throws on a broken zip.</summary>
        public int ExpandOver(string dest, string[] skip, Action<int> progress = null)
            => ZipFiles.ExpandOver(Archive, dest, skip, progress);

        ZipArchive Archive
        {
            get
            {
                if (archive == null)
                {
                    stream.Position = 0;
                    // leaveOpen: the stream outlives the archive, because this object owns it
                    archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
                }
                return archive;
            }
        }

        public void Dispose()
        {
            try { if (archive != null) archive.Dispose(); } catch (Exception) { }
            archive = null;
            try { stream.Dispose(); } catch (Exception) { }
        }
    }
}
