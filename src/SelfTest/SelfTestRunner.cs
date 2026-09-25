// "StarPocket Client.exe" --self-test <dir> (PORT-MAP 4.5 / 8).
// No window, no tray, no mutex of the app, no network, not the real %LOCALAPPDATA% or game folder: every fake folder
// and file is made inside <dir>. Prints "PASS name" / "FAIL name: why" / "INFO text" to the calling console (the exe is a
// WinExe, so it attaches to its parent's console) and to <dir>\self-test.txt, then "RESULT PASS n/n" or
// "RESULT FAIL f/n"; the exit code is 0 only when everything passed.
// Suites: the app shell (ShellSelfTests) and Aegis (AegisSelfTests).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Starpocket.Client.SelfTest
{
    internal sealed class SelfTestRunner
    {
        /// <summary>Every suite, in order.</summary>
        public static readonly List<Action<SelfTestRunner>> Suites = new List<Action<SelfTestRunner>>
        {
            ShellSelfTests.Run,
            AegisSelfTests.Run,
            ReportSelfTests.Run,
            InstallSelfTests.Run,
            IdentitySelfTests.Run,
            AccentSelfTests.Run,
            DevLogSelfTests.Run,
            HeadlessSelfTests.Run,
            ProfileSelfTests.Run,
            ConsentSelfTests.Run,
        };

        readonly string root;
        readonly StringBuilder report = new StringBuilder();
        TextWriter console;
        int pass, fail;
        string section = "";

        SelfTestRunner(string root) { this.root = root; }

        /// <summary>The folder every test writes into (&lt;dir&gt;\work).</summary>
        public string Root => root;

        public static int Run(string dir)
        {
            TextWriter con = OpenConsole();
            if (string.IsNullOrWhiteSpace(dir))
            {
                con?.WriteLine("usage: \"StarPocket Client.exe\" --self-test <folder>");
                return 1;
            }
            string full;
            try
            {
                full = Path.GetFullPath(dir);
                Directory.CreateDirectory(full);
            }
            catch (Exception ex)
            {
                con?.WriteLine("FAIL self-test folder: " + ex.Message);
                con?.WriteLine("RESULT FAIL 1/1");
                return 1;
            }
            // <dir>\work is removed only when an earlier self-test made it (its marker file is inside): a "work" folder of
            // someone else's is never touched
            string work = Path.Combine(full, "work");
            string why = PrepareWork(work);
            if (why != null)
            {
                con?.WriteLine("FAIL self-test folder: " + why);
                con?.WriteLine("RESULT FAIL 1/1");
                return 1;
            }

            var r = new SelfTestRunner(work) { console = con };
            r.Info(AppInfo.Name + " " + AppInfo.Version + " self-test (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")");
            foreach (var suite in Suites)
            {
                try { suite(r); }
                catch (Exception ex) { r.Fail("suite " + suite.Method.DeclaringType?.Name, ex.ToString()); }
            }
            int total = r.pass + r.fail;
            r.Line(r.fail == 0 ? "RESULT PASS " + total + "/" + total : "RESULT FAIL " + r.fail + "/" + total);
            try { File.WriteAllText(Path.Combine(full, "self-test.txt"), r.report.ToString(), new UTF8Encoding(false)); } catch (Exception) { }
            try { con?.Flush(); } catch (Exception) { }
            return r.fail == 0 ? 0 : 1;
        }

        /// <summary>The marker file a self-test puts in the work folder it makes.</summary>
        public const string MarkerName = ".starpocket-selftest";

        /// <summary>A fresh, empty <paramref name="work"/> folder with the marker inside; null when done, else why not. An
        /// existing folder is deleted only when it has the marker (an earlier self-test made it).</summary>
        internal static string PrepareWork(string work)
        {
            try
            {
                if (Directory.Exists(work))
                {
                    if (!File.Exists(Path.Combine(work, MarkerName))) return work + " exists and was not made by the self-test";
                    Directory.Delete(work, true);
                }
                else if (File.Exists(work)) return work + " is a file";
                Directory.CreateDirectory(work);
                File.WriteAllText(Path.Combine(work, MarkerName), "made by \"StarPocket Client.exe\" --self-test; this folder is deleted by the next self-test run\r\n", new UTF8Encoding(false));
                return null;
            }
            catch (Exception ex) { return work + ": " + ex.Message; }
        }

        /// <summary>The caller's console (redirected output is used as it is; otherwise attach to the parent's console).</summary>
        static TextWriter OpenConsole()
        {
            try
            {
                Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
                var s = Console.OpenStandardOutput();
                if (s == Stream.Null) return null;
                return new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };
            }
            catch (Exception) { return null; }
        }

        void Line(string s)
        {
            report.Append(s).Append("\r\n");
            try { console?.WriteLine(s); } catch (Exception) { }
        }

        public void Section(string name) { section = name; }

        string Name(string n) => string.IsNullOrEmpty(section) ? n : section + ": " + n;

        public void Info(string text) => Line("INFO " + text);

        public void Pass(string name) { pass++; Line("PASS " + Name(name)); }

        public void Fail(string name, string why) { fail++; Line("FAIL " + Name(name) + ": " + (why ?? "").Replace("\r", " ").Replace("\n", " | ")); }

        public void Check(string name, bool ok, string why = null)
        {
            if (ok) Pass(name); else Fail(name, why ?? "false");
        }

        public void Equal<T>(string name, T expected, T actual)
        {
            bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
            if (ok) Pass(name); else Fail(name, "expected <" + Show(expected) + "> got <" + Show(actual) + ">");
        }

        static string Show(object o) => o == null ? "null" : o.ToString();

        /// <summary>Runs one test; an exception is a failure of that test only.</summary>
        public void Test(string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { Fail(name, ex.GetType().Name + ": " + ex.Message); }
        }

        /// <summary>A new empty folder under the work folder.</summary>
        public string NewDir(string name)
        {
            string d = Path.Combine(root, name);
            if (Directory.Exists(d)) Directory.Delete(d, true);
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>Creates a file (and its folders) with some bytes.</summary>
        public static string Touch(string path, string content = "x")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }
    }
}
