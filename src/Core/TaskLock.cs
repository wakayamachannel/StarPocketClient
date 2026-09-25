// "Somebody is already copying into that folder" (SPEC 5.1): the named mutex Local\StarPocketGames.Client.Task.
// The open app holds it while it runs a long task (install, Steam sync, update check, report zip, uninstall), and
// "StarPocket Client.exe" --action install / check takes it before it starts. Whichever is second is told "busy" and
// stops; the two never unpack into the same game copy at once.
// A Windows mutex belongs to the thread that took it, so TryTake and Dispose must happen on the same thread: the app
// takes and releases it on its UI thread, the headless job on the thread that runs it.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;

namespace Starpocket.Client.Core
{
    internal sealed class TaskLock : IDisposable
    {
        Mutex mutex;

        TaskLock(Mutex m) { mutex = m; }

        /// <summary>Takes it, or null when somebody else holds it. A name that cannot be opened at all (another Client
        /// started "as administrator") counts as held: never a crash, never a silent "go ahead".</summary>
        public static TaskLock TryTake(string name)
        {
            Mutex m = null;
            try
            {
                bool created;
                m = new Mutex(true, name, out created);
                bool mine = created;
                if (!mine)
                {
                    try { mine = m.WaitOne(0); }
                    catch (AbandonedMutexException) { mine = true; }   // its holder ended without letting go
                }
                if (!mine) { m.Dispose(); return null; }
                return new TaskLock(m);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is WaitHandleCannotBeOpenedException || ex is IOException)
            {
                if (m != null) m.Dispose();
                return null;
            }
        }

        public void Dispose()
        {
            if (mutex == null) return;
            try { mutex.ReleaseMutex(); } catch (Exception) { }
            mutex.Dispose();
            mutex = null;
        }
    }
}
