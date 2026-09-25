// The Client's accent colour (the owner, 2026-09-23: 「クライアントの色は好きに変えれるって言う風にしない？」).
//
// The person picks one colour. Everything the page needs is worked out from it here, in ONE place, so the first paint
// (MainForm injects it before any of the page's own script runs) and a live change say exactly the same thing.
// Nothing is guessed: every value is stepped until a measured WCAG contrast ratio is met.
//
//   --acc-fill  the filled surface (PLAY, the primary button, the NEW badge, the "推奨 / 快適" bands)
//   --acc-hi    the top of a filled gradient          --acc-deep  its bottom
//   --acc-sub   the small pill inside PLAY            --acc-ink   the text ON a filled surface (navy or white)
//   --acc-l-line / --acc-d-line   2 px lines, rings and dots, per theme
//   --acc-l-text / --acc-d-text   text and icons with no fill under them, per theme
//   --acc-l-soft / --acc-d-soft   a wash of the colour, per theme
//
// What must hold (this is what AccentSelfTests measures):
//   ink on fill / hi / deep / sub  >= 4.5   (body text)
//   fill on the hero (#0B0C22)     >= 3.0   (PLAY always sits on the dark hero)
//   line on #E0E2EA (light) / #303030 (dark)  >= 3.0   (the darkest light and lightest dark surface)
//   text on the same two surfaces             >= 4.5
// Red is never an accent: "something is wrong" keeps its own --danger tokens (the page's CSS). That is not only a
// comment now - a colour whose hue is the danger red's is moved out of its band (AwayFromRed below), because the
// accent and "it failed" being the same red would leave the host no way to tell an unread mark from a stopped one.
//
// The person's colour is moved by LIGHTNESS ONLY (and, in the red band, by hue), one percent at a time, and only as
// far as it must be - so what they chose is still the colour they see. When the colour had to move, the app says so
// in their own language (ac_fixed / ac_red).
// The page (design\launcher-proto\index.html) has the same steps in JavaScript for the prototype; both are measured
// against the same table of colours, so they cannot drift apart unnoticed.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starpocket.Client.Core
{
    internal static class AccentColor
    {
        /// <summary>The StarPocket default: gold. Stored as "default", so a later change of the brand colour follows.</summary>
        public const string Default = "#F7C548";

        /// <summary>The presets the settings page offers, in order; the first one is the default.</summary>
        public static readonly string[] Presets =
        {
            "#F7C548",   // 金      gold (default)
            "#2EC4B6",   // ティール teal
            "#4D55C2",   // こん色   navy
            "#8E4EC6",   // すみれ   violet
            "#F05A8C",   // もも     pink
            "#2FA56B",   // みどり   green
            "#F4822A",   // だいだい orange
        };

        // The surfaces that decide everything. The strictest one of each kind is enough: meet it and every other
        // surface of that theme is met too.
        static readonly int[] Navy = { 0x1E, 0x22, 0x57 };    // --navy: the dark ink
        static readonly int[] White = { 0xFF, 0xFF, 0xFF };   // the light ink
        static readonly int[] LightMin = { 0xE0, 0xE2, 0xEA }; // --rail / --col: the darkest light surface
        static readonly int[] DarkMax = { 0x30, 0x30, 0x30 };  // --panel-3: the lightest dark surface
        static readonly int[] Hero = { 0x0B, 0x0C, 0x22 };     // the hero, dark in both themes; PLAY sits on it

        // ------------------------------------------------------------------ colour maths (sRGB, WCAG 2.x)
        /// <summary>"#RRGGBB" (or "default") to three 0-255 values; null when it is neither.</summary>
        public static int[] Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            if (hex == "default") hex = Default;
            if (hex.Length != 7 || hex[0] != '#') return null;
            var c = new int[3];
            for (int i = 0; i < 3; i++)
            {
                int v;
                if (!int.TryParse(hex.Substring(1 + i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return null;
                c[i] = v;
            }
            return c;
        }

        public static string Hex(int[] c) => "#" + c[0].ToString("X2", CultureInfo.InvariantCulture)
            + c[1].ToString("X2", CultureInfo.InvariantCulture) + c[2].ToString("X2", CultureInfo.InvariantCulture);

        static double Channel(int v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        /// <summary>Relative luminance (WCAG 2.x).</summary>
        public static double Luminance(int[] c) => 0.2126 * Channel(c[0]) + 0.7152 * Channel(c[1]) + 0.0722 * Channel(c[2]);

        /// <summary>Contrast ratio between two opaque colours, 1.0 to 21.0.</summary>
        public static double Contrast(int[] a, int[] b)
        {
            double la = Luminance(a), lb = Luminance(b);
            if (la < lb) { double t = la; la = lb; lb = t; }
            return (la + 0.05) / (lb + 0.05);
        }

        /// <summary><paramref name="fg"/> at <paramref name="alpha"/> over <paramref name="bg"/> (what the screen shows).</summary>
        public static int[] Over(int[] fg, int[] bg, double alpha)
        {
            var o = new int[3];
            for (int i = 0; i < 3; i++) o[i] = (int)Math.Floor(fg[i] * alpha + bg[i] * (1 - alpha) + 0.5);
            return o;
        }

        // HSL, so only the lightness moves and the person's hue stays theirs.
        static double[] ToHsl(int[] c)
        {
            double r = c[0] / 255.0, g = c[1] / 255.0, b = c[2] / 255.0;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            double l = (mx + mn) / 2.0, h = 0, s = 0, d = mx - mn;
            if (d > 0)
            {
                s = l > 0.5 ? d / (2.0 - mx - mn) : d / (mx + mn);
                if (mx == r) { h = (g - b) / d; if (g < b) h += 6.0; }
                else if (mx == g) h = (b - r) / d + 2.0;
                else h = (r - g) / d + 4.0;
                h /= 6.0;
            }
            return new[] { h, s, l };
        }

        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }

        static int Byte(double v) => (int)Math.Floor(v * 255 + 0.5);

        static int[] FromHsl(double h, double s, double l)
        {
            if (s <= 0) { int v = Byte(l); return new[] { v, v, v }; }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            return new[] { Byte(Hue(p, q, h + 1.0 / 3.0)), Byte(Hue(p, q, h)), Byte(Hue(p, q, h - 1.0 / 3.0)) };
        }

        /// <summary>The same colour at another lightness (0-100, whole percent, so C# and the page agree exactly).</summary>
        static int[] AtL(int[] c, int pct)
        {
            var x = ToHsl(c);
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            return FromHsl(x[0], x[1], pct / 100.0);
        }

        static int L0(int[] c) => (int)Math.Floor(ToHsl(c)[2] * 100 + 0.5);

        /// <summary>The hue in whole degrees, so C# and the page land on exactly the same one.</summary>
        static int H0(int[] c) => (int)Math.Floor(ToHsl(c)[0] * 360 + 0.5) % 360;

        // ------------------------------------------------------------------ the red band
        // Every danger colour of the page (--danger #B02230, --danger-text #A8202E / #FF7A85, --danger-dot #E63946,
        // and the mod's own "stopped / failed" red) sits at hue 352-357. The band 345..20 degrees is therefore closed
        // to the accent. A colour inside it is never refused: its hue is moved to RedClear degrees CLEAR of the band
        // (335 or 30, whichever is nearer), so what comes out is plainly outside it rather than sitting on its edge.
        // A grey has no hue to move, so FromHsl gives back the same grey and nothing is claimed to have changed.
        const int RedBandLo = 345, RedBandHi = 20, RedClear = 10;

        /// <summary>True when this hue (whole degrees) is the danger red's own.</summary>
        public static bool IsDangerHue(int hue) => hue > RedBandLo || hue < RedBandHi;

        /// <summary>The same colour with its hue moved clear of the danger band; unchanged when it is not in it.</summary>
        static int[] AwayFromRed(int[] c)
        {
            int h = H0(c);
            if (!IsDangerHue(h)) return c;
            var x = ToHsl(c);
            int down = h > RedBandLo ? h - RedBandLo : h + 360 - RedBandLo;
            int up = h < RedBandHi ? RedBandHi - h : RedBandHi + 360 - h;
            int target = down <= up ? RedBandLo - RedClear : RedBandHi + RedClear;
            return FromHsl(target / 360.0, x[1], x[2]);
        }

        // ------------------------------------------------------------------ the steps
        /// <summary>Navy or white, whichever reads better on <paramref name="fill"/>.</summary>
        static int[] PickInk(int[] fill) => Contrast(fill, Navy) >= Contrast(fill, White) ? Navy : White;

        static bool FillOk(int[] c) =>
            Math.Max(Contrast(c, Navy), Contrast(c, White)) >= 4.5 && Contrast(c, Hero) >= 3.0;

        /// <summary>The filled surface: the person's colour when it works, else the nearest lightness that does
        /// (lighter wins a tie, because the hero it sits on is dark).</summary>
        static int[] FitFill(int[] baseColor)
        {
            if (FillOk(baseColor)) return baseColor;
            int l0 = L0(baseColor);
            for (int d = 1; d <= 100; d++)
                foreach (int dir in new[] { 1, -1 })
                {
                    int l = l0 + dir * d;
                    if (l < 0 || l > 100) continue;
                    var c = AtL(baseColor, l);
                    if (FillOk(c)) return c;
                }
            return Parse(Default);   // cannot happen for a real colour; the brand default rather than something unreadable
        }

        /// <summary>Steps one way until the wanted ratio against <paramref name="surface"/> is reached.</summary>
        static int[] Fit(int[] baseColor, int[] surface, double want, int dir)
        {
            if (Contrast(baseColor, surface) >= want) return baseColor;
            int l0 = L0(baseColor);
            for (int l = l0 + dir; l >= 0 && l <= 100; l += dir)
            {
                var c = AtL(baseColor, l);
                if (Contrast(c, surface) >= want) return c;
            }
            return dir < 0 ? new[] { 0, 0, 0 } : new[] { 255, 255, 255 };
        }

        /// <summary>A shade of the fill, <paramref name="dl"/> percent away, pulled back until the ink still reads on it
        /// (and, for the darker end, until it still shows on the hero).</summary>
        static int[] Shade(int[] fill, int[] ink, int dl, bool onHero)
        {
            int l0 = L0(fill), target = Math.Max(0, Math.Min(100, l0 + dl)), step = target < l0 ? 1 : -1;
            for (int l = target; ; l += step)
            {
                if (l == l0) return fill;   // back where we started: no shade that works, so the fill itself
                var c = AtL(fill, l);
                if (Contrast(c, ink) >= 4.5 && (!onHero || Contrast(c, Hero) >= 3.0)) return c;
            }
        }

        /// <summary>The small pill inside PLAY: the fill moved AWAY from the ink, so it can only read better, never worse.</summary>
        static int[] SubShade(int[] fill, int[] ink) => AtL(fill, L0(fill) + (Luminance(ink) < 0.5 ? 14 : -14));

        // ------------------------------------------------------------------ the answer
        internal sealed class Palette
        {
            public int[] Fill, Hi, Deep, Sub, Ink, LightLine, LightText, DarkLine, DarkText;
            /// <summary>The hue was in the danger red's band and was moved out of it (ac_red).</summary>
            public bool HueMoved;
            /// <summary>The lightness had to move for the text to read on it (ac_fixed).</summary>
            public bool LightMoved;
            /// <summary>The colour that is used is lighter than the one that was picked (only read with LightMoved).</summary>
            public bool Lighter;
            /// <summary>The colour used is not the colour picked: the person is told, in their own language.</summary>
            public bool Adjusted;
            /// <summary>What they picked, as they picked it.</summary>
            public string Chosen;
        }

        /// <summary>Works out every value from one colour. "default" (or anything unreadable as a colour) is the brand gold.</summary>
        public static Palette Derive(string hex)
        {
            var picked = Parse(hex) ?? Parse(Default);
            // the hue first (red is not the accent's to take), then the lightness: everything below is worked out from
            // the colour that came out of the band, so no value of the eleven is left inside it
            var baseColor = AwayFromRed(picked);
            bool hueMoved = Hex(baseColor) != Hex(picked);
            var fill = FitFill(baseColor);
            var ink = PickInk(fill);
            return new Palette
            {
                Chosen = Hex(picked),
                Fill = fill,
                Hi = Shade(fill, ink, 8, false),
                Deep = Shade(fill, ink, -6, true),
                Sub = SubShade(fill, ink),
                Ink = ink,
                LightLine = Fit(baseColor, LightMin, 3.0, -1),
                LightText = Fit(baseColor, LightMin, 4.5, -1),
                DarkLine = Fit(baseColor, DarkMax, 3.0, 1),
                DarkText = Fit(baseColor, DarkMax, 4.5, 1),
                HueMoved = hueMoved,
                LightMoved = Hex(fill) != Hex(baseColor),
                Lighter = Luminance(fill) > Luminance(baseColor),
                Adjusted = Hex(fill) != Hex(picked),
            };
        }

        static string Soft(int[] c, string alpha) =>
            "rgba(" + c[0].ToString(CultureInfo.InvariantCulture) + "," + c[1].ToString(CultureInfo.InvariantCulture)
            + "," + c[2].ToString(CultureInfo.InvariantCulture) + "," + alpha + ")";

        /// <summary>The custom properties the page reads, in the order the page's own fallbacks list them.</summary>
        public static Dictionary<string, string> Vars(string hex)
        {
            var p = Derive(hex);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["--acc-fill"] = Hex(p.Fill),
                ["--acc-hi"] = Hex(p.Hi),
                ["--acc-deep"] = Hex(p.Deep),
                ["--acc-sub"] = Hex(p.Sub),
                ["--acc-ink"] = Hex(p.Ink),
                ["--acc-l-line"] = Hex(p.LightLine),
                ["--acc-l-text"] = Hex(p.LightText),
                ["--acc-l-soft"] = Soft(p.Fill, ".22"),
                ["--acc-d-line"] = Hex(p.DarkLine),
                ["--acc-d-text"] = Hex(p.DarkText),
                ["--acc-d-soft"] = Soft(p.Fill, ".16"),
            };
        }

        /// <summary>The names of the eleven properties, for the page and the self-test.</summary>
        public static readonly string[] Names =
        {
            "--acc-fill", "--acc-hi", "--acc-deep", "--acc-sub", "--acc-ink",
            "--acc-l-line", "--acc-l-text", "--acc-l-soft", "--acc-d-line", "--acc-d-text", "--acc-d-soft",
        };

        /// <summary>The script MainForm runs when the document is created - before the page's own script and before the
        /// first paint, so nobody ever sees the default colour flash past. "default" writes nothing at all: the page's
        /// own var() fallbacks already are the StarPocket gold.</summary>
        public static string BootScript(string accent)
        {
            if (accent == null || accent == "default") return "";
            var vars = Vars(accent);
            var sb = new System.Text.StringBuilder();
            sb.Append("(function(){try{var s=document.documentElement.style;");
            foreach (var kv in vars)
                sb.Append("s.setProperty(").Append(Json.Serialize(kv.Key)).Append(',').Append(Json.Serialize(kv.Value)).Append(");");
            sb.Append("}catch(e){}})();");
            return sb.ToString();
        }
    }
}
