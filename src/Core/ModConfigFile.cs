// src\Core\ModConfigFile.cs - MOD の BepInEx 設定ファイル（jp.pocketroles.mod.cfg）の、[節] の中の 1 つのキーだけを
// 書き換える。ほかの行は 1 バイトも触らない。
//
//   - 2026-09-27 に足しました。最初の同意画面の 2 つの答え（チャット翻訳・自動通報）は consent.json に書かれるだけで、
//     MOD 側には一度も届いていませんでした（src\Core\Consent.cs のコメントは「インストールの時に書く」と言っていたのに、
//     書く側が無かった）。実際この PC では「自動通報はしない」と答えた記録があるのに [AntiCheat] AutoReport = true の
//     ままでした。同意という一番信用が要る画面で選ばせておいて効いていない、という形だったので塞ぎます。
//   - BepInEx の .cfg は BOM なし UTF-8 です。読むのも書くのも UTF8Encoding(false) で行い、PowerShell の
//     Get-Content / Set-Content 相当のことは絶対にしません（日本語のコメント行が壊れます）。
//   - 改行は元のファイルに合わせます（CRLF のファイルは CRLF のまま）。
//   - 書く時は settings.json と同じ手順（.tmp に書いて File.Replace / Move）。途中で落ちても半分の .cfg は残りません。
//   - 節もキーも無い時は、末尾に足します。ファイルが無い時は、その 1 節だけの小さなファイルを作ります。
//     BepInEx は起動時に残りの既定値とコメントを自分で書き戻すので、これで足ります。
//   - 失敗しても投げません。ログに残して false を返すだけです（インストールが済んだのに、設定 1 行のために
//     「失敗しました」と言うのは間違いなので）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Starpocket.Client.Core
{
    /// <summary><see cref="ModConfigFile.Apply"/> の答え（2026-10-03）。「同じ値だった」と「書けなかった」を分ける。
    /// 崩す係 4: 「無かったので作った」（Created）も「別の値から変えた」（Changed）と分ける。初めてのインストールでは MOD の cfg がまだ無く、
    /// 作っただけなのに「変えました」の札が全員に出ていた。知らせは Changed の時だけ。</summary>
    internal enum SetOutcome { Unchanged, Changed, Created, Failed }

    internal static class ModConfigFile
    {
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>[<paramref name="section"/>] の中の <paramref name="key"/> の値。
        /// ファイル・節・キーのどれかが無ければ null。</summary>
        public static string Read(string path, string section, string key)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var lines = SplitLines(File.ReadAllText(path, Encoding.UTF8));
                int start, end;
                if (!FindSection(lines, section, out start, out end)) return null;
                int at = FindKey(lines, key, start + 1, end);
                if (at < 0) return null;
                int eq = lines[at].IndexOf('=');
                return lines[at].Substring(eq + 1).Trim();
            }
            catch (Exception) { return null; }
        }

        /// <summary>[<paramref name="section"/>] の <paramref name="key"/> を <paramref name="value"/> にする。
        /// 実際に書いた時（変えた・無かったので作った）だけ true。すでに同じ値なら何も書かずに false。**書けなかった時も false**なので、分けたい所は
        /// <see cref="Apply"/> を使う（2026-10-03 公開前の粗探し 3: Installer.ApplyConsentToMod が、書けなかったのに「書いた」印を付けていた）。</summary>
        public static bool Set(string path, string section, string key, string value, Action<string> log)
        {
            var o = Apply(path, section, key, value, log);
            return o == SetOutcome.Changed || o == SetOutcome.Created;
        }

        /// <summary>
        /// <see cref="Set"/> の 4 値の形（2026-10-03）: Changed = 別の値から書き換えた、Created = ファイル・節・キーが無かったので足した
        /// （崩す係 4。初めてのインストールはこれ）、Unchanged = すでにその値だった（何も書いていない）、
        /// Failed = 書けなかった（読み取り専用・掴まれている・場所が無い。ログに理由を残す）。投げない。
        /// </summary>
        public static SetOutcome Apply(string path, string section, string key, string value, Action<string> log)
        {
            log = log ?? (_ => { });
            string tmp = null;
            try
            {
                if (string.IsNullOrEmpty(path)) { log("mod config: no path"); return SetOutcome.Failed; }
                string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
                var outcome = SetOutcome.Created;
                // 改行は元のファイルに合わせる。空のファイル（これから作る）だけ Windows の既定（CRLF）にする。
                // Environment.NewLine を LF のファイルにも使うと、1 行直すだけで全行が CRLF になってしまう。
                string eol = text.Length == 0 ? Environment.NewLine
                           : text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
                bool endsWithEol = text.Length > 0 && (text[text.Length - 1] == '\n' || text[text.Length - 1] == '\r');
                var lines = SplitLines(text);
                if (endsWithEol && lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);

                int start, end;
                if (FindSection(lines, section, out start, out end))
                {
                    int at = FindKey(lines, key, start + 1, end);
                    if (at >= 0)
                    {
                        int eq = lines[at].IndexOf('=');
                        string right = lines[at].Substring(eq + 1);
                        if (right.Trim() == value) return SetOutcome.Unchanged;   // すでにその値
                        int lead = 0;
                        while (lead < right.Length && (right[lead] == ' ' || right[lead] == '\t')) lead++;
                        lines[at] = lines[at].Substring(0, eq + 1) + right.Substring(0, lead) + value;
                        outcome = SetOutcome.Changed;   // 別の値から変えた（これだけが「変えました」の知らせになる）
                    }
                    else
                    {
                        // 節はあるがキーが無い: 節の最後の中身のある行のすぐ下に入れる（後ろの空行は空行のまま残す）
                        int put = end;
                        while (put - 1 > start && lines[put - 1].Trim().Length == 0) put--;
                        lines.Insert(put, key + " = " + value);
                    }
                }
                else
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length != 0) lines.Add("");
                    lines.Add("[" + section + "]");
                    lines.Add(key + " = " + value);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                tmp = path + ".tmp";
                File.WriteAllText(tmp, string.Join(eol, lines.ToArray()) + eol, Utf8NoBom);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                tmp = null;
                log("mod config: [" + section + "] " + key + " = " + value + (outcome == SetOutcome.Created ? " (added; it was not there)" : ""));
                return outcome;
            }
            catch (Exception ex)
            {
                log("mod config: [" + section + "] " + key + ": " + ex.Message);
                return SetOutcome.Failed;
            }
            finally
            {
                if (tmp != null) { try { File.Delete(tmp); } catch (Exception) { } }
            }
        }

        static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
        }

        /// <summary>最初に出てくる [section] の行番号（start）と、その次の節の行番号（end。無ければ行数）。</summary>
        static bool FindSection(List<string> lines, string section, out int start, out int end)
        {
            start = -1; end = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.Length < 2 || t[0] != '[' || t[t.Length - 1] != ']') continue;
                string name = t.Substring(1, t.Length - 2).Trim();
                if (start < 0)
                {
                    if (string.Equals(name, section, StringComparison.OrdinalIgnoreCase)) start = i;
                    continue;
                }
                end = i;
                return true;
            }
            return start >= 0;
        }

        /// <summary>from 以上 to 未満で「key = …」の行。コメント（# / ##）は飛ばす。</summary>
        static int FindKey(List<string> lines, string key, int from, int to)
        {
            for (int i = from; i < to && i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0 || t[0] == '#' || t[0] == ';') continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                if (string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }
    }
}
