// MOD 用のコピーを新しい場所へ移す（2026-10-01、CopyPlace.cs の続き）。
//
// 同じドライブなら名前を変えるだけ（一瞬・途中の状態が無い）。別のドライブなら:
//   1. 新しい場所を作り、まず目印（CopyPlace.MarkerName）を置く。途中で止まっても「これは移動の残り」と分かるように。
//   2. 全部の写しを作る（進み具合を知らせる）。
//   3. 数と大きさが元と同じか確かめ、目印を外す。
//   4. ここで呼んだ側が設定を書き換える。**書き換えが済んでから**元を消す（DeleteOld）。元のコピーのゲームが動いていたら
//      消さない（OldInUse。写している間に起動された時は、写すこと自体を Cancelled でやめる。公開前レビュー 2026-10-01）。
// どこで失敗しても、元のコピーと設定は前のまま残る（作りかけの新しい場所は、目印付きの自分で作った物だけ消す）。
// 人のファイルには触らない: 新しい場所に中身があって目印が無ければ、何もせずに断る（CopyPlace.Check でも断っている）。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starpocket.Client.Core
{
    internal sealed class MoveResult
    {
        public bool Ok;
        /// <summary>だめだった時の Strings.cs の鍵（cp_*）と、その時の例外の文（ログ用）。</summary>
        public string ErrorKey, Error;
        /// <summary>名前を変えただけ（同じドライブ）。false なら写しを作った（元は DeleteOld で消す）。</summary>
        public bool Renamed;
        /// <summary>元にコピーが無かった（まだインストールしていない）。場所の設定だけ変える。</summary>
        public bool NothingToMove;
        public int Files;
        public long Bytes;
    }

    internal sealed class CopyMover
    {
        public Action<TaskProgress> Progress = _ => { };
        public Action<string> Log = _ => { };
        /// <summary>アプリが終わろうとしている。写しの途中で見る。</summary>
        public Func<bool> Cancelled = () => false;
        /// <summary>同じドライブか。自己診断が「別のドライブ」の道を通すために差し替える。</summary>
        public Func<string, string, bool> SameVolume = CopyPlace.SameRoot;

        /// <summary>公開前レビュー 2026-10-01: 元のコピーのゲームが動いているか（ClientApp は Processes.ModdedGameRunning）。
        /// 動いていれば DeleteOld は消さない（動いているゲームのフォルダを途中まで消さない）。自己診断が差し替える。</summary>
        public Func<string, bool> OldInUse = _ => false;

        const int ERROR_NOT_SAME_DEVICE = 17;

        public MoveResult Move(string from, string to)
        {
            var r = new MoveResult();
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return Fail(r, "cp_bad", "no path");
            if (!Directory.Exists(from)) { r.Ok = true; r.NothingToMove = true; return r; }
            try
            {
                Directory.CreateDirectory(GameFolders.Parent(to));
                if (!ClearTarget(to, r)) return r;
                // 中身を一度歩く: つなぎ（ジャンクション・シンボリックリンク）は辿らない。中身がどこか別の所にある物を写したり消したりしない
                var src = new DirectoryInfo(from);
                var dirs = new List<DirectoryInfo>();
                var files = new List<FileInfo>();
                bool link = false;
                Walk(src, dirs, files, ref link);
                // 2026-10-03（公開前の粗探し 9）: 新しい場所が深すぎて、ファイルの場所が Windows の上限（FileCopy.MaxPath、259 文字）を超える時は、
                // 何もする前に断る。それまでは途中で IOException になり「ファイルが使われていて」（cp_locked）と違う文を出していた
                int longest = LongestAt(to, src.FullName, dirs, files);
                if (longest > FileCopy.MaxPath) return Fail(r, "cp_toolong", "the longest path would be " + longest + " characters (limit " + FileCopy.MaxPath + ")");
                // 2026-10-03（崩す係 7）: 元のコピーに前の移動の目印が混ざっていると、名前の変更で目印ごと新しい場所へ動く。その直後（設定を書く前）に
                // 落ちると、次の起動がその唯一のコピーを「作りかけ」とみなして消す。名前を変える前に元の目印を消す。消せなければ写す道へ（写す道は目印を写さない）
                if (SameVolume(from, to) && RemoveStrayMarker(from))
                {
                    try
                    {
                        Directory.Move(from, to);
                        r.Ok = true; r.Renamed = true;
                        Log("move copy: renamed " + from + " -> " + to);
                        Progress(new TaskProgress { Task = "moveCopy", Value = 1 });
                        return r;
                    }
                    catch (IOException ex) when ((ex.HResult & 0xFFFF) == ERROR_NOT_SAME_DEVICE)
                    {
                        // 同じ文字のドライブに見えて、実は別のボリューム（フォルダにつないだドライブ等）: 写す方に回す
                        Log("move copy: not the same volume after all, copying instead");
                    }
                }
                if (link) return Fail(r, "cp_link", from);   // 写す道だけ: 名前を変えるだけなら、つなぎもそのまま一緒に動く
                return CopyThenVerify(from, to, r, src, dirs, files);
            }
            catch (UnauthorizedAccessException ex) { return Fail(r, "cp_write", ex.Message); }
            catch (IOException ex) { return Fail(r, "cp_locked", ex.Message); }
            catch (Exception ex) { return Fail(r, "cp_failed", ex.Message); }
        }

        /// <summary>元のコピーの中に残っていた目印（CopyPlace.MarkerName）を消す。無かった・消せたら true、消せなければ false（ログに残す）。</summary>
        bool RemoveStrayMarker(string from)
        {
            string stray = GameFolders.Join(from, CopyPlace.MarkerName);
            try
            {
                if (!File.Exists(stray)) return true;
                File.Delete(stray);
                Log("move copy: a stray marker inside the copy was removed before the rename");
                return true;
            }
            catch (Exception ex)
            {
                Log("move copy: a stray marker inside the copy could not be removed (" + ex.Message + "); copying instead of renaming");
                return false;
            }
        }

        /// <summary>新しい場所が空か、移動の残りなら片付ける。人の物が入っていたら断る（false）。</summary>
        bool ClearTarget(string to, MoveResult r)
        {
            if (!Directory.Exists(to)) return true;
            if (File.Exists(GameFolders.Join(to, CopyPlace.MarkerName)))
            {
                Log("move copy: removing what a stopped move left at " + to);
                DeleteLeftover(to);
                return true;
            }
            if (Directory.EnumerateFileSystemEntries(to).Any()) { Fail(r, "cp_exists", to); return false; }
            Directory.Delete(to, false);   // 空のフォルダ（Directory.Move は行き先があると失敗する）
            return true;
        }

        MoveResult CopyThenVerify(string from, string to, MoveResult r, DirectoryInfo src, List<DirectoryInfo> dirs, List<FileInfo> files)
        {
            long total = files.Sum(f => f.Length);
            bool created = false;
            try
            {
                Directory.CreateDirectory(to);
                created = true;
                File.WriteAllText(GameFolders.Join(to, CopyPlace.MarkerName), "StarPocket Client: a move of the mod copy that has not finished. Safe to delete.");
                foreach (var d in dirs) Directory.CreateDirectory(GameFolders.Join(to, Rel(src.FullName, d.FullName)));
                long done = 0;
                var last = DateTime.MinValue;
                foreach (var f in files)
                {
                    if (Cancelled()) throw new OperationCanceledException();
                    string dst = GameFolders.Join(to, Rel(src.FullName, f.FullName));
                    File.Copy(f.FullName, dst, false);
                    done += f.Length;
                    r.Files++;
                    if ((DateTime.UtcNow - last).TotalMilliseconds >= 200)
                    {
                        last = DateTime.UtcNow;
                        Progress(new TaskProgress { Task = "moveCopy", Bytes = done, Total = total, Value = total > 0 ? (double)done / total : 1 });
                    }
                }
                // 数と大きさが元と同じか（目印は数えない）
                var copied = new DirectoryInfo(to).EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(x => !string.Equals(x.Name, CopyPlace.MarkerName, StringComparison.OrdinalIgnoreCase)).ToList();
                long copiedBytes = copied.Sum(x => x.Length);
                if (copied.Count != files.Count || copiedBytes != total)
                    throw new IOException("the copy does not match: " + copied.Count + "/" + files.Count + " files, " + copiedBytes + "/" + total + " bytes");
                File.Delete(GameFolders.Join(to, CopyPlace.MarkerName));
                r.Ok = true; r.Bytes = total;
                Log("move copy: copied " + files.Count + " file(s), " + total + " bytes, " + from + " -> " + to);
                Progress(new TaskProgress { Task = "moveCopy", Bytes = total, Total = total, Value = 1 });
                return r;
            }
            catch (Exception ex)
            {
                // 作りかけは、自分で作った物（目印付き）だけ消す。元は触っていない
                if (created) { try { Uninstaller.DeleteTree(to); } catch (Exception e2) { Log("move copy: could not remove the half-made copy: " + e2.Message); } }
                r.Files = 0;
                if (ex is OperationCanceledException) return Fail(r, "cp_failed", "cancelled");
                if (ex is UnauthorizedAccessException) return Fail(r, "cp_write", ex.Message);
                if (ex is IOException && IsDiskFull(ex)) return Fail(r, "cp_space", ex.Message);
                return Fail(r, ex is IOException ? "cp_locked" : "cp_failed", ex.Message);
            }
        }

        /// <summary>設定を書き換えられなかった時に、移す前へ戻す。名前を変えただけなら戻し、写しなら写しを消す。</summary>
        public bool Undo(MoveResult r, string from, string to)
        {
            if (r == null || !r.Ok || r.NothingToMove) return true;
            try
            {
                if (r.Renamed) Directory.Move(to, from);
                else Uninstaller.DeleteTree(to);
                Log("move copy: undone");
                return true;
            }
            catch (Exception ex) { Log("move copy: could not undo: " + ex.Message); return false; }
        }

        /// <summary>
        /// 公開前レビュー（2 回目）2026-10-01: <see cref="Undo"/> が失敗した時に出す文の鍵（{0} は新しい場所）。
        ///   - 名前を変えた時（同じドライブ）: コピーは新しい場所にしか無く、設定は前の場所のまま。もう一度「場所を変える」でそこを
        ///     選べば、そのコピーを使う（CopyPlace.Check の「今の場所にはコピーが無く、そこにコピーがある」）→ cp_undo_failed。
        ///   - 写した時（別のドライブ）: 元のコピーも設定も前のまま（今の場所でそのまま遊べる）。残っているのは消し切れなかった写しで、
        ///     そこを選んでも cp_exists で断られる（元がまだあるため）。手で消してもらう → cp_undo_failed_copy。
        /// </summary>
        public static string UndoFailedKey(bool renamed) => renamed ? "cp_undo_failed" : "cp_undo_failed_copy";

        /// <summary>
        /// 移す物が無い時（インストール前）に、行き先に残っている「途中で止まった移動の残り」（目印付き）を片付ける。
        /// 公開前レビュー 2026-10-01: 片付けずに場所の設定だけ変えると、作りかけをそのまま使い、目印も残っていた。
        /// 目印の無いフォルダ（人の物・本物のコピー）には触らない。片付けた・片付ける物が無かった時は true、消し切れなかったら false。
        /// </summary>
        public bool ClearLeftover(string to)
        {
            try
            {
                if (string.IsNullOrEmpty(to) || !Directory.Exists(to) || !File.Exists(GameFolders.Join(to, CopyPlace.MarkerName))) return true;
                Log("move copy: removing what a stopped move left at " + to);
                DeleteLeftover(to);
                return !Directory.Exists(to);
            }
            catch (Exception ex) { Log("move copy: what a stopped move left could not be removed: " + ex.Message); return false; }
        }

        /// <summary>
        /// 2026-10-03（粗探し 5 を直している時に見つけた穴）: 作りかけ（目印付き）を消す時は、**目印を最後に**消す。Uninstaller.DeleteTree は
        /// 名前の順に消すので、目印（.starpocket-moving）が先に消え、その後で掴まれているファイルに当たって止まると、残りは目印の無い
        /// フォルダ＝人の物に見え、次の起動も「場所を変える」も二度と触れなくなっていた（cp_exists で断られる）。目印が残れば、
        /// 次の起動がもう一度試す。途中で消せない物があれば投げる（呼ぶ側が受ける）。
        /// </summary>
        static void DeleteLeftover(string to)
        {
            string marker = GameFolders.Join(to, CopyPlace.MarkerName);
            var di = new DirectoryInfo(to);
            foreach (var sub in di.GetDirectories()) Uninstaller.DeleteTree(sub.FullName);
            foreach (var f in di.GetFiles())
            {
                if (string.Equals(f.Name, CopyPlace.MarkerName, StringComparison.OrdinalIgnoreCase)) continue;
                try { if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal; } catch (Exception) { }
                f.Delete();
            }
            File.Delete(marker);
            Directory.Delete(to, false);
        }

        /// <summary>
        /// 写している間に毎ファイル見るには重い問い（プロセスの一覧を取る等）を、<paramref name="everyMs"/> に 1 回だけ聞く形にする。
        /// 一度 true になったら、その後はずっと true（止めると決めた事は戻さない）。<paramref name="now"/> は自己診断が時計を差し替える。
        /// </summary>
        public static Func<bool> Every(int everyMs, Func<bool> ask, Func<DateTime> now = null)
        {
            var clock = now ?? (() => DateTime.UtcNow);
            DateTime? last = null;
            bool yes = false;
            return () =>
            {
                if (yes) return true;
                var t = clock();
                if (last.HasValue && (t - last.Value).TotalMilliseconds < everyMs) return false;
                last = t;
                try { yes = ask(); } catch (Exception) { yes = false; }
                return yes;
            };
        }

        /// <summary>写した時の、元のコピーを消す（設定を書き換えた後で）。消し切れなくても失敗にはしない（新しい方で遊べる）。
        /// 元のコピーのゲームが動いている時（<see cref="OldInUse"/>）は消さずに false（「元の場所に残っています」の扱い）。</summary>
        public bool DeleteOld(string from)
        {
            bool inUse;
            try { inUse = OldInUse(from); } catch (Exception) { inUse = true; }   // 分からない時は消さない側に倒す
            if (inUse) { Log("move copy: the game of the old copy is running; the old copy is left as it is: " + from); return false; }
            try { Uninstaller.DeleteTree(from); return true; }
            catch (Exception ex) { Log("move copy: the old copy could not be removed completely: " + ex.Message); return false; }
        }

        /// <summary>中身を集める。つなぎ（ジャンクション・シンボリックリンク）の中には入らず、<paramref name="link"/> を立てる（写す道はそれで断る。
        /// 名前を変える道は、つなぎもそのまま一緒に動くので構わない）。</summary>
        static void Walk(DirectoryInfo d, List<DirectoryInfo> dirs, List<FileInfo> files, ref bool link)
        {
            foreach (var sub in d.GetDirectories())
            {
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) { link = true; continue; }
                dirs.Add(sub);
                Walk(sub, dirs, files, ref link);
            }
            // 目印（CopyPlace.MarkerName）は写さない。写す側が自分で置いて最後に外す物で、元に混じっていたら（前の移動の残りを
            // そのまま使った時など）、写した先に残って「途中で止まった移動の残り」と取り違えられる（公開前レビュー 2026-10-01）
            files.AddRange(d.GetFiles().Where(x => !string.Equals(x.Name, CopyPlace.MarkerName, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>2026-10-03（粗探し 9）: 新しい場所に置いた時の、一番長いファイルの場所の文字数（目印のファイルも数える）。
        /// インストーラーの FileCopy.LongestDestination と同じ考え（ここは .part を使わないので +5 は無い）。</summary>
        internal static int LongestAt(string to, string srcRoot, IEnumerable<DirectoryInfo> dirs, IEnumerable<FileInfo> files)
        {
            string root = to;
            try { root = Path.GetFullPath(to); } catch (Exception) { }
            root = root.TrimEnd('\\') + "\\";
            int longest = Math.Max(root.Length, (root + CopyPlace.MarkerName).Length);
            foreach (var d in dirs) longest = Math.Max(longest, root.Length + Rel(srcRoot, d.FullName).Length);
            foreach (var f in files) longest = Math.Max(longest, root.Length + Rel(srcRoot, f.FullName).Length);
            return longest;
        }

        /// <summary>
        /// 2026-10-03（公開前の粗探し 5）: 起動時に、前回の「場所を変える」が写している途中で終わった（終了・落ちた・電源が切れた）時の
        /// 作りかけを片付ける。ClientApp.DoMoveCopy は写す前に settings.json へ新しい場所を書く（ClientSettings.MovingTo）ので、ここは
        /// その場所に目印（CopyPlace.MarkerName）があれば消す（<see cref="ClearLeftover"/>。目印の無いフォルダ＝人の物・できあがった
        /// コピーには触らない）。片付いたら印を外して保存する。消し切れなかったら印は残し、次の起動がまた試す。
        /// それまでは、作りかけ（最大約 1 GB。BepInEx\PocketRoles\logs のプレイヤー名入りのログも含む）が、同じ場所をもう一度選ぶまで誰にも
        /// 消されず、30 日の削除の外にあった。settings.json が読めなかった回（Unreadable）は何もしない（粗探し 2）。決して投げない。
        /// </summary>
        public static void FinishStoppedMove(ClientSettings settings, Action save, Action<string> log)
        {
            if (TidyStoppedMove(settings, log)) ForgetStoppedMove(settings, save, log);
        }

        /// <summary>
        /// <see cref="FinishStoppedMove"/> のディスクの側（2026-10-03、崩す係 6）: 作りかけを消す所。最大約 1 GB・数千ファイルの削除なので、窓のアプリは
        /// 画面の糸ではなく裏の糸で呼ぶ（ClientApp.StartupHousekeeping。作業の鍵を持って）。true = 印を外してよい（片付いた・目印の無い物は触らず済ませた）。
        /// false = 何もする事が無い（印が無い・読めない回・null）か、消し切れなかった（印は残し、次の起動がまた試す）。決して投げない。
        /// </summary>
        public static bool TidyStoppedMove(ClientSettings settings, Action<string> log)
        {
            log = log ?? (_ => { });
            try
            {
                if (settings == null || settings.Unreadable || string.IsNullOrEmpty(settings.MovingTo)) return false;
                string to = settings.MovingTo;
                var m = new CopyMover { Log = log };
                if (!m.ClearLeftover(to)) { log("move copy: what the stopped move left at " + to + " could not be removed; it is tried again at the next start"); return false; }
                if (Directory.Exists(to) && File.Exists(GameFolders.Join(to, "Among Us.exe")))
                    log("move copy: a finished copy is at " + to + " (no marker), left as it is; the setting still names the old place");
                else log("move copy: the stopped move to " + to + " is cleaned up");
                return true;
            }
            catch (Exception ex) { log("move copy: " + ex.Message); return false; }
        }

        /// <summary><see cref="FinishStoppedMove"/> の設定の側: 印（movingTo）を外して保存する（画面の糸で）。保存に失敗しても投げない（ログに残す。次の起動がまた見る）。</summary>
        public static void ForgetStoppedMove(ClientSettings settings, Action save, Action<string> log)
        {
            log = log ?? (_ => { });
            if (settings == null) return;
            settings.SetMovingTo(null);
            try { save?.Invoke(); }
            catch (Exception ex) { log("move copy: the moving mark could not be removed from settings.json: " + ex.Message); }
        }

        static string Rel(string root, string full) => full.Substring(root.TrimEnd('\\').Length).TrimStart('\\');

        static bool IsDiskFull(Exception ex) { int c = ex.HResult & 0xFFFF; return c == 112 || c == 39; }   // ERROR_DISK_FULL / ERROR_HANDLE_DISK_FULL

        MoveResult Fail(MoveResult r, string key, string error)
        {
            r.Ok = false; r.ErrorKey = key; r.Error = error;
            Log("move copy: " + key + " (" + error + ")");
            return r;
        }
    }

    /// <summary>「場所を変える」の、移す所の全体（<see cref="MoveFlow.Run"/>）の答え。</summary>
    internal sealed class MoveFlowResult
    {
        public bool Ok;
        /// <summary>だめだった時の Strings.cs の鍵: cp_save_failed（印か設定を書けず、何も変えていない）、cp_undo_failed / cp_undo_failed_copy
        /// （設定を書けず、戻す事もできなかった）、または CopyMover.Move の鍵（cp_locked・cp_space …）。</summary>
        public string ErrorKey;
        /// <summary>Move の答え（Move まで行かなかった時は null）。</summary>
        public MoveResult Moved;
        /// <summary>元を消し切れた（名前を変えただけの時も true）。Ok の時だけ意味がある。false なら cp_done_left。</summary>
        public bool Clean;
    }

    /// <summary>
    /// 2026-10-03（崩す係 5）: ClientApp.DoMoveCopy の「移す」部分を、画面の無い所へ出した物。印（settings.json の movingTo）の書き外しの順番が
    /// ここに全部ある（それまでは画面の async の中に散らばっていて、印を書く・外す所を消しても自己点検が通った）:
    ///   1. 写す前に movingTo を書く（書けなければ何もせず cp_save_failed）
    ///   2. 移す（CopyMover.Move。別の糸で）
    ///   3. だめなら movingTo を外して、Move の鍵で断る（何も変えていない）
    ///   4. copyDir を書き、同じ 1 回の保存で movingTo を外す（書けなければ戻す。戻せた → cp_save_failed、戻せない → cp_undo_failed(_copy)）
    ///   5. 写した時は元を消す（消し切れなければ Clean = false → cp_done_left）
    /// settings と save は呼んだ側の糸（画面の糸）で動く。移す・戻す・消すは Task で受ける（画面を止めない）。自己点検は済んだ Task を渡して回す。
    /// </summary>
    internal static class MoveFlow
    {
        public static async Task<MoveFlowResult> Run(ClientSettings settings, Func<bool> save, string target,
            Func<Task<MoveResult>> move, Func<MoveResult, Task<bool>> undo, Func<Task<bool>> deleteOld, Action<string> log)
        {
            log = log ?? (_ => { });
            var r = new MoveFlowResult();
            if (!Mark(settings, save, target, log)) { r.ErrorKey = "cp_save_failed"; return r; }
            var moved = await move();
            r.Moved = moved;
            if (moved == null || !moved.Ok)
            {
                Mark(settings, save, null, log);   // 何も変えていない: 印も外す（外せなくても、次の起動が目印の無い所には触らない）
                r.ErrorKey = moved != null && !string.IsNullOrEmpty(moved.ErrorKey) ? moved.ErrorKey : "cp_failed";
                return r;
            }
            if (!Record(settings, save, target, log))
            {
                bool undone = await undo(moved);
                Mark(settings, save, null, log);
                r.ErrorKey = undone ? "cp_save_failed" : CopyMover.UndoFailedKey(moved.Renamed);
                return r;
            }
            r.Clean = moved.Renamed || await deleteOld();
            r.Ok = true;
            return r;
        }

        /// <summary>「移動中」の印（settings.json の movingTo）を書く（null で外す）。書けたら true、書けなければ前の値に戻して false。</summary>
        public static bool Mark(ClientSettings s, Func<bool> save, string target, Action<string> log)
        {
            string before = s.MovingTo;
            s.SetMovingTo(target);
            if (save()) return true;
            s.SetMovingTo(before);
            (log ?? (_ => { }))("move copy: the moving mark could not be " + (target == null ? "removed" : "written"));
            return false;
        }

        /// <summary>copyDir を書く（移動中の印 movingTo は同じ 1 回の保存で外す）。書けなければ前の値に戻して false。</summary>
        public static bool Record(ClientSettings s, Func<bool> save, string dir, Action<string> log)
        {
            string before = s.CopyDir, movingBefore = s.MovingTo;
            s.SetCopyDir(dir);
            s.SetMovingTo(null);
            if (save()) return true;
            s.SetCopyDir(before);
            s.SetMovingTo(movingBefore);
            (log ?? (_ => { }))("move copy: settings.json could not be saved");
            return false;
        }
    }
}
