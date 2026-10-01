// The black window that started us (SPEC 6.4). The exe is a windowed program, so it has no console of its own:
// AttachConsole(ATTACH_PARENT_PROCESS) borrows the one of whoever typed the command. When the output was redirected
// (a pipe, "> file.txt", a CI step) that stream is used as it is.
// Used by every headless mode of v0.4 (--action, --scan-only, --verify-download, "a Client is already running", a
// usage error). --self-test does NOT come through here: SelfTestRunner.OpenConsole is a copy of this, made before
// this file was fixed, and only one line of that file (see OpenWriter below) would bring it back in step.
// Writing is never allowed to throw: a missing console must not change what a job does or what it returns.
//
// 2026-10-01, the characters. WHERE the text goes decides HOW it has to be written, and the two answers differ:
//
//   - a real console (someone typed the command and is reading the answer): the text is handed over as Unicode with
//     WriteConsoleW. The console then converts nothing, so its code page does not come into it at all - CP932 (the
//     default of Japanese Windows), 65001 after "chcp 65001", and the Chinese strings on either of them, all arrive
//     whole. Writing UTF-8 BYTES into a CP932 console is exactly what made the Japanese output unreadable.
//
//   - redirected (a pipe, a file, the build workflow's step): there is no console to hand characters to, so bytes have
//     to be chosen - and the reader cannot be asked which ones it wants. UTF-8 WITHOUT a BOM is kept, as before: it is
//     what the rest of this repository writes (self-test.txt, launcher-state.json) and what the build workflow's pwsh
//     reads. Windows PowerShell 5.1 is the exception - it reads a program's output in CP932 - and the environment
//     variable POCKETROLES_CONSOLE_CP is there for that case and for no other.
//
// Console.OutputEncoding is deliberately NOT set anywhere here: setting it changes the code page of the console
// itself, and that console belongs to whoever typed the command - it stays changed after this process is gone.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Starpocket.Client.Core
{
    internal sealed class ConsoleOut
    {
        readonly TextWriter writer;

        ConsoleOut(TextWriter w) { writer = w; }

        /// <summary>True when there really is somewhere to write (a usage error then needs no message box).</summary>
        public bool Attached => writer != null;

        public static ConsoleOut Open() => new ConsoleOut(OpenWriter());

        /// <summary>The writer <see cref="Open"/> writes through, for a caller that wants a plain TextWriter; null when
        /// there is nowhere to write. SelfTestRunner.OpenConsole is a second copy of this and should call this instead,
        /// or --self-test keeps printing UTF-8 bytes into a CP932 console.</summary>
        public static TextWriter OpenWriter()
        {
            try
            {
                Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
                var raw = Console.OpenStandardOutput();
                if (raw == null || raw == Stream.Null) return null;
                // the handle asked about below is the very handle this stream writes to, so the answer cannot be stale.
                // (Console.IsOutputRedirected could be: .NET works it out once and remembers it, and anything that read
                //  it before AttachConsole read it while this process still had no console at all.)
                IntPtr handle = StdOut();
                int forced = ForcedCodePage();
                if (forced <= 0 && IsConsole(handle))
                    return new ConsoleWriter(handle, raw, ByteEncoding(ConsoleCodePage()));
                return new StreamWriter(raw, ByteEncoding(forced)) { AutoFlush = true, NewLine = "\r\n" };
            }
            catch (Exception) { return null; }
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

        // ------------------------------------------------------------------ which bytes, when it has to be bytes

        /// <summary>UTF-8 with no BOM: what a redirected run is written in, and what every file this app writes uses.</summary>
        internal static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>The bytes to write for a code page. 0 (not known / no console) and 65001 both mean UTF-8 without a
        /// BOM; a code page this Windows does not have ends there too. A character the code page has no room for is
        /// written as "?" rather than thrown about mid-line, and a BOM is never put in front - one in the middle of a
        /// console line, or at the top of a CI log, is garbage of its own.</summary>
        internal static Encoding ByteEncoding(int codePage)
        {
            if (codePage <= 0 || codePage == 65001) return Utf8NoBom;
            try
            {
                var enc = Encoding.GetEncoding(codePage, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
                return enc == null || enc.GetPreamble().Length > 0 ? Utf8NoBom : enc;
            }
            catch (Exception) { return Utf8NoBom; }
        }

        /// <summary>A code page written by hand: a number, or "utf8"/"utf-8" (65001). 0 when there is nothing usable -
        /// a typo must not decide how the output looks.</summary>
        internal static int CodePageFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string t = text.Trim();
            if (t.Equals("utf8", StringComparison.OrdinalIgnoreCase) || t.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                return 65001;
            int cp;
            return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out cp) && cp > 0 ? cp : 0;
        }

        /// <summary>POCKETROLES_CONSOLE_CP: for the one case nobody can work out on its own - output read by a program
        /// that expects the OEM code page (Windows PowerShell 5.1 reading a pipe or writing "> file.txt").</summary>
        static int ForcedCodePage()
        {
            try { return CodePageFromText(Environment.GetEnvironmentVariable("POCKETROLES_CONSOLE_CP")); }
            catch (Exception) { return 0; }
        }

        // ------------------------------------------------------------------ where the text really goes
        const int STD_OUTPUT_HANDLE = -11;
        const int FILE_TYPE_CHAR = 0x0002;
        static readonly IntPtr INVALID_HANDLE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GetStdHandle(int which);
        [DllImport("kernel32.dll", SetLastError = true)] static extern int GetFileType(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetConsoleMode(IntPtr handle, out int mode);
        [DllImport("kernel32.dll")] static extern int GetConsoleOutputCP();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WriteConsoleW(IntPtr handle, string text, int count, out int written, IntPtr reserved);

        static IntPtr StdOut()
        {
            try { return GetStdHandle(STD_OUTPUT_HANDLE); } catch (Exception) { return IntPtr.Zero; }
        }

        /// <summary>True only for a real console screen buffer. A file and a pipe are not character devices; NUL is one,
        /// but it is not a console, which is what the second question settles.</summary>
        static bool IsConsole(IntPtr handle)
        {
            if (handle == IntPtr.Zero || handle == INVALID_HANDLE) return false;
            try
            {
                if ((GetFileType(handle) & 0xFF) != FILE_TYPE_CHAR) return false;
                int mode;
                return GetConsoleMode(handle, out mode);
            }
            catch (Exception) { return false; }
        }

        /// <summary>The code page of the console we borrowed (932 on Japanese Windows), or 0 when there is none. It is
        /// only read, never set.</summary>
        static int ConsoleCodePage()
        {
            try { return GetConsoleOutputCP(); } catch (Exception) { return 0; }
        }

        /// <summary>A console takes Unicode straight (WriteConsoleW), so nothing of ours is turned into bytes and the
        /// console's code page never enters into it: Japanese on a CP932 console, and the Chinese strings on that same
        /// console, both arrive whole. Only the console FONT can still be short of a glyph, and that is the reader's
        /// setting, not ours. If the console ever refuses the Unicode call, everything from then on is written to the
        /// same standard output as bytes of its code page - something readable rather than silence.</summary>
        sealed class ConsoleWriter : TextWriter
        {
            // one call per line, and a long line in pieces. A piece never ends between the two halves of a surrogate
            // pair, which would put two broken characters on the screen.
            const int Chunk = 8192;

            readonly IntPtr handle;
            readonly Stream raw;
            readonly Encoding bytes;
            bool refused;

            public ConsoleWriter(IntPtr handle, Stream raw, Encoding bytes) : base(CultureInfo.InvariantCulture)
            {
                this.handle = handle;
                this.raw = raw;
                this.bytes = bytes;
                NewLine = "\r\n";
            }

            /// <summary>What the text is handed over as - characters, not a code page.</summary>
            public override System.Text.Encoding Encoding => System.Text.Encoding.Unicode;

            public override void WriteLine(string value) => Write((value ?? "") + "\r\n");

            public override void Write(char value) => Write(value.ToString());

            public override void Write(char[] buffer, int index, int count)
            {
                if (buffer != null && count > 0) Write(new string(buffer, index, count));
            }

            public override void Write(string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                if (!refused && WriteUnicode(value)) return;
                refused = true;
                var b = bytes.GetBytes(value);
                raw.Write(b, 0, b.Length);
                raw.Flush();
            }

            public override void Flush()
            {
                try { raw.Flush(); } catch (Exception) { }   // WriteConsoleW keeps nothing back; this is for the other path
            }

            /// <summary>The handle is Windows's own standard output, never ours to close.</summary>
            protected override void Dispose(bool disposing) { }

            bool WriteUnicode(string text)
            {
                int at = 0;
                while (at < text.Length)
                {
                    int take = Math.Min(Chunk, text.Length - at);
                    if (at + take < text.Length && char.IsHighSurrogate(text[at + take - 1])) take--;
                    if (take <= 0) return at > 0;
                    int written;
                    if (!WriteConsoleW(handle, text.Substring(at, take), take, out written, IntPtr.Zero) || written <= 0)
                        return at > 0;   // nothing went out yet: say so, and the bytes are written instead. Half a line
                                         // out already (which this call has never done): no second copy of it.
                    at += written;
                }
                return true;
            }
        }
    }
}
