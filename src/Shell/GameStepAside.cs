// SPEC 5.4: ゲームを始めた時に窓がよけて（トレイへ）、ゲームが終わったら戻る。その「戻すかどうか」だけを持つ小さな部品。
// 窓にも時計にも触らない（呼ぶ側が今の時刻と、2 秒ごとの見回りの結果を渡す）ので、自己診断がすべての道を通せる。
//
// 2026-10-01（公開前レビュー 2 回目）で足した決まり:
//   - 最小化している窓は「画面に出ている窓」ではない（ClientApp.WindowAway と同じ見方）。よけさせず、ゲームの後で戻しもしない。
//     前は Visible だけを見ていたので、最小化のままトレイの「プレイ」を押すと、ゲームの後で元の大きさに広がって出てきた。
//   - ゲームの最中に人が自分で窓をしまった（✕ でトレイへ・ページの「－」）ら、その窓はもう人の物。ゲームが終わっても出さない。
//     前は、トレイから開いて ✕ で閉じても、ゲームが終わると勝手に出てきた（ClientApp の「already hidden stays hidden」の約束違反）。
//   - ゲームが一度も見えないまま時間が過ぎたら（起動の直後、最初の見回りより前に落ちた・そもそも上がらなかった）、窓を戻す。
//     前は「動いている → 止まった」の切り替わりでしか戻さなかったので、窓がトレイに隠れたまま、人が「開く」を押すまで出なかった。
//     待つ時間は起動の仕方で違う: MOD のコピーは exe を直に起動するのですぐプロセスが見える。素の Among Us は Steam を通すので、
//     Steam が起動していなければ Steam の起動から待つことになる（ClientApp.DoLaunchVanilla の説明）。
//     「起動した時に動いていることにする」（gameRunning = true）は使わない: 素の Among Us では、Steam がまだ起動している最中に
//     次の見回りが「止まった」と取り違えて、ゲームが出る前に窓を戻してしまう。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Starpocket.Client.Shell
{
    internal sealed class GameStepAside
    {
        /// <summary>MOD のコピーを起動してから、ゲームのプロセスが見えるまで待つ秒数。exe を直に起動するので、普通は 1 秒もかからない。</summary>
        public const double ModStartGraceSeconds = 20;
        /// <summary>素の Among Us（Steam の URL）を起動してから待つ秒数。Steam が起動していない時は Steam の起動から待つ。</summary>
        public const double VanillaStartGraceSeconds = 120;

        /// <summary>窓はゲームのためによけたので、ゲームが終わったら戻す。</summary>
        public bool Restore { get; private set; }
        /// <summary>最後に <see cref="Poll"/> が true を返したのが「ゲームが一度も見えなかった」ためか（ログの文を分けるため）。</summary>
        public bool NeverCameUp { get; private set; }

        DateTime? launchedAt;   // よけた時刻。ゲームが一度見えたら null
        bool vanilla;

        /// <summary>画面に出ている窓だけが、ゲームのためによける（最小化している窓・隠れている窓はそのまま）。</summary>
        public static bool StepsAside(bool windowVisible, bool windowMinimized) => windowVisible && !windowMinimized;

        /// <summary>起動がうまく行った時。true なら窓をよけさせる（呼ぶ側がトレイへしまう）。</summary>
        public bool Launched(bool windowVisible, bool windowMinimized, bool vanillaGame, DateTime now)
        {
            NeverCameUp = false;
            if (!StepsAside(windowVisible, windowMinimized)) { Restore = false; launchedAt = null; return false; }
            Restore = true;
            launchedAt = now;
            vanilla = vanillaGame;
            return true;
        }

        /// <summary>人が自分で窓をしまった（✕ でトレイへ・ページの「－」）。その窓はもう人の物なので、ゲームの後でも出さない。</summary>
        public void PersonPutAway()
        {
            Restore = false;
            launchedAt = null;
        }

        /// <summary>2 秒ごとの見回り。<paramref name="wasRunning"/> は前の見回りの答え、<paramref name="running"/> は今の答え。
        /// true なら、今、窓を戻す（フォーカスを取らずに。呼ぶ側の ShowAfterGame）。</summary>
        public bool Poll(bool wasRunning, bool running, DateTime now)
        {
            if (running) { launchedAt = null; return false; }   // ゲームが見えた: ここからは「止まった」で戻す
            if (!Restore) return false;
            if (wasRunning)
            {
                Restore = false; launchedAt = null; NeverCameUp = false;
                return true;   // ゲームが終わった
            }
            if (launchedAt.HasValue && (now - launchedAt.Value).TotalSeconds >= (vanilla ? VanillaStartGraceSeconds : ModStartGraceSeconds))
            {
                Restore = false; launchedAt = null; NeverCameUp = true;
                return true;   // 一度も見えないまま時間が過ぎた（すぐに落ちた・上がらなかった）
            }
            return false;
        }
    }
}
