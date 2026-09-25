// Icon A (StarPocket Games) from the exe's own resources, and the small pictures drawn at run time:
// the tray icon with Aegis's status dot (SPEC 5.3) and the taskbar badge (SPEC 5.2 / PORT-MAP 4.4).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace Starpocket.Client.Shell
{
    /// <summary>An icon made from a bitmap; its HICON is destroyed with it.</summary>
    internal sealed class OwnedIcon : IDisposable
    {
        IntPtr handle;
        public Icon Icon { get; private set; }

        public OwnedIcon(Bitmap bmp)
        {
            handle = bmp.GetHicon();
            Icon = Icon.FromHandle(handle);
        }

        public void Dispose()
        {
            if (Icon != null) { Icon.Dispose(); Icon = null; }
            if (handle != IntPtr.Zero) { Native.DestroyIcon(handle); handle = IntPtr.Zero; }
        }
    }

    internal static class Icons
    {
        public const string ResourceName = "Starpocket.Client.starpocket.ico";
        static byte[] icoBytes;

        // the status colours of the prototype's legend (aegis.l1-l3) and the brand
        public static readonly Color Idle = Color.FromArgb(0x2E, 0xC4, 0xB6);      // teal: standing by
        public static readonly Color Watching = Color.FromArgb(0x18, 0xC3, 0x7E);  // green: watching a game
        public static readonly Color Kicked = Color.FromArgb(0xF4, 0xBA, 0x45);    // amber: 12 s after a removal
        public static readonly Color Off = Color.FromArgb(0x8E, 0x93, 0xA8);       // grey: Aegis not running
        public static readonly Color Serious = Color.FromArgb(0xE6, 0x39, 0x46);   // red: the last scan found a red row
        public static readonly Color Navy = Color.FromArgb(0x1E, 0x22, 0x57);

        public static byte[] IcoBytes
        {
            get
            {
                if (icoBytes == null)
                {
                    using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                    {
                        if (s == null) throw new InvalidOperationException("missing resource " + ResourceName);
                        using (var ms = new MemoryStream()) { s.CopyTo(ms); icoBytes = ms.ToArray(); }
                    }
                }
                return icoBytes;
            }
        }

        /// <summary>Icon A with all its sizes (the window picks what it needs).</summary>
        public static Icon Brand() => new Icon(new MemoryStream(IcoBytes));

        /// <summary>Icon A at one size (the closest image of the .ico).</summary>
        public static Icon Brand(int size) => new Icon(new MemoryStream(IcoBytes), new Size(size, size));

        public static Color StateColor(string state)
        {
            switch (state)
            {
                case "idle": return Idle;
                case "watching": return Watching;
                case "kicked": return Kicked;
                default: return Off;
            }
        }

        static Graphics Smooth(Bitmap bmp)
        {
            var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            return g;
        }

        /// <summary>The tray icon: icon A with Aegis's status dot at the bottom right, ringed so it reads on light and dark taskbars.</summary>
        public static OwnedIcon Tray(int size, Color dot)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Smooth(bmp))
                using (var brand = Brand(size))
                using (var brandBmp = brand.ToBitmap())
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(brandBmp, new Rectangle(0, 0, size, size));
                    float d = Math.Max(5f, size * 0.44f);
                    float ring = Math.Max(1f, size / 16f * 1.25f);
                    var r = new RectangleF(size - d, size - d, d, d);
                    // punch a transparent gap around the dot (reads as a separate badge, like the prototype's rail dot)
                    g.CompositingMode = CompositingMode.SourceCopy;
                    using (var clear = new SolidBrush(Color.Transparent)) g.FillEllipse(clear, RectangleF.Inflate(r, ring, ring));
                    g.CompositingMode = CompositingMode.SourceOver;
                    using (var b = new SolidBrush(dot)) g.FillEllipse(b, r);
                    using (var hi = new LinearGradientBrush(r, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
                        g.FillEllipse(hi, r);
                }
                return new OwnedIcon(bmp);
            }
        }

        /// <summary>The taskbar overlay badge: a filled disc with an exclamation mark (drawn, not a font glyph, so it stays crisp at 16 px).</summary>
        public static OwnedIcon Badge(int size, Color fill, Color mark)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Smooth(bmp))
                {
                    g.Clear(Color.Transparent);
                    float s = size;
                    var disc = new RectangleF(0.5f, 0.5f, s - 1f, s - 1f);
                    using (var b = new SolidBrush(fill)) g.FillEllipse(b, disc);
                    using (var edge = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(1f, s / 16f))) g.DrawEllipse(edge, disc);
                    float w = Math.Max(2f, s * 0.14f);
                    float cx = s / 2f;
                    using (var mb = new SolidBrush(mark))
                    {
                        using (var bar = RoundedBar(new RectangleF(cx - w / 2f, s * 0.2f, w, s * 0.4f), w / 2f)) g.FillPath(mb, bar);
                        float dd = w * 1.1f;
                        g.FillEllipse(mb, new RectangleF(cx - dd / 2f, s * 0.68f, dd, dd));
                    }
                }
                return new OwnedIcon(bmp);
            }
        }

        static GraphicsPath RoundedBar(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 180);
            p.AddArc(r.X, r.Bottom - d, d, d, 0, 180);
            p.CloseFigure();
            return p;
        }
    }
}
