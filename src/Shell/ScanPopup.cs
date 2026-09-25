// The scan on screen while the window is hidden (promised for v0.2, built in v0.4; PORT-MAP 2.2 row "スキャン画面").
// Aegis.ps1 had a window of its own for every scan (class Splash). In the app the scan lives in the Aegis panel of the
// page - which shows nothing at all when the window is in the notification area, and that is exactly when a scan matters
// most: the pre-launch scan, the tray's "scan again", and a start with --tray or --scan-only.
//
// So: a small card at the bottom right, drawn like the Aegis toast (same width, same corner, same shadow, never takes
// the focus, never a taskbar button - a game is usually in front of it), listing the 13 rows as they run. It closes by
// itself after the ps1's own waits: a scan that stopped a start stays 9 s, a quiet one 1.8 s. A click closes it at once.
// While it is open the toasts stack above it (AegisToast.ReservedBottom) instead of over it.
// v1.1: the START scan's card is shown on every start, window or no window (ClientApp.UpdateScanCard; the owner,
// 2026-09-23 「起動のたびに出す形に変えて」). Such a card (KeepWhenSerious) does not close by itself when a red row was found:
// it stays, one line taller, saying 「クリックで閉じる」, until it is clicked. A quiet one goes as before (1.8 s).
//
// Draw() makes the picture with no window at all, which is how the self-test checks it (as AegisToast.DrawCard is
// checked): no window is ever opened by a test run.
// UI thread only; Marshalled() gives a view that can be called from the scan's worker thread.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Starpocket.Client.Aegis;
using Starpocket.Client.Core;

namespace Starpocket.Client.Shell
{
    internal sealed class ScanPopup : Form, IScanView
    {
        // the toast's sizes, so the two cards line up under each other
        const int CardW = 400, EdgeMargin = 18, Pad = 14;
        const int HeaderH = 54, RowH = 22, FixH = 18, FooterH = 20, BottomPad = 10;
        const double AppearMs = 200, FullOpacity = 0.97, FadePerMs = 0.08 / 30.0;

        // the ps1's Splash linger (v0.5.5 line ~933): a blocked pre-launch scan stays long enough to be read
        public const double LingerBlockedMs = 9000, LingerGoMs = 900, LingerWarnMs = 4200, LingerQuietMs = 1800;

        public static readonly Color Red1 = Color.FromArgb(0xE6, 0x39, 0x46), Red2 = Color.FromArgb(0x8E, 0x12, 0x1D);
        public static readonly Color Teal1 = Color.FromArgb(0x2E, 0xC4, 0xB6), Teal2 = Color.FromArgb(0x10, 0x6E, 0x74);
        public static readonly Color Grey = Color.FromArgb(0x5A, 0x60, 0x78);

        readonly Timer timer = new Timer { Interval = 30 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        List<ScanRowView> rows = new List<ScanRowView>();
        string summary = "", lang;
        bool preLaunch, finished, closing;
        double lastMs, doneAtMs = -1, linger, opacity;
        int serious, warnings;
        float k;
        int cardH, x;
        IntPtr hBitmap = IntPtr.Zero, memDc = IntPtr.Zero, oldBitmap = IntPtr.Zero;
        Size bmpSize;

        /// <summary>v1.1: a red row keeps this card on screen until it is clicked (the start scan's card). A pre-launch
        /// card and the tray's "scan again" keep the ps1's waits. Set before Begin.</summary>
        public bool KeepWhenSerious;

        /// <summary>The finished card is one that stays: a red row, and KeepWhenSerious.</summary>
        bool Stays => finished && serious > 0 && KeepWhenSerious;

        public ScanPopup(string lang)
        {
            this.lang = string.IsNullOrEmpty(lang) ? "ja" : lang;
            k = PrimaryScale();
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Aegis";
            AccessibleRole = AccessibleRole.Alert;
            cardH = HeaderH + BottomPad;
            Place();
            MouseDown += (s, e) => closing = true;   // reacts on pointer-down, like the toast
            timer.Tick += (s, e) => Tick();
            FormClosed += (s, e) => { timer.Stop(); AegisToast.ReservedBottom = 0; };
        }

        int S(double v) => (int)Math.Round(v * k);

        void Place()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            bmpSize = new Size(S(CardW + Pad * 2), S(cardH + Pad * 2));
            x = wa.Right - S(CardW) - S(EdgeMargin) - S(Pad);
            int y = wa.Bottom - S(EdgeMargin) - S(Pad) - S(cardH);
            Bounds = new Rectangle(x, y, bmpSize.Width, bmpSize.Height);
            AegisToast.ReservedBottom = cardH + 10;   // the toasts stack above this card
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008 | 0x00080000;   // NOACTIVATE | TOOLWINDOW | TOPMOST | LAYERED
                return cp;
            }
        }

