// Reading a log the game may still be writing (ps1 Read-TextShared / Read-TextCapped).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;

namespace Starpocket.Client.Core
{
    internal static class TextFiles
    {
        /// <summary>The whole file, with ReadWrite | Delete sharing: BepInEx keeps LogOutput.log open while the game
        /// runs, and the launcher's housekeeping may delete a past log at the same moment.</summary>
        public static string ReadShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, Encoding.UTF8, true))
                return sr.ReadToEnd();
        }

        /// <summary>A past log for a zip: the whole file up to <paramref name="max"/> bytes, else its first 1 MB and its
        /// last (max - 1 MB), each cut at a line break, with one line saying how much was left out (ps1 Read-TextCapped).
        /// A [Debug] WireLog session can be hundreds of MB, which would make the zip useless to send.</summary>
        public static string ReadCapped(string path, long max, Func<long, string> formatSize)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long len = fs.Length;
                if (len <= max)
                {
                    using (var sr = new StreamReader(fs, Encoding.UTF8, true)) return sr.ReadToEnd();
                }
                int headLen = (int)Math.Min(1024 * 1024, max / 4);
                int tailLen = (int)(max - headLen);
                var buf = new byte[tailLen];
                int got = 0, r;
                while (got < headLen && (r = fs.Read(buf, got, headLen - got)) > 0) got += r;
                string head = Encoding.UTF8.GetString(buf, 0, got).TrimStart('﻿');
                int i = head.LastIndexOf('\n');
                if (i >= 0) head = head.Substring(0, i + 1);
                fs.Seek(len - tailLen, SeekOrigin.Begin);
                got = 0;
                while (got < tailLen && (r = fs.Read(buf, got, tailLen - got)) > 0) got += r;
                string tail = Encoding.UTF8.GetString(buf, 0, got);
                i = tail.IndexOf('\n');
                if (i >= 0) tail = tail.Substring(i + 1);
                return head + "\r\n[... launcher: " + formatSize(len - headLen - tailLen) + " of this " + formatSize(len) +
                       " log left out of the report (" + formatSize(max) + " per past log) ...]\r\n\r\n" + tail;
            }
        }
    }
}
