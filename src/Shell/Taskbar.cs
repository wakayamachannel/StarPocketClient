// The taskbar button: progress (ITaskbarList3, ready for install / update in later versions; v0.1 uses it for the
// pre-launch scan and "scan again") and the overlay badge while Aegis finds a problem (PORT-MAP 4.4, SPEC 5.2).
// The wanted state is kept and applied whenever the button is (re)created (TaskbarButtonCreated): after the window is
// shown again from the tray, or after Explorer restarts. UI thread only.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Starpocket.Client.Shell
{
    internal enum TaskbarProgress
    {
        None = 0,           // TBPF_NOPROGRESS
        Indeterminate = 1,  // TBPF_INDETERMINATE
        Normal = 2,         // TBPF_NORMAL (green)
        Error = 4,          // TBPF_ERROR (red)
        Paused = 8,         // TBPF_PAUSED (yellow)
    }

    [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        // ITaskbarList3
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, int flags);
        void RegisterTab(IntPtr tab, IntPtr mdi);
        void UnregisterTab(IntPtr tab);
        void SetTabOrder(IntPtr tab, IntPtr insertBefore);
        void SetTabActive(IntPtr tab, IntPtr mdi, uint reserved);
        void ThumbBarAddButtons(IntPtr hwnd, uint count, IntPtr buttons);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint count, IntPtr buttons);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr imageList);
        void SetOverlayIcon(IntPtr hwnd, IntPtr icon, [MarshalAs(UnmanagedType.LPWStr)] string description);
        void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string tip);
        void SetThumbnailClip(IntPtr hwnd, IntPtr clip);
    }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
    class TaskbarListCoClass { }

    internal sealed class Taskbar : IDisposable
    {
        readonly Control window;
        ITaskbarList3 list;
        TaskbarProgress state = TaskbarProgress.None;
        ulong done, total = 1;
        OwnedIcon overlay;
        string overlayText = "";
        readonly Action<string> log;

        public Taskbar(Control window, Action<string> log)
        {
            this.window = window;
            this.log = log ?? (_ => { });
        }

        /// <summary>Call on TaskbarButtonCreated: from now on the button exists; the kept state is applied.</summary>
        public void OnButtonCreated()
        {
            try
            {
                if (list == null)
                {
                    list = (ITaskbarList3)new TaskbarListCoClass();
                    list.HrInit();
                }
                Apply();
            }
            catch (Exception ex) { log("taskbar: " + ex.Message); list = null; }
        }

        /// <summary>Progress on the taskbar button (install / update in later versions use the same call).</summary>
        public void SetProgress(TaskbarProgress s, ulong completed = 0, ulong of = 1)
        {
            state = s;
            done = completed;
            total = of == 0 ? 1 : of;
            Apply();
        }

        public void ClearProgress() => SetProgress(TaskbarProgress.None);

        /// <summary>The overlay badge (null = none). The Taskbar owns the icon from now on.</summary>
        public void SetOverlay(OwnedIcon icon, string description)
        {
            if (overlay != null && !ReferenceEquals(overlay, icon)) overlay.Dispose();
            overlay = icon;
            overlayText = description ?? "";
            Apply();
        }

        void Apply()
        {
            if (list == null || !window.IsHandleCreated) return;
            try
            {
                IntPtr h = window.Handle;
                list.SetProgressState(h, (int)state);
                if (state == TaskbarProgress.Normal || state == TaskbarProgress.Error || state == TaskbarProgress.Paused)
                    list.SetProgressValue(h, Math.Min(done, total), total);
                list.SetOverlayIcon(h, overlay != null && overlay.Icon != null ? overlay.Icon.Handle : IntPtr.Zero, overlay != null ? overlayText : null);
            }
            catch (Exception ex) { log("taskbar: " + ex.Message); }
        }

        public void Dispose()
        {
            if (overlay != null) { overlay.Dispose(); overlay = null; }
            if (list != null) { try { Marshal.ReleaseComObject(list); } catch (Exception) { } list = null; }
        }
    }
}
