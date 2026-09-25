// Win32 calls used by the app shell.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Starpocket.Client
{
    internal static class Native
    {
        // ---- shell32
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

        // ---- user32
        public const int ASFW_ANY = -1;
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, int msg, int action, IntPtr changeFilterStruct);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, int flags);
        /// <summary>SPEC 5.4: the window comes back after a game WITHOUT taking the focus (Form.Show would take it).
        /// WinForms keeps its own Visible in step, because a form updates it from WM_SHOWWINDOW.</summary>
        public const int SW_SHOWNOACTIVATE = 4;
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

        /// <summary>v1.2: Windows の「アニメーション効果」（設定 → アクセシビリティ → 視覚効果）。Chromium はこれを
        /// prefers-reduced-motion にしているので、ページ（host-v01.js の縮み）と窓（MainForm.FadeOut の薄まり）が同じ元を見る。</summary>
        public const int SPI_GETCLIENTAREAANIMATION = 0x1042;
        [DllImport("user32.dll", SetLastError = true)] static extern bool SystemParametersInfo(int action, int param, ref int value, int winIni);

        /// <summary>「アニメーション効果」が ON なら true、OFF なら false。読めない時は null（＝演出しない側に倒す）。投げない。</summary>
        public static bool? TryClientAreaAnimation()
        {
            int v = 0;
            try { return SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref v, 0) ? v != 0 : (bool?)null; }
            catch (Exception) { return null; }
        }

        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 2;
        public const int MSGFLT_ALLOW = 1;
        public const int MONITOR_DEFAULTTONEAREST = 2;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_SYSMENU = 0x00080000;
        public const int CS_DROPSHADOW = 0x00020000;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        // ---- shcore (Windows 8.1+)
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        // ---- dwmapi
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWCP_ROUND = 2;
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // ---- kernel32
        public const int ATTACH_PARENT_PROCESS = -1;
        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        [DllImport("kernel32.dll")] public static extern bool AttachConsole(int processId);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(int access, bool inherit, int processId);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetLongPathName(string shortPath, StringBuilder longPath, int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetShortPathName(string longPath, StringBuilder shortPath, int size);

        /// <summary>The path with 8.3 short folder names (C:\Users\someone\x -&gt; C:\Users\SOMEON~1\x), or null when
        /// Windows cannot tell - the volume may have short names switched off, and then there is nothing to mask.
        /// Used by <see cref="Core.Masking.Home"/>: a path that reached a log in its short form still names the home
        /// folder, and masking only the long form leaves it in the report (2026-09-26).</summary>
        public static string TryGetShortPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int n = GetShortPathName(path, sb, sb.Capacity);
                if (n <= 0 || n > sb.Capacity) return null;
                string s = sb.ToString(0, n);
                return string.Equals(s, path, StringComparison.OrdinalIgnoreCase) ? null : s;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The path with long folder names (C:\Users\SOMEON~1\x -> C:\Users\someone\x), or null when Windows
        /// cannot tell (the path is not there): the caller then keeps the path as it is.</summary>
        public static string TryGetLongPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int n = GetLongPathName(path, sb, sb.Capacity);
                if (n <= 0 || n > sb.Capacity) return null;
                return sb.ToString(0, n);
            }
            catch (Exception) { return null; }
        }

        /// <summary>Full path of another process's exe, read with PROCESS_QUERY_LIMITED_INFORMATION only (no memory access).
        /// null when it cannot be read (protected process, gone, access denied).</summary>
        public static string TryGetProcessImagePath(int processId)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (h == IntPtr.Zero) return null;
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (!QueryFullProcessImageName(h, 0, sb, ref size)) return null;
                return sb.ToString(0, size);
            }
            catch (Exception) { return null; }
            finally { if (h != IntPtr.Zero) CloseHandle(h); }
        }

        // ---- the exe's own Win32 resources (SelfTest\IdentitySelfTests.cs): the manifest and the icon Windows really
        // shows in Task Manager, read from the built file rather than believed from the project file
        public const int RT_ICON = 3;
        public const int RT_GROUP_ICON = 14;
        public const int RT_MANIFEST = 24;
        const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

        delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, int flags);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr LoadResource(IntPtr module, IntPtr res);
        [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr data);
        [DllImport("kernel32.dll", SetLastError = true)] static extern int SizeofResource(IntPtr module, IntPtr res);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc callback, IntPtr param);

        static T WithDataFile<T>(string file, Func<IntPtr, T> body, T none)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = LoadLibraryEx(file, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
                return h == IntPtr.Zero ? none : body(h);
            }
            catch (Exception) { return none; }
            finally { if (h != IntPtr.Zero) FreeLibrary(h); }
        }

        /// <summary>The numbered resources of that type in a file (names that are words are left out: ours are numbers).</summary>
        public static System.Collections.Generic.List<int> ResourceIds(string file, int type)
        {
            var ids = new System.Collections.Generic.List<int>();
            WithDataFile(file, h =>
            {
                EnumResourceNames(h, (IntPtr)type, (m, t, name, p) =>
                {
                    if ((ulong)name.ToInt64() >> 16 == 0) ids.Add((int)name.ToInt64());
                    return true;
                }, IntPtr.Zero);
                return true;
            }, false);
            return ids;
        }

        /// <summary>One numbered resource's bytes, or null.</summary>
        public static byte[] ReadResource(string file, int type, int id) => WithDataFile(file, h =>
        {
            IntPtr res = FindResource(h, (IntPtr)id, (IntPtr)type);
            if (res == IntPtr.Zero) return null;
            int size = SizeofResource(h, res);
            IntPtr data = LoadResource(h, res);
            if (size <= 0 || data == IntPtr.Zero) return null;
            IntPtr p = LockResource(data);
            if (p == IntPtr.Zero) return null;
            var bytes = new byte[size];
            Marshal.Copy(p, bytes, 0, size);
            return bytes;
        }, null);

        /// <summary>Windows 11 (build 22000+): rounded corners for the borderless window.</summary>
        public static bool IsWindows11OrLater
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
                        .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        int build;
                        return k != null && int.TryParse(k.GetValue("CurrentBuildNumber") as string, out build) && build >= 22000;
                    }
                }
                catch (Exception) { return false; }
            }
        }
    }
}
