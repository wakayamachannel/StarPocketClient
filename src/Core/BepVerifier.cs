// "--verify-download": the one thing about the BepInEx pin that no test can answer.
//
// The self-test (src\SelfTest\InstallSelfTests.cs "BepInEx: the file itself") checks every path with small zips it
// made itself: how a fingerprint is written, that a file which does not match is deleted and never unpacked, that the
// file checked is the file unpacked, that a cached file is not trusted. All of that is about OUR code.
//
// What nobody has seen is the other half: does the real builds.bepinex.dev, TODAY, hand back the 31 MB whose SHA-256
// is written down in src\AppInfo.cs, and can the real code unpack that real file? If that is wrong by one character,
// every install stops at [3/5] after a release, and the screen only shows something that looks like a network failure
// (the real reason lands in client.log's "ファイルの照合:" line alone). So it is checked once, by hand, before a
// release - and through THIS app's own code, not by hand with Get-FileHash, which proves nothing about the app.
//
// What this must never do, and what the self-test fixes (InstallSelfTests "verify download" B-10, B-11):
//   - it does not name, read or write the game folder. ClientContext.Paths / ModPaths never reach this file, so there
//     is no folder to pick wrongly - the owner plays in one of those folders and a mistake there costs 1 GB.
//   - it does not call Installer.Expand: that writes .starpocket-unpacking INTO the game folder. VerifiedZip.ExpandOver
//     is called directly instead.
//   - it does not use %TEMP%\PocketRolesLauncher (ClientContext.CacheDir). That folder is shared with the PowerShell
//     launcher, and a file left there would be picked up by a later install. This leaves nothing anywhere.
//   - it does not clear up after a check that is HAPPENING. This mode takes no mutex of any kind, so the owner
//     double-clicking the .cmd twice starts two of these - and the marker alone said "ours, delete it" of the folder
//     the first one was filling. The second one deleted the marker, failed on the half-received file it could not
//     delete, and left 150 MB nobody would ever clear. The folder now also holds a .lock held open with
//     FileShare.None: a folder whose lock cannot be opened is in use, and the second run says so and stops
//     (v0.5 review).
//   - it writes no launcher-state.json, touches no Steam, no registry, no shortcut, and takes no task mutex: the
//     command works while the Client is open, while an install runs and while the game is running.
// Everything is a field, like Installer: the self-test hands it a fake IWebFetch and runs every path without a single
// byte leaving the PC and without a window. The little window is opened by Program, never from here.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Starpocket.Client.Core
{
    /// <summary>How it went. The exit code groups these into three (<see cref="VerifyResult.ExitCode"/>): checked (0),
    /// our side could not tell (1), or the file the official site served really was not ours (2).
    ///
    /// <para>The failures that end in 1 are told apart on purpose (v0.5 review). They used to be one: everything that
    /// was not a mismatch answered "the official site could not be reached, so no file came down" - which was a lie
    /// when the drive had filled up, when the file had come down and could not be read, and, worst of all, when the
    /// official site had pointed the app at a host it does not fetch from. That last one is a thing this command
    /// exists to catch, and it was being read as "your internet is playing up, try again".</para></summary>
    internal enum VerifyOutcome
    {
        Ok,
        /// <summary>This build has no fingerprint written down for its BepInEx: nothing is fetched at all.</summary>
        NoPin,
        /// <summary>Nothing came down from any address.</summary>
        NoFile,
        /// <summary>Something came down and this PC could not read it (before any fingerprint could be compared).</summary>
        NoRead,
        /// <summary>The drive ran out of room, while receiving or while unpacking.</summary>
        NoRoom,
        /// <summary>The official site pointed at an address the app does not fetch from. Nothing came down, and this
        /// one is worth stopping a release over even though nobody's bytes were wrong.</summary>
        Refused,
        Mismatch,
        BadZip,
        ExpandFailed,
        NoWorkFolder,
        /// <summary>Another --verify-download is running and owns the work folder. Not a failure of anything: wait.</summary>
        Busy,
    }

    internal sealed class VerifyResult
    {
        public VerifyOutcome Outcome;
        /// <summary>The finished sentence for the little window and the console, in the viewer's language.</summary>
        public string Text = "";
        public int Files;
        public long Bytes;
        public string Sha, Want, Url, LeftFolder;
        /// <summary>Why no work folder could be made, in the viewer's own language (<c>vd_ng_temp</c>'s hole). Never a
        /// message written in English here: the little window is the one thing the owner has to be able to read.</summary>
        public string Error;
        /// <summary>The system's own words for an unpacking that stopped after the fingerprint had matched
        /// (<c>vd_ng_expand</c>'s hole). Kept apart from everything else so that a LATER address failing in some
        /// unrelated way cannot put its words inside this sentence (v0.5 review).</summary>
        public string ExpandError;

        /// <summary>0 checked / 1 could not be checked / 2 IT CAME DOWN AND IT WAS NOT OURS. The two failures are
        /// different events: 1 is our side (no pin, no connection, no room), 2 stops a release.</summary>
        public int ExitCode =>
            Outcome == VerifyOutcome.Ok ? 0 :
            Outcome == VerifyOutcome.Mismatch || Outcome == VerifyOutcome.BadZip ? 2 : 1;
    }

    /// <summary>Another <c>--verify-download</c> holds the work folder right now. Its own type so that the sentence
    /// the owner reads is chosen from the string table, never taken from an exception's message.</summary>
    internal sealed class VerifyBusyException : Exception
    {
        public readonly string Folder;
        public VerifyBusyException(string folder) : base(folder + " is in use by another --verify-download") { Folder = folder; }
    }

    /// <summary>Both names the command may use are held by folders that are not ours. Same reason for the type: the
    /// message below is for client.log, and the window gets <c>vd_temp_taken</c> instead (v0.5 review).</summary>
    internal sealed class WorkFolderTakenException : Exception
    {
        public readonly string Folder;
        public WorkFolderTakenException(string folder) : base(folder + " is there already and was not made by this command") { Folder = folder; }
    }

    internal sealed class BepVerifier
    {
        /// <summary>The self-test hands it a fake: no call ever leaves the PC.</summary>
        public IWebFetch Web = WebFetch.Instance;
        public Func<string, string> BepSha256 = AppInfo.BepSha256;
        public Func<string> Lang = () => "ja";
        /// <summary>client.log.</summary>
        public Action<string> Log = _ => { };
        /// <summary>The console the command was typed into.</summary>
        public Action<string> Say = _ => { };
        /// <summary>%TEMP%. The work folder is made under it and deleted again.</summary>
        public string TempRoot;
        /// <summary>The full path named by <c>vd_seelog</c> when something went wrong.</summary>
        public string LogPath;
        public Func<bool> Cancelled = () => false;

        /// <summary>%TEMP%\StarPocket-verify. Deliberately NOT ClientContext.CacheDir.</summary>
        internal const string WorkFolderName = "StarPocket-verify";
        /// <summary>Inside the work folder: this is how a later run knows the folder is one of ours to delete.</summary>
        internal const string MarkerName = ".starpocket-verify";
        /// <summary>Inside the work folder as well, and held open with FileShare.None for as long as the run lasts.
        /// The marker alone said "ours, delete it", which is also true of the folder a run that is HAPPENING owns - so
        /// a second --verify-download deleted the first one's half-received 31 MB out from under it and then failed
        /// itself on the .part it could not delete (v0.5 review). This file answers "and is it in use right now".
        /// It is opened DeleteOnClose, so a run that is killed leaves no lock behind, only the folder - which the next
        /// run then clears as its own.</summary>
        internal const string LockName = ".starpocket-verify.lock";
        internal const string MarkerText = "\"StarPocket Client.exe\" --verify-download が作った一時フォルダーです。消して構いません\r\n";

        string T(string key, params object[] args) => S.T(Lang(), key, args);

        void Both(string line) { Log(line); Say(line); }

        public VerifyResult Run()
        {
            var res = new VerifyResult();
            // 1. do we even know what the right file looks like? A version with no pin fetches NOTHING and makes no
            //    folder - the same refusal the installer gives (Installer.StepBepInEx).
            string want = null;
            try { want = BepSha256(AppInfo.BepInExVersion); } catch (Exception) { want = null; }
            res.Want = want;
            if (string.IsNullOrEmpty(want))
            {
                res.Outcome = VerifyOutcome.NoPin;
                return Done(res);
            }

            Both(T("vd_start", AppInfo.BepInExVersion));

            WorkFolder work = null;
            try { work = PrepareWorkFolder(TempRoot); }
            catch (VerifyBusyException ex)
            {
                // the owner double-clicked twice. Nothing is wrong and nothing of the running check is touched.
                Log(ex.Message);
                res.Outcome = VerifyOutcome.Busy;
                return Done(res);
            }
            catch (WorkFolderTakenException ex)
            {
                Log(ex.Message);
                res.Error = T("vd_temp_taken", ex.Folder);   // the window's words, not the exception's
            }
            catch (Exception ex) { res.Error = ex.Message; Log(T("err", ex.Message)); }
            if (work == null)
            {
                res.Outcome = VerifyOutcome.NoWorkFolder;
                return Done(res);
            }
            // a run that was killed part-way leaves its 150 MB behind and says nothing. The next run clears it, and
            // now says so, so that "everything is deleted afterwards" can be checked rather than believed.
            if (!string.IsNullOrEmpty(work.Cleaned)) Log(T("vd_cleaned", work.Cleaned));
            Both(T("vd_where", work.Path));

            try { Fetch(res, work.Path, want); }
            finally
            {
                // whatever happened, including an exception nobody expected: the 150 MB goes again. The lock is let
                // go first, or the folder holding it could not be deleted at all.
                work.Dispose();
                res.LeftFolder = RemoveWorkFolder(work.Path);
            }
            return Done(res);
        }

        /// <summary>The one sentence goes into client.log and into the result; Program writes it to the console and
        /// shows it. Nothing else is ever shown - the owner is not sent to a log to find out how it went.</summary>
        VerifyResult Done(VerifyResult res)
        {
            Log(Reason(res));
            if (!string.IsNullOrEmpty(res.LeftFolder)) Log(T("vd_left", res.LeftFolder));
            res.Text = Compose(res);
            return res;
        }

        // ------------------------------------------------------------------ the addresses, in the install's own order
        void Fetch(VerifyResult res, string work, string want)
        {
            string zip = Path.Combine(work, AppInfo.BepZipName);
            string unpacked = Path.Combine(work, "out");
            var urls = Installer.BepAddresses(Web, u => Log(T("in_dl_refused", u)));
            var worst = VerifyOutcome.NoFile;
            foreach (var url in urls)
            {
                if (Cancelled()) break;
                res.Url = url;
                Both(T("in_dl", url));
                try
                {
                    var tick = new Ticker();
                    Web.Download(url, zip, (done, total) => Tick(tick, done, total));
                }
                catch (Exception ex)
                {
                    // nothing came down from this address: try the next one, and keep "could not connect" as the
                    // answer unless a later address does hand us something. WHY it did not come down is kept apart,
                    // because two of the reasons are not "could not connect" at all (v0.5 review).
                    Log(T("err", ex.Message));
                    DeleteQuiet(zip);
                    var why = ex is AddressRefusedException ? VerifyOutcome.Refused
                            : OutOfRoom(ex) ? VerifyOutcome.NoRoom
                            : VerifyOutcome.NoFile;
                    if (Weight(why) > Weight(worst)) worst = why;
                    // the next address would fetch the same 31 MB onto the same full drive
                    if (why == VerifyOutcome.NoRoom) break;
                    continue;
                }
                var got = Check(res, zip, unpacked, want, url);
                DeleteQuiet(zip);
                if (got == VerifyOutcome.Ok) { res.Outcome = VerifyOutcome.Ok; return; }
                if (Weight(got) > Weight(worst)) worst = got;
                try { if (Directory.Exists(unpacked)) Directory.Delete(unpacked, true); } catch (Exception) { }
                // same reason as above: the next address means another 31 MB onto a drive that is already full
                if (got == VerifyOutcome.NoRoom) break;
            }
            res.Outcome = worst;
        }

        /// <summary>One address's file: the fingerprint FIRST (before the file is so much as parsed as an archive),
        /// then the entry, then the unpacking - the same three questions in the same order as the install
        /// (Installer.CheckAndExpandBep), on the same single open handle that was never let go.</summary>
        VerifyOutcome Check(VerifyResult res, string zip, string unpacked, string want, string url)
        {
            long bytes = 0;
            try { bytes = new FileInfo(zip).Length; } catch (Exception) { }
            res.Bytes = bytes;
            bool matched = false;
            try
            {
                using (var vz = VerifiedZip.Open(zip))
                {
                    Both(T("vd_got", bytes, Path.GetFileName(zip)));
                    string got = vz.Sha256;
                    res.Sha = got;
                    // written down every time, matching or not: this line IS the evidence the check really ran
                    Log(T("in_hash_detail", want, got));
                    if (!FileHash.Same(got, want))
                    {
                        Log(T("in_hash_bad", url));
                        return VerifyOutcome.Mismatch;
                    }
                    if (!vz.Has(Installer.BepCoreEntry))
                    {
                        Log(T("in_zip_bad", url, "BepInEx.Core.dll"));
                        return VerifyOutcome.BadZip;
                    }
                    matched = true;
                    Both(T("in_hash_ok", AppInfo.BepInExVersion));
                    Say(T("in_extract", Path.GetFileName(zip)));
                    // the count as it goes: 110 MB and several hundred files take a good few seconds, and a console
                    // that says nothing at all in that time looks like a program that has hung (SPEC 4.2 step 5-3)
                    res.Files = vz.ExpandOver(unpacked, new string[0], n => { if (n % 100 == 0) Say(T("vd_prog_files", n)); });
                    Both(T("in_extract_done", res.Files));
                    return VerifyOutcome.Ok;
                }
            }
            catch (Exception ex)
            {
                Log(T("err", ex.Message));
                // a full drive is a full drive, whichever half it happened in, and "check your connection" would be
                // the wrong thing to tell anyone about it
                if (OutOfRoom(ex)) return VerifyOutcome.NoRoom;
                // after the fingerprint matched, an exception is this PC's own trouble with the 110 MB (a scanner
                // holding a file, say): its words go in the sentence - and ONLY for this outcome, so that a later
                // address failing for some unrelated reason cannot end up quoted inside it (v0.5 review).
                if (matched) { res.ExpandError = ex.Message; return VerifyOutcome.ExpandFailed; }
                // before it matched: the bytes arrived and this PC could not read them. That is not "no connection",
                // which is what the owner used to be told, and it is not the official site's fault either.
                return VerifyOutcome.NoRead;
            }
        }

        /// <summary>Did the drive run out of room? Asked of the codes Windows gives, not of the message, so it is the
        /// same answer in every language the system speaks (ERROR_DISK_FULL / ERROR_HANDLE_DISK_FULL).</summary>
        internal static bool OutOfRoom(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (!(e is IOException)) continue;
                if (e.HResult == unchecked((int)0x80070070) || e.HResult == unchecked((int)0x80070027)) return true;
            }
            return false;
        }

        /// <summary>Which of several failures is the one to report. "It came down and it was not ours" beats anything
        /// our own side could not manage, so a release is still stopped when a later address merely failed to answer -
        /// and "the official site sent us somewhere else" beats every ordinary failure for the same reason.</summary>
        static int Weight(VerifyOutcome o)
        {
            switch (o)
            {
                case VerifyOutcome.Mismatch: return 7;
                case VerifyOutcome.BadZip: return 6;
                case VerifyOutcome.Refused: return 5;
                case VerifyOutcome.ExpandFailed: return 4;
                case VerifyOutcome.NoRoom: return 3;
                case VerifyOutcome.NoRead: return 2;
                default: return 1;   // NoFile
            }
        }

        // ------------------------------------------------------------------ the sentence the owner reads
        string Compose(VerifyResult res)
        {
            var sb = new StringBuilder();
            sb.Append(Reason(res));
            // Busy is not a failure: another check is running and this one stood aside. Asking for client.log there
            // would send the owner looking for a problem that does not exist.
            if (res.Outcome != VerifyOutcome.Ok && res.Outcome != VerifyOutcome.Busy)
                sb.Append("\r\n\r\n").Append(T("vd_seelog", LogPath ?? ""));
            if (!string.IsNullOrEmpty(res.LeftFolder)) sb.Append("\r\n\r\n").Append(T("vd_left", res.LeftFolder));
            return sb.ToString();
        }

        /// <summary>The one sentence for an outcome. Internal so the self-test can ask it of EVERY outcome there is
        /// (B-14): a new one added later must not quietly fall through to "the official site could not be reached".</summary>
        internal string Reason(VerifyResult res)
        {
            switch (res.Outcome)
            {
                case VerifyOutcome.Ok: return T("vd_ok", AppInfo.BepInExVersion, res.Files);
                case VerifyOutcome.NoPin: return T("in_bep_nohash", AppInfo.BepInExVersion);
                case VerifyOutcome.Mismatch: return T("vd_ng_hash");
                case VerifyOutcome.BadZip: return T("vd_ng_zip", "BepInEx.Core.dll");
                case VerifyOutcome.ExpandFailed: return T("vd_ng_expand", res.ExpandError ?? "");
                case VerifyOutcome.NoWorkFolder: return T("vd_ng_temp", res.Error ?? "");
                case VerifyOutcome.Busy: return T("vd_busy");
                case VerifyOutcome.NoRoom: return T("vd_ng_space");
                case VerifyOutcome.NoRead: return T("vd_ng_read");
                case VerifyOutcome.Refused: return T("vd_ng_refused");
                default: return T("vd_ng_net");
            }
        }

        // ------------------------------------------------------------------ "receiving..."
        sealed class Ticker { public long Step = -1; }

        /// <summary>A line only when it would say something new (every 10%, or every 5 MB when the server did not say
        /// how big the file is). 31 MB arrives in 65 KB pieces, and a line each would be 500 lines of nothing.</summary>
        void Tick(Ticker t, long done, long total)
        {
            if (total > 0)
            {
                long pct = done * 100 / total;
                if (pct / 10 == t.Step) return;
                t.Step = pct / 10;
                Say(T("vd_prog", pct.ToString(CultureInfo.InvariantCulture)));
                return;
            }
            long mb = done / (1024 * 1024);
            if (mb / 5 == t.Step) return;
            t.Step = mb / 5;
            Say(T("vd_prog_mb", mb.ToString(CultureInfo.InvariantCulture)));
        }

        static void DeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }

        // ------------------------------------------------------------------ the work folder
        /// <summary>The work folder this run owns. While it is not disposed, the .lock inside it is held open with
        /// FileShare.None, which is how a second --verify-download can tell "a folder a run of ours left behind" from
        /// "a folder a run of ours is using right now" (v0.5 review).</summary>
        internal sealed class WorkFolder : IDisposable
        {
            public readonly string Path;
            /// <summary>The folder of ours a killed run had left behind and this one cleared, or null.</summary>
            public readonly string Cleaned;
            FileStream hold;

            internal WorkFolder(string path, FileStream hold, string cleaned) { Path = path; this.hold = hold; Cleaned = cleaned; }

            /// <summary>Lets the lock go (and with DeleteOnClose, removes it). Must happen before the folder itself is
            /// deleted. Doing it twice is harmless.</summary>
            public void Dispose()
            {
                var h = hold;
                hold = null;
                if (h != null) { try { h.Dispose(); } catch (Exception) { } }
            }
        }

        /// <summary>What is sitting at a work-folder name.</summary>
        enum Slot
        {
            /// <summary>Nothing is there.</summary>
            Free,
            /// <summary>A folder of ours that nothing is using: an earlier run's leftovers, ours to delete.</summary>
            Stale,
            /// <summary>A folder of ours that another --verify-download is using this moment.</summary>
            Busy,
            /// <summary>Anything else. Never touched.</summary>
            Theirs,
        }

        /// <summary>A fresh %TEMP%\StarPocket-verify with the marker and the lock inside. The rule is the self-test's
        /// own (SelfTestRunner.PrepareWork) with one thing added: a folder of that name WITH our marker was ours and
        /// is deleted - unless its lock is held, which means a check is running in it and NOTHING may be deleted.
        /// A folder without our marker belongs to somebody else and is left completely alone, and we step aside to
        /// StarPocket-verify-&lt;process number&gt;.</summary>
        /// <exception cref="VerifyBusyException">another --verify-download owns the folder.</exception>
        /// <exception cref="WorkFolderTakenException">neither name is usable.</exception>
        internal static WorkFolder PrepareWorkFolder(string tempRoot)
        {
            string root = string.IsNullOrEmpty(tempRoot) ? Path.GetTempPath() : tempRoot;
            string work = Path.Combine(root, WorkFolderName);
            var slot = Look(work);
            if (slot == Slot.Busy) throw new VerifyBusyException(work);
            if (slot == Slot.Theirs)
            {
                // our own process number: no other RUNNING process can have it, so Busy here would be a lock nobody
                // holds any more, which Look already reads as Stale
                work = work + "-" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
                slot = Look(work);
                if (slot == Slot.Busy) throw new VerifyBusyException(work);
                if (slot == Slot.Theirs) throw new WorkFolderTakenException(work);
            }
            string cleaned = null;
            if (slot == Slot.Stale) { Directory.Delete(work, true); cleaned = work; }
            return Make(work, cleaned);
        }

        /// <summary>The folder, then the lock, then the marker - and if either of the last two fails, the folder this
        /// call made goes again. It used to be left behind with no marker in it, which made every later run treat it
        /// as somebody else's and step around it for ever (v0.5 review).</summary>
        internal static WorkFolder Make(string work, string cleaned)
        {
            Directory.CreateDirectory(work);
            string lockPath = Path.Combine(work, LockName);
            FileStream hold;
            try { hold = new FileStream(lockPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024, FileOptions.DeleteOnClose); }
            catch (Exception) when (Held(lockPath))
            {
                // between Look and this line, another --verify-download claimed the very same name. Its folder is
                // its own: not one file of it is touched, and nothing of ours is deleted here.
                throw new VerifyBusyException(work);
            }
            catch (Exception)
            {
                RemoveWorkFolder(work);
                throw;
            }
            try { File.WriteAllText(Path.Combine(work, MarkerName), MarkerText, new UTF8Encoding(false)); }
            catch (Exception)
            {
                // the folder this call made must not be left standing without its marker: every later run would then
                // read it as somebody else's, step around it for ever, and never clear it (v0.5 review)
                try { hold.Dispose(); } catch (Exception) { }
                RemoveWorkFolder(work);
                throw;
            }
            return new WorkFolder(work, hold, cleaned);
        }

        /// <summary>What is at this name, without touching anything.</summary>
        static Slot Look(string work)
        {
            if (File.Exists(work)) return Slot.Theirs;
            if (!Directory.Exists(work)) return Slot.Free;
            if (!File.Exists(Path.Combine(work, MarkerName))) return Slot.Theirs;
            return Held(Path.Combine(work, LockName)) ? Slot.Busy : Slot.Stale;
        }

        /// <summary>Is another process holding this lock open? A lock file that is not there at all is not held: the
        /// run that made it is over (DeleteOnClose removes it even when the process was killed), or the folder was
        /// left by a build from before there were locks.</summary>
        static bool Held(string lockPath)
        {
            if (!File.Exists(lockPath)) return false;
            try
            {
                using (new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                return false;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        /// <summary>Deletes the work folder, trying three times 200 ms apart (a virus scanner reading the 110 MB we
        /// just wrote is the usual reason a first try fails). Returns null when it is gone, else the folder that is
        /// still there - which is said in the message but never changes the exit code: the check itself is finished.</summary>
        internal static string RemoveWorkFolder(string work)
        {
            for (int i = 0; i < 3; i++)
            {
                try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch (Exception) { }
                if (!Directory.Exists(work)) return null;
                if (i < 2) { try { Thread.Sleep(200); } catch (Exception) { } }
            }
            return work;
        }
    }
}
