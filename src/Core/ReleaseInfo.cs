// The mod's latest release on GitHub (the launcher's Get-LatestRelease / Normalize-Version / Test-ModCurrent /
// Get-FileSourceKey, ps1:559-567, 744-772, 941-953), read in-process from the public API - the same decisions and the
// same words for every answer (404 / 403 / no asset / no network).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Starpocket.Client.Core
{
    internal sealed class ReleaseInfo
    {
        public bool Ok;
        /// <summary>"asset" (the release has no PocketRoles-&lt;ver&gt;.zip) or the exception's message.</summary>
        public string Error = "";
        /// <summary>The HTTP status when there was one (404 no release, 403 / 429 rate limit).</summary>
        public int Status;
        public string Tag, HtmlUrl, AssetUrl, AssetName, AssetKey;
        public Version Version;

        // the launcher's -match / -notmatch ignore case (PowerShell's default), so these do too: "pocketroles-0.5.6.zip"
        // is the mod, and "PocketRoles-0.5.6-setup.zip" is not (v0.3 review: a case-sensitive port took the setup zip)
        static readonly Regex AssetName_ = new Regex(@"^PocketRoles-[\w.\-]+\.zip$", RegexOptions.IgnoreCase);
        /// <summary>The launcher's "Setup" test, case-insensitive like its -match.</summary>
        public static bool LooksLikeSetup(string name) => !string.IsNullOrEmpty(name) && name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0;
        static readonly Regex VersionIn = new Regex(@"\d+(\.\d+){1,3}");

        /// <summary>Normalize-Version: "v0.4.0", "0.4.0.0", "PocketRoles-0.5.5.zip" -&gt; 0.4.0 / 0.5.5 (3 parts); null when
        /// there is no number in it.</summary>
        public static Version Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var m = VersionIn.Match(s);
            if (!m.Success) return null;
            var parts = new List<string>(m.Value.Split('.'));
            while (parts.Count < 3) parts.Add("0");
            try { return new Version(parts[0] + "." + parts[1] + "." + parts[2]); }
            catch (Exception) { return null; }
        }

        /// <summary>Test-ModCurrent: the installed build is current when it is newer than what is on offer, or the same
        /// version AND it came from that very file (launcher-state.json modSource) - so a zip published again under the
        /// same number is installed.</summary>
        public static bool ModCurrent(Version installed, Version available, string sourceKey, string stateModSource)
        {
            if (installed == null || available == null) return false;
            if (installed > available) return true;
            if (installed < available) return false;
            return !string.IsNullOrEmpty(sourceKey) && sourceKey == stateModSource;
        }

        /// <summary>Get-FileSourceKey: which file on this PC a build came from.</summary>
        public static string FileSourceKey(string prefix, string path)
        {
            try
            {
                var fi = new FileInfo(path);
                return prefix + ":" + fi.Name + ":" + fi.Length.ToString(CultureInfo.InvariantCulture) + ":" + fi.LastWriteTimeUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            }
            catch (Exception) { return prefix + ":" + path; }
        }

        /// <summary>Get-LatestRelease. Never throws: a failure comes back in Error / Status.</summary>
        public static ReleaseInfo Latest(IWebFetch web)
        {
            var r = new ReleaseInfo();
            try
            {
                var o = Json.TryParseObject(web.GetText(AppInfo.ApiLatest));
                if (o == null) { r.Error = "the answer of the GitHub API could not be read"; return r; }
                r.Tag = Json.Str(o, "tag_name");
                r.Version = Normalize(r.Tag);
                r.HtmlUrl = Json.Str(o, "html_url");
                object assets;
                Dictionary<string, object> asset = null;
                if (o.TryGetValue("assets", out assets) && assets is System.Collections.IEnumerable list)
                {
                    foreach (var a in list)
                    {
                        var m = a as Dictionary<string, object>;
                        string name = Json.Str(m, "name");
                        if (name == null || !AssetName_.IsMatch(name) || LooksLikeSetup(name)) continue;
                        asset = m;
                        break;
                    }
                }
                if (asset == null || r.Version == null) { r.Error = "asset"; return r; }
                r.AssetUrl = Json.Str(asset, "browser_download_url");
                r.AssetName = Json.Str(asset, "name");
                object size;
                asset.TryGetValue("size", out size);
                // identifies this exact build (a zip published again under the same version number is installed)
                r.AssetKey = "gh:" + r.AssetName + ":" + (size == null ? "" : Convert.ToString(size, CultureInfo.InvariantCulture)) + ":" + Json.Str(asset, "updated_at");
                r.Ok = !string.IsNullOrEmpty(r.AssetUrl);
                if (!r.Ok) r.Error = "asset";
                return r;
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp != null) { try { r.Status = (int)resp.StatusCode; } catch (Exception) { } }
                r.Error = ex.Message;
                return r;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                return r;
            }
        }

        /// <summary>Log-ReleaseError: the launcher's words for this failure.</summary>
        public string ErrorText(string lang)
        {
            if (Error == "asset") return S.T(lang, "up_asset_missing", Tag ?? "");
            if (Status == 404) return S.T(lang, "up_norelease");
            if (Status == 403 || Status == 429) return S.T(lang, "up_ratelimit");
            return S.T(lang, "up_api_fail", Error ?? "");
        }
    }
}
