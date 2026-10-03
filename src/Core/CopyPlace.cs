// MOD 用のコピー（Among Us PocketRoles）を置く場所を、設定画面から選べるようにする（2026-10-01）。
//
// 頼まれた経緯: bilibili のコメント（2026-09-24）「デスクトップに入れたくない」。持ち主は「今は環境変数 POCKETROLES_GAMEDIR で
// 指定できる。次の版で設定に『インストール先を選ぶ』を付ける」と返事した。その約束を果たす物。
//
// 決まり:
//   - 場所を決める順番は 引数 --game-dir > 環境変数 POCKETROLES_GAMEDIR > **設定（settings.json の copyDir）** > 今までの既定。
//     引数か環境変数で決まっている時は、設定画面からは変えない（どちらが本当の場所か分からなくなるため）。理由を言って断る。
//   - 選ぶのは「入れ物のフォルダ」。その中に Among Us PocketRoles を作る（今までのデスクトップと同じ形）。
//     選んだフォルダ自体がその名前なら、そのフォルダをそのまま使う。
//   - 置いてはいけない所: Program Files / Windows（管理者が要る・書けない）、Steam の steamapps の中（Steam が本物のゲームと
//     取り違えて消すことがある）、OneDrive の中（1 GB を同期し続け、ゲームの最中にファイルを掴む）、ネットワークの場所
//     （\\ で始まる所も、Z: のように文字を割り当てた所も）、今のコピーの中、中身の入った別のフォルダや同じ名前のファイル
//     （人のファイルを上書きしない）、このアプリ自身のフォルダ（%LOCALAPPDATA%\StarPocket\Client・exe の場所・Aegis の記録。
//     アンインストールや更新で、消えたり置き換わったりする。exe の場所はアプリだけのフォルダの時だけ。IsOwnExeFolder）。
//   - 今のコピーが Steam のフォルダの中なら（settings.json を手で書いた時だけ起きる）、移さない（FromSteam。元を消すため）。
//   - 移動の途中で止まった時の残り（目印 .starpocket-moving が入っている）だけは、上書きしてよい（CopyMover が消してから移す）。
// ここは純粋な判断だけ（自己診断が回す）。実際にディスクを見るのは Probe、動かすのは CopyMover と ClientApp。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;

namespace Starpocket.Client.Core
{
    /// <summary>移す先を確かめるのに要る事実（<see cref="CopyPlace.Probe"/> が集める。自己診断は手で作る）。</summary>
    internal sealed class CopyTargetFacts
    {
        public string Target;            // 新しいコピーの場所（…\Among Us PocketRoles）
        public string Current;           // 今のコピーの場所
        public bool CurrentExists;       // 今のコピーにゲームが入っている（Among Us.exe がある）
        public bool TargetExists;        // 新しい場所にもうフォルダがある
        public bool TargetEmpty;         // そのフォルダが空
        public bool TargetHasMarker;     // 移動の途中で止まった残り（CopyPlace.MarkerName が入っている）
        public bool TargetIsCopy;        // 新しい場所に、もうゲームのコピーがある（Among Us.exe がある）
        public bool Writable;            // 入れ物のフォルダに書ける
        public bool SameVolume;          // 今と同じドライブ（名前を変えるだけで移せる）
        public bool Network;             // 割り当てたネットワークドライブ（Z: → \\nas\…）か、種類が分からないドライブ（公開前レビュー 2026-10-01）
        public long? FreeBytes;          // 新しいドライブの空き（読めない時は null）
        public long NeededBytes;         // 今のコピーの大きさ
        public string[] Protected = new string[0];   // Program Files・Windows・このアプリ自身のフォルダ（DataDir・ExeDir・Aegis の記録）
        public string[] OneDrive = new string[0];    // OneDrive のフォルダ
    }

    internal static class CopyPlace
    {
        /// <summary>移動の途中で作った印。これが入っているフォルダは、途中で止まった移動の残り。</summary>
        public const string MarkerName = ".starpocket-moving";

        /// <summary>空きの見積もりに足すゆとり（BepInEx の interop とキャッシュが後から増える分）。</summary>
        public const long SpaceMargin = 500L * 1024 * 1024;

