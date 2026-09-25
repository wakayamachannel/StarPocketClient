// launcher-state.json - the same file, the same keys and the same merge rule as the PowerShell launcher's Load-State /
// Update-State (ps1:569-584), so an existing user keeps their settings and records (PORT-MAP 13.1).
//
// Where it is (v0.2):
//   - the app runs from the old launcher's folder (PocketRolesLauncher.ps1 or launcher-state.json next to the exe), or
//     from the source folder in developer mode: <Src>\launcher-state.json, shared with the launcher as today.
//   - otherwise: %LOCALAPPDATA%\StarPocket\Client\launcher-state.json (the app's own), and the FIRST time it is missing
//     the old launcher's file is taken over: the folder of the Desktop shortcut "PocketRoles Launcher.lnk" (its target
//     .cmd), or the old default folders. Every key is copied as it is, plus migratedFrom / migratedAt. The old file is
//     never changed or deleted: the old launcher keeps working until the viewer removes it themselves.
// Keys the app reads: steamDir, lang, lastBuiltGameVersion, copiedGameVersion, installedVersion, modSource, bepinex,
// gameVersion, lastCheck. Unknown keys are kept as they are (a newer launcher's state survives).
// Written UTF-8 without a BOM through a temp file (PowerShell reads both); nothing else in the folder is touched.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Starpocket.Client.Core
{
    internal sealed class LauncherStateFile
    {
        readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);

        public string Path { get; private set; }
        /// <summary>The file existed when it was read.</summary>
        public bool Found { get; private set; }
        /// <summary>The file IS there but could not be read this time (a sharing violation from antivirus, OneDrive or a
        /// backup; a half-written file; text that is not JSON). Nothing is written over it while this is true, and it is
        /// never taken for "there is no state file yet" - that is what would silently throw the viewer's settings away
        /// and put the old launcher's back (v0.3 review).</summary>
        public bool Unreadable { get; private set; }
        /// <summary>Where the values were taken over from (null when nothing was migrated).</summary>
        public string MigratedFrom { get; private set; }

        public Action<string> Log = _ => { };

        // ---- the keys the app uses
        public string SteamDir => Str("steamDir");
        public string Lang => Str("lang");
        public string LastBuiltGameVersion => Str("lastBuiltGameVersion");
        public string CopiedGameVersion => Str("copiedGameVersion");
        public string InstalledVersion => Str("installedVersion");
        public string ModSource => Str("modSource");
        public string GameVersion => Str("gameVersion");
        public string BepInEx => Str("bepinex");

        public string Str(string key)
        {
            object v;
            return values.TryGetValue(key, out v) ? v as string : null;
        }

        public IEnumerable<KeyValuePair<string, object>> All => values;

        public static LauncherStateFile Load(string path)
        {
            var s = new LauncherStateFile { Path = path };
            s.Read();
            return s;
        }

        /// <summary>Values with no file behind them (Path stays null, so nothing is ever written): the self-tests, and
        /// anywhere that needs an empty state before <see cref="ClientContext.LoadState"/> has run.</summary>
        public static LauncherStateFile InMemory(params string[] keysAndValues)
        {
            var s = new LauncherStateFile();
            for (int i = 0; i + 1 < keysAndValues.Length; i += 2) s.values[keysAndValues[i]] = keysAndValues[i + 1];
            return s;
        }

        void Read()
        {
            values.Clear();
            Found = false;
            Unreadable = false;
            bool there = false;
            try
            {
                if (string.IsNullOrEmpty(Path)) return;
                there = File.Exists(Path);
                if (!there) return;
                var obj = Json.TryParseObject(Json.ReadUtf8File(Path));
                if (obj == null) { Unreadable = true; Log("launcher-state.json: there but not readable as JSON: " + Path); return; }
                Found = true;
                foreach (var kv in obj) values[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                if (there) Unreadable = true;
                Log("launcher-state.json: " + ex.Message);
            }
        }

        /// <summary>The name of the lock the PowerShell launcher, the tray and this app share for the log folder is
        /// already in AppInfo; this is the same idea for the state file, so a read-merge-write here cannot cross a
        /// Set-Content of the launcher running at the same time.</summary>
        public const string MutexName = @"Local\PocketRolesLauncher.state";
        public int LockWaitMs = 4000;

        /// <summary>Update-State: read what is on disk now, put <paramref name="changes"/> on top, write the whole file
        /// (so a change made by the old launcher meanwhile is not lost). Never throws.</summary>
        public bool Update(IDictionary<string, object> changes)
        {
            Mutex gate = null;
            bool held = false;
            try
            {
                try
                {
                    gate = new Mutex(false, MutexName);
                    try { held = gate.WaitOne(LockWaitMs); }
                    catch (AbandonedMutexException) { held = true; }   // the other side died holding it: it is ours now
                }
                catch (Exception) { gate = null; }   // no lock to be had (a policy, a sandbox): write anyway, like the ps1
                var onDisk = Load(Path);
                // the file is there but could not be read this time: writing now would throw away whatever is in it
                if (onDisk.Unreadable) { Log("launcher-state.json: not written (the file could not be read this time)"); return false; }
                foreach (var kv in onDisk.values) values[kv.Key] = kv.Value;
                if (changes != null) foreach (var kv in changes) values[kv.Key] = kv.Value;
                return Write();
            }
            catch (Exception ex) { Log("launcher-state.json: " + ex.Message); return false; }
            finally
            {
                if (gate != null)
                {
                    if (held) { try { gate.ReleaseMutex(); } catch (Exception) { } }
                    gate.Dispose();
                }
            }
        }

        public bool Set(string key, object value) => Update(new Dictionary<string, object> { [key] = value });

        bool Write()
        {
            string tmp = null;
            try
            {
                if (string.IsNullOrEmpty(Path)) return false;
                string dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                tmp = Path + ".tmp";
                File.WriteAllText(tmp, Json.Serialize(values), new UTF8Encoding(false));
                if (File.Exists(Path)) File.Replace(tmp, Path, null);
                else File.Move(tmp, Path);
                tmp = null;
                Found = true;
                Unreadable = false;
                return true;
            }
            catch (Exception ex) { Log("launcher-state.json: " + ex.Message); return false; }
            finally { if (tmp != null) { try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { } } }   // no orphan .tmp left behind
        }

        /// <summary>The time string the launcher writes (Get-Date 'yyyy-MM-dd HH:mm:ss').</summary>
        public static string Stamp(DateTime now) => now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>Takes over an old launcher's state file once, when this one has none yet. Returns true when it did.
        /// "None yet" means the file is really not there: a file that could not be read this time is left alone, or the
        /// viewer's own copiedGameVersion / installedVersion / steamDir would be replaced by the old launcher's and an
        /// out-of-date game copy could be taken for a current one (v0.3 review).</summary>
        public bool MigrateFrom(string oldPath, DateTime now)
        {
            if (Found || Unreadable || string.IsNullOrEmpty(oldPath)) return false;
            if (!string.IsNullOrEmpty(Path) && File.Exists(Path)) return false;
            var old = Load(oldPath);
            if (!old.Found) return false;
            foreach (var kv in old.values) values[kv.Key] = kv.Value;
            values["migratedFrom"] = oldPath;
            values["migratedAt"] = Stamp(now);
            if (!Write()) return false;
            MigratedFrom = oldPath;
            Log("launcher-state.json: settings and records taken over from " + oldPath);
            return true;
        }
    }

    /// <summary>Finding the PowerShell launcher's folder, to take its launcher-state.json over (PORT-MAP 13.1).</summary>
    internal static class LegacyLauncher
    {
        public const string ShortcutName = "PocketRoles Launcher.lnk";
        public const string ScriptName = "PocketRolesLauncher.ps1";

        /// <summary>A folder holds the old launcher when its PocketRolesLauncher.ps1 or launcher-state.json is there.</summary>
        public static bool IsLauncherFolder(string dir) =>
            GameFolders.PathExists(GameFolders.Join(dir, ScriptName)) || GameFolders.PathExists(GameFolders.Join(dir, AppInfo.StateFileName));

        /// <summary>The old launcher's launcher-state.json, or null: the folder the Desktop shortcut points into, then the
        /// folders the Setup zip is usually unpacked into (&lt;Desktop&gt;\PocketRoles, &lt;Desktop&gt;\PocketRoles-Setup).
        /// <paramref name="readShortcut"/> is ShellLink.TargetPath (the self-test gives its own).</summary>
        public static string FindStateFile(string desktop, Func<string, string> readShortcut)
        {
            foreach (var dir in Folders(desktop, readShortcut))
            {
                string state = GameFolders.Join(dir, AppInfo.StateFileName);
                if (GameFolders.PathExists(state)) return state;
            }
            return null;
        }

        public static IEnumerable<string> Folders(string desktop, Func<string, string> readShortcut)
        {
            if (!string.IsNullOrEmpty(desktop))
            {
                string lnk = GameFolders.Join(desktop, ShortcutName);
                if (readShortcut != null && GameFolders.PathExists(lnk))
                {
                    string target = null;
                    try { target = readShortcut(lnk); } catch (Exception) { }
                    if (!string.IsNullOrEmpty(target))
                    {
                        string dir = GameFolders.Parent(target);
                        if (!string.IsNullOrEmpty(dir)) yield return dir;
                    }
                }
                foreach (var name in new[] { "PocketRoles", "PocketRoles-Setup", "PocketRoles Launcher" })
                    yield return GameFolders.Join(desktop, name);
            }
        }
    }
}
