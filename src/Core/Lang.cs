// Which language the app speaks (PORT-MAP 3.14; the launcher's Detect-Lang, ps1:1892-1904, plus the app's settings.json).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Starpocket.Client.Core
{
    internal static class Lang
    {
        public static readonly string[] Codes = { "ja", "zh-CN", "en" };

        /// <summary>"ja" / "zh-CN" / "en" in their own spelling when <paramref name="s"/> is one of them (any case, like
        /// PowerShell's -contains), else null.</summary>
        public static string Canonical(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            foreach (var c in Codes) if (string.Equals(c, s, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        /// <summary>The Windows display language: ja* -> ja, zh* -> zh-CN (Traditional Chinese too, PORT-MAP 9.1 L-10), else en.</summary>
        public static string FromCulture(string cultureName)
        {
            string c = cultureName ?? "";
            if (c.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
            if (c.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
            return "en";
        }

        /// <summary>settings.json "lang": auto / ja / zh-CN / en ("zh" is read as zh-CN); anything else is auto.</summary>
        public static string NormalizePref(string pref)
        {
            if (string.Equals(pref, "zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
            return Canonical(pref) ?? "auto";
        }

        /// <summary>1. --language  2. settings.json lang (not auto)  3. launcher-state.json lang  4. Windows.</summary>
        public static string Resolve(string argLanguage, string settingsPref, string stateLang, string uiCultureName)
        {
            string a = Canonical(argLanguage);
            if (a != null) return a;
            string p = NormalizePref(settingsPref);
            if (p != "auto") return p;
            string st = Canonical(stateLang);
            if (st != null) return st;
            return FromCulture(uiCultureName);
        }

        /// <summary>The UI's own tags: ja / zh / en.</summary>
        public static string ToUi(string code) => code == "zh-CN" ? "zh" : (code == "en" ? "en" : "ja");

        /// <summary>The UI's preference (auto / ja / zh / en) as stored in settings.json (auto / ja / zh-CN / en); null when invalid.</summary>
        public static string PrefFromUi(string ui)
        {
            switch (ui)
            {
                case "auto": return "auto";
                case "ja": return "ja";
                case "zh": return "zh-CN";
                case "en": return "en";
                default: return null;
            }
        }

        public static string PrefToUi(string pref) => pref == "auto" ? "auto" : ToUi(pref);

        /// <summary>The Windows UI font for the language (the Japanese fonts have no Simplified Chinese glyphs).</summary>
        public static string UiFontName(string code) => code == "zh-CN" ? "Microsoft YaHei UI" : code == "en" ? "Segoe UI" : "Yu Gothic UI";
    }
}
