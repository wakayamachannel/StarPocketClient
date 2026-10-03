// v1.1.2 直し 1（持ち主の決定 2026-10-01 Q6。乗り換えの設計 MIGRATION.md 7 章の 1、64bit 移行の設計 4-2）:
// この PC で Client を初めて開いた時は、片づけ（消す処理）を 1 つも走らせない。
//
// 片づけ = 次の 3 つ。どれも、アプリが開いた直後・利用者が何も押す前に走っていた。
//   - GameLogs.RemoveExpired    30 日より古いログ・logs の中身・デスクトップの報告 zip と証拠 zip を消す
//   - GameLogs.CompressOldLogs  7 日より古いログを日ごとの zip にまとめ、元のログを消す
//   - EventsLog.Prune           共有の events.log（%LOCALAPPDATA%\PocketRoles\Aegis）から 30 日より古い行を消す（AegisService）
// GameLogs.SaveGameLog（前のゲームのログを logs に写すだけ。何も消さない）は、初めての時も今までどおり走らせる。
//
// 印は settings.json の "cleanupArmed": true（ClientSettings.CleanupArmed）。引き継ぎ（launcher-state.json の migratedFrom）とは
// 関係なく決める。引き継ぎが見つからなかった人・古いランチャーのフォルダに置いた人にも効くように（MIGRATION 7 章: migratedFrom を
// 引き金にすると、一番困る人に効かない）。
//   - アプリとしての起動（ClientApp.StartAfterConsent。--tray・--autolaunch も同じ）で 1 回だけ Decide を呼ぶ。印が無ければ、
//     この起動の片づけを全部飛ばして印を書く。2 回目の起動からは今までどおり片づける（30 日・7 日の決まりは 1 日も変えていない。
//     プレイヤー名を 30 日で消す約束のため）。
//   - 印を書けなかった時は、次の起動もまた飛ばす（消さない側に倒す）。
//   - 窓の無い --action（Program.NewHeadless）は印を見るだけで、書かない。「初めて開いた時」はアプリとしての起動のこと。
//   - v1.1.1 以前から使っている人も、この版で初めて開いた 1 回は片づけない（印がまだ無いため）。1 回遅れるだけで、困る事は無い。
//   - 「場所を変える」や開発の切り替えでアプリが開き直した時は、もう 2 回目の起動として片づける。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Starpocket.Client.Core
{
    internal static class FirstCleanup
    {
        /// <summary>アプリとしての起動で 1 回だけ呼ぶ（ClientApp.StartAfterConsent。Aegis の events.log の整理より先）。
        /// true = 今までどおり片づける。false = この PC で初めての起動: 何も消さず、次の起動のために印を書く
        /// （<paramref name="save"/> が投げたら印は戻し、次の起動もまた飛ばす）。決して投げない。</summary>
        public static bool Decide(ClientSettings settings, Action save, Action<string> log)
        {
            log = log ?? (_ => { });
            if (settings == null) return false;
            if (settings.CleanupArmed) return true;
            settings.CleanupArmed = true;
            try
            {
                save?.Invoke();
                log("clean-up: the first start on this PC - nothing is deleted this time (old logs, report zips, events.log lines); from the next start as before");
            }
            catch (Exception ex)
            {
                settings.CleanupArmed = false;
                log("clean-up: the first start on this PC - nothing is deleted; settings.json could not be written (" + ex.Message + "), so the next start skips it too");
            }
            return false;
        }

        /// <summary>起動の片づけ（ClientApp）と、--action の前の片づけ（Program）。今のランチャーと同じ順番（ps1:2135）:
        /// 30 日の削除 → 前のゲームのログを写す → 日ごとの zip（<paramref name="dayZips"/>。--action はまとめない、今までどおり）。
        /// <paramref name="cleanUp"/> が false（この PC で初めて）の時は、ログを写すだけで何も消さない。</summary>
        public static void Housekeep(GameLogs logs, bool cleanUp, bool dayZips)
        {
            if (logs == null) return;
            if (cleanUp) logs.RemoveExpired();
            logs.SaveGameLog();
            if (cleanUp && dayZips) logs.CompressOldLogs();
        }
    }
}
