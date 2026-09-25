// 優しく閉じる（v1.2、持ち主 2026-09-24「× 押したときの挙動が落ちた感じ」）: ✕・トレイ退避・終了で、窓が 140 ms かけて薄くなって
// から消える。ここは時計・α・「終わったら何をするか」だけの純粋ロジックで、窓もタイマーも持たない（自己診断が偽の時計で回す）。
// 窓側の手（Form.Opacity、System.Windows.Forms.Timer）は MainForm.FadeOut、ページ側の「ほんの少し縮む」は ui\host-v01.js の
// "window" イベント（closing: true, ms）。
//
// なぜ窓側で薄くするのか:
//   - ページだけで opacity を落としても、WebView2 の外の窓（枠なしとはいえ HWND）はそのまま残り、最後の 1 フレームで
//     「消えた」感じになる。窓ごと薄くしないと落ちた感じは消えない。
//   - HWND の大きさは変えない。縮めるとレイアウトが走り、WebView2 が 1〜2 フレーム遅れて追いつくので、かえってガタつく。
//   - AnimateWindow(AW_BLEND | AW_HIDE) は使わない。窓の絵を WM_PRINT で取るので、WebView2（別プロセスの合成面）が真っ黒に
//     なってから薄くなる。
//   - 常時レイヤ化（WS_EX_LAYERED）もしない。BitBlt の画面キャプチャ（配信ソフトの「ウィンドウキャプチャ」）や Windows 11 の
//     角丸に影響し得るので、閉じる瞬間だけ Opacity < 1 にして、終わったら 1.0 に戻す（MainForm.EnsureOpaque）。
//
// 決めごと:
//   - 演出が失敗しても必ず閉じる: Tick の中の例外は Finish に倒し、then が投げても reset は呼ぶ（Finish の finally）。
//   - 上限は MaxMs（160）。ページの縮みも同じ上限で clamp する（host-v01.js）。
//   - 「動きを減らす」（Windows のアニメーション効果 OFF = prefers-reduced-motion）や窓が見えていない時は PlannedMs が 0 で、
//     Start が同期に Finish する（演出なし、即座）。
//   - Then は後から来た方が勝つ（✕ のあとにトレイの「終了」が来た時、薄くなり終わったら終了する）。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Starpocket.Client.Shell
{
    internal sealed class CloseFade
    {
        /// <summary>薄くなる時間。120〜160 ms が「落ちた感じ」が消えて、かつ待たされない範囲（自己診断が挟む）。</summary>
        public const int DurationMs = 140;
        /// <summary>ページ側が受け付ける上限（host-v01.js の Math.min）。</summary>
        public const int MaxMs = 160;
        /// <summary>タイマーの刻み（60 Hz に近い 16 ms）。</summary>
        public const int TickMs = 16;

        readonly int ms;
        readonly Func<double> clock;
        readonly Action<double> setAlpha;
        readonly Action reset;
        readonly Action<string> log;

        /// <summary>終わったら何をするか（Hide / Shutdown）。後から来た方が勝つ。</summary>
        public Action Then { get; set; }
        public bool Running { get; private set; }
        public bool Finished { get; private set; }

        /// <param name="ms">掛ける時間。0 以下なら演出なしで Start が同期に Finish する</param>
        /// <param name="clock">Start からの経過ミリ秒</param>
        /// <param name="setAlpha">窓の不透明度（1.0 → 0.0）</param>
        /// <param name="then">終わったらすること</param>
        /// <param name="reset">then の後に必ず（不透明度を 1.0 に戻す）</param>
        /// <param name="log">client.log</param>
        public CloseFade(int ms, Func<double> clock, Action<double> setAlpha, Action then, Action reset, Action<string> log)
        {
            this.ms = ms;
            this.clock = clock ?? (() => double.MaxValue);
            this.setAlpha = setAlpha ?? (_ => { });
            Then = then;
            this.reset = reset;
            this.log = log ?? (_ => { });
        }

        /// <summary>掛ける時間: 窓が見えていて、Windows の「アニメーション効果」が ON（true）の時だけ DurationMs。読めない（null）
        /// 時も 0（演出しない側）。</summary>
        public static int PlannedMs(bool windowVisible, bool? animationsOn) => windowVisible && animationsOn == true ? DurationMs : 0;

        /// <summary>経過時間に対する不透明度: 1.0 から 0.0 へ、コサインでなめらかに。durationMs が 0 以下なら 0。</summary>
        public static double AlphaAt(double elapsedMs, int durationMs)
        {
            if (durationMs <= 0) return 0;
            double t = elapsedMs / durationMs;
            if (double.IsNaN(t) || t < 0) t = 0;
            if (t > 1) t = 1;
            return (1 + Math.Cos(Math.PI * t)) / 2;
        }

        /// <summary>ms が 0 以下なら同期に Finish（then がすぐ呼ばれる）。それ以外は Running にして最初の Tick を 1 回。</summary>
        public void Start()
        {
            if (Finished || Running) return;
            if (ms <= 0) { Finish(); return; }
            Running = true;
            Tick();
        }

        /// <summary>タイマーの 1 刻み。時計を読んで α を置き、時間が来たら Finish。何かが投げたら（Opacity が触れない窓）Finish。</summary>
        public void Tick()
        {
            if (!Running) return;
            try
            {
                double e = clock();
                setAlpha(AlphaAt(e, ms));
                if (e >= ms) Finish();
            }
            catch (Exception ex)
            {
                log("close fade: " + ex.Message);
                Finish();
            }
        }

        /// <summary>v1.2: 途中でやめる（薄くなっている最中に窓がもう一度求められた時）。**then は呼ばない**ので、
        /// 隠すことも終わることも起きない。不透明度は reset で 1.0 に戻る。以後この CloseFade は使い終わり（Finished）。</summary>
        public void Cancel()
        {
            if (Finished) return;
            Then = null;
            Finish();
        }

        /// <summary>then を 1 回だけ、そのあと reset を必ず。2 回目以降は何もしない。</summary>
        public void Finish()
        {
            if (Finished) return;
            Finished = true;
            Running = false;
            try { Then?.Invoke(); }
            catch (Exception ex) { log("close fade then: " + ex.Message); }
            finally
            {
                try { reset?.Invoke(); } catch (Exception) { }
            }
        }
    }
}
