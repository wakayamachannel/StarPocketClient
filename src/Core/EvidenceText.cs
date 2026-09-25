// The text rules the Aegis evidence records share with the mod and with the maintainer's ban console: how a detection
// line is normalized, which two log lines back a record, and how an erase code / evidence id / friend code typed by a
// person is read. Ported line for line from the launcher's embedded helper (ps1:1647-1800, class
// PocketRolesLauncher.Evidence), which is itself the mod's AegisPrivacyCore.EvidenceBacking / BackingDetection /
// NormLogText / NormalizeEraseCode and AegisBans.HashOf.
//
// These must not drift: the ban console's LogCheck looks for exactly these two lines, so a record whose backing lines
// were written by a different rule reads as "does not match its log".
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Starpocket.Client.Core
{
    internal static class EvidenceText
    {
        const RegexOptions Opt = RegexOptions.CultureInvariant;
        static readonly Regex WrittenRe = new Regex("AegisEvidence:\\s+(AEG-[0-9A-Za-z]{5,6})\\s+written\\s+\\(", Opt);
        static readonly Regex StampRe = new Regex("^\\d{2}:\\d{2}:\\d{2}Z (.+)$", Opt);
        static readonly Regex NormRe = new Regex("<puid-masked>|<friend-code-masked>|(?<![0-9A-Za-z])[0-9a-fA-F]{32}(?![0-9A-Za-z])|(?<![0-9A-Za-z_#])[A-Za-z][A-Za-z0-9]{1,24}[#＃][0-9]{4}(?![0-9])|(?<=\\bhash )[0-9a-fA-F*]{8}(?=…)", Opt);
        static readonly Regex IdRe = new Regex("^AEG-[0-9A-Z]{5,6}$", Opt);
        static readonly Regex FriendRe = new Regex("^[A-Za-z][A-Za-z0-9]{1,24}#[0-9]{4}$", Opt);
        /// <summary>The file name the mod writes, and the only one that is read as a record.</summary>
        public static readonly Regex RecordFileRe = new Regex("^AEG-[0-9A-Z]{5,6}\\.json$", Opt);
        const string Alphabet = "BDFGHJKMNPRSTVXZ";

        /// <summary>A log line with every identity in it turned into "#", so two lines about the same detection match
        /// whether or not they were masked on the way.</summary>
        public static string Norm(string s) => NormRe.Replace(s ?? "", "#");

        /// <summary>The detection a record is about: its last "CheatDetector:" line, normalized and cut to 160.</summary>
        public static string Detection(IList<string> trail)
        {
            if (trail == null) return "";
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                var m = StampRe.Match(trail[i] ?? "");
                if (!m.Success || !m.Groups[1].Value.StartsWith("CheatDetector:", StringComparison.Ordinal)) continue;
                string t = m.Groups[1].Value;
                if (t.EndsWith("…", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1);
                t = Norm(t);
                return t.Length > 160 ? t.Substring(0, 160) : t;
            }
            return "";
        }

        /// <summary>The two lines of a log that back each wanted record (id -&gt; its detection text): the detection line
        /// about that player, and the line saying the record was written.</summary>
        public static Dictionary<string, List<string>> Backing(IEnumerable<string> lines, IDictionary<string, string> wanted)
        {
            var found = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (lines == null || wanted == null || wanted.Count == 0) return found;
            var last = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (line.IndexOf("CheatDetector:", StringComparison.Ordinal) >= 0)
                {
                    string n = null;
                    foreach (var kv in wanted)
                    {
                        if (string.IsNullOrEmpty(kv.Value)) continue;
                        if (n == null) n = Norm(line);
                        if (n.IndexOf(kv.Value, StringComparison.Ordinal) >= 0) last[kv.Key] = line;
                    }
                }
                if (line.IndexOf("AegisEvidence:", StringComparison.Ordinal) < 0) continue;
                var m = WrittenRe.Match(line);
                if (!m.Success) continue;
                string id = m.Groups[1].Value.ToUpperInvariant();
                string det;
                if (!wanted.TryGetValue(id, out det) || found.ContainsKey(id)) continue;
                var pair = new List<string>();
                string d;
                if (!string.IsNullOrEmpty(det) && last.TryGetValue(id, out d)) pair.Add(d);
                pair.Add(line);
                found[id] = pair;
            }
            return found;
        }

        /// <summary>The lines of a log the game may still be writing (ReadWrite | Delete sharing, like the launcher).</summary>
        public static IEnumerable<string> ReadLines(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, Encoding.UTF8, true))
            {
                string line;
                while ((line = sr.ReadLine()) != null) yield return line;
            }
        }

        public static IEnumerable<string> LinesOf(TextReader r)
        {
            string line;
            while ((line = r.ReadLine()) != null) yield return line;
        }

        static bool IsSeparator(char c)
        {
            switch (c)
            {
                case ' ': case '　': case '\t': case '-': case '_': case '・':
                case 'ー': case '‐': case '‑': case '‒': case '–': case '—': case '―': case '−':
                    return true;
            }
            return false;
        }

        static char CheckLetter(string data15)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes("PocketRoles.EraseCheck.v1" + (data15 ?? "")));
                return Alphabet[h[0] >> 4];
            }
        }

        /// <summary>An erase code (/cmd id) as the player typed it, or "" when it is not one (the 16th letter is a check
        /// letter, so a typo does not turn into somebody else's code).</summary>
        public static string NormalizeEraseCode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t;
            try { t = s.Normalize(NormalizationForm.FormKC); } catch (ArgumentException) { t = s; }
            var sb = new StringBuilder(16);
            foreach (char c0 in t)
            {
                if (IsSeparator(c0)) continue;
                char c = char.ToUpperInvariant(c0);
                if (Alphabet.IndexOf(c) < 0 || sb.Length >= 16) return "";
                sb.Append(c);
            }
            if (sb.Length != 16) return "";
            string code = sb.ToString();
            return CheckLetter(code.Substring(0, 15)) == code[15] ? code : "";
        }

        /// <summary>An evidence id (AEG-XXXXX) as it was typed, or "".</summary>
        public static string EvidenceId(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t;
            try { t = s.Normalize(NormalizationForm.FormKC); } catch (ArgumentException) { t = s; }
            t = t.Trim().ToUpperInvariant();
            return IdRe.IsMatch(t) ? t : "";
        }

        /// <summary>The hash a typed friend code (name#1234) has in the records (AegisBans.HashOf), or "". The friend code
        /// itself never leaves this method.</summary>
        public static string FriendCodeHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t;
            try { t = s.Normalize(NormalizationForm.FormKC); } catch (ArgumentException) { t = s; }
            t = t.Replace('　', ' ').Trim();
            if (!FriendRe.IsMatch(t)) return "";
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes("PocketRoles.Aegis.v1" + t.ToLowerInvariant()));
                var sb = new StringBuilder(64);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
