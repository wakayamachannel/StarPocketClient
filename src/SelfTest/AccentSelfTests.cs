// The Client's accent colour (src\Core\AccentColor.cs) and the settings key that keeps it.
//
// Nothing here is looked at by eye: every row is a measured WCAG contrast ratio. The two things this suite must not
// let slip are
//   1. an unreadable colour getting through - for EVERY colour on the wheel, not only the ready-made ones, and
//   2. this file and the page's JavaScript copy (design\launcher-proto\index.html, const ACC) drifting apart.
// (2) is caught by the Table below: tools\uitest\checks.js checks the page's copy against the same numbers, so either
// copy wandering off fails its own suite.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client.SelfTest
{
    internal static class AccentSelfTests
    {
        // The surfaces the rules are about (the same ones AccentColor works from).
        static readonly int[] Navy = { 0x1E, 0x22, 0x57 };
        static readonly int[] White = { 0xFF, 0xFF, 0xFF };
        static readonly int[] LightMin = { 0xE0, 0xE2, 0xEA };   // --rail / --col: the darkest light surface
        static readonly int[] DarkMax = { 0x30, 0x30, 0x30 };    // --panel-3: the lightest dark surface
        static readonly int[] Hero = { 0x0B, 0x0C, 0x22 };       // the hero: dark in both themes, PLAY sits on it

        /// <summary>Every colour the page reserves for "something is wrong" (its --danger tokens and the state lamp).
        /// No accent value may be one of these, or a red mark would mean two different things at once.</summary>
        static readonly string[] Danger = { "#B02230", "#A8202E", "#E63946", "#C22F3C", "#FF7A85", "#D4303E" };

        /// <summary>The hues those six sit on, 352 to 357, with a little room either side. This is what no accent value
        /// may come out as. AccentColor's own band (345..20) is deliberately wider: it moves colours that merely
        /// NEIGHBOUR the danger red as well, which leaves this narrower one room for the one degree a lightness step
        /// can shift a hue by when it rounds to bytes and back.</summary>
        static bool IsRed(int hue) => hue >= 350 || hue <= 10;

        /// <summary>What every colour must come out as. tools\uitest\checks.js holds the same table for the page's own
        /// copy of these steps; if either copy changes, that copy's suite fails and says which value moved.</summary>
        static readonly string[][] Table =
        {
            //  in,        fill,      hi,        deep,      sub,       ink,       l-line,    l-text,    d-line,    d-text
            new[]{ "#F7C548","#F7C548","#F9D271","#F6BC2D","#FADB8F","#1E2257","#A67907","#7F5C06","#F7C548","#F7C548" },
            new[]{ "#2EC4B6","#2EC4B6","#45D3C6","#28A99D","#5ED9CE","#1E2257","#218C82","#1A7068","#2EC4B6","#2EC4B6" },
            // navy: a bottom shade darker than the fill would sink into the hero, so the gradient keeps a flat bottom
            new[]{ "#4D55C2","#4D55C2","#636ACA","#4D55C2","#333994","#FFFFFF","#4D55C2","#4D55C2","#6B72CC","#8D92D8" },
            new[]{ "#8E4EC6","#8E4EC6","#9559CA","#813DBD","#69329A","#FFFFFF","#8E4EC6","#8642C2","#9A61CC","#B387D8" },
            new[]{ "#F05A8C","#F05A8C","#F481A7","#F0578A","#F69DBB","#1E2257","#ED3673","#C4124D","#F05A8C","#F16997" },
            new[]{ "#2FA56B","#2FA56B","#39C681","#2EA36A","#50CD90","#1E2257","#298F5D","#206F48","#2FA56B","#31AB6F" },
            new[]{ "#F4822A","#F4822A","#F69950","#F2710D","#F7AA6E","#1E2257","#CC5F0B","#A04A08","#F4822A","#F4822A" },
            // the three the owner asked to be sure about: pure white, pure black and a flat yellow
            new[]{ "#FFFFFF","#FFFFFF","#FFFFFF","#F0F0F0","#FFFFFF","#1E2257","#808080","#636363","#FFFFFF","#FFFFFF" },
            new[]{ "#000000","#616161","#757575","#616161","#3D3D3D","#FFFFFF","#000000","#000000","#7A7A7A","#999999" },
            new[]{ "#FFFF00","#FFFF00","#FFFF29","#E0E000","#FFFF47","#1E2257","#858500","#666600","#FFFF00","#FFFF00" },
            // StarPocket navy: too dark to show on the hero PLAY sits on, so it is brightened and the person is told
            new[]{ "#1E2257","#4D55C2","#636ACA","#4D55C2","#333994","#FFFFFF","#1E2257","#1E2257","#6B72CC","#8D92D8" },
            // the crewmate's red, picked as an accent: never refused, but moved out of the danger red's band - "it
            // failed" and "it stopped" are that red, and the accent may not be it as well
            new[]{ "#E63946","#E21D6F","#E21D6F","#C71A62","#A31550","#FFFFFF","#E63981","#BE185D","#E63981","#ED6EA3" },
            // pure red, and the dark theme's own --danger-text: the same band, the same way out
            new[]{ "#FF0000","#E6005F","#E6005F","#C70052","#9E0041","#FFFFFF","#FA0068","#C70053","#FF006A","#FF5CA0" },
            new[]{ "#FF6B78","#FF6BA9","#FF94C1","#FF4D97","#FFB3D3","#1E2257","#FA0069","#C70053","#FF6BA9","#FF6BA9" },
        };

        static readonly string[] Keys =
        {
            "--acc-fill", "--acc-hi", "--acc-deep", "--acc-sub", "--acc-ink",
            "--acc-l-line", "--acc-l-text", "--acc-d-line", "--acc-d-text",
        };

        static string R(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        public static void Run(SelfTestRunner r)
        {
            Table_(r);
            Rules(r);
            Guard(r);
            Settings(r);
            Boot(r);
        }

        // ------------------------------------------------------------------ the shared table
        static void Table_(SelfTestRunner r)
        {
            r.Section("accent");
            foreach (var row in Table)
            {
                var vars = AccentColor.Vars(row[0]);
                for (int i = 0; i < Keys.Length; i++)
                {
                    string got;
                    vars.TryGetValue(Keys[i], out got);
                    r.Equal(row[0] + " " + Keys[i], row[i + 1], got);
                }
            }
            r.Equal<int>("every colour gives all eleven values", AccentColor.Names.Length, AccentColor.Vars("#2EC4B6").Count);
            r.Check("the soft wash is the fill at .22 / .16",
                AccentColor.Vars("#2EC4B6")["--acc-l-soft"] == "rgba(46,196,182,.22)"
                && AccentColor.Vars("#2EC4B6")["--acc-d-soft"] == "rgba(46,196,182,.16)",
                AccentColor.Vars("#2EC4B6")["--acc-l-soft"] + " / " + AccentColor.Vars("#2EC4B6")["--acc-d-soft"]);
        }

        // ------------------------------------------------------------------ what must hold for ANY colour
        /// <summary>Every colour the rules are checked against: the whole hue wheel at several lightnesses and
        /// saturations, the greys, the ready-made ones and the awkward ones. 400+ colours, not a hand-picked few.</summary>
        static IEnumerable<string> EveryColour()
        {
            foreach (var p in AccentColor.Presets) yield return p;
            foreach (var x in new[] { "#FFFFFF", "#000000", "#FFFF00", "#00FF00", "#00FFFF", "#FF00FF", "#FF0000",
                "#0000FF", "#1E2257", "#E63946", "#808080", "#010101", "#FEFEFE", "#7F7F7F" }) yield return x;
            for (int h = 0; h < 360; h += 15)
                foreach (int l in new[] { 8, 25, 50, 75, 92 })
                    foreach (int s in new[] { 20, 60, 100 })
                        yield return HslHex(h / 360.0, s / 100.0, l / 100.0);
        }

        static string HslHex(double h, double s, double l)
        {
            Func<double, double, double, double> hue = (p, q, t) =>
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
                if (t < 0.5) return q;
                if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
                return p;
            };
            double q2 = l < 0.5 ? l * (1 + s) : l + s - l * s, p2 = 2 * l - q2;
            Func<double, int> b = v => (int)Math.Floor(v * 255 + 0.5);
            return AccentColor.Hex(new[] { b(hue(p2, q2, h + 1.0 / 3.0)), b(hue(p2, q2, h)), b(hue(p2, q2, h - 1.0 / 3.0)) });
        }

        static void Rules(SelfTestRunner r)
        {
            r.Section("accent rules");
            // one failure line per rule, naming the first colour that broke it: 400 colours must not be 400 lines
            string inkFill = null, inkHi = null, inkDeep = null, inkSub = null, heroFill = null;
            string lLine = null, lText = null, dLine = null, dText = null, hueKept = null;
            string stillRed = null, isDanger = null, quietRed = null;
            int n = 0, reds = 0;
            foreach (var hex in EveryColour())
            {
                n++;
                var p = AccentColor.Derive(hex);
                // red is never the accent: no value may be left in the danger band, or be a danger colour itself
                bool wasRed = AccentColor.IsDangerHue(HueOf(AccentColor.Parse(hex)));
                if (wasRed) reds++;
                foreach (var c in new[] { p.Fill, p.Hi, p.Deep, p.Sub, p.LightLine, p.LightText, p.DarkLine, p.DarkText })
                {
                    // a grey has no hue to move, and a grey is nobody's idea of red
                    if (stillRed == null && Chroma(c) > 8 && IsRed(HueOf(c))) stillRed = hex + " -> " + AccentColor.Hex(c);
                    if (isDanger == null && Array.IndexOf(Danger, AccentColor.Hex(c)) >= 0) isDanger = hex + " -> " + AccentColor.Hex(c);
                }
                // and it is never done silently
                if (quietRed == null && wasRed && Chroma(AccentColor.Parse(hex)) > 8 && !p.HueMoved) quietRed = hex;
                Check(ref inkFill, hex, AccentColor.Contrast(p.Fill, p.Ink), 4.5);
                Check(ref inkHi, hex, AccentColor.Contrast(p.Hi, p.Ink), 4.5);
                Check(ref inkDeep, hex, AccentColor.Contrast(p.Deep, p.Ink), 4.5);
                Check(ref inkSub, hex, AccentColor.Contrast(p.Sub, p.Ink), 4.5);
                Check(ref heroFill, hex, AccentColor.Contrast(p.Fill, Hero), 3.0);
                Check(ref lLine, hex, AccentColor.Contrast(p.LightLine, LightMin), 3.0);
                Check(ref lText, hex, AccentColor.Contrast(p.LightText, LightMin), 4.5);
                Check(ref dLine, hex, AccentColor.Contrast(p.DarkLine, DarkMax), 3.0);
                Check(ref dText, hex, AccentColor.Contrast(p.DarkText, DarkMax), 4.5);
                // outside the red band only the lightness may move: a grey stays grey, and a colour keeps the order
                // of its channels
                if (hueKept == null && !p.HueMoved && !SameHueOrder(AccentColor.Parse(hex), p.Fill)) hueKept = hex;
            }
            r.Info("accent: " + n + " colours measured (the whole hue wheel, the greys and the ready-made ones), "
                + reds + " of them in the danger red's band");
            r.Check("the text on a filled surface reads (>= 4.5)", inkFill == null, inkFill);
            r.Check("... and on the top of its gradient", inkHi == null, inkHi);
            r.Check("... and on the bottom of its gradient", inkDeep == null, inkDeep);
            r.Check("... and on the small pill inside PLAY", inkSub == null, inkSub);
            r.Check("the filled surface shows on the hero (>= 3)", heroFill == null, heroFill);
            r.Check("lines show on the darkest light surface (>= 3)", lLine == null, lLine);
            r.Check("text reads on the darkest light surface (>= 4.5)", lText == null, lText);
            r.Check("lines show on the lightest dark surface (>= 3)", dLine == null, dLine);
            r.Check("text reads on the lightest dark surface (>= 4.5)", dText == null, dText);
            r.Check("only the lightness moves: the colour stays the person's", hueKept == null, hueKept);
            r.Check("no accent value comes out as the danger red (350..10 degrees)", stillRed == null, stillRed);
            r.Check("... and none of them IS one of the page's danger colours", isDanger == null, isDanger);
            r.Check("... and a red is never moved without saying so", quietRed == null, quietRed);

            // the ready-made colours are meant to need no moving at all
            string moved = null;
            foreach (var p in AccentColor.Presets) if (moved == null && AccentColor.Derive(p).Adjusted) moved = p;
            r.Check("every ready-made colour is taken exactly as it is", moved == null, moved);
            r.Equal("the first ready-made colour is the StarPocket gold", AccentColor.Default, AccentColor.Presets[0]);
            r.Equal("there are seven ready-made colours", 7, AccentColor.Presets.Length);
            r.Check("no ready-made colour is the crewmate's red",
                Array.IndexOf(AccentColor.Presets, "#E63946") < 0 && Array.IndexOf(AccentColor.Presets, "#B02230") < 0);
        }

        static void Check(ref string first, string hex, double got, double want)
        {
            if (first == null && got < want) first = hex + ": " + R(got) + " < " + R(want);
        }

        /// <summary>The hue in whole degrees, worked out the way AccentColor does it.</summary>
        static int HueOf(int[] c)
        {
            double r = c[0] / 255.0, g = c[1] / 255.0, b = c[2] / 255.0;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn, h = 0;
            if (d > 0)
            {
                if (mx == r) { h = (g - b) / d; if (g < b) h += 6.0; }
                else if (mx == g) h = (b - r) / d + 2.0;
                else h = (r - g) / d + 4.0;
                h /= 6.0;
            }
            return (int)Math.Floor(h * 360 + 0.5) % 360;
        }

        /// <summary>How much colour there is in it at all (0 for a grey): a grey has no hue worth talking about.</summary>
        static int Chroma(int[] c) => Math.Max(c[0], Math.Max(c[1], c[2])) - Math.Min(c[0], Math.Min(c[1], c[2]));

        /// <summary>Red bigger than green bigger than blue stays that way (the hue did not turn); grey stays grey.</summary>
        static bool SameHueOrder(int[] a, int[] b)
        {
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    if (Math.Sign(a[i].CompareTo(a[j])) != Math.Sign(b[i].CompareTo(b[j]))) return false;
            return true;
        }

        // ------------------------------------------------------------------ the guard itself
        static void Guard(SelfTestRunner r)
        {
            r.Section("accent guard");
            // what "default" means, and that nothing unreadable is ever refused outright
            r.Equal("no colour chosen means the StarPocket gold", "#F7C548",
                AccentColor.Hex(AccentColor.Derive("default").Fill));
            r.Check("a colour that is not a colour falls back to the gold rather than to nothing",
                AccentColor.Derive("not a colour").Fill[0] == 0xF7);

            // pure white: light, but navy reads on it, so it is taken as it is
            var w = AccentColor.Derive("#FFFFFF");
            r.Check("pure white is kept and given navy text", !w.Adjusted && AccentColor.Hex(w.Ink) == "#1E2257");
            r.Check("... and that text reads (" + R(AccentColor.Contrast(w.Fill, w.Ink)) + ")",
                AccentColor.Contrast(w.Fill, w.Ink) >= 4.5);
            // pure black: nothing can be read on the hero, so it is lightened and said so
            var k = AccentColor.Derive("#000000");
            r.Check("pure black is lightened rather than refused", k.Adjusted && AccentColor.Hex(k.Fill) != "#000000");
            r.Check("... and then both rules hold (" + R(AccentColor.Contrast(k.Fill, k.Ink)) + " / "
                + R(AccentColor.Contrast(k.Fill, Hero)) + ")",
                AccentColor.Contrast(k.Fill, k.Ink) >= 4.5 && AccentColor.Contrast(k.Fill, Hero) >= 3.0);
            r.Check("... and its light text is still black, which reads best of all",
                AccentColor.Hex(k.LightText) == "#000000");
            // a flat yellow: the trap the owner asked about - fine as a fill, never as text on white
            var y = AccentColor.Derive("#FFFF00");
            r.Check("a flat yellow is kept as a fill", !y.Adjusted);
            r.Check("... its text on white would be " + R(AccentColor.Contrast(AccentColor.Parse("#FFFF00"), White))
                + ", so a darker one is used instead",
                AccentColor.Contrast(AccentColor.Parse("#FFFF00"), White) < 4.5
                && AccentColor.Contrast(y.LightText, White) >= 4.5);
            r.Check("... and it is navy that goes on top of it, not white",
                AccentColor.Hex(y.Ink) == "#1E2257");

            // the crewmate's red, picked as the Client's colour: not refused, but not taken either. Red is what the
            // page draws "it failed" and "it stopped" in, and the accent being that same red left the host looking at
            // two red dots that meant opposite things (the unread mark and the stopped lamp came out byte for byte
            // the same, #E63946).
            var red = AccentColor.Derive("#E63946");
            r.Check("the crewmate's red is not refused as the Client's colour", AccentColor.Hex(red.Fill).Length == 7);
            r.Check("... but it is moved out of the danger red's band, and said so", red.HueMoved && red.Adjusted);
            r.Check("... its lines are no longer the state lamp's own red",
                AccentColor.Hex(red.LightLine) != "#E63946" && AccentColor.Hex(red.DarkLine) != "#E63946",
                AccentColor.Hex(red.LightLine) + " / " + AccentColor.Hex(red.DarkLine));
            r.Check("... and what was picked is still remembered as picked", red.Chosen == "#E63946");
            r.Check("a hue just outside the band is left alone (the ready-made pink is 340 degrees)",
                !AccentColor.Derive("#F05A8C").HueMoved && HueOf(AccentColor.Parse("#F05A8C")) == 340);
            r.Check("a grey is not 'a red' and is never turned", !AccentColor.Derive("#808080").HueMoved
                && !AccentColor.Derive("#000000").HueMoved && !AccentColor.Derive("#FFFFFF").HueMoved);
            r.Equal("the band is pushed clear of, not onto, its own edge (355 degrees goes to 335)", 335,
                HueOf(AccentColor.Parse(AccentColor.Vars("#E63946")["--acc-l-line"])));

            // the person is told exactly when, and only when, their colour was moved, and what was done to it
            r.Check("the app says so when it moves a colour", AccentColor.Derive("#1E2257").Adjusted);
            r.Check("... and says nothing when it does not", !AccentColor.Derive("#F7C548").Adjusted);
            r.Check("... and says which way it went", AccentColor.Derive("#1E2257").LightMoved
                && AccentColor.Derive("#1E2257").Lighter && !AccentColor.Derive("#F7C548").LightMoved);
            r.Check("... lighter and darker are not the same answer", AccentColor.Derive("#000000").Lighter);
            r.Equal("what was chosen is kept as it was chosen", "#1E2257", AccentColor.Derive("#1e2257").Chosen);
            foreach (var lang in new[] { "ja", "zh-CN", "en" })
            {
                string text = S.T(lang, "ac_fixed", "#1E2257", "#4D55C2");
                r.Check("the words for a moved colour (" + lang + ") name both colours",
                    text.Contains("#1E2257") && text.Contains("#4D55C2") && text.Length > 20, text);
                r.Check("the words for a red one (" + lang + ")", S.T(lang, "ac_red").Length > 10, S.T(lang, "ac_red"));
            }
        }

        // ------------------------------------------------------------------ settings.json
        static void Settings(SelfTestRunner r)
        {
            r.Section("accent setting");
            r.Check("a colour is accepted", ClientSettings.IsValidAccent("#F7C548"));
            r.Check("lower case is accepted", ClientSettings.IsValidAccent("#f7c548"));
            r.Check("\"default\" is accepted", ClientSettings.IsValidAccent("default"));
            foreach (var bad in new[] { null, "", "#F7C54", "#F7C5488", "F7C548", "#GGGGGG", "red",
                "#F7C548; background:url(x)", "javascript:1" })
                r.Check("refused: " + (bad ?? "null"), !ClientSettings.IsValidAccent(bad));

            var s = new ClientSettings();
            r.Equal("nothing chosen yet", ClientSettings.DefaultAccent, s.Accent);
            r.Check("a bad value changes nothing", !s.SetAccent("#nope") && s.Accent == ClientSettings.DefaultAccent);
            r.Check("a good one is kept in capitals", s.SetAccent("#2ec4b6") && s.Accent == "#2EC4B6");

            string dir = r.NewDir("accent"), path = Path.Combine(dir, "settings.json");
            s.Save(path);
            r.Check("it went to settings.json", File.ReadAllText(path).Contains("\"accent\":\"#2EC4B6\""));
            r.Equal("and comes back", "#2EC4B6", ClientSettings.Load(path).Accent);
            // 元にもどす: the word "default" is not written at all, so the brand colour can change later
            s.SetAccent("default");
            s.Save(path);
            r.Check("back to the first colour writes no accent at all", !File.ReadAllText(path).Contains("accent"));
            r.Equal("... and reads back as the default", ClientSettings.DefaultAccent, ClientSettings.Load(path).Accent);
            // a file someone edited by hand
            File.WriteAllText(path, "{\"close\":\"quit\",\"accent\":\"purple\"}");
            var bad2 = ClientSettings.Load(path);
            r.Equal("an accent nobody can read is ignored", ClientSettings.DefaultAccent, bad2.Accent);
            r.Equal("... and the rest of the file still works", ClientSettings.CloseQuits, bad2.Close);

            r.Check("the page may set it", Bridge.SettingKeys.Contains("accent"));
            var saved = false;
            var ok = Bridge.SetSetting(new ClientSettings(), new Dictionary<string, object> { ["key"] = "accent", ["value"] = "#2EC4B6" },
                () => { saved = true; return Bridge.Ok(); });
            r.Check("settings.set accent saves", saved && ok["ok"] is bool b1 && b1);
            saved = false;
            var no = Bridge.SetSetting(new ClientSettings(), new Dictionary<string, object> { ["key"] = "accent", ["value"] = "rgb(1,2,3)" },
                () => { saved = true; return Bridge.Ok(); });
            r.Check("a value it cannot read saves nothing", !saved && no["ok"] is bool b2 && !b2);
        }

        // ------------------------------------------------------------------ nothing flashes
        static void Boot(SelfTestRunner r)
        {
            r.Section("accent first paint");
            r.Equal("the default writes no script at all (the page's own fallbacks are the gold)", "",
                AccentColor.BootScript("default"));
            r.Equal("... and neither does no setting", "", AccentColor.BootScript(null));
            string js = AccentColor.BootScript("#2EC4B6");
            foreach (var name in AccentColor.Names)
                r.Check("the first-paint script sets " + name, js.Contains("\"" + name + "\""));
            r.Check("it sets them on <html>", js.Contains("document.documentElement.style"));
            r.Check("it cannot throw into the page", js.Contains("try{") && js.Contains("catch"));
            // the values are quoted as JSON, so nothing that reaches here can become code. Such a value never gets
            // this far anyway (ClientSettings.IsValidAccent refuses it), but the script must be safe on its own.
            string evil = AccentColor.BootScript("#F7C548\");alert(1);//");
            r.Check("a value that is not a colour writes the gold, not what was typed",
                !evil.Contains("alert") && evil.Contains("\"#F7C548\""), evil);
            r.Check("every value in the script is quoted", !js.Contains("setProperty(--"));
        }
    }
}
