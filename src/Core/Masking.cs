// What must never leave this PC inside a report zip (ps1:1515-1646 Mask-Text / Mask-Secrets / Mask-Identities /
// Mask-ShortHashes / ConvertTo-ReportEvidence). The same expressions and the same replacements as the PowerShell
// launcher: a zip made by the app and one made by the launcher hide exactly the same things.
//
// Why each one:
//  - the home folder: a report should not carry the viewer's Windows account name.
//  - API keys (a UUID, with or without ":fx") and Discord webhook URLs: a log line or a stack trace can carry one.
//  - PUIDs (32 hex digits) and friend codes (name#1234): /ban without days and the permission lists log them as they
//    are, and both name a real account.
//  - an erase code (16 of BDFGHJKMNPRSTVXZ): the mod never logs one, but a [Debug] WireLog session logs the /cmd id
//    reply like every other chat line.
//  - the mod's short "hash 1a2b3c4d…": 8 digits of a friend code's hash are enough to find it again.
// The evidence records are masked differently (ConvertToReportEvidence): their 64-digit hashes and the erase code must
// stay whole for the ban console, but the friend code's hash is replaced by the PUID's.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text.RegularExpressions;

namespace Starpocket.Client.Core
{
    internal static class Mask
    {
        const RegexOptions Opt = RegexOptions.CultureInvariant;
        static readonly Regex ApiKey = new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(:fx)?", Opt);
        static readonly Regex Webhook = new Regex("https?://(?:[a-z0-9-]+\\.)*discord(?:app)?\\.com/api/(?:v\\d+/)?webhooks/[^\\s\"'<>]+", Opt | RegexOptions.IgnoreCase);
        static readonly Regex Puid = new Regex("(?<![0-9A-Za-z])[0-9a-fA-F]{32}(?![0-9A-Za-z])", Opt);
        // the lookbehind is the launcher's, character for character (ps1:1562): a fullwidth ＃ is NOT in it, so a line
        // like "名前＃Taro#1234" still has its friend code masked. (v0.3 review: an extra ＃ here masked less than the
        // launcher does, and a friend code could reach a report zip.)
        static readonly Regex FriendCode = new Regex("(?<![0-9A-Za-z_#])[A-Za-z][A-Za-z0-9]{1,24}[#＃][0-9]{4}(?![0-9])", Opt);
        static readonly Regex EraseCode = new Regex("(?<![0-9A-Za-z])[BDFGHJKMNPRSTVXZ]{4}([ -]?[BDFGHJKMNPRSTVXZ]{4}){3}(?![0-9A-Za-z])", Opt);
        static readonly Regex ShortHash = new Regex("(?<=\\bhash )[0-9a-fA-F]{8}(?=…)", Opt);
        static readonly Regex EvidenceHash = new Regex("\"hash\"\\s*:\\s*\"([0-9a-fA-F]{64})?\"", Opt);
        static readonly Regex EvidencePuid = new Regex("\"puidHash\"\\s*:\\s*\"([0-9a-fA-F]{64})?\"", Opt);

        /// <summary>
        /// The home folder, wherever it appears (ps1 Mask-Text). <paramref name="userProfile"/> is %USERPROFILE%.
        /// <para>Both spellings go: the long one AND the 8.3 short one (C:\Users\SOMEON~1). A path can reach a log in its
        /// short form - %TEMP% is one way - and masking only the long form left the first six letters of the account name
        /// in the report a player hands to the author (found while testing the report zip, 2026-09-26).</para>
        /// </summary>
        public static string Home(string text, string userProfile)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(userProfile)) return text;
            try
            {
                text = Regex.Replace(text, Regex.Escape(userProfile), "%USERPROFILE%", RegexOptions.IgnoreCase);
                string shortHome = ShortHome(userProfile);
                if (!string.IsNullOrEmpty(shortHome))
                    text = Regex.Replace(text, Regex.Escape(shortHome), "%USERPROFILE%", RegexOptions.IgnoreCase);
                return text;
            }
            catch (Exception) { return text; }
        }

        /// <summary>The 8.3 form of the home folder, worked out once. null when the volume has short names switched off
        /// (then nothing was ever written that way) or when Windows will not say.</summary>
        static string shortHomeOf, shortHomeIs;
        static string ShortHome(string userProfile)
        {
            if (!string.Equals(shortHomeOf, userProfile, StringComparison.OrdinalIgnoreCase))
            {
                shortHomeOf = userProfile;
                shortHomeIs = Native.TryGetShortPath(userProfile);
            }
            return shortHomeIs;
        }

        /// <summary>API keys, Discord webhook URLs and the home folder (ps1 Mask-Secrets).</summary>
        public static string Secrets(string text, string userProfile)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = ApiKey.Replace(text, "<api-key-masked>");
            text = Webhook.Replace(text, "<discord-webhook-masked>");
            return Home(text, userProfile);
        }

        /// <summary>PUIDs, friend codes, erase codes and short hashes in a log (ps1 Mask-Identities). Never used on an
        /// evidence record.</summary>
        public static string Identities(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Puid.Replace(text, "<puid-masked>");
            text = FriendCode.Replace(text, "<friend-code-masked>");
            text = EraseCode.Replace(text, "<erase-code-masked>");
            return ShortHashes(text);
        }

        /// <summary>The mod's "hash 1a2b3c4d…" (ps1 Mask-ShortHashes).</summary>
        public static string ShortHashes(string text) =>
            string.IsNullOrEmpty(text) ? text : ShortHash.Replace(text, "********");

        /// <summary>Everything a log file in a zip gets: secrets first, then identities.</summary>
        public static string LogText(string text, string userProfile) => Identities(Secrets(text, userProfile));

        /// <summary>An evidence record for a zip (ps1 ConvertTo-ReportEvidence): "player.hash" (the friend code's hash,
        /// which can be tried back to the friend code because the salt is public and a friend code is a word plus four
        /// digits) becomes the PUID's hash, which cannot. null when the file is not in the shape the mod writes
        /// (exactly one "hash" and one "puidHash") - such a file is left out of the zip.</summary>
        public static string ConvertToReportEvidence(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var hm = EvidenceHash.Matches(text);
            var pm = EvidencePuid.Matches(text);
            if (hm.Count != 1 || pm.Count != 1) return null;
            string puid = pm[0].Groups[1].Value.ToLowerInvariant();
            text = text.Substring(0, hm[0].Index) + "\"hash\": \"" + puid + "\"" + text.Substring(hm[0].Index + hm[0].Length);
            return ShortHashes(text);
        }
    }
}
