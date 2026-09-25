// Reading and writing a Windows shortcut (.lnk) in-process through the shell's own COM object - the app never runs
// wscript.exe or a WScript.Shell script like the PowerShell launcher's New-LauncherShortcut does.
//   read  (v0.2): the Desktop's "PocketRoles Launcher.lnk", to find the old launcher's folder and take its
//                 launcher-state.json over (PORT-MAP 13.1).
//   write (v0.3): one shortcut to this exe, and ONLY when the viewer pressed the button that says so (Shortcuts.cs).
//                 Nothing here runs at start, on install or on update (SignPath: nothing is added to the Desktop or
//                 the Start menu without the viewer asking for it).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Starpocket.Client.Core
{
    internal static class ShellLink
    {
        const int MAX_PATH = 260;
        const int SLGP_RAWPATH = 4;
        const int STGM_READ = 0;

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLinkObject { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, int flags);
            void GetIDList(out IntPtr pidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxArgs);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int show);
            void SetShowCmd(int show);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int maxPath, out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, int reserved);
            void Resolve(IntPtr hwnd, int flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPersistFile
        {
            void GetClassID(out Guid classId);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string file, int mode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string file, [MarshalAs(UnmanagedType.Bool)] bool remember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file);
            void GetCurPath([Out, MarshalAs(UnmanagedType.LPWStr)] out string file);
        }

        /// <summary>The file a .lnk points at (no "resolve": no network and no dialog), or null when it cannot be read.</summary>
        public static string TargetPath(string lnkPath)
        {
            object com = null;
            try
            {
                com = new ShellLinkObject();
                ((IPersistFile)com).Load(lnkPath, STGM_READ);
                var sb = new StringBuilder(MAX_PATH);
                ((IShellLinkW)com).GetPath(sb, sb.Capacity, IntPtr.Zero, SLGP_RAWPATH);
                string path = sb.ToString();
                return string.IsNullOrEmpty(path) ? null : path;
            }
            catch (Exception) { return null; }
            finally { if (com != null) { try { Marshal.FinalReleaseComObject(com); } catch (Exception) { } } }
        }

        /// <summary>Writes a .lnk (v0.3). Only Shortcuts.cs calls it, and only from the viewer's own button press.
        /// Throws when it cannot be written (the caller shows the message).</summary>
        public static void Save(string lnkPath, string target, string workingDir, string description, string iconPath)
        {
            object com = null;
            try
            {
                com = new ShellLinkObject();
                var link = (IShellLinkW)com;
                link.SetPath(target);
                if (!string.IsNullOrEmpty(workingDir)) link.SetWorkingDirectory(workingDir);
                if (!string.IsNullOrEmpty(description)) link.SetDescription(Trim(description, 259));
                // the exe's own first icon: the app never points a shortcut at another program's icon
                if (!string.IsNullOrEmpty(iconPath)) link.SetIconLocation(iconPath, 0);
                ((IPersistFile)com).Save(lnkPath, true);
            }
            finally { if (com != null) { try { Marshal.FinalReleaseComObject(com); } catch (Exception) { } } }
        }

        static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
    }
}
