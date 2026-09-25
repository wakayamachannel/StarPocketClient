// UI <-> app messages (SPEC 3, PORT-MAP 7). The page sends {type:"invoke", id, cmd, args}; the app answers every invoke
// exactly once with {type:"result", id, ok, ...} and sends {type:"event", name, data}.
// This file holds the fixed tables (which commands v0.1 does, which come later) and the answer shapes; ClientApp runs them.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Starpocket.Client.Core;

namespace Starpocket.Client.Shell
{
    internal sealed class Invoke
    {
        public string Id, Cmd;
        public Dictionary<string, object> Args;

        /// <summary>null for anything that is not an invoke we can answer (no id).</summary>
        public static Invoke Parse(string json)
        {
            var m = Json.TryParseObject(json);
            if (m == null || Json.Str(m, "type") != "invoke") return null;
            string id = Json.Str(m, "id");
            if (string.IsNullOrEmpty(id) || id.Length > 128) return null;
            return new Invoke { Id = id, Cmd = Json.Str(m, "cmd") ?? "", Args = Json.Obj(m, "args") ?? new Dictionary<string, object>() };
        }
    }

    internal static class Bridge
    {
        /// <summary>What the app does (PORT-MAP 7.1 "する"). v0.1.1: launchVanilla (PLAY while Settings → 起動するゲーム is
        /// plain Among Us). v0.2: install / repair, the Steam sync, the update check and picking Steam's folder.
        /// v0.3: the report zip and one player's evidence, the shortcut button and the uninstall.
        /// v0.4: the app's own log page and, in developer mode only, the rebuild and the update.
        /// v1.3: プロフィール（名前と絵の番号は settings.json、自分の画像は profile\avatar.png の写し。src\Core\ProfileImage.cs）。</summary>
        public static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "launch", "launchWindowed", "launchVanilla",
            "install", "syncSteam", "checkUpdate", "pickSteam",
            "pickModSource",   // v1.4: 開発モードのソースのフォルダを選ぶ（作者の PC だけ。ClientApp.PickModSource）
            "aegis.rescan", "aegis.scanOnly", "aegis.events",
            "openModFolder", "openLogsFolder", "openConfig", "openLog", "openReadme",
            // 2026-09-26: Discord の「参加する」、製品サイト、公開した利用規約・プライバシーポリシー。
            // ページが渡すのは **名前** だけで、行き先は AppInfo.ExternalPage が決めます（URL は受け取りません）。
            "openExternal",
            "window.minimize", "window.close", "window.drag", "app.quit",
            "settings.set", "setLang",
            "makeReport", "exportOne", "mailBug", "mailRequest", "openReportFolder",
            "shortcut.create", "uninstall",
            "showLog", "rebuild", "devUpdate",
            "profile.set", "profile.pickImage", "profile.clearImage",   // v1.3: プロフィールの絵（src\Core\ProfileImage.cs）
            "consent.set", "consent.decline",   // 最初の同意の画面（first-run.md 2.5・利用規約 第12条3項）
        };

        /// <summary>What the page may still ask for while the first-run screen ① is on top and nothing has been answered
        /// yet (src\Core\Consent.cs). Everything NOT in here is refused with <see cref="NeedConsent"/> - that refusal is
        /// what actually keeps the promise in Terms of Use Article 12(3), because a page that was changed (or a script
        /// talking to the window) must not be able to start Aegis, an install or an update check by asking politely.
        ///
        /// <para>Why each one is allowed: the language switch is on the screen itself (first-run.md 2.2 item 1);
        /// 「アンインストール」 has to work without agreeing (a SignPath condition, first-run.md 2.2 item 8); the window
        /// buttons and quit are how 「同意しない」 ends; and consent.* is the answer itself.</para></summary>
        public static readonly HashSet<string> BeforeConsent = new HashSet<string>(StringComparer.Ordinal)
        {
            "consent.set", "consent.decline",
            "setLang", "uninstall",
            "window.minimize", "window.close", "window.drag", "app.quit",
        };

