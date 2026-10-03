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
                if (SameVolume(from, to))
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
                return CopyThenVerify(from, to, r);
            }
            catch (UnauthorizedAccessException ex) { return Fail(r, "cp_write", ex.Message); }
            catch (IOException ex) { return Fail(r, "cp_locked", ex.Message); }
            catch (Exception ex) { return Fail(r, "cp_failed", ex.Message); }
        }

        /// <summary>新しい場所が空か、移動の残りなら片付ける。人の物が入っていたら断る（false）。</summary>
        bool ClearTarget(string to, MoveResult r)
        {
            if (!Directory.Exists(to)) return true;
            if (File.Exists(GameFolders.Join(to, CopyPlace.MarkerName)))
            {
                Log("move copy: removing what a stopped move left at " + to);
                Uninstaller.DeleteTree(to);
                return true;
            }
            if (Directory.EnumerateFileSystemEntries(to).Any()) { Fail(r, "cp_exists", to); return false; }
            Directory.Delete(to, false);   // 空のフォルダ（Directory.Move は行き先があると失敗する）
            return true;
        }

        MoveResult CopyThenVerify(string from, string to, MoveResult r)
        {
            var src = new DirectoryInfo(from);
            // つなぎ（ジャンクション・シンボリックリンク）は辿らない。中身がどこか別の所にある物を写したり消したりしない
            var dirs = new List<DirectoryInfo>();
            var files = new List<FileInfo>();
            if (!Walk(src, dirs, files)) return Fail(r, "cp_link", from);
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
                Uninstaller.DeleteTree(to);
                return !Directory.Exists(to);
            }
            catch (Exception ex) { Log("move copy: what a stopped move left could not be removed: " + ex.Message); return false; }
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

        static bool Walk(DirectoryInfo d, List<DirectoryInfo> dirs, List<FileInfo> files)
        {
            foreach (var sub in d.GetDirectories())
            {
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                dirs.Add(sub);
                if (!Walk(sub, dirs, files)) return false;
            }
            // 目印（CopyPlace.MarkerName）は写さない。写す側が自分で置いて最後に外す物で、元に混じっていたら（前の移動の残りを
            // そのまま使った時など）、写した先に残って「途中で止まった移動の残り」と取り違えられる（公開前レビュー 2026-10-01）
            files.AddRange(d.GetFiles().Where(x => !string.Equals(x.Name, CopyPlace.MarkerName, StringComparison.OrdinalIgnoreCase)));
            return true;
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
}