        /// <summary>どの決まりで今の場所になったか: "arg"（--game-dir）/ "env"（POCKETROLES_GAMEDIR）/ "setting" / "default"。
        /// 開発モードでは設定（copyDir）を使わない（GameFolders.ResolveModded。再ビルドの DLL はソースの隣のコピーにしか入らない）ので、
        /// 設定が残っていても "default"（公開前レビュー 2026-10-01）。</summary>
        public static string Source(string gameDirArg, string envGameDir, string settingCopyDir, bool devMode)
        {
            if (!string.IsNullOrEmpty(gameDirArg)) return "arg";
            if (!string.IsNullOrEmpty(envGameDir)) return "env";
            if (!devMode && !string.IsNullOrEmpty(settingCopyDir)) return "setting";
            return "default";
        }

        /// <summary>選ばれたフォルダから、新しいコピーの場所を作る。選んだフォルダ自体が「Among Us PocketRoles」ならそのまま。</summary>
        public static string TargetFor(string picked)
        {
            if (string.IsNullOrEmpty(picked)) return null;
            string p = picked.TrimEnd('\\', '/');
            if (p.Length == 2 && p[1] == ':') p += "\\";   // "D:" はドライブの一番上
            string name = null;
            try { name = Path.GetFileName(p); } catch (Exception) { }
            if (string.Equals(name, GameFolders.CopyFolderName, StringComparison.OrdinalIgnoreCase)) return p;
            return GameFolders.Join(p, GameFolders.CopyFolderName);
        }

        /// <summary>
        /// 移してよいか。よければ null、だめなら Strings.cs の鍵（cp_*）。**危ない物から順に**断る。
        /// </summary>
        public static string Check(CopyTargetFacts f)
        {
            if (f == null || string.IsNullOrEmpty(f.Target)) return "cp_bad";
            string t = Norm(f.Target);
            if (t.StartsWith("\\\\", StringComparison.Ordinal)) return "cp_network";
            if (!IsRootedLocal(t)) return "cp_bad";
            if (f.Network) return "cp_network";   // Z: のように文字を割り当てたネットワークの場所も（\\ で始まらないので上ではすり抜ける）
            string cur = Norm(f.Current);
            if (cur.Length > 0 && Same(t, cur)) return "cp_same";
            if (cur.Length > 0 && Inside(t, cur)) return "cp_inside";
            if (f.Protected.Any(p => !string.IsNullOrEmpty(p) && Inside(t, Norm(p)))) return "cp_protected";
            if (t.IndexOf("\\steamapps\\", StringComparison.OrdinalIgnoreCase) >= 0) return "cp_steam";
            if (f.OneDrive.Any(p => !string.IsNullOrEmpty(p) && Inside(t, Norm(p)))) return "cp_onedrive";
            // 中身の入ったフォルダには移さない。ただし「今の場所にはコピーが無く、そこにコピーがある」時だけは、そのコピーを使う
            // （手で移した人・環境変数をやめた人・設定の保存に失敗して元に戻せなかった時。何も上書きしない）
            if (f.TargetExists && !f.TargetEmpty && !f.TargetHasMarker && !(f.TargetIsCopy && !f.CurrentExists)) return "cp_exists";
            if (!f.Writable) return "cp_write";
            if (f.CurrentExists && !f.SameVolume && f.FreeBytes.HasValue && f.FreeBytes.Value < f.NeededBytes + SpaceMargin) return "cp_space";
            return null;
        }

        /// <summary>画面とログに出す時の形: ユーザーのフォルダは %USERPROFILE% と書く（配信中に Windows のユーザー名を映さない）。</summary>
        public static string Display(string path, string userProfile)
        {
            if (string.IsNullOrEmpty(path)) return path ?? "";
            string up = (userProfile ?? "").TrimEnd('\\');
            if (up.Length > 3 && (Same(path, up) || Inside(path, up))) return "%USERPROFILE%" + path.Substring(up.Length);
            return path;
        }

        /// <summary>1.2 GB のように、人が読む大きさ。</summary>
        public static string Size(long bytes)
        {
            double gb = bytes / (1024.0 * 1024 * 1024);
            if (gb >= 1) return gb.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB";
            return Math.Max(1, (int)Math.Round(bytes / (1024.0 * 1024))).ToString(System.Globalization.CultureInfo.InvariantCulture) + " MB";
        }