        const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Render();
            Push(0);
            lastMs = clock.Elapsed.TotalMilliseconds;
            timer.Start();
        }

        // ------------------------------------------------------------------ IScanView (UI thread)
        public void Begin(string lang, bool preLaunch)
        {
            this.lang = string.IsNullOrEmpty(lang) ? this.lang : lang;
            this.preLaunch = preLaunch;
            finished = false;
            doneAtMs = -1;
            closing = false;
            if (!Visible) Show();
        }

        public void Update(IList<ScanRowView> rows, string summary)
        {
            this.rows = rows == null ? new List<ScanRowView>() : new List<ScanRowView>(rows);
            this.summary = summary ?? "";
            Refit();
            Render();
        }

        public void Done(int serious, int warnings, string summary)
        {
            this.serious = serious;
            this.warnings = warnings;
            this.summary = summary ?? this.summary;
            finished = true;
            doneAtMs = clock.Elapsed.TotalMilliseconds;
            linger = LingerFor(preLaunch, serious, warnings, KeepWhenSerious);
            Refit();   // a card that stays grows by its "click to close" line
            Render();
        }

        /// <summary>How long the finished card stays: for ever (PositiveInfinity - a click closes it) when a red row was
        /// found and the card is one that keeps (v1.1, the start scan), else the ps1's own waits.</summary>
        public static double LingerFor(bool preLaunch, int serious, int warnings, bool keepWhenSerious)
        {
            if (serious > 0 && keepWhenSerious) return double.PositiveInfinity;
            return preLaunch ? (serious > 0 ? LingerBlockedMs : LingerGoMs)
                             : (serious > 0 ? LingerBlockedMs : warnings > 0 ? LingerWarnMs : LingerQuietMs);
        }

        public void CloseNow()
        {
            if (!IsDisposed) Close();
        }

        /// <summary>The card's height in logical pixels for these rows (a red row carries its fix under it).</summary>
        public static int CardHeight(IList<ScanRowView> rows) => CardHeight(rows, false);

        /// <summary>... and one line more for a card that stays (its "click to close" line, v1.1).</summary>
        public static int CardHeight(IList<ScanRowView> rows, bool footer)
        {
            int h = HeaderH + BottomPad + (footer ? FooterH : 0);
            if (rows != null)
                foreach (var r in rows) h += RowH + (r.State == 4 && !string.IsNullOrEmpty(r.Fix) ? FixH : 0);
            return h;
        }

        void Refit()
        {
            int want = CardHeight(rows, Stays);
            if (want == cardH) return;
            cardH = want;
            Place();
        }

        // ------------------------------------------------------------------ the animation
        void Tick()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double dt = Math.Min(64, Math.Max(0, now - lastMs));
            lastMs = now;
            if (!closing)
            {
                opacity = now <= AppearMs ? Math.Min(FullOpacity, now / AppearMs) : FullOpacity;
                if (finished && doneAtMs >= 0 && now - doneAtMs > linger) closing = true;
            }
            if (closing)
            {
                opacity = Math.Max(0, opacity - dt * FadePerMs);
                if (opacity <= 0.01) { Close(); return; }
            }
            Push(opacity);
        }

        void Push(double alpha)
        {
            if (!IsHandleCreated || memDc == IntPtr.Zero) return;
            var pos = new Pt { X = Bounds.X, Y = Bounds.Y };
            var size = new Sz { W = bmpSize.Width, H = bmpSize.Height };
            var src = new Pt();
            var blend = new Blend { Op = 0, Flags = 0, Alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255))), Format = 1 };
            UpdateLayeredWindow(Handle, IntPtr.Zero, ref pos, ref size, memDc, ref src, 0, ref blend, 2);
        }

        void Render()
        {
            if (!IsHandleCreated) return;
            FreeDc();
            using (var bmp = Draw(rows, summary, lang, k, finished, serious, warnings, Stays ? AegisText.Get(lang, "card.close") : null))
            {
                IntPtr screen = GetDC(IntPtr.Zero);
                try
                {
                    memDc = CreateCompatibleDC(screen);
                    hBitmap = PremultipliedDib(screen, bmp);
                    oldBitmap = SelectObject(memDc, hBitmap);
                }
                finally { ReleaseDC(IntPtr.Zero, screen); }
            }
            Push(opacity);
        }

        // ------------------------------------------------------------------ the picture (no window needed)
        /// <summary>The card as it looks with these rows. The self-test draws it into a bitmap and reads its pixels.</summary>
        public static Bitmap Draw(IList<ScanRowView> rows, string summary, string lang, float k, bool done, int serious, int warnings)
            => Draw(rows, summary, lang, k, done, serious, warnings, null);

        /// <summary>The same with a last line under the rows (<paramref name="footer"/>: 「クリックで閉じる」 on a card that
        /// stays, v1.1); null draws none.</summary>
        public static Bitmap Draw(IList<ScanRowView> rows, string summary, string lang, float k, bool done, int serious, int warnings, string footer)
        {
            rows = rows ?? new List<ScanRowView>();
            int cardH = CardHeight(rows, footer != null);
            Color c1 = !done ? Teal1 : serious > 0 ? Red1 : warnings > 0 ? AegisToast.Amber1 : AegisToast.Green1;
            Color c2 = !done ? Teal2 : serious > 0 ? Red2 : warnings > 0 ? AegisToast.Amber2 : AegisToast.Green2;
            Func<double, int> R = v => (int)Math.Round(v * k);
            var bmp = new Bitmap(R(CardW + Pad * 2), R(cardH + Pad * 2), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                var card = new RectangleF(R(Pad), R(Pad), R(CardW), R(cardH));
                float r = 12f * k;
                for (int i = R(Pad) - 1; i >= 1; i--)
                {
                    int a = (int)(34.0 * Math.Pow(1.0 - i / (double)R(Pad), 2));
                    if (a <= 0) continue;
                    var sr = RectangleF.Inflate(card, i, i);
                    sr.Offset(0, 3f * k);
                    using (var p = Round(sr, r + i)) using (var b = new SolidBrush(Color.FromArgb(a, 4, 6, 20))) g.FillPath(b, p);
                }
                using (var path = Round(card, r))
                {
                    using (var bg = new LinearGradientBrush(card, Color.FromArgb(250, 30, 34, 72), Color.FromArgb(250, 15, 17, 44), 90f)) g.FillPath(bg, path);
                    var clip = g.Clip;
                    g.SetClip(path);
                    using (var accent = new SolidBrush(c1)) g.FillRectangle(accent, card.X, card.Y, 4f * k, card.Height);
                    g.Clip = clip;
                    using (var border = new Pen(Color.FromArgb(90, c1), 1f)) g.DrawPath(border, path);
                }
                AegisToast.DrawShieldFor(g, new RectangleF(card.X + 16f * k, card.Y + 12f * k, 30f * k, 34f * k), c1, c2);
                using (var fTitle = AegisToast.UiFontFor(lang, 10f * 96f / 72f * k, FontStyle.Bold))
                using (var fSub = AegisToast.UiFontFor(lang, 9f * 96f / 72f * k, FontStyle.Regular))
                using (var fRow = AegisToast.UiFontFor(lang, 8.5f * 96f / 72f * k, FontStyle.Regular))
                using (var white = new SolidBrush(Color.White))
                using (var sub = new SolidBrush(Color.FromArgb(222, 230, 244)))
                using (var dim = new SolidBrush(Color.FromArgb(150, 162, 186)))
                using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    g.DrawString("AEGIS", fTitle, white, card.X + 56f * k, card.Y + 10f * k);
                    g.DrawString(summary ?? "", fSub, sub, new RectangleF(card.X + 56f * k, card.Y + 28f * k, card.Width - 70f * k, 20f * k), sf);
                    float y = card.Y + HeaderH * k;
                    foreach (var row in rows)
                    {
                        Dot(g, new RectangleF(card.X + 20f * k, y + 6f * k, 8f * k, 8f * k), StateColor(row.State));
                        var titleBrush = row.State == 4 ? new SolidBrush(Red1) : null;
                        g.DrawString(row.Title ?? "", fRow, titleBrush ?? sub, new RectangleF(card.X + 36f * k, y + 2f * k, 150f * k, RowH * k), sf);
                        g.DrawString(row.Detail ?? "", fRow, row.State == 4 ? (titleBrush ?? dim) : dim, new RectangleF(card.X + 188f * k, y + 2f * k, card.Width - 200f * k, RowH * k), sf);
                        y += RowH * k;
                        if (row.State == 4 && !string.IsNullOrEmpty(row.Fix))
                        {
                            g.DrawString("→ " + row.Fix, fRow, sub, new RectangleF(card.X + 36f * k, y, card.Width - 48f * k, FixH * k), sf);
                            y += FixH * k;
                        }
                        if (titleBrush != null) titleBrush.Dispose();
                    }
                    if (footer != null) g.DrawString(footer, fRow, dim, new RectangleF(card.X + 36f * k, y + 3f * k, card.Width - 48f * k, FooterH * k), sf);
                }
            }
            return bmp;
        }

        public static Color StateColor(int state)
        {
            switch (state)
            {
                case 1: return Teal1;
                case 2: return AegisToast.Green1;
                case 3: return AegisToast.Amber1;
                case 4: return Red1;
                default: return Grey;
            }
        }

        static void Dot(Graphics g, RectangleF r, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
        }

        static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static float PrimaryScale()
        {
            try
            {
                var b = Screen.PrimaryScreen.Bounds;
                var c = new Native.POINT { X = b.Left + b.Width / 2, Y = b.Top + b.Height / 2 };
                IntPtr mon = Native.MonitorFromPoint(c, Native.MONITOR_DEFAULTTONEAREST);
                uint dx, dy;
                if (Native.GetDpiForMonitor(mon, 0, out dx, out dy) == 0 && dx >= 96) return dx / 96f;
            }
            catch (Exception) { }
            return 1f;
        }

        // ------------------------------------------------------------------ from another thread
        /// <summary>A view that can be called from the scan's worker thread: every call is handed to the UI thread.</summary>
        public IScanView Marshalled() => new Marshal2(this);

        sealed class Marshal2 : IScanView
        {
            readonly ScanPopup p;
            public Marshal2(ScanPopup p) { this.p = p; }

            void Post(Action a)
            {
                if (p.IsDisposed || !p.IsHandleCreated) return;
                try { p.BeginInvoke(a); } catch (InvalidOperationException) { }
            }

            public void Begin(string lang, bool preLaunch) => Post(() => p.Begin(lang, preLaunch));
            public void Update(IList<ScanRowView> rows, string summary) => Post(() => p.Update(rows, summary));
            public void Done(int serious, int warnings, string summary) => Post(() => p.Done(serious, warnings, summary));
            public void CloseNow() => Post(p.CloseNow);
        }

        // ------------------------------------------------------------------ per-pixel alpha window
        void FreeDc()
        {
            if (memDc != IntPtr.Zero)
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                DeleteDC(memDc);
                memDc = IntPtr.Zero;
                oldBitmap = IntPtr.Zero;
            }
            if (hBitmap != IntPtr.Zero) { DeleteObject(hBitmap); hBitmap = IntPtr.Zero; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            AegisToast.ReservedBottom = 0;   // also for a card that was made and never shown
            FreeDc();
            base.Dispose(disposing);
        }

        static IntPtr PremultipliedDib(IntPtr dc, Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var bi = new BitmapInfoHeader { Size = 40, Width = w, Height = -h, Planes = 1, BitCount = 32, Compression = 0 };
            IntPtr bits;
            IntPtr dib = CreateDIBSection(dc, ref bi, 0, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero) return bmp.GetHbitmap(Color.FromArgb(0));
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[w * 4];
                for (int y = 0; y < h; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                    System.Runtime.InteropServices.Marshal.Copy(row, 0, bits + y * w * 4, row.Length);
                }
            }
            finally { bmp.UnlockBits(data); }
            return dib;
        }

        [StructLayout(LayoutKind.Sequential)] struct Pt { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct Sz { public int W, H; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct Blend { public byte Op, Flags, Alpha, Format; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Pt pptDst, ref Sz psize, IntPtr hdcSrc, ref Pt pptSrc, int crKey, ref Blend pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfoHeader
        {
            public int Size, Width, Height;
            public short Planes, BitCount;
            public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;
        }
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader pbmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    }
}
