// MOD の設定ファイル（jp.pocketroles.mod.cfg）を 1 キーだけ書き換える道具の自己診断（src\Core\ModConfigFile.cs）と、
// 最初の同意画面の答えをそこへ届ける所（Installer.ApplyConsentToMod）。
//
// なぜここを見張るか（2026-09-27）:
//   同意画面の「チャット翻訳」「自動通報」の答えは consent.json に書かれるだけで、MOD には一度も届いていませんでした。
//   この PC では「自動通報はしない」と答えた記録があるのに [AntiCheat] AutoReport = true のままでした。
//   同意という一番信用が要る画面で、選ばせておいて効いていない、という形です。届くようにしたので、見張りを置きます。
//
// いちばん怖いのは「別の節の同名キーを書き換えてしまう」ことです。本物の cfg には Enabled というキーが
// [Cosmetics] [General] [Hotkeys] [Translate] の 4 つの節にあります。間違えると、翻訳を切ったつもりで
// MOD 全体が止まります。だからそこを厚く見ます。
//
// 窓もネットも使いません。<work>\modcfg の中でファイルを読み書きするだけです。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;
using Starpocket.Client.Core;

namespace Starpocket.Client.SelfTest
{
    internal static class ModConfigSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            SetTests(r);
            SectionTests(r);
            EncodingTests(r);
            ConsentTests(r);
            r.Section("");
        }

        /// <summary>本物の cfg の形をまねた小さな写し。Enabled が 3 つの節にあるのが肝。</summary>
        const string Sample =
            "## Settings file was created by plugin PocketRoles\r\n" +
            "\r\n" +
            "[AntiCheat]\r\n" +
            "\r\n" +
            "## v0.5.5: 確実な検知で出した人を Among Us にも通報する\r\n" +
            "# Setting type: Boolean\r\n" +
            "# Default value: false\r\n" +
            "AutoReport = true\r\n" +
            "Detect = true\r\n" +
            "\r\n" +
            "[Cosmetics]\r\n" +
            "Enabled = true\r\n" +
            "\r\n" +
            "[General]\r\n" +
            "Enabled = true\r\n" +
            "Language = ja\r\n" +
            "\r\n" +
            "[Translate]\r\n" +
            "Enabled = false\r\n" +
            "Provider = auto\r\n";

        static string Write(SelfTestRunner r, string dir, string name, string text)
        {
            string p = Path.Combine(dir, name);
            File.WriteAllText(p, text, new UTF8Encoding(false));
            return p;
        }

        static string ReadRaw(string p) { return File.ReadAllText(p, Encoding.UTF8); }

        // ------------------------------------------------------------------ 1 キーだけ変える
        static void SetTests(SelfTestRunner r)
        {
            r.Section("mod cfg 書く");
            string dir = r.NewDir("modcfg");

            r.Test("値が変わる", () =>
            {
                string p = Write(r, dir, "a.cfg", Sample);
                r.Check("書き換えた と返る", ModConfigFile.Set(p, "AntiCheat", "AutoReport", "false", null));
                r.Equal("値", "false", ModConfigFile.Read(p, "AntiCheat", "AutoReport"));
            });

            r.Test("ほかの行は 1 文字も変わらない", () =>
            {
                string p = Write(r, dir, "b.cfg", Sample);
                ModConfigFile.Set(p, "AntiCheat", "AutoReport", "false", null);
                string want = Sample.Replace("AutoReport = true", "AutoReport = false");
                r.Equal("ファイル全体", want, ReadRaw(p));
            });

            r.Test("同じ値なら書かない", () =>
            {
                string p = Write(r, dir, "c.cfg", Sample);
                var before = File.GetLastWriteTimeUtc(p);
                r.Check("変えていない と返る", !ModConfigFile.Set(p, "AntiCheat", "AutoReport", "true", null));
                r.Equal("中身", Sample, ReadRaw(p));
                r.Check("書き込んでいない", File.GetLastWriteTimeUtc(p) == before);
            });

            r.Test("コメントの中の同名キーに騙されない", () =>
            {
                string p = Write(r, dir, "d.cfg", "[X]\r\n# AutoReport = true\r\n## Default value: false\r\nAutoReport = true\r\n");
                ModConfigFile.Set(p, "X", "AutoReport", "false", null);
                r.Equal("コメントは残る・値だけ変わる",
                    "[X]\r\n# AutoReport = true\r\n## Default value: false\r\nAutoReport = false\r\n", ReadRaw(p));
            });

            r.Test("キーの前後の書き方を保つ", () =>
            {
                string p = Write(r, dir, "e.cfg", "[X]\r\nKey   =   old\r\n");
                ModConfigFile.Set(p, "X", "Key", "new", null);
                r.Equal("空白はそのまま", "[X]\r\nKey   =   new\r\n", ReadRaw(p));
            });
        }

        // ------------------------------------------------------------------ 節の探し方（ここが本丸）
        static void SectionTests(SelfTestRunner r)
        {
            r.Section("mod cfg 節");
            string dir = r.NewDir("modcfg-sec");

            r.Test("同じ名前のキーが 3 つの節にあっても、狙った節だけ変わる", () =>
            {
                string p = Write(r, dir, "many.cfg", Sample);
                ModConfigFile.Set(p, "Translate", "Enabled", "true", null);
                r.Equal("Translate は変わった", "true", ModConfigFile.Read(p, "Translate", "Enabled"));
                r.Equal("Cosmetics はそのまま", "true", ModConfigFile.Read(p, "Cosmetics", "Enabled"));
                r.Equal("General はそのまま", "true", ModConfigFile.Read(p, "General", "Enabled"));
                r.Equal("General の言語はそのまま", "ja", ModConfigFile.Read(p, "General", "Language"));
                r.Check("General の Enabled の行は 1 つだけ",
                    ReadRaw(p).Contains("[General]\r\nEnabled = true\r\nLanguage = ja"));
            });

            r.Test("節はあるがキーが無い: その節の中に入る", () =>
            {
                string p = Write(r, dir, "nokey.cfg", "[A]\r\nOne = 1\r\n\r\n[B]\r\nTwo = 2\r\n");
                ModConfigFile.Set(p, "A", "New", "x", null);
                r.Equal("A の中の、後ろの空行より前", "[A]\r\nOne = 1\r\nNew = x\r\n\r\n[B]\r\nTwo = 2\r\n", ReadRaw(p));
                r.Equal("B には入っていない", null, ModConfigFile.Read(p, "B", "New"));
            });

            r.Test("節が無い: 末尾に足す", () =>
            {
                string p = Write(r, dir, "nosec.cfg", "[A]\r\nOne = 1\r\n");
                ModConfigFile.Set(p, "Z", "Key", "v", null);
                r.Equal("節ごと足される", "[A]\r\nOne = 1\r\n\r\n[Z]\r\nKey = v\r\n", ReadRaw(p));
            });

            r.Test("ファイルが無い: 作る", () =>
            {
                string p = Path.Combine(dir, "new", "made.cfg");
                r.Check("作った と返る", ModConfigFile.Set(p, "Translate", "Enabled", "true", null));
                r.Check("ファイルがある", File.Exists(p));
                r.Equal("読み返せる", "true", ModConfigFile.Read(p, "Translate", "Enabled"));
            });

            r.Test("節の名前は大文字小文字を区別しない", () =>
            {
                string p = Write(r, dir, "case.cfg", Sample);
                ModConfigFile.Set(p, "translate", "enabled", "true", null);
                r.Equal("同じ節が使われた", "true", ModConfigFile.Read(p, "Translate", "Enabled"));
                r.Check("節が増えていない", !ReadRaw(p).Contains("[translate]"));
            });

            r.Test("無い物を読むと null", () =>
            {
                string p = Write(r, dir, "read.cfg", Sample);
                r.Equal("無い節", null, ModConfigFile.Read(p, "Nope", "Enabled"));
                r.Equal("無いキー", null, ModConfigFile.Read(p, "General", "Nope"));
                r.Equal("無いファイル", null, ModConfigFile.Read(Path.Combine(dir, "nope.cfg"), "General", "Enabled"));
            });
        }

        // ------------------------------------------------------------------ 文字コードと改行
        static void EncodingTests(SelfTestRunner r)
        {
            r.Section("mod cfg 文字");
            string dir = r.NewDir("modcfg-enc");

            r.Test("日本語のコメントが壊れない", () =>
            {
                string jp = "[Translate]\r\n## チャットの翻訳を使う（既定は切）\r\nEnabled = false\r\n";
                string p = Write(r, dir, "jp.cfg", jp);
                ModConfigFile.Set(p, "Translate", "Enabled", "true", null);
                r.Equal("コメントはそのまま", jp.Replace("Enabled = false", "Enabled = true"), ReadRaw(p));
            });

            r.Test("BOM を書かない", () =>
            {
                string p = Write(r, dir, "bom.cfg", "[A]\r\nK = 1\r\n");
                ModConfigFile.Set(p, "A", "K", "2", null);
                var b = File.ReadAllBytes(p);
                r.Check("先頭が EF BB BF ではない", !(b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF));
            });

            r.Test("CRLF のファイルは CRLF のまま", () =>
            {
                string p = Write(r, dir, "crlf.cfg", "[A]\r\nK = 1\r\nL = 2\r\n");
                ModConfigFile.Set(p, "A", "K", "9", null);
                string got = ReadRaw(p);
                r.Equal("中身", "[A]\r\nK = 9\r\nL = 2\r\n", got);
                r.Check("裸の LF が無い", got.Replace("\r\n", "").IndexOf('\n') < 0);
            });

            r.Test("LF のファイルは LF のまま", () =>
            {
                string p = Write(r, dir, "lf.cfg", "[A]\nK = 1\nL = 2\n");
                ModConfigFile.Set(p, "A", "K", "9", null);
                r.Equal("中身", "[A]\nK = 9\nL = 2\n", ReadRaw(p));
            });

            r.Test("壊れたファイルでも投げない", () =>
            {
                string p = Write(r, dir, "junk.cfg", "これは設定ファイルではありません\r\n[[[\r\n= = =\r\n");
                bool threw = false;
                try { ModConfigFile.Set(p, "A", "K", "1", null); } catch (Exception) { threw = true; }
                r.Check("例外は出ない", !threw);
                r.Equal("足した物は読める", "1", ModConfigFile.Read(p, "A", "K"));
            });
        }

        // ------------------------------------------------------------------ 同意の答えが MOD まで届く
        static void ConsentTests(SelfTestRunner r)
        {
            r.Section("mod cfg 同意");
            string dir = r.NewDir("modcfg-consent");

            Func<string, string, string, string> consentJson = (at, tr, ar) =>
                "{\"agreed\":true,\"terms\":\"" + AppInfo.TermsVersion + "\",\"privacy\":\"" + AppInfo.PrivacyVersion +
                "\",\"rules\":\"" + AppInfo.RulesVersion + "\",\"agreedAt\":\"" + at +
                "\",\"chatTranslate\":\"" + tr + "\",\"autoReport\":\"" + ar + "\"}";

            Func<string, string, Installer> make = (name, json) =>
            {
                string game = Path.Combine(dir, name, "game");
                string data = Path.Combine(dir, name, "data");
                Directory.CreateDirectory(Path.Combine(game, @"BepInEx\config"));
                Directory.CreateDirectory(data);
                File.WriteAllText(Path.Combine(game, @"BepInEx\config\jp.pocketroles.mod.cfg"), Sample, new UTF8Encoding(false));
                if (json != null) File.WriteAllText(Consent.PathIn(data), json, new UTF8Encoding(false));
                return new Installer
                {
                    Paths = ModPaths.For(game),
                    OriginDir = data,
                    State = LauncherStateFile.InMemory(),
                    Log = _ => { },
                };
            };

            r.Test("答えが MOD に届く", () =>
            {
                var i = make("ok", consentJson("2026-09-26T01:42:18.9477163+09:00", "on", "off"));
                i.ApplyConsentToMod();
                r.Equal("翻訳は入", "true", ModConfigFile.Read(i.Paths.CfgPath, "Translate", "Enabled"));
                r.Equal("自動通報は切", "false", ModConfigFile.Read(i.Paths.CfgPath, "AntiCheat", "AutoReport"));
                r.Equal("ほかの節の Enabled は無事", "true", ModConfigFile.Read(i.Paths.CfgPath, "General", "Enabled"));
                r.Equal("いつの答えを書いたか残る", "2026-09-26T01:42:18.9477163+09:00", i.State.Str("consentAppliedFor"));
            });

            r.Test("同じ答えでは二度書かない（ホストが後で変えた物を戻さない）", () =>
            {
                string at = "2026-09-26T01:42:18.9477163+09:00";
                var i = make("twice", consentJson(at, "on", "off"));
                i.ApplyConsentToMod();
                ModConfigFile.Set(i.Paths.CfgPath, "Translate", "Enabled", "false", null);   // ホストが /opt で切った
                i.ApplyConsentToMod();
                r.Equal("ホストの選択が生きている", "false", ModConfigFile.Read(i.Paths.CfgPath, "Translate", "Enabled"));
            });

            r.Test("同意を取り直したら、新しい答えで書く", () =>
            {
                var i = make("again", consentJson("2026-09-26T01:42:18.9477163+09:00", "on", "off"));
                i.ApplyConsentToMod();
                File.WriteAllText(Consent.PathIn(i.OriginDir),
                    consentJson("2026-10-01T10:00:00.0000000+09:00", "off", "on"), new UTF8Encoding(false));
                i.ApplyConsentToMod();
                r.Equal("翻訳は切", "false", ModConfigFile.Read(i.Paths.CfgPath, "Translate", "Enabled"));
                r.Equal("自動通報は入", "true", ModConfigFile.Read(i.Paths.CfgPath, "AntiCheat", "AutoReport"));
            });

            r.Test("同意していなければ何も書かない", () =>
            {
                var i = make("none", null);
                i.ApplyConsentToMod();
                r.Equal("cfg はそのまま", Sample, File.ReadAllText(i.Paths.CfgPath, Encoding.UTF8));
                r.Check("印も付かない", string.IsNullOrEmpty(i.State.Str("consentAppliedFor")));
            });
        }
    }
}
