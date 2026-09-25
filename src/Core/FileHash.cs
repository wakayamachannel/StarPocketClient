// The SHA-256 of a file, written the way every fingerprint in this app is written: 64 lowercase hex characters
// (Aegis's mod-fingerprint.txt, the evidence records and AegisScan all use this same spelling).
//
// Why it exists: what the installer downloads is unpacked INTO the game copy and loaded as code by the game, so
// "it came from the right host" is not enough - the file itself has to be the file we pinned (src\AppInfo.cs
// BepSha256, docs\BEPINEX-PIN.md). The host list in src\Core\Downloads.cs and this check are the two halves of the
// same guard: where it came from, and what it is.
//
// The file is read in blocks, never into memory at once (the BepInEx zip is about 31 MB and the cap on a download
// is 256 MB), and it is opened for reading only, with sharing allowed, so a virus scanner holding the file does not
// turn into a crash.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace Starpocket.Client.Core
{
    internal static class FileHash
    {
        /// <summary>How long a SHA-256 is when it is written as hex (what a pinned value must look like).</summary>
        public const int Sha256HexLength = 64;

        /// <summary>The file's SHA-256 as 64 lowercase hex characters. Throws like File.OpenRead when the file is not
        /// there or cannot be read.</summary>
        public static string Sha256(string path)
        {
            using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            using (var alg = SHA256.Create())
                return Hex(alg.ComputeHash(s));
        }

        public static string Hex(byte[] bytes)
        {
            if (bytes == null) return null;
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Two written-down hashes are the same one (case is ignored; nothing at all is never the same as
        /// anything, so a missing pin can never pass for a match).</summary>
        public static bool Same(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>Is this text a hash we could have pinned at all: 64 hex characters, lowercase. A pin that is empty,
        /// short, or written in another shape is a mistake in the source, and the self-test says so before a build goes
        /// out (a pin nobody can match would refuse every install).</summary>
        public static bool LooksLikeSha256(string hex)
        {
            if (hex == null || hex.Length != Sha256HexLength) return false;
            foreach (var c in hex)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
}
