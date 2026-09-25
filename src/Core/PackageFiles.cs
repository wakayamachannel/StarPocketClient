// The files that must sit next to "StarPocket Client.exe" (the zip extracted whole). Checked first thing at start, before
// anything loads WebView2: an exe copied on its own gets a plain "files are missing" notice, not a silent crash and not
// the "install the WebView2 runtime" window (installing the runtime would not help).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;

namespace Starpocket.Client.Core
{
    internal static class PackageFiles
    {
        public static readonly string[] Required =
        {
            "Microsoft.Web.WebView2.Core.dll",
            "Microsoft.Web.WebView2.WinForms.dll",
            "WebView2Loader.dll",
            AppInfo.UiFolderName + @"\index.html",
        };

        /// <summary>The required files not found in <paramref name="exeDir"/> (empty: all there).</summary>
        public static List<string> Missing(string exeDir)
        {
            var missing = new List<string>();
            foreach (var f in Required)
            {
                bool there;
                try { there = File.Exists(Path.Combine(exeDir ?? "", f)); } catch (Exception) { there = false; }
                if (!there) missing.Add(f);
            }
            return missing;
        }
    }
}
