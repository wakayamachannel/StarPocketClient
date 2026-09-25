// The notification-area icon: icon A with Aegis's status dot, the tooltip, and the menu
// 開く / プレイ / Aegis の状態 / もう一度スキャン / 終了 in the app's language (PORT-MAP 3.13, SPEC 5.3).
// The Aegis tray of Aegis.ps1 and the launcher are one icon now. UI thread only.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Drawing;
using System.Windows.Forms;
using Starpocket.Client.Aegis;
using Starpocket.Client.Core;

namespace Starpocket.Client.Shell
{
    internal interface ITrayActions
    {
        void TrayOpen();
        void TrayPlay();
        void TrayAegisStatus();
        void TrayRescan();
        void TrayQuit();
        bool TrayCanPlay();
    }

    internal static class TrayText
    {
        /// <summary>.NET Framework's NotifyIcon.Text throws at 64 characters or more.</summary>
        public const int MaxTooltip = 63;

        public static string Tooltip(string lang, AegisSnapshot snap)
        {
            string s;
            string state = snap != null ? snap.State : "off";
            if (snap != null && snap.LastScanSerious > 0) s = S.T(lang, "tip.serious");   // red: seen even while the window is hidden
            else if (state == "watching" || state == "kicked") s = S.T(lang, "tip.watching", snap.Detected, snap.Kicked);
            else if (state == "idle") s = S.T(lang, "tip.idle");
            else if (snap != null && snap.OldTray) s = S.T(lang, "tip.oldtray");
            else s = S.T(lang, "tip.off");
            return Clip(s);
        }

        public static string Clip(string s)
        {
            if (s == null) return "";
            if (s.Length <= MaxTooltip) return s;
            int n = MaxTooltip - 1;
            if (char.IsHighSurrogate(s[n - 1])) n--;
            return s.Substring(0, n) + "…";
        }
    }

    internal sealed class TrayController : IDisposable
    {
        readonly NotifyIcon icon;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem open, play, aegis, rescan, quit;
        OwnedIcon current;
        string currentState;

        public TrayController(ITrayActions actions)
        {
            menu = new ContextMenuStrip { ShowImageMargin = false };
            open = new ToolStripMenuItem("", null, (s, e) => actions.TrayOpen());
            play = new ToolStripMenuItem("", null, (s, e) => actions.TrayPlay());
            aegis = new ToolStripMenuItem("", null, (s, e) => actions.TrayAegisStatus());
            rescan = new ToolStripMenuItem("", null, (s, e) => actions.TrayRescan());
            quit = new ToolStripMenuItem("", null, (s, e) => actions.TrayQuit());
            menu.Items.AddRange(new ToolStripItem[] { open, play, aegis, rescan, new ToolStripSeparator(), quit });
            menu.Opening += (s, e) => { play.Enabled = actions.TrayCanPlay(); };
            icon = new NotifyIcon { ContextMenuStrip = menu, Text = AppInfo.Name };
            icon.MouseDoubleClick += (s, e) => { if (e.Button == MouseButtons.Left) actions.TrayOpen(); };
        }

        /// <summary>Texts and font of the menu in the language (again whenever it changes).</summary>
        public void SetLanguage(string lang)
        {
            var font = new Font(Lang.UiFontName(lang), 9f);
            menu.Font = font;
            open.Text = S.T(lang, "tray.open");
            open.Font = new Font(font, FontStyle.Bold);   // the default action (double-click)
            play.Text = S.T(lang, "tray.play");
            aegis.Text = S.T(lang, "tray.aegis");
            rescan.Text = S.T(lang, "tray.rescan");
            quit.Text = S.T(lang, "tray.quit");
        }

        public void Show() => icon.Visible = true;

        /// <summary>The dot (idle teal / watching green / 12 s after a removal amber / off grey; red while the last scan has a
        /// red row, like the taskbar badge - the tray is what shows while the window is hidden) and the tooltip.</summary>
        public void Update(string lang, AegisSnapshot snap)
        {
            string state = snap != null ? snap.State : "off";
            bool serious = snap != null && snap.LastScanSerious > 0;
            string key = serious ? "serious" : state;
            if (current == null || key != currentState)
            {
                var next = Icons.Tray(SystemInformation.SmallIconSize.Width, serious ? Icons.Serious : Icons.StateColor(state));
                icon.Icon = next.Icon;
                if (current != null) current.Dispose();
                current = next;
                currentState = key;
            }
            icon.Text = TrayText.Tooltip(lang, snap);
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
            if (current != null) { current.Dispose(); current = null; }
        }
    }
}
