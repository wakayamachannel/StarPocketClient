// StarPocket Client - fixed names and numbers in one place (PORT-MAP 9.2 A-10: no copies scattered around).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Starpocket.Client
{
    internal static class AppInfo
    {
        /// <summary>What Task Manager, the window, the tray and every text call the app (FileDescription / ProductName).</summary>
        public const string Name = "StarPocket Client";
        public const string Company = "StarPocket Games";
        /// <summary>Moved together with StarpocketClient.csproj and app.manifest, every time. CI compares those three
        /// with EACH OTHER but has no way of knowing which number this release should carry, so a stale one passes CI
        /// in silence and then names itself in the log's first line, --action's heading, the report zip and the exe's
        /// own properties (v0.4 review: all three still said 0.3.0 in the middle of v0.4).</summary>
        public const string Version = "1.0.5";
        /// <summary>The version the UI shows in "not in this version (v0.4)" notices.</summary>
        public const string UiVersion = "1.0";

        // The three documents screen ① shows, and the version each of them carries (first-run.md 2.2 item 4).
        // These are NOT the app's version: they move when the document is changed in a way that needs asking again
        // (Terms of Use Article 26(4)), and they stay put for a typo fix (Article 26(5)). Consent.Covers compares the
        // record in consent.json with exactly these three, so a bump here is what brings the screen back.
        // 2026-09-26: published. Until now these said 0.9 / 0.9 / 0.8, and ui\legal\ held the drafts - so the screen
        // asked people to agree to a document whose own first lines read 「利用規約（案）」 and 「版: 0.9（案・まだ公開して
        // いません）」, while the site was already serving it (found by testing the v1.0.1 build). The owner confirmed the
        // text is final, so all three moved to 1.0 together with the files under ui\legal\, which are rebuilt from the
        // same drafts by legal\確定にする.pl + legal\公開用に清書する.pl. **The text of the documents did not change** -
        // only the title, the version line, the dates and the lines that described the draft marks.
        // Do not move one without the other: a version here that does not match the file on screen makes the record say
        // something that was never shown. src\SelfTest\ConsentSelfTests.cs ("同梱の文書と AppInfo の版が合っている")
        // opens the nine files next to the exe and fails if a title still carries a draft mark or a version differs.
        /// <summary>利用規約 / Terms of Use.</summary>
        public const string TermsVersion = "1.0";
        /// <summary>プライバシーポリシー / Privacy Policy.</summary>
        public const string PrivacyVersion = "1.0";
        /// <summary>遊び方のルール / Play Rules.</summary>
        public const string RulesVersion = "1.0";

        /// <summary>Taskbar identity (PORT-MAP 10; the 2026-09-23 spelling: nothing is released yet, and after a release a
        /// change here would drop pinned taskbar buttons and jump lists).</summary>
        public const string AppUserModelId = "StarPocketGames.Client";

        // named kernel objects (PORT-MAP 3.15); kernel names are case-sensitive, so these changed with the spelling
        public const string InstanceMutexName = @"Local\StarPocketGames.Client";
        public const string ShowEventName = @"Local\StarPocketGames.Client.Show";
        /// <summary>v0.4: a second start with --autolaunch asks the running Client to play (SPEC 6.4). Two events instead
        /// of a message channel: one for a normal start, one for the 1600x900 window (--windowed).</summary>
        public const string PlayEventName = @"Local\StarPocketGames.Client.Play";
        public const string PlayWindowedEventName = @"Local\StarPocketGames.Client.PlayWindowed";
        /// <summary>Held while the app runs a long task (install / sync / update check / report / uninstall). SPEC 5.1:
        /// "--action Install" and "--action Check" take it too, so the two never copy into the same folder at once.</summary>
        public const string TaskMutexName = @"Local\StarPocketGames.Client.Task";
        /// <summary>Shared with the PowerShell launcher and the mod (log housekeeping never overlaps).</summary>
        public const string LogMutexName = @"Local\PocketRolesLauncher.logs";
        /// <summary>Held by the Aegis tray (today Aegis.ps1; inside the app once Aegis is ported).</summary>
        public const string AegisMutexName = @"Local\wakayamachannel.Aegis.AntiCheat";

        // the UI inside WebView2 (SPEC 2.3): files next to the exe, mapped to a local host name, never the internet
        public const string UiHostName = "app.starpocket.local";
        public const string UiOrigin = "https://app.starpocket.local/";
        public const string UiStartPage = "https://app.starpocket.local/index.html";
        public const string UiFolderName = "ui";

        /// <summary>Microsoft's official WebView2 page (opened only when the viewer presses the button; nothing is downloaded by the app).</summary>
        public const string WebView2DownloadPage = "https://developer.microsoft.com/microsoft-edge/webview2/";

        // ---- the pages "openExternal" may open in the viewer's own browser.
        // The page NEVER hands over a URL. It sends a NAME ("discord.invite", "site", "legal.terms"…) and this table
        // turns it into an address, so a page that was changed - or a script talking to the window - cannot send
        // someone's browser wherever it likes. Every one of these is also on ShellOpen.AllowedWebPages.
        // Until 2026-09-26 openExternal was "later", so all four buttons only answered 「このページはまだ用意できていません」
        // and host-v01.js greyed them out: 「Discord は準備中です」 while the server had been up for weeks, and
        // 「利用規約（準備中）」 while the document was on the web.
        public const string DiscordInvite = "https://discord.gg/ahNvRMVeHP";
        public const string ProductSite = "https://pocketroles.starpocketgames.com/";
        /// <summary>The published legal pages. {0} is ja / zh / en - the SITE's spelling, which is not the bundled
        /// files' (those are ja / zh-CN / en). Checked against the live pages on 2026-09-26.</summary>
        public const string TermsPage = "https://starpocketgames.com/terms.{0}.html";
        public const string PrivacyPage = "https://starpocketgames.com/privacy.{0}.html";
        public const string RulesPage = "https://starpocketgames.com/rules.{0}.html";

        /// <summary>ja / zh-CN / en (the app's) -> ja / zh / en (the site's file names).</summary>
        public static string SiteLang(string lang) => lang == "zh-CN" ? "zh" : (lang == "en" ? "en" : "ja");

        /// <summary>The address behind an openExternal name, or null when the name is not one we know.</summary>
        public static string ExternalPage(string target, string lang)
        {
            string s = SiteLang(lang);
            switch (target)
            {
                case "discord.invite": return DiscordInvite;
                case "site":           return ProductSite;
                case "legal.terms":    return string.Format(TermsPage, s);
                case "legal.privacy":  return string.Format(PrivacyPage, s);
                case "legal.rules":    return string.Format(RulesPage, s);
                default:               return null;
            }
        }

        /// <summary>Every address <see cref="ExternalPage"/> can produce, for ShellOpen's allow list.</summary>
        public static IEnumerable<string> AllExternalPages()
        {
            yield return DiscordInvite;
            yield return ProductSite;
            foreach (var s in new[] { "ja", "zh", "en" })
            {
                yield return string.Format(TermsPage, s);
                yield return string.Format(PrivacyPage, s);
                yield return string.Format(RulesPage, s);
            }
        }

        // the game the mod is built for (change together when Among Us updates)
        public const string SupportedGameVersion = "2026.8.18";
        public const string BepInExVersion = "6.0.0-be.735";
        public const int AegisRuleCount = 26;
        public const string SteamAppId = "945360";

        // ---- the mod's release and BepInEx (the launcher's constants, ps1:38-50)
        /// <summary>
        /// Where the MOD comes from. This app downloads PocketRoles releases from here, so it stays the mod's repository
        /// even though this app lives somewhere else (<see cref="ClientRepo"/>).
        /// </summary>
        public const string Repo = "wakayamachannel/PocketRoles";
        public const string ApiLatest = "https://api.github.com/repos/" + Repo + "/releases/latest";

        /// <summary>
        /// Where THIS APP comes from: its own source, its own releases, and what "Apps &amp; Features" links to. Kept apart
        /// from <see cref="Repo"/> on purpose - pointing people at the mod's repository when they ask about this app sends
        /// them to the wrong issue tracker, and it is also what the code-signing application is made for.
        /// </summary>
        public const string ClientRepo = "wakayamachannel/StarPocketClient";
        public const string ClientRepoUrl = "https://github.com/" + ClientRepo;

        /// <summary>
        /// Sent with every request this app makes (the old PowerShell launcher sent PocketRolesLauncher/&lt;version&gt;).
        /// It names THIS app's repository, so whoever sees the request can find the program that made it.
        /// </summary>
        public const string UserAgent = "StarPocketClient/" + Version + " (+" + ClientRepoUrl + ")";
        public const string BepIndexUrl = "https://builds.bepinex.dev/projects/bepinex_be";
        public const string BepZipName = "BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.735.zip";
        public static readonly string[] BepUrls =
        {
            "https://builds.bepinex.dev/projects/bepinex_be/735/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.735%2B5fef357.zip",
            "https://builds.bepinex.dev/projects/bepinex_be/735/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.735+5fef357.zip",
        };

        // ---- and WHAT may come back from those addresses (the other half of the host list above).
        // The addresses say where the file is fetched from; this says which file it has to be. Everything in that zip is
        // unpacked into the game copy and loaded as code by the game, so a swapped, corrupted or half-written file must
        // never reach the folder: the SHA-256 is checked AFTER the download and BEFORE anything is unpacked
        // (src\Core\Installer.cs StepBepInEx / CheckBepZip). builds.bepinex.dev publishes no checksum of its own for
        // these bleeding-edge builds - the git commit on its page is the commit, not the zip - so this value is what the
        // official host served on the day named below, written down once so that every later install gets the same file.
        //
        // HOW TO UPDATE THIS when BepInEx puts out a new build: docs\BEPINEX-PIN.md. Short version: someone has to
        // download the new zip once and read its SHA-256; nothing here may be guessed, and a version with no line in
        // this table is REFUSED, never installed anyway.
        static readonly Dictionary<string, string> BepZipSha256 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.735+5fef357.zip - 31,305,993 bytes
            // from https://builds.bepinex.dev/projects/bepinex_be/735/ , checked 2026-09-23
            ["6.0.0-be.735"] = "9cd83eae4d47ab07e4ad7f4d98a0085f60fb4b61957857ff197c8729cf1bc483",
        };

        /// <summary>The SHA-256 the BepInEx zip of <paramref name="version"/> must have, or null when this build of the
        /// app has no pin for it. null means REFUSE: the installer neither downloads nor unpacks anything, and says so
        /// (<c>in_bep_nohash</c>). There is deliberately no "install it anyway" - a pin that is missing is a pin that
        /// was forgotten when the version was raised, and the self-test turns that into a failing build.</summary>
        public static string BepSha256(string version)
        {
            string sha;
            return version != null && BepZipSha256.TryGetValue(version, out sha) ? sha : null;
        }
        /// <summary>Steam's own page for Among Us (opened only when the viewer presses "install it in Steam").</summary>
        public const string SteamInstallUrl = "steam://install/" + SteamAppId;
        /// <summary>Plain Among Us through Steam (ps1:2002).</summary>
        public const string SteamRunGameUrl = "steam://rungameid/" + SteamAppId;

        /// <summary>The app's own data (PORT-MAP 10). Windows paths ignore case, so the folder an older build made as
        /// "StarPocket\Client" is this same folder.</summary>
        public const string DataFolderRelative = @"StarPocket\Client";
        /// <summary>Aegis data, shared with Aegis.ps1 (PORT-MAP 3.8).</summary>
        public const string AegisStateFolderRelative = @"PocketRoles\Aegis";
        /// <summary>The launcher's state file, read and written like the PowerShell launcher's (PORT-MAP 13.1).</summary>
        public const string StateFileName = "launcher-state.json";

        // ---- where a report goes (ps1:54-56). The app never sends anything itself: these only fill in a mailto: link
        // the viewer opens, and the person attaches the zip by hand (SignPath: nothing leaves the PC on its own).
        public const string MailBug = "pocketroles.report@gmail.com";
        public const string MailRequest = "pocketroles.report+request@gmail.com";
        /// <summary>Hosts and admins: a one-player evidence zip goes to the author here and nowhere else.</summary>
        public const string MailHost = "pocketroles.report+host@gmail.com";

        /// <summary>This exe's own path: what a shortcut points at and what the uninstall looks for.</summary>
        public static string ExePath()
        {
            try { return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; }
            catch (System.Exception) { return System.Reflection.Assembly.GetEntryAssembly()?.Location; }
        }
    }
}