        /// <summary>同じドライブか（名前を変えるだけで移せるか）。読めない時は false（コピーする側に倒す）。</summary>
        public static bool SameRoot(string a, string b)
        {
            try { return string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return false; }
        }

        /// <summary>ドライブの種類が「ネットワークの場所」として断る物か。Network（割り当てたネットワークドライブ）のほか、
        /// NoRootDirectory（その文字のドライブが無い）と Unknown（種類が分からない）も、確かめられないので断る側に倒す。</summary>
        public static bool IsNetworkLike(DriveType type) => type == DriveType.Network || type == DriveType.NoRootDirectory || type == DriveType.Unknown;

        /// <summary>ディスクを見て事実を集める。投げない（読めない物は安全な側の値にする）。
        /// <paramref name="appFolders"/> はこのアプリ自身のフォルダ（DataDir・ExeDir・Aegis の記録）。アンインストールや更新で
        /// 消えたり置き換わったりするので、その中には置かない（Protected に入れて cp_protected。公開前レビュー 2026-10-01:
        /// 中に置けてしまい、アンインストールが「MOD 用のコピーは残します」と言いながら DataDir ごと消していた）。</summary>
        public static CopyTargetFacts Probe(string target, string current, string currentExe, params string[] appFolders)
        {
            var f = new CopyTargetFacts { Target = target, Current = current };
            try { f.CurrentExists = !string.IsNullOrEmpty(currentExe) && File.Exists(currentExe); } catch (Exception) { }
            try
            {
                f.TargetExists = Directory.Exists(target);
                // 公開前レビュー 2026-10-01: 同じ名前の「ファイル」がある時も、空きではない（そこへは作れない）。cp_exists で断る
                if (!f.TargetExists && File.Exists(target)) { f.TargetExists = true; f.TargetEmpty = false; }
                else if (f.TargetExists)
                {
                    f.TargetHasMarker = File.Exists(GameFolders.Join(target, MarkerName));
                    f.TargetEmpty = !Directory.EnumerateFileSystemEntries(target).Any();
                    f.TargetIsCopy = !f.TargetHasMarker && File.Exists(GameFolders.Join(target, "Among Us.exe"));
                }
            }
            catch (Exception) { f.TargetExists = true; f.TargetEmpty = false; }   // 見られない所には移さない
            f.Writable = CanWrite(GameFolders.Parent(target));
            f.SameVolume = SameRoot(target, current);
            DriveInfo drive = null;
            try { drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(target))); } catch (Exception) { }
            // 公開前レビュー 2026-10-01: Z: → \\nas\share のように文字を割り当てたネットワークドライブは "\\" で始まらないので、
            // ドライブの種類で見る（IsNetworkLike）。種類を読むこと自体が失敗した時は、ここでは断らない（書けるかは別に確かめている）
            try { f.Network = drive != null && IsNetworkLike(drive.DriveType); } catch (Exception) { }
            try { f.FreeBytes = drive != null ? drive.AvailableFreeSpace : (long?)null; } catch (Exception) { f.FreeBytes = null; }
            if (f.CurrentExists) f.NeededBytes = FolderSize(current);   // 確かめの文に大きさを出すので、同じドライブでも数える
            f.Protected = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetEnvironmentVariable("ProgramW6432"),
            }.Concat(appFolders ?? new string[0]).ToArray();
            f.OneDrive = new[]
            {
                Environment.GetEnvironmentVariable("OneDrive"),
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
            };
            return f;
        }

        /// <summary>フォルダの中身の合計（読めないファイルは数えない）。</summary>
        public static long FolderSize(string dir)
        {
            long n = 0;
            try { foreach (var fi in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories)) { try { n += fi.Length; } catch (Exception) { } } }
            catch (Exception) { }
            return n;
        }

        /// <summary>本当に書けるかは、書いてみるのが一番確か（読み取り専用・権限・空き 0 をまとめて見る）。書いた物はすぐ消す。
        ///
        /// 公開前レビュー 2026-10-01: 入れ物のフォルダに**ファイル**を書いて確かめていた頃は、「ローカル ディスク (C:)」を選ぶと
        /// 「書き込めません」と断っていた。C:\ の権限は、普通の人には「フォルダを作る」だけを許し、作ったフォルダの中には書ける
        /// （ファイルを C:\ に直に置くことは許さない）。移す時に要るのも同じ 2 つ（Among Us PocketRoles を作る・その中に書く）なので、
        /// 確かめも同じ形にする: 入れ物の中に一時フォルダを作り、その中にファイルを書き、両方消す。</summary>
        internal static bool CanWrite(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            string probeDir = GameFolders.Join(dir, ".starpocket-write-test-" + Guid.NewGuid().ToString("N"));
            string probe = GameFolders.Join(probeDir, "x.tmp");
            bool made = false;
            try
            {
                if (!Directory.Exists(dir)) return false;
                Directory.CreateDirectory(probeDir);
                made = true;
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                Directory.Delete(probeDir, false);
                return true;
            }
            catch (Exception)
            {
                if (made)
                {
                    try { if (File.Exists(probe)) File.Delete(probe); } catch (Exception) { }
                    try { if (Directory.Exists(probeDir)) Directory.Delete(probeDir, false); } catch (Exception) { }
                }
                return false;
            }
        }

        /// <summary>
        /// 公開前レビュー 2026-10-01: exe の置き場所を「このアプリ自身のフォルダ」として守る（Protected に入れる）のは、そこが
        /// アプリのためだけのフォルダの時だけ。zip をデスクトップやダウンロードに**そのまま**広げた人は exe の場所がデスクトップ
        /// そのものになり、守るとデスクトップのどこにも（今までの既定の場所にも）置けなくなった。しかも断る理由（アンインストールや
        /// 更新で一緒に消える）は、そういう皆で使うフォルダには当てはまらない（アンインストールはそのフォルダを残す）。
        /// アプリだけのフォルダ（%LOCALAPPDATA%\Programs\… や、zip を広げてできたフォルダ）は今までどおり守る: アンインストールは
        /// 「いらなければ消してください」とそのフォルダを名指しし、zip の更新はフォルダごと置き換えるので、中のコピーも一緒に消える。
        /// <paramref name="sharedFolders"/> はデスクトップ・ドキュメント・ダウンロード・ユーザーのフォルダ。ドライブの一番上も皆の物。
        /// </summary>
        public static bool IsOwnExeFolder(string exeDir, params string[] sharedFolders)
        {
            string d = Norm(exeDir);
            if (d.Length == 0) return false;
            if (d.Length == 2 && IsRootedLocal(d)) return false;   // "D:\"（Norm で "D:"）
            return !(sharedFolders ?? new string[0]).Any(s => !string.IsNullOrEmpty(s) && Same(d, s));
        }

        /// <summary>
        /// 公開前レビュー 2026-10-01: 今のコピーの場所が Steam のフォルダの中（steamapps、または見つけた Steam のゲームの場所）。
        /// 画面からはこの状態にできない（Check が steamapps を断る）が、settings.json の copyDir を手で書くとなり得る。その時に
        /// 「場所を変える」を通すと、写した後で元（＝ Steam の本物のゲーム）を消してしまうので、何もせずに断る（cp_from_steam）。
        /// アンインストールも Steam の中は必ず断っている（Uninstaller.Allowed）。
        /// </summary>
        public static bool FromSteam(string current, string steamGameDir)
        {
            string c = Norm(current);
            if (c.Length == 0) return false;
            if ((c + "\\").IndexOf("\\steamapps\\", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrEmpty(steamGameDir) && Inside(c, steamGameDir);
        }

        static string Norm(string p) => (p ?? "").Replace('/', '\\').TrimEnd('\\');

        static bool IsRootedLocal(string p) => p.Length >= 2 && char.IsLetter(p[0]) && p[1] == ':';

        static bool Same(string a, string b) => string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>a が b の中（b 自身も含む）。</summary>
        static bool Inside(string a, string b)
        {
            string x = Norm(a), y = Norm(b);
            if (y.Length == 0) return false;
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase) || x.StartsWith(y + "\\", StringComparison.OrdinalIgnoreCase);
        }
    }
}
