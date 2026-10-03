// %LOCALAPPDATA%\StarPocket\Client\settings.json - the app's own settings (PORT-MAP 3.8).
// {"close":"tray"|"quit","lang":"auto"|"ja"|"zh-CN"|"en","trayHintShown":true,"startGame":"vanilla",
//  "accent":"#RRGGBB","startScan":false,"devBuild":true}. Written as UTF-8 without a BOM, read with or without one.
// trayHintShown is written only once the "still running in the notification area" notice was shown; startGame (v0.1.1,
// Settings -> PocketRoles -> 起動するゲーム) only while PLAY starts plain Among Us; accent (v0.3.1, Settings -> 全般 ->
// いろ) only once a colour was chosen - the default is the word "default", which is never written; startScan (v1.1,
// Settings -> 全般 -> 起動時の動作) only while the start scan's card was turned OFF - on is the default and never written;
// devBuild (v1.1, Settings -> PocketRoles -> 開発) only while the author's switch is ON (src\Core\DevSource.cs);
// sound (v1.2, Settings -> 全般 -> 音。音は Aegis が見つけた時の 1 つだけ) はオフにした間だけ、volume (0-100) は既定の 40
// でない間だけ書く。
// profileName (v1.3, フレンド欄のプロフィールの名前、16 文字まで) は空でない間だけ、profileAvatar (0-5、組み込みの絵の番号) は
// 0 でない間だけ書く（ページの profile.set が持ってくる。自分の画像そのものは settings.json ではなく profile\avatar.png）。
// cleanupArmed (v1.1.2, 直し 1) は、この PC で初めてアプリを開いた時に書く true だけ（それまでは起動の片づけをしない。
// src\Core\FirstCleanup.cs）。ページからは変えられない（Bridge.SettingKeys に無い）。
// movingTo (v1.1.2, 2026-10-03 粗探し 5) は、コピーを別のドライブへ写している間だけ書く新しい場所。次の起動が作りかけを片付ける。
// 2026-10-03（粗探し 2）: ファイルはあるのに読めなかった時は Unreadable を立て、その起動では一度も書かない（Save が投げる）。
// 2026-10-03（崩す係 1）: 「一瞬掴まれていた」と「中身が壊れている」を分ける。掴まれていた時は短く読み直し（3 回・150 ms おき）、それでも
//   だめなら Unreadable。中身が JSON でない（0 バイト・NUL 埋め・配列・4 MB 超）時は settings.json.broken-<日時> に名前を変えて残し、既定値で
//   続ける（書ける）。それまでは壊れたファイルが何度開き直しても Unreadable のままで、設定の保存も 30 日の削除もずっと始まらなかった。
// Keys this version does not know are kept as they are (a later version's settings survive a downgrade).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Starpocket.Client.Core
{
    internal sealed class ClientSettings
    {
        public const string CloseToTray = "tray";
        public const string CloseQuits = "quit";
        /// <summary>起動するゲーム: PLAY starts PocketRoles (the mod copy; the default).</summary>
        public const string StartPocketRoles = "pocketroles";
        /// <summary>起動するゲーム: PLAY starts plain Among Us through Steam (steam://rungameid/945360), without the mod.</summary>
        public const string StartVanilla = "vanilla";

        /// <summary>What the window's close button does: tray (default, SPEC 5.4) or quit.</summary>
        public string Close { get; private set; } = CloseToTray;
        /// <summary>auto / ja / zh-CN / en.</summary>
        public string Lang { get; private set; } = "auto";
        /// <summary>The first hide to the tray showed its one-time notice (SPEC 5.4).</summary>
        public bool TrayHintShown { get; set; }
        /// <summary>v1.1.2 直し 1（src\Core\FirstCleanup.cs）: この PC でアプリを一度開いた＝起動の片づけ（古いログ・報告 zip・
        /// events.log の古い行を消す処理）を始めてよい、の印。初めての起動が書く。本物の true の時だけ、true の間だけ書く。</summary>
        public bool CleanupArmed { get; set; }
        /// <summary>What PLAY starts (the owner, 2026-09-23: a choice in the Client's settings): pocketroles (default) or vanilla.</summary>
        public string StartGame { get; private set; } = StartPocketRoles;

        readonly Dictionary<string, object> other = new Dictionary<string, object>(StringComparer.Ordinal);

        public static bool IsValidClose(string v) => v == CloseToTray || v == CloseQuits;

        public bool SetClose(string v)
        {
            if (!IsValidClose(v)) return false;
            Close = v;
            return true;
        }

        public void SetLang(string pref) => Lang = Core.Lang.NormalizePref(pref);

        public static bool IsValidStartGame(string v) => v == StartPocketRoles || v == StartVanilla;

        public bool SetStartGame(string v)
        {
            if (!IsValidStartGame(v)) return false;
            StartGame = v;
            return true;
        }

        /// <summary>PLAY starts plain Among Us (no mod, no pre-launch scan, nothing in the mod copy touched).</summary>
        public bool PlaysVanilla => StartGame == StartVanilla;

        /// <summary>v1.1 (the owner, 2026-09-23 「起動のたびに出す形に変えて」): Aegis's start scan is drawn on the little card
        /// at the bottom right on EVERY start, window or no window (Shell\ClientApp.UpdateScanCard). Off: only while the
        /// window is away, as v0.4 did. A red row opens the Aegis panel whatever this says. Default on.</summary>
        public bool StartScan { get; private set; } = true;

        public void SetStartScan(bool on) => StartScan = on;

        /// <summary>v1.2（持ち主 2026-09-24「あと音ないのか」）: ページの音。Aegis が赤い項目を見つけた時の 1 つだけ（同じ日に、窓が
        /// 出た時とプレイを押した時の音は要らないと決まった）。ui\index.html の中で WebAudio で作る（音のファイルは無い）。
        /// 設定 → 全般 → 音 → 音を鳴らす（hero の消音ボタンは動画と一緒に無くなった）。既定はオン。オフの間だけ書く。</summary>
        public bool Sound { get; private set; } = true;

        public void SetSound(bool on) => Sound = on;

        /// <summary>その音（Aegis が見つけた時の 1 つ）の音量、0-100（設定 → 全般 → 音 → 音量）。ページが波形に掛ける。既定 40 は
        /// わざと控えめ。0 なら鳴らない（切とは別）。</summary>
        public const int DefaultVolume = 40;
        public int Volume { get; private set; } = DefaultVolume;

        public static bool IsValidVolume(int v) => v >= 0 && v <= 100;

        public bool SetVolume(int v)
        {
            if (!IsValidVolume(v)) return false;
            Volume = v;
            return true;
        }

        /// <summary>A volume as JSON hands it over: a whole number (the page's slider sends an int; the serializer may
        /// make it a long or a decimal), or a whole number written as a string (a script). Anything else - a fraction,
        /// a word, a bool, nothing - is no volume.</summary>
        public static bool TryVolume(object value, out int v)
        {
            v = 0;
            if (value is int i) { v = i; return true; }
            if (value is long l) { if (l < int.MinValue || l > int.MaxValue) return false; v = (int)l; return true; }
            if (value is decimal m) { if (m != decimal.Truncate(m) || m < int.MinValue || m > int.MaxValue) return false; v = (int)m; return true; }
            if (value is double d) { if (double.IsNaN(d) || d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) return false; v = (int)d; return true; }
            var s = value as string;
            return s != null && s.Length <= 4 && int.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        /// <summary>v1.1 (the owner, 2026-09-24 「開発用もそこで何とかしてよ」): run against the author's working copy of the mod
        /// (Settings → PocketRoles → 開発). Only a real true turns it on, and it is written only while on. It means
        /// nothing on a PC where <see cref="DevSource.Find"/> finds no working copy: friend mode, no error, no switch.
        /// The mode is decided once at start (ClientContext.Detect), so flipping it restarts the app.</summary>
        public bool DevBuild { get; private set; }

        public void SetDevBuild(bool on) => DevBuild = on;

        /// <summary>
        /// v1.4: the author's working copy, chosen with the folder dialog in Settings → PocketRoles → 開発
        /// (the owner, 2026-09-26 「特定のフォルダからやらないといけないのめんどくさい」). Until v1.3 the folder had to be
        /// on the Desktop, so moving it turned developer mode off with nothing said.
        /// <para>Written ONLY by "pickModSource", which is a dialog the person opened themselves, and only after
        /// <see cref="DevSource.IsDevFolder"/> said yes. It is read back beside <see cref="DevBuild"/>, which still has
        /// to be on: whoever can write this line can write that one, and the Startup folder is next door.</para>
        /// </summary>
        public string DevSourcePath { get; private set; } = "";

        public void SetDevSourcePath(string path) => DevSourcePath = path ?? "";

        /// <summary>2026-10-01: MOD 用のコピー（…\Among Us PocketRoles）を置く場所。空なら今までの既定（デスクトップ）。
        /// 引数 --game-dir と環境変数 POCKETROLES_GAMEDIR の方が強い（GameFolders.ResolveModded / CopyPlace.Source）。
        /// 書くのは設定画面の「場所を変える」（ClientApp.DoMoveCopy）だけで、コピーを移し終えてから書く。
        /// 公開前レビュー 2026-10-01: 今までのランチャー（PocketRolesLauncher.ps1）と Aegis.ps1 はこの値を読まない（読むのは
        /// POCKETROLES_GAMEDIR だけ）。移した後に今までのランチャーを開くと、コピーが「なし」に見え、「インストール」でデスクトップに
        /// 2 つ目のコピー（約 1 GB）を作る。v1.1.2（持ち主の決定 2026-10-01 Q6、設計 B12）: 移し終えた時の文（cp_done・cp_done_left・
        /// cp_set_done、3 言語）は「今までのランチャーは、もう開かないでください」と頼む（前は「環境変数 POCKETROLES_GAMEDIR も同じ場所に」
        /// と頼んでいたが、利用者には難しかった）。アプリから環境変数は書かない（ユーザーの環境を勝手に変えない）。</summary>
        public string CopyDir { get; private set; } = "";

        public void SetCopyDir(string path) => CopyDir = path ?? "";

        /// <summary>v1.3: フレンド欄のプロフィールの名前（ページの profile.set が持ってくる）。16 文字まで、制御文字は除く。
        /// 空でない間だけ settings.json に書く。</summary>
        public string ProfileName { get; private set; } = "";

        /// <summary>組み込みの絵の数（ページの #av-0 〜 #av-5）。</summary>
        public const int AvatarCount = 6;

        /// <summary>v1.3: 組み込みの絵の番号、0〜5。0 でない間だけ書く。自分の画像は別（profile\avatar.png、src\Core\ProfileImage.cs）。</summary>
        public int ProfileAvatar { get; private set; }

        /// <summary>settings.json にプロフィールがある（名前か絵が既定でない）。無ければ "shell" イベントに profile を載せず、
        /// ページが自分の分を profile.set で送ってくる。</summary>
        public bool HasProfile => ProfileName.Length > 0 || ProfileAvatar != 0;

        /// <summary>16 文字に切り、制御文字を除いた名前（null は ""）。</summary>
        public static string CleanProfileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(16);
            foreach (char c in name)
            {
                if (char.IsControl(c)) continue;
                sb.Append(c);
                if (sb.Length >= 16) break;
            }
            return sb.ToString();
        }

        /// <summary>profile.set {name, avatar}: name は null なら ""、avatar は TryVolume と同じ読み方（JSON の整数か、整数の字）で
        /// 0〜5 以外は false（何も変えない）。</summary>
        public bool SetProfile(string name, object avatarRaw)
        {
            int av;
            if (!TryVolume(avatarRaw, out av) || av < 0 || av >= AvatarCount) return false;
            ProfileName = CleanProfileName(name);
            ProfileAvatar = av;
            return true;
        }

        /// <summary>The answer to the one setup question before the first install ("use chat translation"): "unset" until
        /// it is answered, then "on" or "off". Kept so it is never asked twice; the mod's own setting follows later.</summary>
        public string ChatTranslate { get; private set; } = "unset";

        public static bool IsValidChatTranslate(string v) => v == "on" || v == "off";

        public bool SetChatTranslate(string v)
        {
            if (!IsValidChatTranslate(v)) return false;
            ChatTranslate = v;
            return true;
        }

        /// <summary>What the accent colour is when nobody chose one: the StarPocket gold. Kept as the word "default"
        /// (not the hex), so the brand colour can change later without rewriting everyone's settings.json.</summary>
        public const string DefaultAccent = "default";

        /// <summary>The Client's accent colour (the owner, 2026-09-23 「クライアントの色は好きに変えれるって言う風にしない？」):
        /// "default" or "#RRGGBB". What is actually painted is worked out from it by <see cref="AccentColor"/>.</summary>
        public string Accent { get; private set; } = DefaultAccent;

        public static bool IsValidAccent(string v)
        {
            if (v == DefaultAccent) return true;
            if (v == null || v.Length != 7 || v[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char c = v[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }

        public bool SetAccent(string v)
        {
            if (!IsValidAccent(v)) return false;
            Accent = v == DefaultAccent ? DefaultAccent : v.ToUpperInvariant();
            return true;
        }

        /// <summary>
        /// 2026-10-03（公開前の粗探し 2）: ファイルは**あるのに**読めなかった（起動の瞬間にウイルス対策や OneDrive が掴んでいた・
        /// 書きかけ）。LauncherStateFile.Unreadable と同じ考え。この印がある間は <see cref="Save"/> が投げ、
        /// FirstCleanup.Decide も書かない（ClientApp.SaveSettings は set_unreadable の文で断る。コピーを書き換える作業は in_unreadable で断る）。
        /// 無いと: 既定値の入ったこの物を、起動直後の自動の保存（cleanupArmed・トレイの知らせの印）がそのまま書き戻し、
        /// copyDir・devSource・プロフィールが消えていた。コピーの場所が既定（デスクトップ）に戻って「未インストール」になり、
        /// そこで「インストール」を押すとデスクトップに 2 つ目の約 1 GB ができる。
        /// ファイルが無い時（初めての PC）は false: 書いてよい。中身が壊れている時（JSON でない）も false: 壊れた物は
        /// <see cref="BrokenMovedTo"/> へ名前を変えて残し、既定値で続ける（崩す係 1。読めない状態から二度と抜けないのを塞ぐ）。
        /// 名前を変える事もできなかった（掴まれている）時だけ true。
        /// </summary>
        public bool Unreadable { get; private set; }

        /// <summary>2026-10-03（崩す係 1）: 中身が壊れていた settings.json を名前を変えて残した場所（settings.json.broken-yyyyMMdd-HHmmss）。
        /// 無ければ null。ClientContext が client.log に書く。中身は消していないので、必要なら人が開いて copyDir などを読める。</summary>
        public string BrokenMovedTo { get; private set; }

        /// <summary>掴まれていた時に読み直す回数と間隔（崩す係 1）。ウイルス対策・OneDrive・バックアップが掴むのは一瞬なので、合わせて 300 ms 待つ。</summary>
        internal const int ReadRetries = 3, ReadRetryDelayMs = 150;

        /// <summary>
        /// 2026-10-03（粗探し 5）: 「場所を変える」で別のドライブへ写している最中の、新しい場所（…\Among Us PocketRoles）。
        /// 写す前に書き、設定を書き換える時（SetCopyDir の後の Save）か、失敗して戻した時に空にする。
        /// 写している間に終了・落ちる・電源が切れると、目印（CopyPlace.MarkerName）付きの作りかけ（最大約 1 GB。プレイヤー名の
        /// 入ったログも含む）が残り、同じ場所をもう一度選ぶまで誰も消さなかった。次の起動が、この値の場所に目印があれば片付ける
        /// （CopyMover.FinishStoppedMove）。目印の無いフォルダ（人の物・できあがったコピー）には触らない。
        /// 空でない間だけ書く。ページからは変えられない（Bridge.SettingKeys に無い）。
        /// </summary>
        public string MovingTo { get; private set; } = "";

        public void SetMovingTo(string path) => MovingTo = path ?? "";

        /// <summary>Missing file: the defaults. Held by another program (after a few short retries): the defaults and
        /// <see cref="Unreadable"/>. Not JSON: put aside as <see cref="BrokenMovedTo"/>, then the defaults (writable).
        /// <paramref name="log"/> gets one line when the file was put aside or could not be read.</summary>
        public static ClientSettings Load(string path, Action<string> log = null) => Load(path, log, ReadRetries, ReadRetryDelayMs, null);

        /// <summary>The same with the retry and the clock in hand (the self-test shortens the wait and fixes the stamp).</summary>
        internal static ClientSettings Load(string path, Action<string> log, int retries, int delayMs, Func<DateTime> now)
        {
            log = log ?? (_ => { });
            var s = new ClientSettings();
            bool there = false;
            try
            {
                there = File.Exists(path);
                if (!there) return s;
                // 2026-10-03（崩す係 1）: 4 MB より大きい物はうちのファイルではない（Json.ReadUtf8File が投げる前に、壊れた物として脇へ）
                if (new FileInfo(path).Length > Json.MaxFileBytes) return PutAside(path, "larger than " + (Json.MaxFileBytes / (1024 * 1024)) + " MB", log, now);
                // 掴まれていた（共有違反・権限）なら短く読み直す。読めないままなら Unreadable（書かない側に倒す）
                string text = null;
                Exception last = null;
                for (int attempt = 1; ; attempt++)
                {
                    try { text = Json.ReadUtf8File(path); break; }
                    catch (Exception ex)
                    {
                        last = ex;
                        if (attempt >= Math.Max(1, retries)) break;
                        if (delayMs > 0) Thread.Sleep(delayMs);
                    }
                }
                if (text == null)
                {
                    log("settings.json: could not be read (" + (last != null ? last.Message : "?") + ") after " + Math.Max(1, retries) + " tries; nothing is written over it this start");
                    return new ClientSettings { Unreadable = true };
                }
                var obj = Json.TryParseObject(text);
                // 中身が JSON の物（object）でない: 停電の後の 0 バイト・NUL 埋め、人が書き換えた配列など。開き直しても直らないので、名前を変えて残し、
                // 既定値で続ける（次の保存が新しいファイルを書く）。それまでは Unreadable のままで、設定の保存も 30 日の削除も二度と始まらなかった
                if (obj == null) return PutAside(path, "not readable as JSON", log, now);
                foreach (var kv in obj)
                {
                    if (kv.Key == "close") { var v = kv.Value as string; if (IsValidClose(v)) s.Close = v; }
                    else if (kv.Key == "lang") s.Lang = Core.Lang.NormalizePref(kv.Value as string);
                    else if (kv.Key == "trayHintShown") s.TrayHintShown = kv.Value is bool b && b;
                    else if (kv.Key == "cleanupArmed") s.CleanupArmed = kv.Value is bool armed && armed;   // v1.1.2 直し 1: 本物の true だけ
                    else if (kv.Key == "startGame") { var v = kv.Value as string; if (IsValidStartGame(v)) s.StartGame = v; }
                    else if (kv.Key == "chatTranslate") { var v = kv.Value as string; if (IsValidChatTranslate(v)) s.ChatTranslate = v; }
                    else if (kv.Key == "accent") { var v = kv.Value as string; if (IsValidAccent(v)) s.SetAccent(v); }
                    else if (kv.Key == "startScan") s.StartScan = !(kv.Value is bool off && !off);   // only a real false turns it off
                    else if (kv.Key == "sound") s.Sound = !(kv.Value is bool quiet && !quiet);       // v1.2: the same rule
                    else if (kv.Key == "volume") { int v; if (TryVolume(kv.Value, out v) && IsValidVolume(v)) s.Volume = v; }
                    else if (kv.Key == "devBuild") s.DevBuild = kv.Value is bool dev && dev;         // only a real true turns it on
                    // v1.4: a string only, and it still has to pass DevSource.IsDevFolder every start - this line is a
                    // remembered answer, never a permission on its own
                    else if (kv.Key == "devSource") { var v = kv.Value as string; if (v != null) s.DevSourcePath = v; }
                    else if (kv.Key == "copyDir") { var v = kv.Value as string; if (v != null) s.CopyDir = v; }   // 2026-10-01: string のみ
                    else if (kv.Key == "movingTo") { var v = kv.Value as string; if (v != null) s.MovingTo = v; }   // 2026-10-03（粗探し 5）: string のみ
                    else if (kv.Key == "profileName") { var v = kv.Value as string; if (v != null) s.ProfileName = CleanProfileName(v); }   // v1.3: string のみ
                    else if (kv.Key == "profileAvatar") { int v; if (TryVolume(kv.Value, out v) && v >= 0 && v < AvatarCount) s.ProfileAvatar = v; }   // v1.3: 0〜5 のみ
                    else s.other[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                // 読めなかっただけで、ファイルはある: 既定値で上書きしてはいけない印（粗探し 2）。読めた分の値は捨てる（半分だけの状態を作らない）
                if (there)
                {
                    log("settings.json: could not be read (" + ex.Message + "); nothing is written over it this start");
                    return new ClientSettings { Unreadable = true };
                }
            }
            return s;
        }

        /// <summary>2026-10-03（崩す係 1）: 壊れた settings.json を settings.json.broken-yyyyMMdd-HHmmss に名前を変えて残し、既定値を返す
        /// （<see cref="BrokenMovedTo"/> にその場所）。名前を変えられなかった（掴まれている）時は Unreadable（この起動では書かない。次の起動がまた試す）。</summary>
        static ClientSettings PutAside(string path, string why, Action<string> log, Func<DateTime> now)
        {
            string stamp = (now ?? (() => DateTime.Now))().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string aside = path + ".broken-" + stamp;
            try
            {
                for (int i = 2; File.Exists(aside); i++) aside = path + ".broken-" + stamp + "-" + i;
                File.Move(path, aside);
            }
            catch (Exception ex)
            {
                log("settings.json: " + why + ", and it could not be put aside (" + ex.Message + "); nothing is written over it this start");
                return new ClientSettings { Unreadable = true };
            }
            log("settings.json: " + why + "; put aside as " + Path.GetFileName(aside) + " and the defaults are used (the next save writes a fresh file)");
            return new ClientSettings { BrokenMovedTo = aside };
        }

        /// <summary>Writes through a temp file, then replaces (a crash never leaves half a file). Throws on failure -
        /// and always while <see cref="Unreadable"/> (粗探し 2: writing now would put the defaults over a file that is there).</summary>
        public void Save(string path)
        {
            if (Unreadable) throw new IOException("settings.json is there but could not be read this time; not written over (open the app again)");
            var obj = new Dictionary<string, object>(StringComparer.Ordinal) { ["close"] = Close, ["lang"] = Lang };
            if (TrayHintShown) obj["trayHintShown"] = true;
            if (CleanupArmed) obj["cleanupArmed"] = true;   // v1.1.2 直し 1
            if (StartGame != StartPocketRoles) obj["startGame"] = StartGame;
            if (ChatTranslate != "unset") obj["chatTranslate"] = ChatTranslate;
            if (Accent != DefaultAccent) obj["accent"] = Accent;
            if (!StartScan) obj["startScan"] = false;
            if (!Sound) obj["sound"] = false;
            if (Volume != DefaultVolume) obj["volume"] = Volume;
            if (DevBuild) obj["devBuild"] = true;
            if (DevSourcePath.Length > 0) obj["devSource"] = DevSourcePath;   // v1.4
            if (CopyDir.Length > 0) obj["copyDir"] = CopyDir;   // 2026-10-01
            if (MovingTo.Length > 0) obj["movingTo"] = MovingTo;   // 2026-10-03（粗探し 5）: 写している間だけ
            if (ProfileName.Length > 0) obj["profileName"] = ProfileName;   // v1.3
            if (ProfileAvatar != 0) obj["profileAvatar"] = ProfileAvatar;
            foreach (var kv in other) obj[kv.Key] = kv.Value;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Json.Serialize(obj), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
