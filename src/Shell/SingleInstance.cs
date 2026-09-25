// One StarPocket Client per Windows session (PORT-MAP 4.1, SPEC 5.1): the named mutex Local\StarPocketGames.Client.
// A second start lets the first one take the foreground (AllowSetForegroundWindow), signals the named event
// Local\StarPocketGames.Client.Show and exits; the first one shows its window and brings it to the front.
// v0.4 adds two more events, .Play and .PlayWindowed: a second start with --autolaunch asks the running Client to play
// instead of raising its window (SPEC 6.4). Two events rather than a message channel - "play" and "play in a window" is
// everything that has to cross.
// The first one lets go of all of them as soon as it starts quitting (ReleaseEarly), before the slow part (WebView2's
// clean-up): a start in that moment is a normal start, not a signal to a window that is going away.
// Names another Client made "as administrator" cannot be opened (access denied): that counts as "another Client is
// running" - never a crash.
//
// WHAT A SIGNAL IS NOT: every Signal* here says only whether the OTHER process was TOLD. It never means the work was
// done. Only a mode whose whole purpose is "let the running Client do it" may end with 0 on a delivered signal
// (a plain start: show the window; --autolaunch: play). --uninstall, --action and --scan-only never signal at all -
// that is exactly how "--uninstall" once reported success while removing nothing (PORT-MAP 15.1).
// --self-test never comes here.
//
// AND THE OTHER HALF OF THAT RULE (v0.4 review): a process that will not LISTEN must not open the three events either.
// The uninstall takes this same mutex, so for those seconds it IS "the first one"; if it made the events as well, a
// start with --autolaunch would find one, set it, be told "delivered" and end with 0 - while nobody was waiting on the
// other side and nothing was ever played. That is the same silent success wearing different clothes. So Acquire is
// told whether this process means to answer (true for the app itself, false for the uninstall), and a process that
// does not answer never opens a mouth to be spoken into.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;

namespace Starpocket.Client.Shell
{
    internal sealed class SingleInstance : IDisposable
    {
        /// <summary>The four kernel names. The app uses <see cref="Default"/>; the self-test its own set, so it can run
        /// a first and a second instance against each other without touching the real Client's names.</summary>
        internal sealed class Names
        {
            public string Mutex = AppInfo.InstanceMutexName;
            public string Show = AppInfo.ShowEventName;
            public string Play = AppInfo.PlayEventName;
            public string PlayWindowed = AppInfo.PlayWindowedEventName;

            public static Names Default => new Names();

            /// <summary>A set of names of its own (the self-test).</summary>
            public static Names For(string suffix) => new Names
            {
                Mutex = @"Local\StarPocketGames.Client.test." + suffix,
                Show = @"Local\StarPocketGames.Client.test." + suffix + ".Show",
                Play = @"Local\StarPocketGames.Client.test." + suffix + ".Play",
                PlayWindowed = @"Local\StarPocketGames.Client.test." + suffix + ".PlayWindowed",
            };
        }

        readonly Names names;
        readonly bool listens;
        Mutex mutex;
        EventWaitHandle showEvent, playEvent, playWindowedEvent;
        RegisteredWaitHandle showWait, playWait, playWindowedWait;
        public bool IsFirst { get; private set; }

        /// <summary>True when this process made the three events, i.e. when a second start can reach it at all.</summary>
        public bool Listening => IsFirst && listens;

        SingleInstance(Names n, bool listen) { names = n; listens = listen; }

