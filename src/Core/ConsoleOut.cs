// The black window that started us (SPEC 6.4). The exe is a windowed program, so it has no console of its own:
// AttachConsole(ATTACH_PARENT_PROCESS) borrows the one of whoever typed the command. When the output was redirected
// (a pipe, "> file.txt", a CI step) that stream is used as it is.
// Used by --self-test and by every headless mode of v0.4 (--action, --scan-only, a usage error).
// Writing is never allowed to throw: a missing console must not change what a job does or what it returns.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;

namespace Starpocket.Client.Core
{
    internal sealed class ConsoleOut
    {
        readonly TextWriter writer;

        ConsoleOut(TextWriter w) { writer = w; }

        /// <summary>True when there really is somewhere to write (a usage error then needs no message box).</summary>
        public bool Attached => writer != null;

        public static ConsoleOut Open()
        {
            try
            {
                Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
                var s = Console.OpenStandardOutput();
                if (s == Stream.Null) return new ConsoleOut(null);
                return new ConsoleOut(new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" });
            }
            catch (Exception) { return new ConsoleOut(null); }
        }

        /// <summary>Nothing is written anywhere (the self-test gives its own writer instead).</summary>
        public static ConsoleOut None => new ConsoleOut(null);

        public static ConsoleOut To(TextWriter w) => new ConsoleOut(w);

        public void Write(string line)
        {
            if (writer == null) return;
            try { writer.WriteLine(line ?? ""); } catch (Exception) { }
        }

        /// <summary>A text that may hold line breaks (the usage): one console line each.</summary>
        public void WriteBlock(string text)
        {
            foreach (var line in (text ?? "").Replace("\r\n", "\n").Split('\n')) Write(line);
        }

        public void Flush()
        {
            if (writer == null) return;
            try { writer.Flush(); } catch (Exception) { }
        }
    }
}
