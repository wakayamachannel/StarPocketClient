// client.log - the app's own log (PORT-MAP 2.1: same line format and the same 1 MB rule as the launcher's launcher.log,
// which the app never touches in v0.1). Lines: "[HH:mm:ss] text", CRLF, UTF-8 (a new file starts with a BOM, like
// [IO.File]::AppendAllText with [Text.Encoding]::UTF8 in the ps1). Never throws.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;

namespace Starpocket.Client.Core
{
    /// <summary>What the app's own log page shows (v0.4 showLog).</summary>
    internal sealed class LogTail
    {
        public string[] Lines = new string[0];
        /// <summary>true when older lines were left out (the file is longer than the page shows).</summary>
        public bool Truncated;
        public long Bytes;
    }

    internal sealed class ClientLog
    {
        readonly object gate = new object();
        public string Path { get; }

        /// <summary>A log that writes nowhere (the self-test's default).</summary>
        public static readonly ClientLog None = new ClientLog(null);

        public ClientLog(string path) { Path = path; }

        /// <summary>Deletes the log when it is over 1 MB (at start, like "rotate launcher.log" in the ps1).</summary>
        public void RotateAtStart()
        {
            if (Path == null) return;
            try { var fi = new FileInfo(Path); if (fi.Exists && fi.Length > 1024 * 1024) fi.Delete(); } catch (Exception) { }
        }

        public void Write(string text)
        {
            if (Path == null) return;
            string line = "[" + DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "] " + text + "\r\n";
            lock (gate)
            {
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(Path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(Path, line, Encoding.UTF8);
                }
                catch (Exception) { }
            }
        }

        /// <summary>v0.4 showLog: the newest lines, for the log page inside the app. At most
        /// <paramref name="maxLines"/> lines, and never more than <paramref name="maxBytes"/> read from the end of the
        /// file - a log the app is still writing to must not be able to fill the window. The file is opened so that
        /// this app's own writer, and the PowerShell launcher, can carry on writing while it is read. Never throws:
        /// a log that is not there, or cannot be read, comes back as no lines at all.</summary>
        public LogTail Tail(int maxLines = 400, int maxBytes = 256 * 1024)
        {
            var t = new LogTail();
            if (Path == null) return t;
            try
            {
                using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    t.Bytes = fs.Length;
                    bool cut = fs.Length > maxBytes;
                    if (cut) fs.Seek(fs.Length - maxBytes, SeekOrigin.Begin);
                    string text;
                    using (var sr = new StreamReader(fs, new UTF8Encoding(false), !cut)) text = sr.ReadToEnd();
                    var lines = text.Replace("\r\n", "\n").Split('\n');
                    int from = 0;
                    // a read that started in the middle of the file starts in the middle of a line: that half-line goes
                    if (cut && lines.Length > 1) from = 1;
                    var kept = new System.Collections.Generic.List<string>();
                    for (int i = from; i < lines.Length; i++)
                        if (lines[i].Length > 0) kept.Add(lines[i].TrimStart('﻿'));
                    t.Truncated = cut || kept.Count > maxLines;
                    if (kept.Count > maxLines) kept.RemoveRange(0, kept.Count - maxLines);
                    t.Lines = kept.ToArray();
                }
            }
            catch (Exception) { }
            return t;
        }
    }
}
