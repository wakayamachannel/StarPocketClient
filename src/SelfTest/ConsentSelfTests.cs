// 最初の同意の画面（first-run.md ①）の記録 consent.json の自己診断（src\Core\Consent.cs）。
//
// ここで確かめているのは、利用規約 第12条3項の約束「同意する前には、通信しません」を支えている 3 つのことです。
//   - 記録が無い・壊れている・版が違う → 「まだ同意していない」と読む（安全な側に倒れる）
//   - 「同意しない」は何も書かない（ファイルそのものが出来ない）
//   - ページが勝手な版の番号を書き込めない（FromAnswer と ClientApp の突き合わせ）
// あわせて Bridge の表（同意の前に通してよい命令）も見ます。ここが緩むと、ページを差し替えた人が
// aegis や install を同意の前に呼べてしまい、約束が嘘になります。
//
// 窓もネットも使いません。<work>\consent の中でファイルを読み書きするだけです。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client.SelfTest
{
    internal static class ConsentSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            LoadTests(r);
            VersionTests(r);
            SaveTests(r);
            GateTests(r);
            r.Section("");
        }

        const string T = AppInfo.TermsVersion, P = AppInfo.PrivacyVersion, RU = AppInfo.RulesVersion;

        // ------------------------------------------------------------------ 読む
        static void LoadTests(SelfTestRunner r)
        {
            r.Section("consent 読む");
            string dir = r.NewDir("consent");

            r.Test("無い", () =>
            {
                var c = Consent.Load(Path.Combine(dir, "nope.json"));
                r.Check("ファイルが無ければ同意していない", !c.Agreed);
                r.Check("無い記録は今の版を覆わない", !c.Covers(T, P, RU));
            });

            r.Test("ゴミ", () =>
            {
                string p = SelfTestRunner.Touch(Path.Combine(dir, "junk.json"), "{ これは JSON では");
                var c = Consent.Load(p);
                r.Check("読めない記録は同意していない", !c.Agreed);
            });

            r.Test("空の JSON", () =>
            {
                string p = SelfTestRunner.Touch(Path.Combine(dir, "empty.json"), "{}");
                var c = Consent.Load(p);
                r.Check("agreed が無ければ同意していない", !c.Agreed);
            });

            r.Test("agreed が false", () =>
            {
                string p = SelfTestRunner.Touch(Path.Combine(dir, "no.json"),
                    "{\"agreed\":false,\"terms\":\"" + T + "\",\"privacy\":\"" + P + "\",\"rules\":\"" + RU + "\"}");
                var c = Consent.Load(p);
                r.Check("false は同意していない", !c.Agreed);
                r.Check("版がそろっていても覆わない", !c.Covers(T, P, RU));
            });

            r.Test("そろっている", () =>
            {
                string p = SelfTestRunner.Touch(Path.Combine(dir, "ok.json"),
                    "{\"agreed\":true,\"terms\":\"" + T + "\",\"privacy\":\"" + P + "\",\"rules\":\"" + RU + "\"," +
                    "\"chatTranslate\":\"on\",\"autoReport\":\"off\"}");
                var c = Consent.Load(p);
                r.Check("同意している", c.Agreed);
                r.Check("今の版を覆う", c.Covers(T, P, RU));
                r.Equal("chatTranslate", "on", c.ChatTranslate);
                r.Equal("autoReport", "off", c.AutoReport);
            });

            r.Test("知らない値は off に倒す", () =>
            {
                string p = SelfTestRunner.Touch(Path.Combine(dir, "odd.json"),
                    "{\"agreed\":true,\"terms\":\"" + T + "\",\"privacy\":\"" + P + "\",\"rules\":\"" + RU + "\"," +
                    "\"chatTranslate\":\"yes\",\"autoReport\":\"ON\"}");
                var c = Consent.Load(p);
                r.Equal("chatTranslate は off", "off", c.ChatTranslate);
                r.Equal("autoReport は off", "off", c.AutoReport);
            });
        }

        // ------------------------------------------------------------------ 版
        static void VersionTests(SelfTestRunner r)
        {
            r.Section("consent 版");
            string dir = r.NewDir("consent-ver");

            r.Test("どれか 1 つでも古ければ画面が戻る", () =>
            {
                foreach (var which in new[] { "terms", "privacy", "rules" })
                {
                    string t = which == "terms" ? "0.1" : T;
                    string p2 = which == "privacy" ? "0.1" : P;
                    string ru = which == "rules" ? "0.1" : RU;
                    string p = SelfTestRunner.Touch(Path.Combine(dir, which + ".json"),
                        "{\"agreed\":true,\"terms\":\"" + t + "\",\"privacy\":\"" + p2 + "\",\"rules\":\"" + ru + "\"}");
                    r.Check(which + " が古いと覆わない", !Consent.Load(p).Covers(T, P, RU));
                }
            });

            r.Test("版の形", () =>
            {
                foreach (var good in new[] { "1.0", "0.9", "1", "10.20.30" })
                    r.Check("よい形: " + good, Consent.IsValidVersion(good));
                foreach (var bad in new[] { "", null, ".", "1.", ".1", "v1.0", "1.0-rc", "..", "abc",
                                            "11111111111111111" })
                    r.Check("だめな形: " + (bad ?? "null"), !Consent.IsValidVersion(bad));
            });

            r.Test("FromAnswer は同意しない答えを作らない", () =>
            {
                r.Check("agreed=false は null", Consent.FromAnswer(false, T, P, RU, "off", "off", "1.0.0", DateTime.Now) == null);
                r.Check("版がおかしければ null", Consent.FromAnswer(true, "v1", P, RU, "off", "off", "1.0.0", DateTime.Now) == null);
                var c = Consent.FromAnswer(true, T, P, RU, "on", "on", "1.0.0", DateTime.Now);
                r.Check("よい答えは作れる", c != null && c.Agreed);
                r.Equal("chatTranslate", "on", c.ChatTranslate);
                r.Equal("autoReport", "on", c.AutoReport);
                r.Check("agreedAt が入る", !string.IsNullOrEmpty(c.AgreedAt));
            });
        }

        // ------------------------------------------------------------------ 書く
        static void SaveTests(SelfTestRunner r)
        {
            r.Section("consent 書く");
            string dir = r.NewDir("consent-save");

            r.Test("書いて読み直せる", () =>
            {
                string p = Path.Combine(dir, Consent.FileName);
                var c = Consent.FromAnswer(true, T, P, RU, "off", "on", AppInfo.Version, DateTime.Now);
                c.Save(p);
                r.Check("ファイルが出来る", File.Exists(p));
                r.Check("一時ファイルが残らない", !File.Exists(p + ".tmp"));
                var back = Consent.Load(p);
                r.Check("読み直すと覆う", back.Covers(T, P, RU));
                r.Equal("autoReport", "on", back.AutoReport);
                r.Equal("client", AppInfo.Version, back.Client);
            });

            r.Test("上書きできる", () =>
            {
                string p = Path.Combine(dir, "again.json");
                Consent.FromAnswer(true, T, P, RU, "off", "off", "0.0.1", DateTime.Now).Save(p);
                Consent.FromAnswer(true, T, P, RU, "on", "on", AppInfo.Version, DateTime.Now).Save(p);
                var back = Consent.Load(p);
                r.Equal("2 回目の値になる", "on", back.ChatTranslate);
                r.Check("一時ファイルが残らない", !File.Exists(p + ".tmp"));
            });

            r.Test("BOM を付けない", () =>
            {
                string p = Path.Combine(dir, "bom.json");
                Consent.FromAnswer(true, T, P, RU, "off", "off", AppInfo.Version, DateTime.Now).Save(p);
                var b = File.ReadAllBytes(p);
                r.Check("先頭が { である", b.Length > 0 && b[0] == (byte)'{');
            });
        }

        // ------------------------------------------------------------------ 同意の前に通す命令
        static void GateTests(SelfTestRunner r)
        {
            r.Section("consent 関所");

            r.Test("同意の前に通してよい命令", () =>
            {
                foreach (var c in new[] { "consent.set", "consent.decline", "setLang", "uninstall",
                                          "window.minimize", "window.close", "window.drag", "app.quit" })
                    r.Check("通る: " + c, Bridge.BeforeConsent.Contains(c));
            });

            r.Test("同意の前に通してはいけない命令", () =>
            {
                // ここが緩むと、ページを差し替えた人が同意の前にネットを使えます（利用規約 第12条3項）
                foreach (var c in new[] { "launch", "launchWindowed", "launchVanilla", "install", "syncSteam",
                                          "checkUpdate", "aegis.rescan", "aegis.scanOnly", "aegis.events",
                                          "makeReport", "exportOne", "rebuild", "devUpdate", "settings.set",
                                          "shortcut.create", "profile.pickImage" })
                    r.Check("通らない: " + c, !Bridge.BeforeConsent.Contains(c));
            });

            r.Test("consent の 2 つは Bridge が知っている", () =>
            {
                r.Check("consent.set", Bridge.Supported.Contains("consent.set"));
                r.Check("consent.decline", Bridge.Supported.Contains("consent.decline"));
            });

            r.Test("NeedConsent の形", () =>
            {
                var m = Bridge.NeedConsent();
                r.Check("ok は false", m.ContainsKey("ok") && m["ok"] is bool ok && !ok);
                r.Equal("reason", "needConsent", m["reason"] as string);
            });

            r.Test("版の番号が 3 つとも入っている", () =>
            {
                r.Check("terms", Consent.IsValidVersion(AppInfo.TermsVersion));
                r.Check("privacy", Consent.IsValidVersion(AppInfo.PrivacyVersion));
                r.Check("rules", Consent.IsValidVersion(AppInfo.RulesVersion));
            });
        }
    }
}
