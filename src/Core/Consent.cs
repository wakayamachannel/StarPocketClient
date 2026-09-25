// The answer of the first-run screen ①, kept in consent.json next to settings.json (first-run.md 2.5).
//
// Why this file exists at all: Terms of Use Article 12(3) promises that the official Client touches the network for the
// FIRST time only after someone pressed 「同意する」. Up to v1.0.0 that promise was not kept - ClientApp.Start() called
// aegis.Start() straight away, which fetches the definitions file. This class is the record that lets the app know
// whether it may do that, and it is deliberately the ONLY thing the app reads before it is allowed to go online.
//
// What it is not: it is not a licence check and not an account. Nothing here is ever sent anywhere (Privacy Policy 3.10).
// A missing, unreadable or malformed file simply means "has not agreed yet", which is the safe answer: the app then asks
// again instead of quietly going online. "agreed" is always true when the file exists, because declining writes nothing
// at all and closes the app (first-run.md 2.4).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Starpocket.Client.Core
{
    /// <summary>What the person answered on the first-run screen, and whether that answer still covers today's documents.</summary>
    internal sealed class Consent
    {
        public const string FileName = "consent.json";

        /// <summary>True only when the file said so. Declining writes no file, so this is true for every file that exists.</summary>
        public bool Agreed;
        /// <summary>The version of each document that was on screen when they agreed ("1.0"). Empty when unknown.</summary>
        public string Terms = "", Privacy = "", Rules = "";
        /// <summary>When they pressed the button, as the app wrote it (round-trip "o" format). Empty when unknown.</summary>
        public string AgreedAt = "";
        /// <summary>The two opt-in answers of screen ① ("on" / "off"). They are written into the mod's config when the mod
        /// is installed (first-run.md 2.5), so they are kept here even though the mod holds the real switch.</summary>
        public string ChatTranslate = "off", AutoReport = "off";
        /// <summary>Which build wrote the record. Only for reading a support zip later; nothing depends on it.</summary>
        public string Client = "";

        public static string PathIn(string dataDir) { return Path.Combine(dataDir ?? "", FileName); }

        /// <summary>Anything that is not a clean record reads as "not agreed". Never throws.</summary>
        public static Consent Load(string path)
        {
            var c = new Consent();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return c;
                var obj = Json.TryParseObject(Json.ReadUtf8File(path));
                if (obj == null) return c;
                c.Agreed = obj.TryGetValue("agreed", out var a) && a is bool ab && ab;
                c.Terms = Json.Str(obj, "terms") ?? "";
                c.Privacy = Json.Str(obj, "privacy") ?? "";
                c.Rules = Json.Str(obj, "rules") ?? "";
                c.AgreedAt = Json.Str(obj, "agreedAt") ?? "";
                c.ChatTranslate = Normalize(Json.Str(obj, "chatTranslate"));
                c.AutoReport = Normalize(Json.Str(obj, "autoReport"));
                c.Client = Json.Str(obj, "client") ?? "";
            }
            catch (Exception) { return new Consent(); }
            return c;
        }

        static string Normalize(string v) { return v == "on" ? "on" : "off"; }

        /// <summary>A version string the app is willing to write down: "1.0", "1.0.1", "0.9". Anything else (a page that
        /// made one up, a stray path, a very long string) is refused, so consent.json can never hold something the
        /// version comparison below would then have to guess about.</summary>
        public static bool IsValidVersion(string v)
        {
            if (string.IsNullOrEmpty(v) || v.Length > 16) return false;
            bool digitSeen = false;
            foreach (char ch in v)
            {
                if (ch >= '0' && ch <= '9') { digitSeen = true; continue; }
                if (ch == '.') continue;
                return false;
            }
            return digitSeen && v[0] != '.' && v[v.Length - 1] != '.';
        }

        /// <summary>Does this record cover the documents the app is shipping today? Every one of the three has to match
        /// exactly. A newer document means screen ① comes back as the "新しくなりました" screen (first-run.md 5), which is
        /// what Terms of Use Article 26(4) promises for an important change - so this deliberately does NOT try to be
        /// clever about "only the patch number changed". The documents carry one version each; if a change is small
        /// enough not to need asking again, it does not get a new version (Terms of Use Article 26(5)).</summary>
        public bool Covers(string terms, string privacy, string rules)
        {
            return Agreed
                && !string.IsNullOrEmpty(terms) && Terms == terms
                && !string.IsNullOrEmpty(privacy) && Privacy == privacy
                && !string.IsNullOrEmpty(rules) && Rules == rules;
        }

        /// <summary>Writes through a temp file, then replaces (a crash never leaves half a record, and half a record
        /// reads as "not agreed" anyway). Throws on failure - the caller must not carry on as if the answer was kept.</summary>
        public void Save(string path)
        {
            var obj = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["agreed"] = Agreed,
                ["terms"] = Terms,
                ["privacy"] = Privacy,
                ["rules"] = Rules,
                ["agreedAt"] = AgreedAt,
                ["chatTranslate"] = ChatTranslate,
                ["autoReport"] = AutoReport,
                ["client"] = Client,
            };
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Json.Serialize(obj), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        /// <summary>Builds the record for an answer that just came from screen ①. Returns null when the page sent
        /// something the app will not write down (a version it does not ship, or agreed = false - declining writes
        /// nothing at all). The caller checks for null and refuses the invoke.</summary>
        public static Consent FromAnswer(bool agreed, string terms, string privacy, string rules,
                                         string chatTranslate, string autoReport, string clientVersion, DateTime now)
        {
            if (!agreed) return null;
            if (!IsValidVersion(terms) || !IsValidVersion(privacy) || !IsValidVersion(rules)) return null;
            return new Consent
            {
                Agreed = true,
                Terms = terms,
                Privacy = privacy,
                Rules = rules,
                AgreedAt = now.ToString("o"),
                ChatTranslate = Normalize(chatTranslate),
                AutoReport = Normalize(autoReport),
                Client = clientVersion ?? "",
            };
        }
    }
}
