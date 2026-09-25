// Aegis's own toast (aegis\Aegis.ps1 class Toast, v0.5.5 lines 1097-1181; PORT-MAP 3.12). Not a Windows balloon or
// toast notification: those are put away while a game runs (Do Not Disturb when playing a game), so Aegis draws its own
// small window at the bottom right of the primary monitor's work area.
// Kept from the ps1: 400x78 (logical pixels), 18 px from the right and the bottom, stacked upwards 10 px apart, at most 4
// (the oldest closes first), never takes the focus (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST), no taskbar
// button, 250 ms to appear (opacity 0.96), then the hold time (warning 6 s, information 3.5 s), then fading 0.08 per
// 30 ms; a click closes it; the colour band at the left, the shield, "AEGIS" and the text (… when too long).
// Drawn per apple-design: a per-pixel-alpha window (smooth rounded corners and a soft two-layer shadow), a spring slide
// in (tension 270 / friction 18) and spring re-stacking (170 / 26) that can be retargeted at any moment; it reacts on
// pointer-down; hovering pauses the hold time and turns a fade-out back (interruptible).
// The app's own notices (ShowNotice, e.g. "still running in the notification area" the first time the window goes to the
// tray) use the same card with the app's name, icon A and the brand's teal band, as tall as their text needs.
// UI thread only.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Starpocket.Client.Aegis
{
    internal sealed class AegisToast : Form
    {
        // the ps1's sizes (logical pixels) and times
        const int CardW = 400, CardH = 78, EdgeMargin = 18, Gap = 10, MaxOpen = 4;
        const double AppearMs = 250, FullOpacity = 0.96, FadePerMs = 0.08 / 30.0;
        public const double WarningHoldMs = 6000, InfoHoldMs = 3500;
        /// <summary>The app's own notices are two sentences: longer to read (hovering still pauses it).</summary>
        public const double NoticeHoldMs = 9000;
        const int NoticeMaxH = 150;
        // room for the shadow around the card (inside the window, fully transparent at the edge)
        const int Pad = 14;

        static readonly List<AegisToast> Open = new List<AegisToast>();

        public static readonly Color Amber1 = Color.FromArgb(255, 186, 48), Amber2 = Color.FromArgb(170, 96, 0);
        public static readonly Color Green1 = Color.FromArgb(52, 211, 140), Green2 = Color.FromArgb(10, 110, 72);
        /// <summary>The app's notices: the brand's teal (the "standing by" colour of the tray dot).</summary>
        public static readonly Color Teal1 = Color.FromArgb(0x2E, 0xC4, 0xB6);

        readonly string text;
        /// <summary>null: an Aegis toast ("AEGIS" and the shield); else an app notice with this heading and icon A.</summary>
        readonly string title;
        /// <summary>The card's height in logical pixels (78 for Aegis, as the ps1).</summary>
        readonly int cardH;
        readonly bool warning;
        readonly double holdMs;
        readonly float k;
        readonly string lang;
        readonly Timer timer = new Timer { Interval = 15 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        double lastMs, heldMs, opacity;
        bool closing, dismissed, hover;
        Spring y;
        int x;
        IntPtr hBitmap = IntPtr.Zero, memDc = IntPtr.Zero, oldBitmap = IntPtr.Zero;
        Size bmpSize;

        /// <summary>Shows a toast (Toast.Show): warning = amber for 6 s, else green for 3.5 s.</summary>
        public static void Show(string text, bool warning, string lang)
        {
            MakeRoom();
            new AegisToast(text ?? "", warning, warning ? WarningHoldMs : InfoHoldMs, lang, null).Show();
        }

        /// <summary>A notice of the app itself (not Aegis): <paramref name="title"/> instead of "AEGIS", icon A instead of the
        /// shield, the brand's teal band, a card as tall as its text needs, 9 s. Same place, stacking and behaviour.</summary>
        public static void ShowNotice(string title, string text, string lang)
        {
            MakeRoom();
            new AegisToast(text ?? "", false, NoticeHoldMs, lang, string.IsNullOrEmpty(title) ? AppInfo.Name : title).Show();
        }

        static void MakeRoom()
        {
            while (Open.Count >= MaxOpen)
            {
                var oldest = Open[0];
                oldest.CloseNow();
                Open.Remove(oldest);
            }
        }

        /// <summary>Closes every toast (the app quits).</summary>
        public static void CloseAll()
        {
            foreach (var t in Open.ToArray()) t.CloseNow();
        }

        AegisToast(string text, bool warning, double holdMs, string lang, string title)
        {
            this.text = text; this.warning = warning; this.holdMs = holdMs; this.lang = lang; this.title = title;
            k = PrimaryScale();
            cardH = title == null ? CardH : NoticeHeight(title, text, lang, k);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Text = title ?? "Aegis";
            AccessibleName = (title ?? "Aegis") + ": " + text;
            AccessibleRole = AccessibleRole.Alert;
            bmpSize = new Size(S(CardW + Pad * 2), S(cardH + Pad * 2));
            var wa = Screen.PrimaryScreen.WorkingArea;
            x = wa.Right - S(CardW) - S(EdgeMargin) - S(Pad);
            double target = TargetTop(Open.Count);
            y = new Spring { X = target + S(16), Target = target, Tension = 270, Friction = 18 };   // slides up into place
            Bounds = new Rectangle(x, (int)Math.Round(y.X), bmpSize.Width, bmpSize.Height);
            Open.Add(this);
            FormClosed += (s, e) => { timer.Stop(); Open.Remove(this); Relayout(); };
            MouseDown += (s, e) => Dismiss();   // instant response: the fade starts on pointer-down
            MouseEnter += (s, e) => { hover = true; if (closing && !dismissed) closing = false; };   // a fade-out turns back
            MouseLeave += (s, e) => hover = false;
            timer.Tick += (s, e) => Tick();
        }

        int S(double v) => (int)Math.Round(v * k);

        /// <summary>Logical pixels kept free at the bottom right for the scan card (Shell\ScanPopup): the toasts stack
        /// above it instead of over it. 0 while no scan card is open.</summary>
        public static int ReservedBottom
        {
            get { return reservedBottom; }
            set { if (value == reservedBottom) return; reservedBottom = value; Relayout(); }
        }
        static int reservedBottom;

        /// <summary>The window's top for the n-th toast from the bottom (the card is 18 px above the work area's bottom).</summary>
        double TargetTop(int index)
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            double top = wa.Bottom - S(EdgeMargin) - S(Pad) - S(cardH) - S(reservedBottom);
            for (int j = 0; j < index && j < Open.Count; j++) top -= Open[j].S(Open[j].cardH) + S(Gap);   // the cards below (an app notice may be taller)
            return top;
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

        void Dismiss()
        {
            dismissed = true;
            closing = true;
        }

        void CloseNow()
        {
            if (!IsDisposed) Close();
        }

        static void Relayout()
        {
            for (int i = 0; i < Open.Count; i++)
            {
                var t = Open[i];
                if (t.IsDisposed) continue;
                t.y.Target = t.TargetTop(i);
                t.y.Tension = 170; t.y.Friction = 26;   // re-stacking: no bounce
            }
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Render();
            Push(0);
            lastMs = clock.Elapsed.TotalMilliseconds;
            timer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }   // a click never takes the focus from the game
            base.WndProc(ref m);
        }

        void Tick()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double dt = Math.Min(64, Math.Max(0, now - lastMs));
            lastMs = now;
            if (!closing)
            {
                if (now <= AppearMs) opacity = Math.Min(FullOpacity, now / AppearMs);
                else
                {
                    opacity = Math.Min(FullOpacity, opacity + dt * FadePerMs * 2);   // back to full after a turned-back fade
                    if (!hover) heldMs += dt;
                    if (heldMs > holdMs) closing = true;
                }
            }
            if (closing)
            {
                opacity = Math.Max(0, opacity - dt * FadePerMs);
                if (opacity <= 0.01) { Close(); return; }
            }
            y.Step(dt / 1000.0);
            Push(opacity);
        }

        /// <summary>Moves the window and sets its opacity (the picture stays the one Render made).</summary>
        void Push(double alpha)
        {
            if (!IsHandleCreated || memDc == IntPtr.Zero) return;
            var pos = new Pt { X = x, Y = (int)Math.Round(y.X) };
            var size = new Sz { W = bmpSize.Width, H = bmpSize.Height };
            var src = new Pt();
            var blend = new Blend { Op = 0, Flags = 0, Alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255))), Format = 1 };
            UpdateLayeredWindow(Handle, IntPtr.Zero, ref pos, ref size, memDc, ref src, 0, ref blend, 2);
        }

        /// <summary>The picture of a toast at scale <paramref name="k"/> (the card plus room for its shadow). The self-test
        /// draws one into a PNG without any window.</summary>
        public static Bitmap DrawCard(string text, bool warning, string lang, float k) => DrawCard(null, text, warning, lang, k, CardH);

        /// <summary>The picture of an app notice (heading + icon A, teal band, as tall as the text needs).</summary>
        public static Bitmap DrawNotice(string title, string text, string lang, float k) => DrawCard(title, text, false, lang, k, NoticeHeight(title, text, lang, k));

        /// <summary>The card height (logical pixels) an app notice needs: the heading, then the text wrapped to the text
        /// column (at least the Aegis card's 78, at most 150).</summary>
        public static int NoticeHeight(string title, string text, string lang, float k)
        {
            try
            {
                using (var probe = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(probe))
                using (var fText = UiFont(lang, 9.5f * 96f / 72f * k, FontStyle.Regular))
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    var size = g.MeasureString(text ?? "", fText, (int)Math.Floor((CardW - 84) * k));
                    int h = (int)Math.Ceiling(32 + size.Height / k + 12);
                    return Math.Max(CardH, Math.Min(NoticeMaxH, h));
                }
            }
            catch (Exception) { return CardH; }
        }

        static Bitmap DrawCard(string title, string text, bool warning, string lang, float k, int cardH)
        {
            bool notice = title != null;
            Color c1 = notice ? Teal1 : warning ? Amber1 : Green1, c2 = warning ? Amber2 : Green2;
            int R(double v) => (int)Math.Round(v * k);
            var bmp = new Bitmap(R(CardW + Pad * 2), R(cardH + Pad * 2), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;   // ClearType would fringe on a transparent window
                g.Clear(Color.Transparent);
                var card = new RectangleF(R(Pad), R(Pad), R(CardW), R(cardH));
                float r = 12f * k;
                // depth: an ambient shadow (wide, faint) and a direct one (close, a little below)
                for (int i = R(Pad) - 1; i >= 1; i--)
                {
                    int a = (int)(34.0 * Math.Pow(1.0 - i / (double)R(Pad), 2));
                    if (a <= 0) continue;
                    var sr = RectangleF.Inflate(card, i, i);
                    sr.Offset(0, 3f * k);
                    using (var p = Round(sr, r + i)) using (var b = new SolidBrush(Color.FromArgb(a, 4, 6, 20))) g.FillPath(b, p);
                }
                var direct = RectangleF.Inflate(card, 0.5f * k, 0.5f * k);
                direct.Offset(0, 1.5f * k);
                using (var p = Round(direct, r + 0.5f * k)) using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 10))) g.FillPath(b, p);
                using (var path = Round(card, r))
                {
                    using (var bg = new LinearGradientBrush(card, Color.FromArgb(250, 30, 34, 72), Color.FromArgb(250, 15, 17, 44), 90f)) g.FillPath(bg, path);
                    var clip = g.Clip;
                    g.SetClip(path);
                    using (var accent = new SolidBrush(c1)) g.FillRectangle(accent, card.X, card.Y, 4f * k, card.Height);
                    using (var hl = new Pen(Color.FromArgb(34, 255, 255, 255), 1f)) g.DrawLine(hl, card.X + r, card.Y + 0.5f, card.Right - r, card.Y + 0.5f);   // top light
                    g.Clip = clip;
                    using (var border = new Pen(Color.FromArgb(90, c1), 1f)) g.DrawPath(border, path);
                }
                if (notice) DrawBrand(g, new RectangleF(card.X + 15f * k, card.Y + 15f * k, 44f * k, 44f * k));
                else DrawShield(g, new RectangleF(card.X + 16f * k, card.Y + 14f * k, 42f * k, 48f * k), c1, c2);
                using (var fTitle = UiFont(lang, 10f * 96f / 72f * k, FontStyle.Bold))
                using (var fText = UiFont(lang, 9.5f * 96f / 72f * k, FontStyle.Regular))
                using (var tb = new SolidBrush(Color.White))
                using (var xb = new SolidBrush(Color.FromArgb(222, 230, 244)))
                using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
                {
                    g.DrawString(notice ? title : "AEGIS", fTitle, tb, card.X + 70f * k, card.Y + 10f * k);
                    g.DrawString(text ?? "", fText, xb, new RectangleF(card.X + 70f * k, card.Y + 32f * k, card.Width - 84f * k, card.Height - 38f * k), sf);
                }
            }
            return bmp;
        }

        /// <summary>Draws the card once into the window's memory DC (UpdateLayeredWindow then only moves and fades it).</summary>
        void Render()
        {
            using (var bmp = DrawCard(title, text, warning, lang, k, cardH))
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
        }

        /// <summary>A top-down 32-bit DIB with premultiplied alpha, as UpdateLayeredWindow(ULW_ALPHA) wants it.</summary>
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
                    Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                    Marshal.Copy(row, 0, bits + y * w * 4, row.Length);
                }
            }
            finally { bmp.UnlockBits(data); }
            return dib;
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

        /// <summary>Icon A (StarPocket Games) for the app's own notices.</summary>
        static void DrawBrand(Graphics g, RectangleF r)
        {
            try
            {
                using (var ico = Shell.Icons.Brand((int)Math.Round(r.Width)))
                using (var bmp = ico.ToBitmap())
                {
                    var old = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, r);
                    g.InterpolationMode = old;
                }
            }
            catch (Exception) { }
        }

        /// <summary>The same shield for the scan card (Shell\ScanPopup), so the two cards are one family.</summary>
        public static void DrawShieldFor(Graphics g, RectangleF r, Color c1, Color c2) => DrawShield(g, r, c1, c2);

        /// <summary>The same font rule for the scan card.</summary>
        public static Font UiFontFor(string lang, float px, FontStyle style) => UiFont(lang, px, style);

        /// <summary>The Aegis shield with its "A" mark (Art.DrawShield of the ps1).</summary>
        static void DrawShield(Graphics g, RectangleF r, Color c1, Color c2)
        {
            float w = r.Width, h = r.Height, x = r.X, y = r.Y;
            float l = x + w * 0.12f, rt = x + w * 0.88f, t = y + h * 0.06f, mid = x + w * 0.5f, b = y + h * 0.96f;
            using (var p = new GraphicsPath())
            {
                p.AddLine(mid, t, rt, t + h * 0.13f);
                p.AddBezier(rt, t + h * 0.13f, rt, y + h * 0.56f, rt - w * 0.06f, y + h * 0.74f, mid, b);
                p.AddBezier(mid, b, l + w * 0.06f, y + h * 0.74f, l, y + h * 0.56f, l, t + h * 0.13f);
                p.CloseFigure();
                using (var br = new LinearGradientBrush(r, c1, c2, 90f)) g.FillPath(br, p);
                using (var pen = new Pen(Color.FromArgb(235, 255, 255, 255), Math.Max(1.2f, r.Width / 22f))) g.DrawPath(pen, p);
            }
            float cx = r.X + r.Width / 2f, top = r.Y + r.Height * 0.26f, bot = r.Y + r.Height * 0.70f, half = r.Width * 0.20f;
            using (var pen = new Pen(Color.White, Math.Max(1.6f, r.Width / 11f)))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new[] { new PointF(cx - half, bot), new PointF(cx, top), new PointF(cx + half, bot) });
                g.DrawLine(pen, cx - half * 0.52f, r.Y + r.Height * 0.54f, cx + half * 0.52f, r.Y + r.Height * 0.54f);
            }
        }

        /// <summary>Art.UiFont: Yu Gothic UI → Meiryo UI (ja), Microsoft YaHei UI (zh-CN), Segoe UI; pixels.</summary>
        static Font UiFont(string lang, float px, FontStyle style)
        {
            string[] names = lang == "ja" ? new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" }
                           : (lang == "zh-CN" || lang == "zh") ? new[] { "Microsoft YaHei UI", "Segoe UI" }
                           : new[] { "Segoe UI" };
            foreach (var n in names)
            {
                try { var f = new Font(n, px, style, GraphicsUnit.Pixel); if (f.Name == n) return f; f.Dispose(); } catch (Exception) { }
            }
            return new Font(FontFamily.GenericSansSerif, px, style, GraphicsUnit.Pixel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            if (memDc != IntPtr.Zero)
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                DeleteDC(memDc);
                memDc = IntPtr.Zero;
            }
            if (hBitmap != IntPtr.Zero) { DeleteObject(hBitmap); hBitmap = IntPtr.Zero; }
            base.Dispose(disposing);
        }

        /// <summary>A damped spring (mass 1), stepped in small slices so it stays stable at any frame time; its target can
        /// change at any moment (the motion continues from where it is, with its speed).</summary>
        sealed class Spring
        {
            public double X, V, Target, Tension, Friction;

            public void Step(double dt)
            {
                if (dt <= 0) return;
                int n = Math.Max(1, (int)Math.Ceiling(dt / 0.004));
                double h = dt / n;
                for (int i = 0; i < n; i++)
                {
                    double a = -Tension * (X - Target) - Friction * V;
                    V += a * h;
                    X += V * h;
                }
                if (Math.Abs(X - Target) < 0.05 && Math.Abs(V) < 0.05) { X = Target; V = 0; }
            }
        }

        // ---- per-pixel alpha window
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
