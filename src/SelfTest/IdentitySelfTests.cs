// What the owner asked for in one place: "our apps must show in Task Manager as themselves, not as Windows PowerShell"
// (2026-09-23). These checks read the BUILT exe - its version resource, its manifest, its icon and its bytes - rather
// than believing the project file, so a lost <ApplicationIcon>, a stale app.manifest or a new call to a console tool
// fails the build instead of shipping.
//   - the name, the publisher and the version Task Manager's Details tab shows
//   - the manifest's version, which nothing else compares with the csproj's
//   - the icon: a group icon resource with images, which is the icon beside the name (the separate "icon A embedded"
//     test in ShellSelfTests covers the managed copy the window and the tray draw)
//   - no console tool's name anywhere in the exe (the list is in Backwards below, written backwards so this check does
//     not find its own words). This is the run-time half of the guard; .github\workflows\build.yml reads the sources
//     for Process.Start and the Win32 ways of starting a program outside src\Core\ShellOpen.cs.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client.SelfTest
{
    internal static class IdentitySelfTests
    {
        /// <summary>Names of programs that would make this a shell wrapper. None of them may be in the exe at all.
        /// Written backwards on purpose: a plain list here would be in the exe itself and the check would find its own
        /// words. <see cref="ForbiddenPrograms"/> turns them round again while the test runs.</summary>
        static readonly string[] Backwards =
        {
            "exe.llehsrewop", "exe.hswp", "exe.dmc", "exe.ypocobor", "ypocobor", "exe.tpircsw", "exe.tpircsc",
            "exe.sksathcs", "exe.ger", "exe.dapeton", "exe.rerolpxe", "exe.lruc", "exe.nimdastib",
        };

        internal static string[] ForbiddenPrograms
        {
            get
            {
                var names = new string[Backwards.Length];
                for (int i = 0; i < Backwards.Length; i++)
                {
                    var c = Backwards[i].ToCharArray();
                    Array.Reverse(c);
                    names[i] = new string(c);
                }
                return names;
            }
        }

        public static void Run(SelfTestRunner r)
        {
            r.Section("identity");
            string exe = Assembly.GetExecutingAssembly().Location;
            var vi = FileVersionInfo.GetVersionInfo(exe);

            // ---- what Task Manager shows
            r.Equal("FileDescription (the name in Task Manager)", AppInfo.Name, vi.FileDescription);
            r.Equal("ProductName", AppInfo.Name, vi.ProductName);
            r.Equal("CompanyName", AppInfo.Company, vi.CompanyName);
            // exactly, not "starts with": AppInfo.Version is what the log's first line, --action's heading and the
            // report zip say the app is, and in the middle of v0.4 all of them still said 0.3.0 (v0.4 review)
            r.Equal("FileVersion is AppInfo.Version", AppInfo.Version + ".0", vi.FileVersion);
            r.Equal("the UI's version is the same number, shorter", AppInfo.Version.Substring(0, AppInfo.Version.LastIndexOf('.')), AppInfo.UiVersion);

            // ---- the manifest inside the exe, and its version against the csproj's
            r.Test("the manifest inside the exe", () =>
            {
                byte[] bytes = Native.ReadResource(exe, Native.RT_MANIFEST, 1);
                r.Check("it is there", bytes != null && bytes.Length > 0);
                if (bytes == null) return;
                string text = new UTF8Encoding(false, false).GetString(bytes);
                var id = Regex.Match(text, "<assemblyIdentity[^>]*?version=\"([0-9.]+)\"");
                r.Equal("app.manifest version == FileVersion", vi.FileVersion, id.Success ? id.Groups[1].Value : "(none)");
                r.Check("never asks to be run as an administrator", text.IndexOf("asInvoker", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    text.IndexOf("requireAdministrator", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("highestAvailable", StringComparison.OrdinalIgnoreCase) < 0);
            });

            // ---- the icon Windows draws beside the name (ApplicationIcon = StarPocket icon A, never the crewmate)
            r.Test("the exe's own icon", () =>
            {
                var groups = Native.ResourceIds(exe, Native.RT_GROUP_ICON);
                r.Check("there is an icon group", groups.Count > 0, "groups: " + groups.Count);
                if (groups.Count == 0) return;
                byte[] group = Native.ReadResource(exe, Native.RT_GROUP_ICON, groups[0]);
                r.Check("its directory can be read", group != null && group.Length >= 6 && group[4] > 0, group == null ? "null" : "images: " + group[4]);
                var images = Native.ResourceIds(exe, Native.RT_ICON);
                r.Check("and its images are in the exe", images.Count > 0 && Native.ReadResource(exe, Native.RT_ICON, images[0])?.Length > 0, "images: " + images.Count);
                // what the .ico of the project holds is what the window and the tray use (ShellSelfTests); the sizes of
                // the group in the exe come from the same file, so a wrong count here means a different icon was built in
                r.Equal("as many images as assets\\starpocket.ico has", Icons.IcoBytes[4], group[4]);
            });

            // ---- nothing in here starts a shell or a console tool
            r.Test("no console tool's name in the exe", () =>
            {
                byte[] bytes = File.ReadAllBytes(exe);
                var names = ForbiddenPrograms;
                var found = new List<string>();
                foreach (var name in names)
                    if (Contains(bytes, Encoding.Unicode.GetBytes(name)) || Contains(bytes, Encoding.ASCII.GetBytes(name)))
                        found.Add(name);
                r.Check("none of " + names.Length + " names is in the exe", found.Count == 0, string.Join(", ", found.ToArray()));
            });

            // ---- and the one place that may start anything still refuses everything else
            // (a real shell's name is not written here either: the check above reads this exe's own bytes)
            r.Check("ShellOpen refuses a program as a \"text file\"", !ShellOpen.Allowed(OpenKind.TextFile, @"C:\Windows\System32\anything.exe"));
            r.Check("... and a script too", !ShellOpen.Allowed(OpenKind.TextFile, @"C:\x\run.ps1"));
            r.Check("ShellOpen refuses a steam:// URL of its own", !ShellOpen.Allowed(OpenKind.SteamUrl, "steam://run/12345"));
            r.Check("ShellOpen refuses a web page of its own", !ShellOpen.Allowed(OpenKind.Web, "https://example.com/"));
        }

        static bool Contains(byte[] hay, byte[] needle)
        {
            if (needle.Length == 0 || hay.Length < needle.Length) return false;
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int k = 0;
                while (k < needle.Length && hay[i + k] == needle[k]) k++;
                if (k == needle.Length) return true;
            }
            return false;
        }
    }
}
