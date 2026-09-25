// Is the WebView2 runtime there? If not, a small plain window says so (no HTML) with a button that opens Microsoft's
// official WebView2 page in the default browser, and a "check again" button: once the viewer has installed it, the app
// goes on starting (no need to open it again). The app never downloads or installs anything by itself (SPEC 2.4,
// PORT-MAP 4.2). Program runs this before it takes the single-instance mutex, so the window holds nothing.
// Missing or broken WebView2 DLLs next to the exe are not "the runtime is missing": they get the "files missing" notice.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Starpocket.Client.Core;

namespace Starpocket.Client.Shell
{
    internal static class WebView2Gate
    {
        public enum State { Ok, NoRuntime, BrokenFiles }

        /// <summary>The installed runtime's version (Ok), no runtime, or the app's own WebView2 files cannot be loaded.
        /// Call only after <see cref="PackageFiles.Missing"/> found nothing (the DLLs this method needs are next to the exe).</summary>
        public static State Check(out string version, out string problem)
        {
            version = null;
            problem = null;
            try
            {
                string v = CoreWebView2Environment.GetAvailableBrowserVersionString(null);
                if (string.IsNullOrEmpty(v)) { problem = "no runtime"; return State.NoRuntime; }
                version = v;
                return State.Ok;
            }
            catch (WebView2RuntimeNotFoundException ex) { problem = ex.Message; return State.NoRuntime; }
            catch (Exception ex) when (IsFileProblem(ex)) { problem = ex.GetType().Name + ": " + ex.Message; return State.BrokenFiles; }
            catch (Exception ex) { problem = ex.GetType().Name + ": " + ex.Message; return State.NoRuntime; }
        }

        /// <summary>WebView2Loader.dll missing (DllNotFoundException), a DLL of the wrong kind (BadImageFormatException) or
        /// one that cannot be loaded: the zip was not extracted whole.</summary>
        public static bool IsFileProblem(Exception ex) =>
            ex is DllNotFoundException || ex is BadImageFormatException || ex is FileNotFoundException || ex is FileLoadException || ex is EntryPointNotFoundException;

        /// <summary>The "install WebView2" window. True when the viewer installed it and "check again" found it.</summary>
        public static bool ShowMissing(string lang, Action<string> log)
        {
            using (var f = new WebView2MissingForm(lang, log)) return f.ShowDialog() == DialogResult.OK;
        }
    }

    /// <summary>The "WebView2 is needed" window: icon A, one heading, two short lines, a filled primary button (the
    /// download page) and two quiet ones (check again, close); the press colour shows on mouse-down (apple-design: respond
    /// at once). "Check again" answers in place (a line under the text), never with another dialog. Laid out in logical
    /// pixels times the monitor's scale.</summary>
    internal sealed class WebView2MissingForm : Form
    {
        static readonly Color Ink = Color.FromArgb(0x1B, 0x1D, 0x2E);
        static readonly Color Muted = Color.FromArgb(0x5A, 0x5F, 0x73);
        static readonly Color Paper = Color.FromArgb(0xF7, 0xF7, 0xFA);
        static readonly Color Alert = Color.FromArgb(0xB4, 0x23, 0x2F);

        readonly string lang;
        readonly Action<string> log;
        readonly PictureBox art;
        readonly Label head, body, old, note;
        readonly Button open, retry, close;

        public WebView2MissingForm(string lang, Action<string> log)
        {
            this.lang = lang;
            this.log = log ?? (_ => { });
            string font = Lang.UiFontName(lang);
            AutoScaleMode = AutoScaleMode.None;
            Text = AppInfo.Name;
            Icon = Icons.Brand();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Paper;
            Font = new Font(font, 9.5f);

            art = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Image = Icons.Brand(64).ToBitmap() };
            head = new Label { AutoSize = true, ForeColor = Ink, Font = new Font(font, 12.5f, FontStyle.Bold), Text = S.T(lang, "wv2.head") };
            body = new Label { AutoSize = true, ForeColor = Ink, Text = S.T(lang, "wv2.body") };
            old = new Label { AutoSize = true, ForeColor = Muted, Text = S.T(lang, "wv2.old") };
            note = new Label { AutoSize = true, ForeColor = Alert, Text = "", Visible = false };
            open = MakeButton(S.T(lang, "wv2.open"), true, font);
            // through ShellOpen like everything else the app opens, so that file really is the only place in the app
            // that starts anything (its own list already holds this one Microsoft page and nothing else)
            open.Click += (s, e) =>
            {
                try { ShellOpen.WebPage(AppInfo.WebView2DownloadPage); } catch (Exception) { }
            };
            retry = MakeButton(S.T(lang, "wv2.retry"), false, font);
            retry.Click += (s, e) => CheckAgain();
            close = MakeButton(S.T(lang, "wv2.close"), false, font);
            close.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.AddRange(new Control[] { art, head, body, old, note, open, retry, close });
            AcceptButton = open;
            CancelButton = close;
            Load += (s, e) => Layout2();
        }

        void CheckAgain()
        {
            string version, problem;
            var st = WebView2Gate.Check(out version, out problem);
            if (st == WebView2Gate.State.Ok)
            {
                log("WebView2 runtime found after \"check again\": " + version);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            log("WebView2 runtime still missing: " + problem);
            note.Text = S.T(lang, "wv2.notyet");
            note.Visible = true;
            Layout2();
        }

        void Layout2()
        {
            float k = DeviceDpi / 96f;
            int P(float v) => (int)Math.Round(v * k);
            int width = P(560), pad = P(28), artSize = P(56);
            art.SetBounds(pad, pad, artSize, artSize);
            int x = pad + artSize + P(20), w = width - x - pad;
            foreach (var l in new[] { head, body, old, note }) l.MaximumSize = new Size(w, 0);
            head.Location = new Point(x, pad);
            body.Location = new Point(x, head.Bottom + P(10));
            old.Location = new Point(x, body.Bottom + P(8));
            note.Location = new Point(x, old.Bottom + P(8));
            foreach (var b in new[] { open, retry, close }) { b.MinimumSize = new Size(P(96), P(34)); b.Padding = new Padding(P(14), P(4), P(14), P(4)); }
            int textBottom = note.Visible ? note.Bottom : old.Bottom;
            int top = Math.Max(textBottom, art.Bottom) + P(26);
            ClientSize = new Size(width, top + open.Height + P(22));
            open.Location = new Point(width - pad - open.Width, top);
            retry.Location = new Point(open.Left - P(10) - retry.Width, top);
            close.Location = new Point(pad, top);
        }

        static Button MakeButton(string text, bool primary, string font)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(font, 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
                BackColor = primary ? Icons.Navy : Color.FromArgb(0xE9, 0xEA, 0xF1),
                ForeColor = primary ? Color.White : Ink,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0x2A, 0x2E, 0x75) : Color.FromArgb(0xDF, 0xE0, 0xEA);
            b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(0x15, 0x18, 0x40) : Color.FromArgb(0xD2, 0xD4, 0xE0);
            return b;
        }
    }
}
