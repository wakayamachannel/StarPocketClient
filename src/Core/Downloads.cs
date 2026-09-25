// What the app fetches for an install or an update (v0.2; the launcher's Get-Text / Download-File, ps1:658-705).
// In-process HttpWebRequest, no downloader and no shell. Only these hosts, and only when the viewer presses Install,
// Repair, "Update" or "Check for updates" (README "ネット"):
//   api.github.com        the mod's latest release (a plain GET of the public API)
//   github.com / objects. or release-assets.githubusercontent.com   the release's PocketRoles-<ver>.zip
//   builds.bepinex.dev    BepInEx 6.0.0-be.735 (the file list page and the zip)
// That list is ENFORCED here (AllowedUrl), not only written down: https only, and only those host names. What comes
// out of these downloads is unpacked into the game copy and loaded as code by the game, so a link scraped off a page
// (Installer.DownloadBepInEx) can never send us somewhere else, and a plaintext http:// link is refused - which is
// exactly what a code-signing review looks for (v0.3 review; the PowerShell launcher has no such check).
// The host list answers "where did it come from"; the BepInEx zip's pinned SHA-256 (AppInfo.BepSha256, checked by
// Installer.CheckBepZip after the download and before any unpacking) answers "and what is it" - a swapped, corrupted
// or truncated file is deleted, never extracted, and a version with no pinned hash is refused outright
// (docs\BEPINEX-PIN.md; the owner, 2026-09-23).
// Nothing about this PC is sent: a GET with the app's User-Agent, no cookies, no body, no telemetry.
// Aegis's own definitions download stays in src\Aegis\DefinitionsStore.cs (its own rules and limits).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Net;
using System.Text;

namespace Starpocket.Client.Core
{
    /// <summary>What the installer needs from the network (the self-test gives it fakes: no call ever leaves the PC).</summary>
    internal interface IWebFetch
    {
        /// <summary>The body of a GET as text (the GitHub API, the BepInEx file list). Throws like WebClient on failure.</summary>
        string GetText(string url);

        /// <summary>Downloads to <paramref name="dest"/> (through &lt;dest&gt;.part, like the launcher). progress: done, total
        /// (total is 0 when the server does not say). Throws on failure.</summary>
        void Download(string url, string dest, Action<long, long> progress);
    }

    /// <summary>The address is not one the app fetches from - either it was handed one that is not on the list, or a
    /// server on the list redirected it off the list. It is an <see cref="IOException"/> so every caller that already
    /// treats a failed fetch as a failed fetch goes on doing exactly that; it has its own name only so that the one
    /// caller who must tell the two apart can (BepVerifier: "the official site pointed somewhere else" is the thing
    /// --verify-download exists to catch, and it must not be reported as "your internet is down").</summary>
    internal sealed class AddressRefusedException : IOException
    {
        public readonly string Url;
        public AddressRefusedException(string url) : base("this address is not one the app fetches from: " + url) { Url = url; }
    }

    internal sealed class WebFetch : IWebFetch
    {
        public static readonly WebFetch Instance = new WebFetch();

        public int TextTimeoutMs = 30000;
        public int TimeoutMs = 30000;
        public int ReadWriteTimeoutMs = 60000;
        /// <summary>A hard cap so a wrong URL cannot fill the disk (the mod's zip is ~1 MB, BepInEx's ~31 MB).</summary>
        public long MaxBytes = 256L * 1024 * 1024;

        /// <summary>The only hosts the app ever fetches from. A sub-domain is NOT enough: the name must be one of these.</summary>
        public static readonly string[] AllowedHosts =
        {
            "api.github.com", "github.com",
            // GitHub hands a release file to a CDN name: "objects." was the old one, "release-assets." is what it uses
            // now (2026-09-23: a from-scratch install failed here - the redirect was refused and the mod never came down).
            "objects.githubusercontent.com", "release-assets.githubusercontent.com",
            "builds.bepinex.dev",
        };

        /// <summary>https, one of <see cref="AllowedHosts"/>, no user name or password in the URL. Everything this class
        /// fetches goes through here first, whoever handed it the address.</summary>
        public static bool AllowedUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
            if (u.Scheme != Uri.UriSchemeHttps) return false;
            if (!string.IsNullOrEmpty(u.UserInfo)) return false;
            return Array.FindIndex(AllowedHosts, h => string.Equals(h, u.Host, StringComparison.OrdinalIgnoreCase)) >= 0;
        }

        static void Check(string url)
        {
            if (!AllowedUrl(url)) throw new AddressRefusedException(url);
        }

        /// <summary>Where the answer really came from, after any redirects. BOTH ways of fetching go through here: a
        /// server on the list may not hand the app on to one that is not, whether what comes back is a file or a page
        /// of text. (Until the v0.4 review only the file half checked, and the page half is the one that reads a
        /// server nobody here controls.) A response with no address of its own is left to the caller's own Check.</summary>
        internal static void CheckArrivedFrom(Uri responseUri)
        {
            if (responseUri == null) return;
            Check(responseUri.AbsoluteUri);
        }

        public string GetText(string url)
        {
            Check(url);
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = AppInfo.UserAgent;
            req.Accept = "application/vnd.github+json, text/html, */*";
            req.Timeout = TextTimeoutMs;
            req.ReadWriteTimeout = ReadWriteTimeoutMs;
            using (var resp = req.GetResponse())
            {
                // the same rule as Download's: a redirect may not leave the list either. This half was missing until
                // the v0.4 review, and one of the two pages read here (builds.bepinex.dev's file list) is on a server
                // that is not ours - a redirect from it would have been followed, read and parsed without a word.
                CheckArrivedFrom(resp.ResponseUri);
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        public void Download(string url, string dest, Action<long, long> progress)
        {
            Check(url);
            string dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // this process's own half-file: %TEMP%\PocketRolesLauncher is shared with the PowerShell launcher, and two
            // downloads must not write the same <name>.part (GameLogs.SaveGameLog already names its temp this way)
            string tmp = dest + "." + System.Diagnostics.Process.GetCurrentProcess().Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".part";
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = AppInfo.UserAgent;
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = ReadWriteTimeoutMs;
            try
            {
                using (var resp = req.GetResponse())
                {
                    // a redirect may not leave the list either (github.com hands the release file to objects.githubusercontent.com)
                    CheckArrivedFrom(resp.ResponseUri);
                    long total = 0;
                    try { total = resp.ContentLength; } catch (Exception) { }
                    if (total > MaxBytes) throw new IOException("the file is larger than " + (MaxBytes / (1024 * 1024)) + " MB");
                    using (var input = resp.GetResponseStream())
                    using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buf = new byte[65536];
                        long done = 0;
                        int n;
                        while ((n = input.Read(buf, 0, buf.Length)) > 0)
                        {
                            output.Write(buf, 0, n);
                            done += n;
                            if (done > MaxBytes) throw new IOException("the file is larger than " + (MaxBytes / (1024 * 1024)) + " MB");
                            progress?.Invoke(done, total > 0 ? total : 0);
                        }
                    }
                }
                try { if (File.Exists(dest)) File.Delete(dest); } catch (Exception) { }
                File.Move(tmp, dest);
            }
            catch (Exception)
            {
                // never leave a half-file behind in the shared cache folder
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                throw;
            }
        }
    }
}