        /// <summary>"The first-run screen has not been answered, so this is not available yet." Its own shape (not Fail)
        /// so the page can tell this apart from a real error and simply bring screen ① back to the front.</summary>
        public static Dictionary<string, object> NeedConsent()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ok"] = false, ["reason"] = "needConsent",
                ["error"] = "the first-run screen has not been answered yet",
            };
        }

        /// <summary>Known commands of later versions: answered with a calm "not in this version" (PORT-MAP 7.1 "知らせ").</summary>
        public static readonly Dictionary<string, string> Later = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["aegis.banConsole"] = "later",   // openExternal は 2026-09-26 から Supported
            ["recent.clear"] = "later", ["player.vip"] = "later", ["player.restrict"] = "later",   // profile.set は v1.3 から Supported
            // in SPEC's list only; replaced by settings.set and the "status" event
            ["setOption"] = "replaced", ["status"] = "replaced",
        };

        /// <summary>The launcher's buttons: refused with busy while a long task runs (ps1 Set-Busy disables them all).
        /// The uninstall is here too: it must never start while the app is copying or unpacking something.
        /// showLog is deliberately NOT here: the launcher's log box stayed readable while a task ran, and the whole
        /// point of the log page is to watch a long task go by.</summary>
        public static readonly HashSet<string> BusyGated = new HashSet<string>(StringComparer.Ordinal)
        {
            "launch", "launchWindowed", "launchVanilla", "aegis.rescan", "aegis.scanOnly",
            "install", "syncSteam", "checkUpdate",
            "openModFolder", "openLogsFolder", "openConfig", "openLog", "openReadme",
            "makeReport", "exportOne", "uninstall",
            "rebuild", "devUpdate",
        };

        /// <summary>settings.set keys (SPEC 3, the prototype's PREF_DEFAULTS). v0.1 stores "close"; v0.1.1 also "startGame";
        /// v0.2 also "chatTranslate" (the answer of the one setup question, so it is never asked twice);
        /// v0.3.1 also "accent" (the Client's colour, the owner 2026-09-23); v1.1 also "startScan" (the start scan's card
        /// on every start, the owner 2026-09-23 「起動のたびに出す形に変えて」); v1.2 also "sound" and the prototype's "volume"
        /// (the page's sound and how loud, the owner 2026-09-24 「あと音ないのか」).
        /// 2026-09-24: "bgVideo" は消えた。背景の動画そのものが無くなったので（持ち主「動画はいいや、要らない」。背景は自作の動く絵）。</summary>
        public static readonly HashSet<string> SettingKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "autostart", "close", "hwaccel", "volume",
            "nAegisA", "nAegisS", "nUpdateA", "nUpdateS", "nNewsA", "nNewsS",
            "streamer", "prLang", "channel", "autoUpdate", "autoLaunch", "chatTranslate", "startGame", "accent", "startScan",
            "sound",
            "devBuild",   // v1.1: the author's switch (Settings → PocketRoles → 開発, src\Core\DevSource.cs)
        };

        /// <summary>settings.set {key, value}: "close", "startGame", "chatTranslate", "accent", "startScan", "sound" and
        /// "volume" are kept in settings.json (<paramref name="save"/> writes it and its answer is the reply); the
        /// prototype's other keys answer "not in this version"; anything else is refused. A refused value changes nothing.
        /// ClientApp answers "accent" itself, so its reply can carry the colours to paint with; this path is the plain save
        /// for anything that calls it directly. "startScan" and "sound" are switches: the page sends a real true / false
        /// (JSON), a script may send the words. "volume" is a whole number 0-100 (the slider's), or the same in a string.</summary>
        public static Dictionary<string, object> SetSetting(ClientSettings s, Dictionary<string, object> args, Func<Dictionary<string, object>> save)
        {
            string key = Json.Str(args, "key");
            string value = Json.Str(args, "value");
            if (key == "close") return s.SetClose(value) ? save() : Fail("invalid value");
            if (key == "accent") return s.SetAccent(value) ? save() : Fail("invalid value");
            if (key == "startGame") return s.SetStartGame(value) ? save() : Fail("invalid value");
            if (key == "chatTranslate") return s.SetChatTranslate(value) ? save() : Fail("invalid value");
            if (key == "startScan" || key == "sound")
            {
                object raw;
                bool? on = OnOffValue(args != null && args.TryGetValue("value", out raw) ? raw : null);
                if (!on.HasValue) return Fail("invalid value");
                if (key == "sound") s.SetSound(on.Value); else s.SetStartScan(on.Value);
                return save();
            }
            if (key == "volume")
            {
                object raw; int v;
                if (!ClientSettings.TryVolume(args != null && args.TryGetValue("value", out raw) ? raw : null, out v) || !s.SetVolume(v)) return Fail("invalid value");
                return save();
            }
            if (key == "devBuild")
            {
                // v1.1: the author's switch. This is only the save; ClientApp checks that the working copy is there and
                // restarts the app on top of it (the mode is decided once at start, ClientContext.Detect).
                object raw;
                bool? on = OnOffValue(args != null && args.TryGetValue("value", out raw) ? raw : null);
                if (!on.HasValue) return Fail("invalid value");
                s.SetDevBuild(on.Value);
                return save();
            }
            if (key != null && SettingKeys.Contains(key)) return Unsupported("settings.set", key);
            return Fail("unknown setting");
        }

        /// <summary>A switch's value as the page may send it; null when it is neither on nor off.</summary>
        public static bool? OnOff(string value)
        {
            if (value == "on" || value == "true" || value == "1") return true;
            if (value == "off" || value == "false" || value == "0") return false;
            return null;
        }

        /// <summary>The same for the value as JSON hands it over: a real true / false (what the page's toggles send), or
        /// one of the words above; null for anything else (a number, an object, nothing).</summary>
        public static bool? OnOffValue(object value) => value is bool b ? b : OnOff(value as string);

        public enum Kind { Supported, Later, Unknown }

        public static Kind Classify(string cmd)
        {
            if (cmd != null && Supported.Contains(cmd)) return Kind.Supported;
            if (cmd != null && Later.ContainsKey(cmd)) return Kind.Later;
            return Kind.Unknown;
        }

        // ---- answers
        public static Dictionary<string, object> Ok(object data = null)
        {
            var r = new Dictionary<string, object> { ["ok"] = true };
            if (data != null) r["data"] = data;
            return r;
        }

        public static Dictionary<string, object> Fail(string error) => new Dictionary<string, object> { ["ok"] = false, ["error"] = error ?? "" };

        public static Dictionary<string, object> Busy() => new Dictionary<string, object> { ["ok"] = false, ["busy"] = true };

        /// <summary>{ok:false, unsupported:true, data:{cmd, version:"0.1"}} (+ key for settings.set, + when it arrives).</summary>
        public static Dictionary<string, object> Unsupported(string cmd, string key = null)
        {
            var data = new Dictionary<string, object> { ["cmd"] = cmd, ["version"] = AppInfo.UiVersion };
            string when;
            if (Later.TryGetValue(cmd, out when)) data["arrives"] = when;
            if (key != null) data["key"] = key;
            return new Dictionary<string, object> { ["ok"] = false, ["unsupported"] = true, ["data"] = data };
        }

        public static string ResultJson(string id, Dictionary<string, object> result)
        {
            var msg = new Dictionary<string, object> { ["type"] = "result", ["id"] = id };
            foreach (var kv in result) msg[kv.Key] = kv.Value;
            return Json.Serialize(msg);
        }

        public static string EventJson(string name, object data) =>
            Json.Serialize(new Dictionary<string, object> { ["type"] = "event", ["name"] = name, ["data"] = data ?? new Dictionary<string, object>() });
    }
}
