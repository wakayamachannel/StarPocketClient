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

            // 窓の無い仕事（--action / --scan-only / --verify-download）は、上の Bridge の表をまったく通りません。
            // v1.0.0 では、そこに同意の確認が 1 つも無く、同意していない PC で --action install が
            // BepInEx と MOD を実際に落としました（2026-09-26 のテストで判明）。第12条3項が嘘になっていました。
            r.Test("窓の無い仕事: ネットに出るものは同意の前に断る", () =>
            {
                foreach (var a in new[] { "install", "check" })
                    r.Check("断る: --action " + a, Startup.NeedsConsentFirst(StartupMode.Action, a));
                r.Check("断る: --verify-download", Startup.NeedsConsentFirst(StartupMode.VerifyDownload, null));
            });

            r.Test("窓の無い仕事: この PC を見るだけのものは通す", () =>
            {
                // 断る理由がありません（何も送らない）。断ると、同意する前に「何が入っているか」を
                // 確かめることすらできなくなります。
                foreach (var a in new[] { "status", "report" })
                    r.Check("通す: --action " + a, !Startup.NeedsConsentFirst(StartupMode.Action, a));
                // --scan-only は DefinitionsStore.Load()（この PC にある定義）とゲームのフォルダを見るだけです
                r.Check("通す: --scan-only", !Startup.NeedsConsentFirst(StartupMode.ScanOnly, null));
                // アンインストールは、同意しなくても必ずできなければいけません（SignPath の条件）
                r.Check("通す: --uninstall", !Startup.NeedsConsentFirst(StartupMode.Uninstall, null));
                // 窓のある道は、ここではなく ClientApp が関所です（最初の画面を出して待ちます）
                r.Check("通す: ふつうに開く", !Startup.NeedsConsentFirst(StartupMode.Normal, null));
                r.Check("通す: --tray", !Startup.NeedsConsentFirst(StartupMode.Tray, null));
            });

            r.Test("断り文は 3 つの言葉ともある", () =>
            {
                foreach (var lang in new[] { "ja", "zh-CN", "en" })
                {
                    string m = S.T(lang, "cl_need_consent");
                    r.Check(lang + ": 空でない", !string.IsNullOrWhiteSpace(m) && m != "cl_need_consent");
                    r.Check(lang + ": どこで答えるかが書いてある", m.IndexOf("StarPocket Client", StringComparison.Ordinal) >= 0);
                    r.Check(lang + ": 12(3) を指している", m.IndexOf("12", StringComparison.Ordinal) >= 0);
                }
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

            // 2026-09-26: AppInfo が 0.9 / 0.9 / 0.8 と言い、画面に出る文書は「利用規約（案）・版: 0.9（案・まだ公開して
            // いません）」でした。**もう公開しているのに、下書きに同意を求めていました。** 番号だけは合っていたので、
            // 番号どうしを見るだけの試験では気づけません。**ファイルの中を読んで**突き合わせます。
            r.Test("同梱の文書と AppInfo の版が合っている", () =>
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), "ui", "legal");
                if (!Directory.Exists(dir)) { r.Info("ui\\legal が隣にありません（そろっているかは PackageFiles が見ます）"); return; }
                var 版 = new[] { new { doc = "terms", want = AppInfo.TermsVersion },
                                 new { doc = "privacy", want = AppInfo.PrivacyVersion },
                                 new { doc = "rules", want = AppInfo.RulesVersion } };
                foreach (var d in 版)
                    foreach (var lang in new[] { "ja", "zh-CN", "en" })
                    {
                        string p = Path.Combine(dir, d.doc + "." + lang + ".md");
                        if (!File.Exists(p)) { r.Check(d.doc + "." + lang + ": ある", false); continue; }
                        string[] lines = File.ReadAllLines(p, Encoding.UTF8);
                        string head = lines.Length > 0 ? lines[0] : "";
                        // 見出しに「案」の印が残っていないこと（下書きに同意を求めない）
                        r.Check(d.doc + "." + lang + ": 見出しが下書きのままでない  <" + head + ">",
                                head.IndexOf("（案）", StringComparison.Ordinal) < 0
                                && head.IndexOf("（草案）", StringComparison.Ordinal) < 0
                                && head.IndexOf("(draft)", StringComparison.Ordinal) < 0);
                        // 版の行（3 行目）が AppInfo と一致すること
                        string ver = null;
                        foreach (var l in lines)
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(l, @"^- (?:版|版本|Version)[:：]\s*([0-9]+\.[0-9]+)\s*$");
                            if (m.Success) { ver = m.Groups[1].Value; break; }
                            if (l.StartsWith("---", StringComparison.Ordinal)) break;
                        }
                        r.Equal(d.doc + "." + lang + ": 版が AppInfo と同じ", d.want, ver);
                    }
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
