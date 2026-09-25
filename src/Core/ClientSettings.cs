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
// Keys this version does not know are kept as they are (a later version's settings survive a downgrade).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        /// <summary>Missing, unreadable or malformed file: the defaults.</summary>
        public static ClientSettings Load(string path)
        {
            var s = new ClientSettings();
            try
            {
                if (!File.Exists(path)) return s;
                var obj = Json.TryParseObject(Json.ReadUtf8File(path));
                if (obj == null) return s;
                foreach (var kv in obj)
                {
                    if (kv.Key == "close") { var v = kv.Value as string; if (IsValidClose(v)) s.Close = v; }
                    else if (kv.Key == "lang") s.Lang = Core.Lang.NormalizePref(kv.Value as string);
                    else if (kv.Key == "trayHintShown") s.TrayHintShown = kv.Value is bool b && b;
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
                    else if (kv.Key == "profileName") { var v = kv.Value as string; if (v != null) s.ProfileName = CleanProfileName(v); }   // v1.3: string のみ
                    else if (kv.Key == "profileAvatar") { int v; if (TryVolume(kv.Value, out v) && v >= 0 && v < AvatarCount) s.ProfileAvatar = v; }   // v1.3: 0〜5 のみ
                    else s.other[kv.Key] = kv.Value;
                }
            }
            catch (Exception) { }
            return s;
        }

        /// <summary>Writes through a temp file, then replaces (a crash never leaves half a file). Throws on failure.</summary>
        public void Save(string path)
        {
            var obj = new Dictionary<string, object>(StringComparer.Ordinal) { ["close"] = Close, ["lang"] = Lang };
            if (TrayHintShown) obj["trayHintShown"] = true;
            if (StartGame != StartPocketRoles) obj["startGame"] = StartGame;
            if (ChatTranslate != "unset") obj["chatTranslate"] = ChatTranslate;
            if (Accent != DefaultAccent) obj["accent"] = Accent;
            if (!StartScan) obj["startScan"] = false;
            if (!Sound) obj["sound"] = false;
            if (Volume != DefaultVolume) obj["volume"] = Volume;
            if (DevBuild) obj["devBuild"] = true;
            if (DevSourcePath.Length > 0) obj["devSource"] = DevSourcePath;   // v1.4
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
