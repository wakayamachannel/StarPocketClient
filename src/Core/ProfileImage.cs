// 自分のプロフィールの絵（v1.3、持ち主 2026-09-24: フレンド欄のアイコンに自分の画像を使えるように）。
//
// 窓を出さない純粋ロジック。選ぶ窓（OpenFileDialog）を出すのは Shell\ClientApp.PickAvatarImage で、ここは「選ばれたファイルを
// 読んで、正方形 256×256 の PNG の写しを DataDir\profile\avatar.png に置き、ページに渡す data: の URL を作る」だけ。
//
// 決めごと:
//   - 元の画像は触らない。写しだけを %LOCALAPPDATA%\StarPocket\Client\profile\avatar.png に置く（ClientContext.AvatarPath）。
//     アンインストールは DataDir ごと消す（Uninstaller.cs）。どこにも送らない: ページには data: の URL で渡すだけで、ページは
//     ネットに出ない（MainForm が app.starpocket.local 以外を全部 403 にする）。
//   - 上限は 3 つ。ファイルは 20 MB まで（MaxFileBytes: 全バイトを読むので、それ以上は読まずに断る）、画素は 3000 万まで
//     （MaxPixels: 寸法だけ先に読み、大きすぎる絵はデコードしない。GDI+ は 1 画素 4 バイトで持つので 120 MB が上限）、
//     写しは 2 MB まで（MaxStoredBytes: 256×256 の PNG は大きくても 260 KB ほど。それ以上の avatar.png は誰かが置き換えた
//     ものなので読まない）。
//   - 読める形式は GDI+ が読むもの（PNG / JPG / BMP / GIF）と .ico。WebP はこの版では読まない: WIC（PresentationCore）を
//     参照すると csproj（署名の条件が書いてある）に手を入れることになるので、RIFF…WEBP の署名を見て av_webp で断る。
//   - 読めない画像は元の絵のまま（既にあった写しは変えない）。理由は S.T の日本語文（Error）で画面へ、短い英語（Reason）で
//     client.log へ。選んだ元の画像のパスは、アカウント名が入るので、ログにも返事にも書かない。
//   - 書く時は settings.json と同じ手順（.tmp に書いて File.Replace / Move）: 途中で落ちても半分の avatar.png は残らない。
//   - 起動時（Load）は写しの先頭 8 バイトが PNG の署名で、GDI+ で開けることまで見る。開けない写しは消して av_broken を
//     1 回だけ言い、元の絵に戻る（壊れたファイルを毎回の起動で読み続けない）。
//   - JPG の EXIF の向き（0x0112）は RotateFlip で直す（スマホの写真は横に寝ていることが多い）。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Starpocket.Client.Core
{
    /// <summary>Import の答え: Ok なら Width / Height（元の寸法）と DataUrl、でなければ Error（画面へ）と Reason（ログへ）。</summary>
    internal sealed class ProfileImageResult
    {
        public bool Ok;
        /// <summary>S.T の日本語文（ページの toast）。</summary>
        public string Error;
        /// <summary>ログ用の短い英語。パスは入れない。</summary>
        public string Reason;
        public int Width, Height;
        public string DataUrl;
    }

    internal static class ProfileImage
    {
        public const int Size = 256;
        public const long MaxFileBytes = 20L * 1024 * 1024;
        public const long MaxPixels = 30_000_000;
        public const long MaxStoredBytes = 2L * 1024 * 1024;
        public const string FolderName = "profile";
        public const string FileName = "avatar.png";
        public const string DataUrlPrefix = "data:image/png;base64,";

        static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>選ばれた画像を読み、正方形 256×256 の PNG の写しを <paramref name="targetPath"/> に置く。断る時は何も書かず、
        /// 既にあった写しも変えない。投げない（読めない画像は av_unreadable）。</summary>
        public static ProfileImageResult Import(string sourcePath, string targetPath, string lang)
        {
            var r = new ProfileImageResult();
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return Refuse(r, lang, "av_nofile", "no file");
            long length;
            try { length = new FileInfo(sourcePath).Length; }
            catch (Exception) { return Refuse(r, lang, "av_nofile", "no file"); }
            if (length > MaxFileBytes) return Refuse(r, lang, "av_toobig", "too big (" + Mb(length) + ")", Mb(length));
            byte[] bytes;
            try { bytes = File.ReadAllBytes(sourcePath); }
            catch (Exception) { return Refuse(r, lang, "av_nofile", "cannot read"); }
            // WebP はデコードしない（上の決めごと）
            if (IsWebP(bytes)) return Refuse(r, lang, "av_webp", "webp");
            byte[] png;
            try
            {
                using (var ms = new MemoryStream(bytes, false))
                using (var src = IsIco(bytes) ? IcoBitmap(ms) : Image.FromStream(ms, false, false))   // 寸法だけ先に（validateImageData: false）
                {
                    r.Width = src.Width;
                    r.Height = src.Height;
                    if (r.Width <= 0 || r.Height <= 0) return Refuse(r, lang, "av_unreadable", "empty");
                    if ((long)r.Width * r.Height > MaxPixels)
                        return Refuse(r, lang, "av_toomany", "too many pixels (" + r.Width + "x" + r.Height + ")", r.Width, r.Height);
                    ApplyOrientation(src);
                    using (var bmp = SquareResize(src, Size))
                    using (var outMs = new MemoryStream())
                    {
                        bmp.Save(outMs, ImageFormat.Png);
                        png = outMs.ToArray();
                    }
                }
            }
            catch (ArgumentException) { return Refuse(r, lang, "av_unreadable", "unreadable"); }
            catch (ExternalException) { return Refuse(r, lang, "av_unreadable", "unreadable (gdi+)"); }
            catch (OutOfMemoryException) { return Refuse(r, lang, "av_unreadable", "unreadable (memory)"); }
            catch (InvalidOperationException) { return Refuse(r, lang, "av_unreadable", "unreadable (state)"); }
            if (png.Length > MaxStoredBytes) return Refuse(r, lang, "av_unreadable", "copy too big");
            // settings.json と同じ手順: .tmp に書いてから置き換える
            string tmp = targetPath + ".tmp";
            try
            {
                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(tmp, png);
                if (File.Exists(targetPath)) File.Replace(tmp, targetPath, null);
                else File.Move(tmp, targetPath);
            }
            catch (Exception ex)
            {
                TryDelete(tmp);
                r.Error = S.T(lang, "av_write", Mask.Home(ex.Message, Environment.GetEnvironmentVariable("USERPROFILE")));
                r.Reason = "write failed (" + ex.GetType().Name + ")";
                return r;
            }
            r.Ok = true;
            r.DataUrl = DataUrl(png);
            return r;
        }

        /// <summary>起動時: 写しの PNG のバイト列。無ければ null で <paramref name="error"/> も null。2 MB 超・PNG の署名違い・GDI+ で
        /// 開けない写しは消して null、<paramref name="error"/> は av_broken（元の絵に戻る。理由は 1 回だけ言う）。</summary>
        public static byte[] Load(string targetPath, string lang, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) return null;
            byte[] bytes = null;
            bool good = false;
            try
            {
                if (new FileInfo(targetPath).Length <= MaxStoredBytes)
                {
                    bytes = File.ReadAllBytes(targetPath);
                    if (HasPngSignature(bytes))
                    {
                        using (var ms = new MemoryStream(bytes, false))
                        using (var img = Image.FromStream(ms, false, true))
                            good = img.Width > 0 && img.Height > 0;
                    }
                }
            }
            catch (ArgumentException) { }
            catch (ExternalException) { }
            catch (OutOfMemoryException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (good) return bytes;
            TryDelete(targetPath);
            error = S.T(lang, "av_broken");
            return null;
        }

        /// <summary>ページに渡す形: data:image/png;base64,… （CSP の img-src data: で通る）。</summary>
        public static string DataUrl(byte[] png) => DataUrlPrefix + Convert.ToBase64String(png);

        /// <summary>「画像をやめて元の絵に戻す」: 写しを消す。無くても true。消せなければ false と理由（パスは伏せる）。</summary>
        public static bool Delete(string targetPath, out string error)
        {
            error = null;
            TryDelete(targetPath + ".tmp");
            try
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);
                return true;
            }
            catch (Exception ex)
            {
                error = Mask.Home(ex.Message, Environment.GetEnvironmentVariable("USERPROFILE"));
                return false;
            }
        }

        /// <summary>中央で正方形に切り、HighQualityBicubic で <paramref name="size"/> に縮める（Format32bppArgb、透明はそのまま）。</summary>
        public static Bitmap SquareResize(Image src, int size)
        {
            int side = Math.Min(src.Width, src.Height);
            var crop = new Rectangle((src.Width - side) / 2, (src.Height - side) / 2, side, side);
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            try
            {
                using (var g = Graphics.FromImage(bmp))
                using (var attrs = new ImageAttributes())
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    attrs.SetWrapMode(WrapMode.TileFlipXY);   // 縁が半透明ににじまないように
                    g.DrawImage(src, new Rectangle(0, 0, size, size), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, attrs);
                }
                return bmp;
            }
            catch
            {
                bmp.Dispose();
                throw;
            }
        }

        // ---- 中身
        static ProfileImageResult Refuse(ProfileImageResult r, string lang, string key, string reason, params object[] args)
        {
            r.Ok = false;
            r.Error = S.T(lang, key, args);
            r.Reason = reason;
            return r;
        }

        /// <summary>"12.3 MB" の形（InvariantCulture）。</summary>
        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

        static bool IsWebP(byte[] b) =>
            b.Length >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
            && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P';

        static bool IsIco(byte[] b) => b.Length >= 4 && b[0] == 0 && b[1] == 0 && b[2] == 1 && b[3] == 0;

        static bool HasPngSignature(byte[] b)
        {
            if (b == null || b.Length < PngSignature.Length) return false;
            for (int i = 0; i < PngSignature.Length; i++) if (b[i] != PngSignature[i]) return false;
            return true;
        }

        /// <summary>.ico: 256×256 に一番近い絵を Bitmap に（小さい絵しか無ければそれを拡大する）。</summary>
        static Image IcoBitmap(Stream ms)
        {
            using (var icon = new Icon(ms, Size, Size)) return icon.ToBitmap();
        }

        /// <summary>EXIF の向き（0x0112）どおりに回す。読めない・無い時は何もしない。</summary>
        static void ApplyOrientation(Image img)
        {
            try
            {
                if (Array.IndexOf(img.PropertyIdList, 0x0112) < 0) return;
                var p = img.GetPropertyItem(0x0112);
                if (p == null || p.Value == null || p.Value.Length < 1) return;
                RotateFlipType flip;
                switch (p.Value[0])
                {
                    case 2: flip = RotateFlipType.RotateNoneFlipX; break;
                    case 3: flip = RotateFlipType.Rotate180FlipNone; break;
                    case 4: flip = RotateFlipType.Rotate180FlipX; break;
                    case 5: flip = RotateFlipType.Rotate90FlipX; break;
                    case 6: flip = RotateFlipType.Rotate90FlipNone; break;
                    case 7: flip = RotateFlipType.Rotate270FlipX; break;
                    case 8: flip = RotateFlipType.Rotate270FlipNone; break;
                    default: return;
                }
                img.RotateFlip(flip);
            }
            catch (Exception) { }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }
    }
}