        /// <param name="listen">Will this process answer a second start (the app: yes; the uninstall: no)? A process
        /// that says no takes the mutex but opens no events, so a second start is told "not delivered" instead of
        /// being told "delivered" into a silence.</param>
        public static SingleInstance Acquire(bool listen, Names names = null)
        {
            var si = new SingleInstance(names ?? Names.Default, listen);
            try
            {
                bool created;
                si.mutex = new Mutex(true, si.names.Mutex, out created);
                if (created) si.IsFirst = true;
                else
                {
                    try { si.IsFirst = si.mutex.WaitOne(0); }
                    catch (AbandonedMutexException) { si.IsFirst = true; }   // the last one crashed: this one is first now
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is WaitHandleCannotBeOpenedException || ex is IOException)
            {
                // a Client started "as administrator" holds the name: another Client is running
                if (si.mutex != null) { si.mutex.Dispose(); si.mutex = null; }
                si.IsFirst = false;
                return si;
            }
            if (si.IsFirst && listen)
            {
                si.showEvent = si.MakeEvent(si.names.Show);
                si.playEvent = si.MakeEvent(si.names.Play);
                si.playWindowedEvent = si.MakeEvent(si.names.PlayWindowed);
            }
            return si;
        }

        EventWaitHandle MakeEvent(string name)
        {
            try { return new EventWaitHandle(false, EventResetMode.AutoReset, name); }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is WaitHandleCannotBeOpenedException || ex is IOException)
            {
                return null;   // still the first one; a second start just cannot reach this one
            }
        }

        /// <summary>First instance: <paramref name="onShow"/> runs (on a pool thread) each time another start asks.</summary>
        public void OnShowRequested(Action onShow)
        {
            showWait = Register(showEvent, () => onShow());
        }

        /// <summary>First instance: another start with --autolaunch asks it to play (the bool is --windowed).</summary>
        public void OnPlayRequested(Action<bool> onPlay)
        {
            playWait = Register(playEvent, () => onPlay(false));
            playWindowedWait = Register(playWindowedEvent, () => onPlay(true));
        }

        static RegisteredWaitHandle Register(EventWaitHandle handle, Action body)
        {
            if (handle == null) return null;
            return ThreadPool.RegisterWaitForSingleObject(handle, (s, timedOut) => { try { body(); } catch (Exception) { } }, null, Timeout.Infinite, false);
        }

        /// <summary>Second instance: ask the first one to come to the front (it may still be starting: up to 3 s).
        /// False when it could not be reached (an administrator's Client, or the first one already gone).</summary>
        public static bool SignalFirst(Names names = null)
        {
            try { Native.AllowSetForegroundWindow(Native.ASFW_ANY); } catch (Exception) { }
            return Signal((names ?? Names.Default).Show);
        }

        /// <summary>Second instance with --autolaunch: ask the running Client to play. False when it could not be
        /// reached - and then the caller must SAY so and end with an error, never pretend the game was started.</summary>
        public static bool SignalPlay(bool windowed, Names names = null)
        {
            var n = names ?? Names.Default;
            return Signal(windowed ? n.PlayWindowed : n.Play);
        }

        static bool Signal(string name)
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    using (var ev = EventWaitHandle.OpenExisting(name)) { ev.Set(); return true; }
                }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
                catch (Exception) { return false; }
            }
            return false;
        }

        /// <summary>The first instance is quitting: stop listening and let go of the mutex now (call on the thread that took
        /// it - the app's main thread). Dispose does the rest.</summary>
        public void ReleaseEarly()
        {
            Unregister(ref showWait);
            Unregister(ref playWait);
            Unregister(ref playWindowedWait);
            Close(ref showEvent);
            Close(ref playEvent);
            Close(ref playWindowedEvent);
            if (mutex != null)
            {
                if (IsFirst) { try { mutex.ReleaseMutex(); } catch (Exception) { } }
                mutex.Dispose();
                mutex = null;
            }
        }

        static void Unregister(ref RegisteredWaitHandle w)
        {
            if (w == null) return;
            try { w.Unregister(null); } catch (Exception) { }
            w = null;
        }

        static void Close(ref EventWaitHandle e)
        {
            if (e == null) return;
            e.Dispose();
            e = null;
        }

        public void Dispose() => ReleaseEarly();
    }
}
