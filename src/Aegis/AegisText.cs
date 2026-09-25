// Aegis's texts in ja / zh-CN / en: the table of aegis\Aegis.ps1 (class S, v0.5.5 lines 63-192) word for word, except the
// Chinese rule texts that now use the official Among Us terms (PORT-MAP 9.3: 伪装者 / 通风口 / 报告). The keys only the
// PowerShell windows used (sub, tip.*, m.*, st.*) are not here: the app's tray and UI have their own (Core\Strings.cs).
// The c.* keys are new for the app (the old tray holds the mutex).
// A row's detail is kept as an AegisTextRef (key + arguments) and drawn in the language of the moment, so a language
// change reaches the Aegis panel at once; events and toasts are drawn when they happen, like the ps1.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Starpocket.Client.Aegis
{
    /// <summary>A text drawn later in the language of the moment: a key of <see cref="AegisText"/> with its arguments
    /// (which may be texts themselves), or a plain text (an exception message).</summary>
    internal sealed class AegisTextRef
    {
        public readonly string Key;
        public readonly object[] Args;
        public readonly string Raw;

        AegisTextRef(string key, object[] args, string raw) { Key = key; Args = args; Raw = raw; }

        public static readonly AegisTextRef Empty = new AegisTextRef(null, null, "");

        public static AegisTextRef Of(string key, params object[] args) => new AegisTextRef(key, args, null);

        public static AegisTextRef Plain(string raw) => new AegisTextRef(null, null, raw ?? "");

        public string Render(string lang)
        {
            if (Key == null) return Raw ?? "";
            if (Args == null || Args.Length == 0) return AegisText.Get(lang, Key);
            var a = new object[Args.Length];
            for (int i = 0; i < Args.Length; i++) a[i] = Args[i] is AegisTextRef t ? t.Render(lang) : Args[i];
            return AegisText.Get(lang, Key, a);
        }
    }

    internal static class AegisText
    {
        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            // key                ja                                                   zh-CN                                  en
            { "engine",     new[] { "Aegis エンジン", "Aegis 引擎", "Aegis engine" } },
            { "engine.ok",  new[] { "検知ルール {0} 件・定義ファイル v{1}・署名 OK", "{0} 条检测规则・定义文件 v{1}・签名 OK", "{0} detection rules · definitions v{1} · signature OK" } },
            { "engine.nosig",  new[] { "定義の署名なし・不一致（組み込みの定義を使用）", "定义无签名・签名不符（使用内置定义）", "definitions unsigned or mismatched (built-in list used)" } },
            { "engine.nofile", new[] { "検知ルール {0} 件・組み込みの定義を使用", "{0} 条检测规则・使用内置定义", "{0} detection rules · built-in definitions" } },
            { "game",       new[] { "Among Us", "Among Us", "Among Us" } },
            { "game.ok",    new[] { "{0}（対応版）", "{0}（支持的版本）", "{0} (supported)" } },
            { "game.other", new[] { "{0}（対応版は {1}）", "{0}（支持的版本为 {1}）", "{0} (supported: {1})" } },
            { "game.none",  new[] { "MOD 用のゲームフォルダが見つかりません", "找不到 MOD 用的游戏文件夹", "Modded game folder not found" } },
            { "game.nover", new[] { "Among Us.exe を確認", "已确认 Among Us.exe", "Among Us.exe found" } },
            { "bep",        new[] { "BepInEx", "BepInEx", "BepInEx" } },
            { "bep.ok",     new[] { "BepInEx 6（IL2CPP）を確認", "已确认 BepInEx 6（IL2CPP）", "BepInEx 6 (IL2CPP) found" } },
            { "bep.none",   new[] { "BepInEx が見つかりません", "找不到 BepInEx", "BepInEx not found" } },
            { "mod",        new[] { "MOD 本体の整合性", "MOD 本体完整性", "Mod integrity" } },
            { "mod.same",   new[] { "v{0}・前回から変更なし", "v{0}・与上次相同", "v{0}, unchanged since last time" } },
            { "mod.first",  new[] { "v{0}・指紋を記録しました", "v{0}・已记录指纹", "v{0}, fingerprint recorded" } },
            { "mod.update", new[] { "v{0}・更新を確認（前回 v{1}）", "v{0}・已更新（上次 v{1}）", "v{0}, updated (was v{1})" } },
            { "mod.changed",new[] { "v{0} の中身が前回と違います（書き換えられた可能性）", "v{0} 的内容与上次不同（可能被改写）", "v{0} differs from last time (possibly modified)" } },
            { "mod.fix",    new[] { "ランチャーの「更新を確認」で MOD を入れ直してください", "请用启动器的“检查更新”重新安装 MOD", "Reinstall the mod with the launcher's update check" } },
            { "mod.none",   new[] { "PocketRoles.dll が見つかりません", "找不到 PocketRoles.dll", "PocketRoles.dll not found" } },
            { "plug",       new[] { "ほかのプラグイン", "其他插件", "Other plugins" } },
            { "plug.ok",    new[] { "なし（PocketRoles だけ）", "无（只有 PocketRoles）", "none (PocketRoles only)" } },
            { "plug.warn",  new[] { "見知らぬプラグイン: {0}", "未知插件: {0}", "unknown plugin: {0}" } },
            { "plug.fix",   new[] { "BepInEx\\plugins から {0} を外してください", "请从 BepInEx\\plugins 中移除 {0}", "Remove {0} from BepInEx\\plugins" } },
            { "cfg",        new[] { "Aegis の設定", "Aegis 设置", "Aegis settings" } },
            { "cfg.ok",     new[] { "検知 {0}・自動退出 {1}・お知らせ {2}・言い当て {3}", "检测 {0}・自动移出 {1}・公告 {2}・点中提示 {3}", "detect {0} · auto-remove {1} · announce {2} · callout {3}" } },
            { "cfg.off",    new[] { "検知がオフです（/opt anticheat on）", "检测已关闭（/opt anticheat on）", "detection is off (/opt anticheat on)" } },
            { "cfg.none",   new[] { "設定はまだありません（初回起動で作られます）", "尚无设置（首次启动时生成）", "no settings yet (created on first run)" } },
            { "ban",        new[] { "BAN リスト", "封禁名单", "Ban list" } },
            { "ban.ok",     new[] { "{0} 人", "{0} 人", "{0} player(s)" } },
            { "inj",        new[] { "ゲームへの注入", "游戏注入", "Game injection" } },
            { "inj.ok",     new[] { "不審な DLL なし", "没有可疑的 DLL", "no suspicious DLL" } },
            { "inj.warn",   new[] { "ゲームフォルダに {0}（チートの読み込みに使われる）", "游戏文件夹中有 {0}（用于加载作弊）", "{0} in the game folder (used to load cheats)" } },
            { "inj.fix",    new[] { "MOD 用のゲームフォルダから {0} を削除してください", "请从 MOD 用游戏文件夹中删除 {0}", "Delete {0} from the modded game folder" } },
            { "sb",         new[] { "セキュアブート", "安全启动", "Secure Boot" } },
            { "sb.on",      new[] { "有効", "已启用", "on" } },
            { "sb.off",     new[] { "無効（UEFI の設定でオンにできます）", "未启用（可在 UEFI 设置中开启）", "off (can be enabled in UEFI settings)" } },
            { "sb.unknown", new[] { "確認できません（レガシー BIOS）", "无法确认（传统 BIOS）", "unknown (legacy BIOS)" } },
            { "tpm",        new[] { "TPM", "TPM", "TPM" } },
            { "tpm.ok",     new[] { "TPM 2.0 を確認", "已确认 TPM 2.0", "TPM 2.0 found" } },
            { "tpm.none",   new[] { "TPM 2.0 が見つかりません", "找不到 TPM 2.0", "no TPM 2.0 found" } },
            { "kern",       new[] { "カーネルの保護", "内核保护", "Kernel protection" } },
            { "kern.ok",    new[] { "テスト署名・デバッグモードなし{0}", "无测试签名・调试模式{0}", "no test-signing / debug mode{0}" } },
            { "kern.hvci",  new[] { "・メモリ整合性 ON", "・内存完整性 开", " · memory integrity on" } },
            { "kern.warn",  new[] { "{0} が有効（署名のないドライバーを読み込める状態）", "{0} 已启用（可加载未签名驱动）", "{0} enabled (unsigned drivers can load)" } },
            { "kern.fix",   new[] { "管理者のコマンドプロンプトで bcdedit /set {0} off を実行して再起動してください", "请在管理员命令提示符中运行 bcdedit /set {0} off 并重启", "Run bcdedit /set {0} off in an admin command prompt and restart" } },
            { "vdb",        new[] { "脆弱ドライバーの遮断", "易受攻击驱动阻止", "Driver blocklist" } },
            { "vdb.on",     new[] { "有効", "已启用", "on" } },
            { "vdb.hvci",   new[] { "有効（メモリ整合性で常に有効）", "已启用（内存完整性开启时始终有效）", "on (always with memory integrity)" } },
            { "vdb.default",new[] { "有効（Windows の既定）", "已启用（Windows 默认）", "on (Windows default)" } },
            { "vdb.off",    new[] { "無効（コア分離の設定でオンにできます）", "未启用（可在“内核隔离”中开启）", "off (turn on in Core isolation)" } },
            { "tools",      new[] { "実行中のチートツール", "运行中的作弊工具", "Running cheat tools" } },
            { "tools.ok",   new[] { "なし", "无", "none" } },
            { "tools.warn", new[] { "{0} が起動中（チートに使えるツール）", "{0} 正在运行（可用于作弊的工具）", "{0} is running (usable for cheating)" } },
            { "tools.fix",  new[] { "{0} を終了してから起動してください", "请先关闭 {0} 再启动", "Close {0}, then start" } },
            { "fix.head",   new[] { "直し方", "处理方法", "How to fix" } },
            { "on",         new[] { "ON", "开", "on" } },
            { "off",        new[] { "OFF", "关", "off" } },
            { "scanning",   new[] { "スキャン中… {0}/{1}", "扫描中… {0}/{1}", "Scanning… {0}/{1}" } },
            { "done",       new[] { "スキャン完了 — 保護中", "扫描完成 — 保护中", "Scan complete — protected" } },
            { "done.warn",  new[] { "スキャン完了 — 注意 {0} 件", "扫描完成 — 注意 {0} 项", "Scan complete — {0} warning(s)" } },
            { "go",         new[] { "スキャン完了 — 起動します", "扫描完成 — 正在启动", "Scan complete — starting" } },
            { "blocked",    new[] { "起動を止めました — 赤い項目 {0} 件を直してから起動してください", "已阻止启动 — 请先处理 {0} 个红色项目", "Start blocked — fix the {0} red row(s), then start" } },
            // v1.1 (StarPocket Client): a scan that is not the pre-launch one found a red row (the start scan on the little
            // card, "scan again"); the card then stays until it is clicked, and says so on its last line
            { "found",      new[] { "見つかりました — 赤い項目 {0} 件を直してください", "有发现 — 请处理 {0} 个红色项目", "Found — fix the {0} red row(s)" } },
            { "card.close", new[] { "クリックで閉じる", "点击关闭", "Click to close" } },
            { "b.ready",    new[] { "起動しました。ゲームを始めると監視します。", "已启动。开始游戏后将进行监视。", "Ready. Watching starts when the game runs." } },
            { "b.watch",    new[] { "監視を始めました（PocketRoles {0}）", "开始监视（PocketRoles {0}）", "Watching (PocketRoles {0})" } },
            { "b.watch0",   new[] { "監視を始めました", "开始监视", "Watching" } },
            { "b.stop",     new[] { "ゲームが終わりました。待機中です。", "游戏已结束。待机中。", "The game closed. Standing by." } },
            { "b.removed",  new[] { "{0} を退出させました（{1}）", "已移出 {0}（{1}）", "Removed {0} ({1})" } },
            { "b.flag",     new[] { "{0}: {1}", "{0}: {1}", "{0}: {1}" } },
            { "test",       new[] { "[テスト] ", "[测试] ", "[test] " } },
            // rule texts (the mod's CheatDetector.Rule names); zh-CN: official Among Us terms (PORT-MAP 9.3)
            { "r.KillRole",     new[] { "キルできない役職のキル", "不能击杀的职业击杀", "kill without a killing role" } },
            { "r.VentRole",     new[] { "ベントを使えない役職のベント", "不能用通风口却用了", "vent without a venting role" } },
            { "r.AbilityRole",  new[] { "持っていない能力の使用", "使用了没有的能力", "ability the role does not have" } },
            { "r.TaskImpostor", new[] { "インポスターのタスク完了", "伪装者完成任务", "task done as an impostor" } },
            { "r.ChatAlive",    new[] { "生存中の会議外チャット", "存活时会议外聊天", "alive chat outside a meeting" } },
            { "r.KillDead",     new[] { "死んでいるのにキル", "死亡后击杀", "kill while dead" } },
            { "r.SabotageCrew", new[] { "クルーのサボタージュ", "船员发动破坏", "sabotage as a crewmate" } },
            { "r.KillCooldown", new[] { "クールダウンより早いキル", "快于冷却的击杀", "kill faster than the cooldown" } },
            { "r.ProtectAlive", new[] { "生存中の守護", "存活时守护", "protect while alive" } },
            { "r.TaskUnknown",  new[] { "持っていないタスクの完了", "完成了没有的任务", "task they do not have" } },
            { "r.KillDistance", new[] { "遠すぎるキル", "过远的击杀", "kill from too far" } },
            { "r.RpcUnknown",   new[] { "普通にない通信", "原版没有的通信", "non-vanilla message" } },
            { "r.TaskBurst",    new[] { "ありえない速さのタスク", "不可能的任务速度", "impossibly fast tasks" } },
            { "r.ReportForge",  new[] { "ありえない通報", "不可能的报告", "impossible report" } },
            { "r.Teleport",     new[] { "瞬間移動", "瞬间移动", "teleport" } },
            { "r.KillPhase",    new[] { "会議中・追放画面のキル", "会议或驱逐画面中击杀", "kill during a meeting" } },
            { "r.ChatFlood",    new[] { "チャットの連投", "聊天刷屏", "chat flood" } },
            { "r.NameChange",   new[] { "部屋の中での名前変更", "房间内更改名字", "name change in the room" } },
            { "r.ColorSpam",    new[] { "色の高速切り替え・試合中の色変更", "快速切换颜色・对局中改色", "colour cycling / change in a game" } },
            { "r.SpeedHack",    new[] { "スピードハック", "加速外挂", "speed hack" } },
            { "r.SpeedFast",    new[] { "設定より速い移動", "比设置更快的移动", "faster than the speed setting" } },
            { "r.VentFar",      new[] { "ベントから遠い位置でのベント", "远离通风口进入通风口", "vent from far away" } },
            { "r.NgWord",       new[] { "NG ワードの繰り返し", "反复使用违禁词", "repeated NG words" } },
            // ---- StarPocket Client (new): Aegis cannot run in the app yet
            { "c.oldtray",  new[] { "古い Aegis トレイ（PowerShell）が動いています。そのトレイを終了すると、ここで見張りを始めます。", "旧版 Aegis 托盘程序（PowerShell）正在运行。退出它后，将在这里开始监视。", "The old Aegis tray (PowerShell) is running. Quit it and Aegis starts here." } },
            { "c.notstarted", new[] { "Aegis はまだ始まっていません。", "Aegis 尚未启动。", "Aegis has not started yet." } },
        };

        /// <summary>The ps1's S.Idx: ja 0, zh-CN (or zh) 1, anything else 2 (English).</summary>
        public static int Index(string lang) => lang == "ja" ? 0 : (lang == "zh-CN" || lang == "zh") ? 1 : 2;

        /// <summary>The ps1's S.Get: the text (the key itself when unknown), {0}.. filled when there are arguments.</summary>
        public static string Get(string lang, string key, params object[] args)
        {
            string[] v;
            string s = T.TryGetValue(key, out v) ? v[Index(lang)] : key;
            return args != null && args.Length > 0 ? string.Format(s, args) : s;
        }

        /// <summary>The ps1's S.Rule: the rule's text, or its name when there is none.</summary>
        public static string Rule(string lang, string name)
        {
            string[] v;
            return T.TryGetValue("r." + name, out v) ? v[Index(lang)] : name;
        }

        /// <summary>For the self-test.</summary>
        public static IEnumerable<KeyValuePair<string, string[]>> All => T;
    }
}
