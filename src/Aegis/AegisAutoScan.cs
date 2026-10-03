// Aegis の自動スキャンの決まり（2026-10-01、持ち主「トレイのアイコンからもう一度スキャンしないと、アンチチートの表示が変わらない」）。
// ここは窓もタイマーも FileSystemWatcher も持たない純粋な判断だけ（自己診断が回す）。動かす側は ClientApp。
//
// それまでスキャンが走るのは「アプリの起動時」「プレイを押した時」「もう一度スキャン」の 3 つだけだった。だから
// 「再ビルド」「修復」「インストール」「更新」で MOD のコピーが直っても、Aegis の行（赤／緑）とトレイの点は前の結果のまま残った。
// 直し方は 2 つ:
//   1. MOD のコピーを書き換える作業（インストール・修復・Steam から更新・MOD の更新・再ビルド・開発の更新）が終わったら、
//      自分でもう一度スキャンする（AfterTask）。
//   2. アプリの外で書き換わった時（build.cmd の再ビルド、手でのコピー、そして**誰かが DLL を置いた時**）も、
//      Aegis が読むファイルだけを見張って、落ち着いてから（Debounce）もう一度スキャンする（Watched）。
//
// 自動のスキャンは "auto" という種類で走る:
//   - 右下のカードは出さない（ClientApp.ScanCardIgnores）。窓がトレイにある時、ファイルが変わるたびにカードが出たら邪魔。
//     結果はトレイの点・バッジ・Aegis パネルに出る（OnAegisChanged はどの種類でも同じ）。
//   - 「見つかった」の音は、**赤に変わった時だけ**（host-v01.js）。同じ赤を見つけ直すたびに鳴らさない。
//   - ゲームの最中は走らない（ゲームの起動・動作の邪魔をしない）。終わったら 1 回だけ走る。
//   - 長い作業（インストール等）の最中も走らない。途中のファイルを見て赤にしないため。終わったら走る。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Starpocket.Client.Aegis
{
    internal static class AegisAutoScan
    {
        /// <summary>自動のスキャンの種類（AegisSnapshot.Kind / "aegis" イベントの kind）。</summary>
        public const string Kind = "auto";

        /// <summary>ファイルが変わってから、もう変わらなくなるまで待つ時間。コピーは何十ものファイルを続けて書くので、
        /// 1 つ目で走ると途中の状態を見てしまう。</summary>
        public const int DebounceMs = 2000;

        /// <summary>
        /// この作業が終わったら、もう一度スキャンするか。MOD のコピー（Aegis が読むファイル）を書き換える作業だけ。
        /// checkUpdate は「確かめるだけ」の時は何も書き換えないので走らない（<paramref name="installs"/> が true の時だけ）。
        /// プレイ（launch）は直前にプレイ前のスキャンをしているので走らない。報告の zip、アンインストールも走らない。
        /// </summary>
        public static bool AfterTask(string cmd, bool installs)
        {
            switch (cmd)
            {
                case "install":
                case "syncSteam":
                case "rebuild":
                case "devUpdate":
                    return true;
                case "checkUpdate":
                    return installs;
                default:
                    return false;
            }
        }

        /// <summary>見張っているフォルダ（MOD のゲームのコピー）からの相対パス。外なら null。区切りは \ に揃える。</summary>
        public static string Relative(string dir, string full)
        {
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(full)) return null;
            string d = dir.Replace('/', '\\').TrimEnd('\\') + "\\";
            string f = full.Replace('/', '\\');
            if (!f.StartsWith(d, StringComparison.OrdinalIgnoreCase)) return null;
            return f.Substring(d.Length).TrimStart('\\');
        }

        /// <summary>
        /// Aegis の検査が読むファイルか（AegisScan.cs の 13 行のうち、ゲームのコピーの中を読むもの）:
        ///   - 直下の *.dll と *.ini と *.exe … 注入の検査（直下の DLL すべて）、BepInEx の行（winhttp.dll と doorstop の設定、
        ///     そして 32bit / 64bit の比べ合わせに Among Us.exe）
        ///   - BepInEx\core\ … BepInEx の行（BepInEx.Core.dll）
        ///   - BepInEx\plugins\ … MOD 本体の整合性（PocketRoles.dll）と、ほかのプラグインの行
        ///   - BepInEx\config\jp.pocketroles.mod.cfg … 設定の行
        ///   - BepInEx\PocketRoles\Banlist.txt … BAN の行
        /// ほかは見ない。**特にログ（LogOutput.log など）は、ゲームの最中ずっと書かれるので絶対に入れないこと。**
        /// BepInEx フォルダごと・core / plugins フォルダごと消えた・名前が変わった時も true（中身が全部変わったのと同じ）。
        /// </summary>
        public static bool Watched(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return false;
            string r = relative.Replace('/', '\\').Trim('\\').ToLowerInvariant();
            if (r.Length == 0) return false;
            if (r.IndexOf('\\') < 0)
                return r == "bepinex" || r.EndsWith(".dll", StringComparison.Ordinal) || r.EndsWith(".ini", StringComparison.Ordinal)
                    || r.EndsWith(".exe", StringComparison.Ordinal);
            if (Under(r, "bepinex\\core") || Under(r, "bepinex\\plugins")) return true;
            return r == "bepinex\\config\\jp.pocketroles.mod.cfg" || r == "bepinex\\pocketroles\\banlist.txt";
        }

        static bool Under(string r, string folder) => r == folder || r.StartsWith(folder + "\\", StringComparison.Ordinal);

        /// <summary>
        /// 今、自動のスキャンを始めてよいか。"run"（始める）/ "skip"（何もしない）/ "game"（ゲームが終わるまで待つ）/
        /// "task"（長い作業が終わるまで待つ）/ "again"（今走っている自動スキャンの後にもう 1 回）。
        /// 上から順に強い: 終了の最中・Aegis が無いなら何もしない。ゲームの最中は、作業中でも「ゲーム待ち」。
        /// </summary>
        public static string Decide(bool aegisPorted, bool quitting, bool gameRunning, bool taskRunning, bool autoScanRunning)
        {
            if (!aegisPorted || quitting) return "skip";
            if (gameRunning) return "game";
            if (taskRunning) return "task";
            if (autoScanRunning) return "again";
            return "run";
        }
    }
}
