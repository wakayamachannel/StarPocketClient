// v0.4 part 2: running with no window, and the tray's own behaviours.
//   - every command-line argument: present, absent, misspelled, duplicated, two modes at once
//   - the single-instance trap: for EACH argument, what a second start does while a Client is already running
//   - --action install|check|report|status with fake jobs, and the busy rule of SPEC 5.1
//   - --scan-only against a fake game folder and a fake PC (no network, no real registry, no window)
//   - the scan card's picture, drawn into a bitmap with no window at all
//   - the tray's 15-second rule and the window's behaviour around playing (SPEC 5.4)
// Nothing here opens a window, starts a process, touches the real %LOCALAPPDATA% or reaches the network. The kernel
// names used are the app's own with ".test.<something>" in them, never the Client's.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Starpocket.Client.Aegis;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client.SelfTest
{
    internal static class HeadlessSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            ArgumentTests(r);
            UsageTests(r);
            VerifyDownloadTests(r);
            TrapTests(r);
            SignalTests(r);
            ActionTests(r);
            ScanOnlyTests(r);
            ScanCardTests(r);
            WindowTests(r);
            r.Section("");
        }

        static StartupPlan Plan(params string[] args) => Startup.Plan(CommandLine.Parse(args));

        /// <summary>Runs <paramref name="body"/> on a thread of its own and waits for its answer. A named mutex belongs
        /// to the thread that took it, so "another start" has to be another thread here - on this one the same owner
        /// would simply be let through again, and the test would prove nothing.</summary>
        static bool OnAnotherThread(Func<bool> body)
        {
            bool answer = false;
            var t = new Thread(() => { try { answer = body(); } catch (Exception) { answer = false; } }) { IsBackground = true };
            t.Start();
            t.Join(10000);
            return answer;
        }

        // ------------------------------------------------------------------ every argument, every spelling
        static void ArgumentTests(SelfTestRunner r)
        {
            r.Section("arguments");
            r.Test("no argument at all", () =>
            {
                var p = Plan();
                r.Check("the app with its window", p.Mode == StartupMode.Normal && !p.HideWindow && !p.AutoLaunch && !p.Windowed && !p.TrayStart);
                r.Check("a second start raises the running window", p.WhenRunning == Handoff.Show && p.UsesSingleInstance);
            });

            r.Test("--tray", () =>
            {
                foreach (var spelling in new[] { "--tray", "-tray", "/tray", "-Tray", "--TRAY" })
                {
                    var p = Plan(spelling);
                    r.Check(spelling, p.Mode == StartupMode.Tray && p.HideWindow && p.TrayStart && !p.AutoLaunch);
                }
                r.Check("a second --tray does NOT raise the running window", Plan("--tray").WhenRunning == Handoff.AlreadyThere);
                r.Check("written twice it is still one tray", Plan("--tray", "--tray").Mode == StartupMode.Tray);
                var both = Plan("--tray", "--autolaunch");
                r.Check("--tray --autolaunch: no window, and it plays", both.Mode == StartupMode.Tray && both.HideWindow && both.AutoLaunch && both.TrayStart);
            });

            r.Test("--autolaunch", () =>
            {
                foreach (var spelling in new[] { "--autolaunch", "-autolaunch", "/AutoLaunch", "--auto-launch" })
                {
                    var p = Plan(spelling);
                    r.Check(spelling, p.Mode == StartupMode.Normal && p.AutoLaunch && p.HideWindow && !p.TrayStart);
                }
                r.Check("a second start asks the running one to play", Plan("--autolaunch").WhenRunning == Handoff.Play);
                r.Check("written twice it still plays once", Plan("--autolaunch", "--autolaunch").AutoLaunch);
                var w = Plan("--autolaunch", "--windowed");
                r.Check("--autolaunch --windowed", w.AutoLaunch && w.Windowed && w.HideWindow);
                r.Check("-Windowed on its own: the app, and every start of this run is 1600x900", Plan("-Windowed").Mode == StartupMode.Normal && Plan("-Windowed").Windowed);
            });

            r.Test("--scan-only", () =>
            {
                foreach (var spelling in new[] { "--scan-only", "--scanonly", "-ScanOnly", "/scan-only" })
                {
                    var p = Plan(spelling);
                    r.Check(spelling, p.Mode == StartupMode.ScanOnly && p.WhenRunning == Handoff.None);
                }
                r.Check("it is outside the one-Client rule (SPEC 5.1)", !Plan("--scan-only").UsesSingleInstance);
            });

            r.Test("--action", () =>
            {
                foreach (var a in Startup.Actions)
                {
                    var p = Plan("--action", a);
                    r.Check("--action " + a, p.Mode == StartupMode.Action && p.Action == a);
                }
                r.Equal("-Action Install (the launcher's spelling)", "install", Plan("-Action", "Install").Action);
                r.Equal("--action=Status", "status", Plan("--action=Status").Action);
                r.Equal("  --action  REPORT  (spaces and case)", "report", Plan("--action", " REPORT ").Action);
                r.Check("install takes the task lock", Plan("--action", "install").NeedsTaskLock);
                r.Check("check takes it too", Plan("--action", "check").NeedsTaskLock);
                r.Check("status only reads", !Plan("--action", "status").NeedsTaskLock);
                r.Check("report only reads", !Plan("--action", "report").NeedsTaskLock);
                r.Check("it is outside the one-Client rule (SPEC 5.1)", !Plan("--action", "status").UsesSingleInstance);
                r.Check("and it never hands its work to a running Client", Plan("--action", "install").WhenRunning == Handoff.None);
                // v0.4 review: this used to say "written twice, the last one is meant" and it ran the last one without
                // a word. Two different jobs on one line is a mistake, and it is now refused like any other.
                r.Equal("written twice with two jobs, neither is run", StartupMode.Usage, Plan("--action", "install", "--action", "status").Mode);
            });

            r.Test("--uninstall and --self-test still decide as they did", () =>
            {
                var u = Plan("--uninstall");
                r.Check("--uninstall", u.Mode == StartupMode.Uninstall && u.WhenRunning == Handoff.Refuse);
                r.Check("--uninstall --quiet", Plan("--uninstall", "--quiet").Quiet);
                var t = Plan("--self-test", @"C:\t");
                r.Check("--self-test", t.Mode == StartupMode.SelfTest && !t.UsesSingleInstance);
            });
        }

        // ------------------------------------------------------------------ a word that was not understood
        static void UsageTests(SelfTestRunner r)
        {
            r.Section("arguments not understood");
            // the v0.3 bug in one line: "--uninstall" was not understood, was only logged, and the app OPENED instead.
            // Anything this app cannot read must stop it, say so, and end with 1.
            r.Test("a misspelled option stops the app", () =>
            {
                foreach (var bad in new[] { "--trey", "--tray2", "--scanonlyy", "-Actionn", "--auto_launch", "--window", "--uninstal" })
                {
                    var p = Plan(bad);
                    r.Check(bad + " -> usage, exit 1", p.Mode == StartupMode.Usage && p.ExitCode == 1);
                    r.Check("... and it names " + bad, Startup.UsageText("ja", p).Contains(bad));
                }
            });
            r.Test("a misspelled option beats everything else on the line", () =>
            {
                r.Check("--uninstall --trey does NOT uninstall", Plan("--uninstall", "--trey").Mode == StartupMode.Usage);
                r.Check("--action install --trey does NOT install", Plan("--action", "install", "--trey").Mode == StartupMode.Usage);
                r.Check("--tray --trey does not start a tray", Plan("--tray", "--trey").Mode == StartupMode.Usage);
            });
            r.Test("a word that is not an option at all", () =>
            {
                var p = Plan(@"C:\somebody\dropped\this.txt");
                r.Check("it stops the app", p.Mode == StartupMode.Usage && p.ExitCode == 1);
                r.Check("and it is named", Startup.UsageText("en", p).Contains("this.txt"));
                r.Check("an empty argument is not a mistake", Plan("", "  ").Mode == StartupMode.Normal);
            });
            r.Test("two modes at once", () =>
            {
                foreach (var pair in new[]
                {
                    new[] { "--tray", "--scan-only" },
                    new[] { "--action", "status", "--uninstall" },
                    new[] { "--self-test", @"C:\t", "--tray" },
                    new[] { "--scan-only", "--uninstall" },
                    new[] { "--action", "install", "--tray" },
                })
                    r.Check(string.Join(" ", pair) + " -> usage", Plan(pair).Mode == StartupMode.Usage);
            });
            // v0.4 review: two different values for ONE option. The later one used to win in silence, so
            // "--action install --action status" ran status and never said install had been dropped.
            r.Test("the same option twice with two different values", () =>
            {
                foreach (var line in new[]
                {
                    new[] { "--action", "install", "--action", "status" },
                    new[] { "--action", "status", "--action=report" },
                    new[] { "--game-dir", @"C:\a", "--game-dir", @"C:\b" },
                    new[] { "--language", "ja", "--lang", "en" },
                })
                {
                    var p = Plan(line);
                    r.Check(string.Join(" ", line) + " -> usage, exit 1", p.Mode == StartupMode.Usage && p.ExitCode == 1);
                }
                r.Check("the option that was written twice is named", Startup.UsageText("en", Plan("--action", "install", "--action", "status")).Contains("--action"));
                r.Check("neither job is chosen", Plan("--action", "install", "--action", "status").Action == "");
                // the same value twice is not a mistake: a shortcut with a repeated word still starts
                r.Equal("--action status --action status is still status", StartupMode.Action, Plan("--action", "status", "--action", "status").Mode);
                r.Equal("... and it is the job that was asked for", "status", Plan("--action", "status", "--action", "status").Action);
                r.Check("--game-dir twice with the same folder is fine", Plan("--game-dir", @"C:\a", "--game-dir", @"C:\a").Mode == StartupMode.Normal);
                r.Equal("nothing is listed when nothing was repeated", 0, CommandLine.Parse(new[] { "--action", "status" }).Twice.Count);
            });
            r.Test("--action with something that is not a job", () =>
            {
                foreach (var bad in new[] { "installl", "Status2", "launch", "uninstall" })
                {
                    var p = Plan("--action", bad);
                    r.Check("--action " + bad, p.Mode == StartupMode.Usage && p.ExitCode == 1);
                }
                r.Check("the four are named", Startup.UsageText("en", Plan("--action", "nope")).Contains("install|check|report|status"));
            });
            r.Test("an option that needs a value and was given none", () =>
            {
                foreach (var line in new[]
                {
                    new[] { "--action" },
                    new[] { "--action", "" },
                    new[] { "--game-dir" },
                    new[] { "--language" },
                })
                    r.Check(string.Join(" ", line) + " -> usage", Plan(line).Mode == StartupMode.Usage);
                // the value was forgotten, so the NEXT option must not be swallowed as one
                var p = Plan("--action", "--language", "en");
                r.Check("--action --language en: the mistake is --action, not the language", p.Mode == StartupMode.Usage && Startup.UsageText("en", p).Contains("--action"));
                r.Equal("and --language was still read properly", "en", CommandLine.Parse(new[] { "--action", "--language", "en" }).Language);
                r.Equal("a folder name is still a value, whatever it looks like", @"/weird", CommandLine.Parse(new[] { "--game-dir", @"/weird" }).GameDir);
            });
            r.Test("--autolaunch / --windowed where there is no game to start", () =>
            {
                foreach (var line in new[]
                {
                    new[] { "--action", "status", "--windowed" },
                    new[] { "--action", "install", "--autolaunch" },
                    new[] { "--scan-only", "--windowed" },
                    new[] { "--uninstall", "--autolaunch" },
                    new[] { "--self-test", @"C:\t", "--windowed" },
                })
                    r.Check(string.Join(" ", line) + " -> usage", Plan(line).Mode == StartupMode.Usage);
            });
            r.Test("the usage text", () =>
            {
                string ja = Startup.UsageText("ja", Plan("--trey"));
                string en = Startup.UsageText("en", Plan("--trey"));
                string zh = Startup.UsageText("zh-CN", Plan("--trey"));
                r.Check("three languages, three texts", ja != en && en != zh && ja != zh);
                foreach (var opt in new[] { "--tray", "--autolaunch", "--windowed", "--scan-only", "--action", "--uninstall", "--game-dir", "--language" })
                    r.Check("the usage names " + opt, ja.Contains(opt) && en.Contains(opt) && zh.Contains(opt));
                r.Check("and the exit codes are written down", ja.Contains("0") && ja.Contains("1") && ja.Contains("3"));
                // v0.4 review: the usage must not offer an option this build does not have (--source-dir is a
                // developer build's only), or the app would name a word and then refuse it
                foreach (var text in new[] { S.T("ja", "cl_usage"), S.T("en", "cl_usage"), S.T("zh-CN", "cl_usage") })
                    foreach (var word in text.Split(' ', '\r', '\n', '\t', '[', ']', '|'))
                        if (word.StartsWith("--", StringComparison.Ordinal))
                        {
                            // the exit-code line reads "2 --verify-download: the file did not match", so what follows
                            // an option name in a sentence is punctuation, not part of the name
                            string opt = word.TrimEnd(':', ',', '.', ';');
                            bool known = false;
                            foreach (var o in CommandLine.KnownOptions) if (o == opt) known = true;
                            r.Check("the usage only names options this build has: " + opt, known);
                        }
            });
        }

        // ------------------------------------------------------------------ --verify-download: the words alone
        // What the command means, before any file or address is involved. The one that matters most is A-5: this mode
        // must stay OUTSIDE the "one Client" rule for ever. The moment it could hand its work to a running Client, the
        // command would answer 0 having checked nothing - which is exactly what --uninstall did in v0.3 (PORT-MAP 15.1)
        // and the only reason anyone would run this command is to get one honest answer before a release.
        static void VerifyDownloadTests(SelfTestRunner r)
        {
            r.Section("--verify-download");

            r.Test("A-1: it is a mode of its own", () =>
            {
                var p = Plan("--verify-download");
                r.Check("the checking mode", p.Mode == StartupMode.VerifyDownload);
                r.Check("it never hands its work over", p.WhenRunning == Handoff.None);
                r.Check("no window is hidden or shown, and nothing is played", !p.HideWindow && !p.AutoLaunch && !p.Windowed && !p.TrayStart);
                r.Equal("it is not an --action", "", p.Action);
                r.Check("no task lock (the game folder is never touched, so nothing can be busy)", !p.NeedsTaskLock);
            });

            r.Test("A-2: the other spellings mean the same thing", () =>
            {
                foreach (var spelling in new[] { "--verifydownload", "-verify-download", "/verify-download", "-VerifyDownload", "--VERIFY-DOWNLOAD" })
                {
                    var p = Plan(spelling);
                    r.Check(spelling, p.Mode == StartupMode.VerifyDownload && p.WhenRunning == Handoff.None && !p.UsesSingleInstance);
                }
                r.Check("written twice it is still one check", Plan("--verify-download", "--verifydownload").Mode == StartupMode.VerifyDownload);
            });

            r.Test("A-3: --quiet leaves the box out", () =>
            {
                var p = Plan("--verify-download", "--quiet");
                r.Check("the mode is unchanged", p.Mode == StartupMode.VerifyDownload);
                r.Check("and the box is asked for silence", p.Quiet);
                r.Check("without it, the box is shown", !Plan("--verify-download").Quiet);
            });

            r.Test("A-4: two modes at once is a usage error", () =>
            {
                string head = S.T("ja", "cl_two_modes", "");
                foreach (var line in new[]
                {
                    new[] { "--verify-download", "--action", "install" },
                    new[] { "--verify-download", "--uninstall" },
                    new[] { "--verify-download", "--tray" },
                    new[] { "--verify-download", "--scan-only" },
                    new[] { "--verify-download", "--self-test", @"C:\t" },
                })
                {
                    var p = Plan(line);
                    r.Check(string.Join(" ", line) + " -> usage, exit 1", p.Mode == StartupMode.Usage && p.ExitCode == 1);
                    string text = Startup.UsageText("ja", p);
                    r.Check("... and it says they cannot be used together", text.StartsWith(head, StringComparison.Ordinal), text.Split('\r')[0]);
                    r.Check("... and it names --verify-download", text.Split('\r')[0].Contains("--verify-download"));
                }
            });

            // A-5: the v0.3 trap, nailed down. Nothing about this plan may depend on another Client being there.
            r.Test("A-5: a running Client cannot answer for this one", () =>
            {
                var p = Plan("--verify-download");
                r.Check("it is outside the one-Client rule (SPEC 5.1)", !p.UsesSingleInstance);
                r.Check("it sends nothing to a running Client", Startup.SignalFor(p.WhenRunning) == SignalKind.None);
                r.Equal("and a mode that signals nothing can never end with 0 there", 1, Startup.SecondStartExit(Handoff.None, true));
                r.Equal("... which is this mode's own answer too", 1, Startup.SecondStartExit(p.WhenRunning, true));
                // and with a Client really holding the mutex on another thread, the plan is the same plan: it is read
                // from the words and from nothing else
                var names = SingleInstance.Names.For("verify" + Guid.NewGuid().ToString("N").Substring(0, 6));
                using (var first = SingleInstance.Acquire(listen: true, names: names))
                {
                    r.Check("a Client is holding it", first.IsFirst);
                    r.Check("a second start knows it is not the first", !OnAnotherThread(() =>
                    {
                        using (var second = SingleInstance.Acquire(listen: true, names: names)) return second.IsFirst;
                    }));
                    var again = Plan("--verify-download");
                    r.Check("the plan did not change", again.Mode == StartupMode.VerifyDownload && again.WhenRunning == Handoff.None && !again.UsesSingleInstance);
                }
            });

            r.Test("A-6: it cannot be mixed with starting the game", () =>
            {
                foreach (var word in new[] { "--autolaunch", "--windowed" })
                {
                    var p = Plan("--verify-download", word);
                    r.Check("--verify-download " + word + " -> usage, exit 1", p.Mode == StartupMode.Usage && p.ExitCode == 1);
                    r.Check("... and it says why", Startup.UsageText("ja", p).StartsWith(S.T("ja", "cl_no_launch", word), StringComparison.Ordinal));
                }
            });

            r.Test("A-7: the usage text knows about it", () =>
            {
                foreach (var l in new[] { "ja", "zh-CN", "en" })
                {
                    string usage = S.T(l, "cl_usage");
                    r.Check("the usage names --verify-download (" + l + ")", usage.Contains("--verify-download"), usage);
                    r.Check("and the exit codes name 2 (" + l + ")", usage.Contains("2"), usage);
                }
                // the one line that tells the owner what 2 means, in each language
                r.Check("ja says what 2 is", S.T("ja", "cl_usage").Contains("2 --verify-download"));
                r.Check("zh-CN says what 2 is", S.T("zh-CN", "cl_usage").Contains("2 --verify-download"));
                r.Check("en says what 2 is", S.T("en", "cl_usage").Contains("2 --verify-download"));
            });

            // A-8: --quiet has to mean it. docs\BEPINEX-PIN.md step 9 tells anyone running this automatically to add
            // --quiet "so no window opens"; a box opened where nobody is there to press OK would hold the process open
            // until somebody noticed. The one place that was still open was the failure BEFORE the --verify-download
            // branch is even reached: ClientContext.Detect throwing (v0.5 review).
            r.Test("A-8: --quiet opens no box, not even when the app cannot start at all", () =>
            {
                var lines = new List<string>();
                var boxes = new List<string>();
                var trouble = new InvalidOperationException("nowhere to look");

                int quiet = Program.StartFailed(lines.Add, true, trouble, boxes.Add);
                r.Equal("it still ends with 1", 1, quiet);
                r.Equal("and NO window was opened", 0, boxes.Count);
                r.Check("the reason is in the log all the same",
                    lines.Exists(l => l.Contains("start failed") && l.Contains("nowhere to look")), string.Join(" | ", lines.ToArray()));

                lines.Clear();
                int loud = Program.StartFailed(lines.Add, false, trouble, boxes.Add);
                r.Equal("without --quiet it still ends with 1", 1, loud);
                r.Equal("and a person IS told, in a box", 1, boxes.Count);
                r.Check("which says what went wrong", boxes[0].Contains("nowhere to look"), boxes[0]);
                r.Check("and the log has it too", lines.Exists(l => l.Contains("start failed")), string.Join(" | ", lines.ToArray()));

                // and those words really do parse to "quiet" for this command
                r.Check("--verify-download --quiet is quiet", CommandLine.Parse(new[] { "--verify-download", "--quiet" }).Quiet);
                r.Check("--verify-download on its own is not", !CommandLine.Parse(new[] { "--verify-download" }).Quiet);
            });
        }

        // ------------------------------------------------------------------ the trap: a Client is already running
        static void TrapTests(SelfTestRunner r)
        {
            r.Section("a Client is already running");
            // What made "--uninstall" report success in v0.3: the second start signalled the first one and returned 0.
            // Every headless argument is checked against exactly that here.
            r.Test("which arguments may hand their work over at all", () =>
            {
                r.Check("--action never takes the mutex", !Plan("--action", "install").UsesSingleInstance && Startup.SignalFor(Plan("--action", "install").WhenRunning) == SignalKind.None);
                r.Check("--scan-only never takes it either", !Plan("--scan-only").UsesSingleInstance && Startup.SignalFor(Plan("--scan-only").WhenRunning) == SignalKind.None);
                r.Check("--self-test never comes near it", !Plan("--self-test", @"C:\t").UsesSingleInstance);
                r.Check("--tray sends NOTHING (the running window is not raised)", Startup.SignalFor(Plan("--tray").WhenRunning) == SignalKind.None);
                r.Check("--uninstall sends nothing and refuses", Startup.SignalFor(Plan("--uninstall").WhenRunning) == SignalKind.None && Plan("--uninstall").WhenRunning == Handoff.Refuse);
                r.Check("a plain start raises the window", Startup.SignalFor(Plan().WhenRunning) == SignalKind.Show);
                r.Check("--autolaunch asks it to play", Startup.SignalFor(Plan("--autolaunch").WhenRunning) == SignalKind.Play);
            });
            r.Test("the exit code of a second start", () =>
            {
                r.Equal("plain start: the window came up, 0", 0, Startup.SecondStartExit(Handoff.Show, true));
                r.Equal("--tray: it is already there, 0", 0, Startup.SecondStartExit(Handoff.AlreadyThere, false));
                r.Equal("--autolaunch delivered: 0", 0, Startup.SecondStartExit(Handoff.Play, true));
                r.Equal("--autolaunch NOT delivered: 1 (never a silent success)", 1, Startup.SecondStartExit(Handoff.Play, false));
                r.Equal("--uninstall: 1, it did not uninstall anything", 1, Startup.SecondStartExit(Handoff.Refuse, true));
                r.Equal("a mode that never signals cannot succeed here", 1, Startup.SecondStartExit(Handoff.None, true));
            });
        }

        // ------------------------------------------------------------------ the real kernel objects
        static void SignalTests(SelfTestRunner r)
        {
            r.Section("one Client");
            r.Test("a second start is told so, and can ask the first one to play", () =>
            {
                var names = SingleInstance.Names.For("play" + Guid.NewGuid().ToString("N").Substring(0, 6));
                using (var first = SingleInstance.Acquire(listen: true, names: names))
                using (var played = new ManualResetEventSlim(false))
                using (var playedWindowed = new ManualResetEventSlim(false))
                {
                    r.Check("the first one holds it", first.IsFirst);
                    r.Check("... and it is listening", first.Listening);
                    first.OnPlayRequested(windowed => { if (windowed) playedWindowed.Set(); else played.Set(); });
                    // a Windows mutex belongs to a THREAD, so a second start has to be asked on a thread of its own -
                    // asking again on this one would be the same owner saying yes to itself
                    r.Check("a second one knows it is not the first", !OnAnotherThread(() =>
                    {
                        using (var second = SingleInstance.Acquire(listen: true, names: names)) return second.IsFirst;
                    }));

                    r.Check("the play signal is delivered", SingleInstance.SignalPlay(false, names));
                    r.Check("... and the first one really got it", played.Wait(5000));
                    r.Check("--windowed arrives as its own signal", SingleInstance.SignalPlay(true, names) && playedWindowed.Wait(5000));
                }
                // and once the first one has gone, the name is free again: the next start is a normal first start
                using (var again = SingleInstance.Acquire(listen: true, names: names)) r.Check("after it quit, the next start IS the first", again.IsFirst);
            });
            r.Test("nobody there: the play signal fails, it does not pretend", () =>
            {
                var names = SingleInstance.Names.For("none" + Guid.NewGuid().ToString("N").Substring(0, 6));
                r.Check("no Client running -> not delivered", !SingleInstance.SignalPlay(false, names));
                r.Equal("... so --autolaunch would end with 1", 1, Startup.SecondStartExit(Handoff.Play, false));
            });
            // v0.4 review: the uninstall holds the same mutex. It must not also open the three events, or a start with
            // --autolaunch would be told "delivered" while nothing is listening and nothing is ever played.
            r.Test("a process that will not answer opens no events (the uninstall)", () =>
            {
                var names = SingleInstance.Names.For("quiet" + Guid.NewGuid().ToString("N").Substring(0, 6));
                using (var busy = SingleInstance.Acquire(listen: false, names: names))
                {
                    r.Check("it still holds the one-Client mutex", busy.IsFirst);
                    r.Check("... but it is not listening", !busy.Listening);
                    r.Check("a second start is told it is not the first", !OnAnotherThread(() =>
                    {
                        using (var second = SingleInstance.Acquire(listen: true, names: names)) return second.IsFirst;
                    }));
                    r.Check("--autolaunch cannot be delivered to it", !SingleInstance.SignalPlay(false, names));
                    r.Check("... nor a plain start's 'show the window'", !SingleInstance.SignalFirst(names));
                    r.Equal("so --autolaunch ends with 1, not 0", 1, Startup.SecondStartExit(Handoff.Play, false));
                }
            });
            r.Test("the task lock (SPEC 5.1)", () =>
            {
                string name = @"Local\StarPocketGames.Client.test.task." + Guid.NewGuid().ToString("N").Substring(0, 6);
                using (var held = TaskLock.TryTake(name))
                {
                    r.Check("the first one takes it", held != null);
                    r.Check("the second one is refused (busy)", OnAnotherThread(() =>
                    {
                        using (var again = TaskLock.TryTake(name)) return again == null;
                    }));
                }
                r.Check("it is free again afterwards", OnAnotherThread(() =>
                {
                    using (var after = TaskLock.TryTake(name)) return after != null;
                }));
            });
        }

        // ------------------------------------------------------------------ --action
        sealed class Recorder
        {
            public readonly List<string> Out = new List<string>();
            public readonly List<string> Order = new List<string>();
            public readonly List<string> Locks = new List<string>();
            public bool LockFree = true;

            public ConsoleOut Console => ConsoleOut.To(new Writer(Out));

            sealed class Writer : StringWriterLike
            {
                public Writer(List<string> sink) : base(sink) { }
            }
        }

        /// <summary>A TextWriter that only remembers the lines (no file, no console).</summary>
        class StringWriterLike : TextWriter
        {
            readonly List<string> sink;
            public StringWriterLike(List<string> sink) { this.sink = sink; }
            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
            public override void WriteLine(string value) { sink.Add(value ?? ""); }
            public override void Write(string value) { sink.Add(value ?? ""); }
        }

        static Headless NewHeadless(Recorder rec, string lang = "ja")
        {
            return new Headless
            {
                Out = rec.Console,
                Log = s => rec.Order.Add("log"),
                Lang = () => lang,
                ModdedDir = @"C:\fake\Among Us PocketRoles",
                SteamDir = null,
                Housekeep = () => rec.Order.Add("housekeep"),
                TakeLock = name => { rec.Locks.Add(name); return rec.LockFree ? TaskLock.TryTake(name + ".test." + Guid.NewGuid().ToString("N").Substring(0, 6)) : null; },
                Install = p => { rec.Order.Add("install"); return TaskOutcome.Good("installed"); },
                Check = p => { rec.Order.Add("check"); return TaskOutcome.Good("checked"); },
                Report = () => { rec.Order.Add("report"); return new ReportResult { Ok = true, Name = "PocketRoles-report-20260923-1200.zip", Records = 2 }; },
                ComputeStatus = () => { rec.Order.Add("status"); return LaunchStatus.Compute(false, new InstallInfo(), null, null, false, false, false); },
            };
        }

        static void ActionTests(SelfTestRunner r)
        {
            r.Section("--action");
            r.Test("the four jobs", () =>
            {
                foreach (var a in Startup.Actions)
                {
                    var rec = new Recorder();
                    int code = NewHeadless(rec).RunAction(a);
                    r.Equal("--action " + a + " ends with 0", 0, code);
                    r.Check("... and it really ran " + a, rec.Order.Contains(a));
                    r.Check("... after the 30-day clean-up and the log copy", rec.Order.IndexOf("housekeep") < rec.Order.IndexOf(a));
                    r.Check("... and it said something", rec.Out.Count > 3);
                }
            });
            r.Test("a job that fails ends with 1", () =>
            {
                var rec = new Recorder();
                var h = NewHeadless(rec);
                h.Install = p => TaskOutcome.Bad("no room on the disk");
                r.Equal("install", 1, h.RunAction("install"));
                r.Check("and it says why", rec.Out.Any(l => l.Contains("no room on the disk")));
                var rec2 = new Recorder();
                var h2 = NewHeadless(rec2);
                h2.Report = () => new ReportResult { Ok = false, Error = "nothing to report" };
                r.Equal("report", 1, h2.RunAction("report"));
                var rec3 = new Recorder();
                var h3 = NewHeadless(rec3);
                h3.Install = p => { throw new InvalidOperationException("boom"); };
                r.Equal("a job that throws is a failure, not a crash", 1, h3.RunAction("install"));
            });
            r.Test("the busy rule (SPEC 5.1)", () =>
            {
                var rec = new Recorder { LockFree = false };
                var h = NewHeadless(rec);
                r.Equal("install while the app is copying: 1", 1, h.RunAction("install"));
                r.Check("... it says 'busy' in the viewer's language", rec.Out.Any(l => l == S.T("ja", "cl_busy")));
                r.Check("... and it did NOT install anything", !rec.Order.Contains("install"));
                r.Equal("check is refused the same way", 1, NewHeadless(new Recorder { LockFree = false }).RunAction("check"));
                var reading = new Recorder { LockFree = false };
                r.Equal("status only reads: it runs anyway", 0, NewHeadless(reading).RunAction("status"));
                r.Equal("and it never asked for the lock", 0, reading.Locks.Count);
                var rep = new Recorder { LockFree = false };
                r.Equal("report only reads: it runs anyway", 0, NewHeadless(rep).RunAction("report"));
                r.Equal("and it never asked for the lock either", 0, rep.Locks.Count);
                var locked = new Recorder();
                NewHeadless(locked).RunAction("install");
                r.Check("install asks for THE task lock of SPEC 5.1", locked.Locks.Contains(AppInfo.TaskMutexName));
            });
            r.Test("an action nobody knows, handed straight to the runner", () =>
            {
                var rec = new Recorder();
                r.Equal("exit 1", 1, NewHeadless(rec).RunAction("frobnicate"));
                r.Check("and it says which four there are", rec.Out.Any(l => l.Contains("install")));
            });
            r.Test("status prints what the window would show", () =>
            {
                var info = new InstallInfo { Exe = true, GameVer = "2026.8.18", Bep = true, BepVer = "6.0.0-be.735", BepOk = true, Dll = true, DllVer = "0.5.5", Interop = true };
                var st = LaunchStatus.Compute(false, info, "2026.8.18", null, true, false, false);
                var lines = Headless.StatusLines("ja", st, @"D:\Steam\Among Us", false);
                r.Equal("eight lines", 8, lines.Count);
                r.Check("the Steam copy and where it is", lines[0].Contains("2026.8.18") && lines[0].Contains(@"D:\Steam\Among Us"));
                r.Check("BepInEx with its OK", lines[2].Contains("6.0.0-be.735") && lines[2].Contains(S.T("ja", "v_ok")));
                r.Check("Steam is running", lines[5].Contains(S.T("ja", "v_running")));
                r.Check("Among Us is not (and that row does not say 'start Steam first')", lines[6].Contains(S.T("ja", "v_notrunning")) && !lines[6].Contains(S.T("ja", "v_stopped")));
                r.Check("the play button's state and its line", lines[7].Contains("ready") && lines[7].Contains(S.T("ja", "al_ok")));
                var none = Headless.StatusLines("en", LaunchStatus.Compute(false, new InstallInfo(), null, null, false, false, false), null, false);
                r.Check("nothing installed, no Steam", none[0].Contains("not found") && none[1].Contains("none"));
            });
        }

        // ------------------------------------------------------------------ --scan-only
        sealed class FakeView : IScanView
        {
            public readonly List<string> Calls = new List<string>();
            public List<ScanRowView> Last = new List<ScanRowView>();
            public string Summary = "";
            public int Serious, Warnings;
            public bool PreLaunch, Closed;

            public void Begin(string lang, bool preLaunch) { PreLaunch = preLaunch; Calls.Add("begin"); }
            public void Update(IList<ScanRowView> rows, string summary) { Last = new List<ScanRowView>(rows); Summary = summary; Calls.Add("update"); }
            public void Done(int serious, int warnings, string summary) { Serious = serious; Warnings = warnings; Summary = summary; Calls.Add("done"); }
            public void CloseNow() { Closed = true; Calls.Add("close"); }
        }

        /// <summary>A PC with no cheat tool, no secure boot key and no registry at all.</summary>
        sealed class EmptyPc : IAegisSystem
        {
            public IRegKey OpenHklm(string path) => null;
            public string[] ProcessNames() => new[] { "explorer", "steam" };
        }

        static void ScanOnlyTests(SelfTestRunner r)
        {
            r.Section("--scan-only");
            r.Test("a quiet PC: every row printed, exit 0", () =>
            {
                string root = r.NewDir("scanonly");
                string game = Path.Combine(root, "game"), state = Path.Combine(root, "state");
                Directory.CreateDirectory(state);
                SelfTestRunner.Touch(Path.Combine(game, "Among Us.exe"));
                SelfTestRunner.Touch(Path.Combine(game, "winhttp.dll"));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\core\BepInEx.Core.dll"));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\PocketRoles.dll"));
                var rec = new Recorder();
                var view = new FakeView();
                var h = NewHeadless(rec);
                int code = h.RunScanOnly(game, state, null, view, new EmptyPc());
                r.Equal("exit 0 (nothing stops a start)", 0, code);
                r.Equal("13 rows on the card", 13, view.Last.Count);
                r.Check("the card was opened, filled and finished", view.Calls.First() == "begin" && view.Calls.Last() == "done");
                r.Check("it is not a pre-launch scan", !view.PreLaunch);
                r.Check("every row is on the console", view.Last.All(row => rec.Out.Any(l => l.Contains(row.Title))));
                r.Check("and the last line is the summary", rec.Out.Any(l => l == AegisText.Get("ja", "done") || l.StartsWith(AegisText.Get("ja", "done.warn", 1).Substring(0, 4))));
                r.Check("it wrote no prelaunch-result.txt (it decides nothing)", !File.Exists(Path.Combine(state, "prelaunch-result.txt")));
                r.Check("and no events.log line", !File.Exists(Path.Combine(state, "events.log")));
            });
            r.Test("a plugin that does not belong: exit 3", () =>
            {
                string root = r.NewDir("scanonly-red");
                string game = Path.Combine(root, "game"), state = Path.Combine(root, "state");
                Directory.CreateDirectory(state);
                SelfTestRunner.Touch(Path.Combine(game, "Among Us.exe"));
                SelfTestRunner.Touch(Path.Combine(game, "winhttp.dll"));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\core\BepInEx.Core.dll"));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\PocketRoles.dll"));
                SelfTestRunner.Touch(Path.Combine(game, @"BepInEx\plugins\SomeoneElse.dll"));
                var rec = new Recorder();
                var view = new FakeView();
                int code = NewHeadless(rec).RunScanOnly(game, state, null, view, new EmptyPc());
                r.Equal("exit 3, the same 3 the pre-launch scan uses", 3, code);
                r.Check("the card was told how many", view.Serious >= 1);
                r.Check("the console says which plugin", rec.Out.Any(l => l.Contains("SomeoneElse.dll")));
                r.Check("and it marks the row as one that stops a start", rec.Out.Any(l => l.StartsWith(S.T("ja", "scan_stop"))));
            });
            r.Test("it takes no mutex: it works while the app is open", () =>
            {
                // Aegis.ps1 -ScanOnly ran into the tray's own mutex and the process ended having done nothing at all.
                string root = r.NewDir("scanonly-mutex");
                string game = Path.Combine(root, "game"), state = Path.Combine(root, "state");
                Directory.CreateDirectory(state);
                using (var aegisHeld = new Mutex(true, @"Local\StarPocketGames.Client.test.aegis." + Guid.NewGuid().ToString("N").Substring(0, 6)))
                {
                    var rec = new Recorder();
                    var view = new FakeView();
                    int code = NewHeadless(rec).RunScanOnly(game, state, null, view, new EmptyPc());
                    r.Check("it still ran all 13 rows", view.Last.Count == 13);
                    r.Check("and it still said what it found", code == 0 || code == 3);
                }
            });
            r.Test("no card at all: the scan still runs and still prints", () =>
            {
                string root = r.NewDir("scanonly-nocard");
                var rec = new Recorder();
                int code = NewHeadless(rec).RunScanOnly(Path.Combine(root, "game"), Path.Combine(root, "state"), null, null, new EmptyPc());
                r.Check("an exit code all the same", code == 0 || code == 3);
                r.Check("and the rows on the console", rec.Out.Count >= 13);
            });
        }

        // ------------------------------------------------------------------ the scan card (no window opened)
        static void ScanCardTests(SelfTestRunner r)
        {
            r.Section("scan card");
            var rows = new List<ScanRowView>();
            for (int i = 0; i < 13; i++) rows.Add(new ScanRowView { Title = "row " + i, Detail = "detail " + i, State = 2 });
            r.Test("its height follows the rows", () =>
            {
                int plain = ScanPopup.CardHeight(rows);
                r.Check("13 rows fit", plain > 13 * 20 && plain < 600);
                var withFix = new List<ScanRowView>(rows);
                withFix[3] = new ScanRowView { Title = "mod", Detail = "changed", Fix = "install it again", State = 4 };
                r.Check("a row that stops a start carries its fix under it", ScanPopup.CardHeight(withFix) > plain);
                r.Equal("nothing at all is still a card", ScanPopup.CardHeight(new List<ScanRowView>()), ScanPopup.CardHeight(null));
                r.Equal("v1.1: a card that stays is one line taller (its \"click to close\" line)", plain + 20, ScanPopup.CardHeight(rows, true));
                using (var bmp = ScanPopup.Draw(rows, AegisText.Get("ja", "found", 1), "ja", 1f, true, 1, 1, AegisText.Get("ja", "card.close")))
                    r.Check("v1.1: the card that stays is drawn with its last line", bmp.Width == 428 && bmp.Height == ScanPopup.CardHeight(rows, true) + 28);
            });
            r.Test("its colour says how the scan went", () =>
            {
                r.Check("waiting is grey", ScanPopup.StateColor(0) == ScanPopup.Grey);
                r.Check("running is the brand's teal", ScanPopup.StateColor(1) == ScanPopup.Teal1);
                r.Check("OK is green", ScanPopup.StateColor(2) == AegisToast.Green1);
                r.Check("a warning is amber", ScanPopup.StateColor(3) == AegisToast.Amber1);
                r.Check("one that stops a start is red", ScanPopup.StateColor(4) == ScanPopup.Red1);
            });
            r.Test("the picture, drawn with no window", () =>
            {
                foreach (var c in new[]
                {
                    new { done = false, serious = 0, warn = 0, want = ScanPopup.Teal1, what = "while it scans: teal" },
                    new { done = true, serious = 0, warn = 0, want = AegisToast.Green1, what = "all clear: green" },
                    new { done = true, serious = 0, warn = 2, want = AegisToast.Amber1, what = "warnings: amber" },
                    new { done = true, serious = 1, warn = 1, want = ScanPopup.Red1, what = "it stops a start: red" },
                })
                    using (var bmp = ScanPopup.Draw(rows, "…", "ja", 1f, c.done, c.serious, c.warn))
                    {
                        r.Check(c.what + " (size)", bmp.Width == 428 && bmp.Height == ScanPopup.CardHeight(rows) + 28);
                        var band = bmp.GetPixel(15, ScanPopup.CardHeight(rows) / 2 + 14);   // the colour band at the left edge
                        r.Check(c.what, band.R == c.want.R && band.G == c.want.G && band.B == c.want.B, band.ToString());
                        r.Check(c.what + " (the card is opaque in the middle)", bmp.GetPixel(bmp.Width / 2, bmp.Height / 2).A > 200);
                        r.Check(c.what + " (and see-through at the very edge)", bmp.GetPixel(0, 0).A == 0);
                    }
            });
            r.Test("it draws in every language and at every scale", () =>
            {
                foreach (var lang in new[] { "ja", "zh-CN", "en" })
                    foreach (var k in new[] { 1f, 1.5f, 2f })
                        using (var bmp = ScanPopup.Draw(rows, AegisText.Get(lang, "scanning", 7, 13), lang, k, false, 0, 0))
                            r.Check(lang + " at " + k + "x", bmp.Width == (int)Math.Round(428 * k) && bmp.Height > 100);
            });
            r.Test("how long the finished card stays (the ps1's own waits)", () =>
            {
                r.Check("a pre-launch scan that stopped a start stays longest", ScanPopup.LingerBlockedMs == 9000);
                r.Check("one that lets the game start goes at once", ScanPopup.LingerGoMs == 900);
                r.Check("warnings stay longer than a quiet scan", ScanPopup.LingerWarnMs > ScanPopup.LingerQuietMs);
                // v1.1: the start scan's card (KeepWhenSerious) - a red row keeps it until it is clicked; a quiet one goes as before
                r.Check("v1.1: a red row on the start scan's card: it stays until clicked", double.IsPositiveInfinity(ScanPopup.LingerFor(false, 1, 1, true)));
                r.Check("... the same red row on the tray's \"scan again\": the ps1's 9 s", ScanPopup.LingerFor(false, 1, 1, false) == ScanPopup.LingerBlockedMs);
                r.Check("... a quiet start scan goes as before (1.8 s)", ScanPopup.LingerFor(false, 0, 0, true) == ScanPopup.LingerQuietMs);
                r.Check("... warnings only: 4.2 s", ScanPopup.LingerFor(false, 0, 2, true) == ScanPopup.LingerWarnMs);
                r.Check("... a pre-launch scan keeps its own waits", ScanPopup.LingerFor(true, 0, 0, false) == ScanPopup.LingerGoMs && ScanPopup.LingerFor(true, 1, 0, false) == ScanPopup.LingerBlockedMs);
            });
            r.Test("the toasts make room for it", () =>
            {
                int before = AegisToast.ReservedBottom;
                AegisToast.ReservedBottom = 200;
                r.Equal("the card's height is kept free", 200, AegisToast.ReservedBottom);
                AegisToast.ReservedBottom = 0;
                r.Equal("and given back when it closes", 0, AegisToast.ReservedBottom);
                AegisToast.ReservedBottom = before;
            });
        }

        // ------------------------------------------------------------------ the window around playing, the 15 s rule
        static void WindowTests(SelfTestRunner r)
        {
            r.Section("window and tray");
            r.Test("SPEC 5.4: the window steps aside for the game", () =>
            {
                r.Check("a window on screen steps aside", ClientApp.StepAsideForGame(true));
                r.Check("one that is already away is left alone", !ClientApp.StepAsideForGame(false));
            });
            r.Test("the tray's 15-second rule (Aegis.ps1)", () =>
            {
                r.Check("started with --tray, a game has been and gone 16 s ago: it ends", ClientApp.TrayAutoQuit(true, false, true, false, 16, false));
                r.Check("at 15 s it is still there", !ClientApp.TrayAutoQuit(true, false, true, false, 15, false));
                r.Check("no game has run yet: it waits for ever", !ClientApp.TrayAutoQuit(true, false, false, false, 9999, false));
                r.Check("the game is still running: it stays", !ClientApp.TrayAutoQuit(true, false, true, true, 0, false));
                r.Check("the person opened the window: it is their app now", !ClientApp.TrayAutoQuit(true, true, true, false, 9999, false));
                r.Check("something is still being copied: it waits", !ClientApp.TrayAutoQuit(true, false, true, false, 9999, true));
                r.Check("a normal start never ends by itself", !ClientApp.TrayAutoQuit(false, false, true, false, 9999, false));
            });
            r.Test("the tray's play button is unchanged", () =>
            {
                var notInstalled = LaunchStatus.Compute(false, new InstallInfo(), null, null, true, false, false);
                r.Check("nothing installed: not playable", !ClientApp.TrayPlayable(false, false, false, false, notInstalled));
                r.Check("... but plain Among Us is", ClientApp.TrayPlayable(false, false, true, false, notInstalled));
                r.Check("during a game: no", !ClientApp.TrayPlayable(false, true, true, false, notInstalled));
                r.Check("during a long task: no", !ClientApp.TrayPlayable(true, false, true, false, notInstalled));
            });
        }
    }
}
