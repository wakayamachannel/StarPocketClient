// v1.3: 自分のプロフィールの絵（src\Core\ProfileImage.cs）の自己診断。
//   - 取り込み: 300×200 / 100×100 の PNG と .ico が 256×256 の PNG の写しになる。ゴミ・21 MB・9000×9000・WebP は断られ、既にあった
//     写しは変わらない
//   - 起動時の読み込み: 無い / ゴミ / 3 MB の写しの扱い（壊れた写しは消えて av_broken）
//   - 消す: 無くても true
//   - Bridge の表、settings.json の profileName / profileAvatar、av_* の 11 の言葉が 3 言語にあること
// 窓は出さない。System.Drawing で作った小さな絵と、わざと壊したファイルを <work>\avatar の中だけで読み書きする。
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Starpocket.Client.Core;
using Starpocket.Client.Shell;

namespace Starpocket.Client.SelfTest
{
    internal static class ProfileSelfTests
    {
        public static void Run(SelfTestRunner r)
        {
            ImportTests(r);
            LoadTests(r);
            BridgeAndSettingsTests(r);
            WordTests(r);
            r.Section("");
        }

        const string L = "ja";
        static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // ------------------------------------------------------------------ 道具
        /// <summary>左から赤・緑・青の 3 つの帯を描いた PNG（中央で切った時に真ん中の緑が残る）。</summary>
        static string MakePng(string path, int w, int h)
        {
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.FillRectangle(Brushes.Red, 0, 0, w / 3, h);
                    g.FillRectangle(Brushes.Lime, w / 3, 0, w - 2 * (w / 3), h);
                    g.FillRectangle(Brushes.Blue, w - w / 3, 0, w / 3, h);
                }
                bmp.Save(path, ImageFormat.Png);
            }
            return path;
        }

        static bool HasPngSig(string path)
        {
            var b = new byte[8];
            using (var fs = File.OpenRead(path)) { if (fs.Read(b, 0, 8) < 8) return false; }
            return b.SequenceEqual(PngSig);
        }

        static string Sha(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(fs));
        }

        /// <summary>Image.FromFile はファイルを掴むので、using で確実に放す。</summary>
        static Size PngSize(string path)
        {
            using (var img = Image.FromFile(path)) return img.Size;
        }

        static Color PixelAt(string path, int x, int y)
        {
            using (var bmp = new Bitmap(path)) return bmp.GetPixel(x, y);
        }

        static uint Crc32(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + count; i++)
            {
                crc ^= data[i];
                for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            return crc ^ 0xFFFFFFFF;
        }

        static void Chunk(List<byte> png, string type, byte[] data)
        {
            var body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            png.AddRange(new[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length });
            png.AddRange(body);
            uint crc = Crc32(body, 0, body.Length);
            png.AddRange(new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
        }

        /// <summary>IHDR（w×h、RGB 8 bit）と IEND だけの短い PNG: 寸法は読めるが絵は無い。</summary>
        static byte[] HeaderOnlyPng(int w, int h)
        {
            var png = new List<byte>(PngSig);
            var ihdr = new byte[13];
            ihdr[0] = (byte)(w >> 24); ihdr[1] = (byte)(w >> 16); ihdr[2] = (byte)(w >> 8); ihdr[3] = (byte)w;
            ihdr[4] = (byte)(h >> 24); ihdr[5] = (byte)(h >> 16); ihdr[6] = (byte)(h >> 8); ihdr[7] = (byte)h;
            ihdr[8] = 8; ihdr[9] = 2; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(png, "IHDR", ihdr);
            Chunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        /// <summary>ノイズの ARGB を LockBits で流し込んだ、縮まない（大きい）正しい PNG。</summary>
        static string NoisePng(string path, int side)
        {
            var rnd = new Random(12345);
            using (var bmp = new Bitmap(side, side, PixelFormat.Format32bppArgb))
            {
                var data = bmp.LockBits(new Rectangle(0, 0, side, side), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var bytes = new byte[data.Stride * side];
                    rnd.NextBytes(bytes);
                    Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
                }
                finally { bmp.UnlockBits(data); }
                bmp.Save(path, ImageFormat.Png);
            }
            return path;
        }

        // ------------------------------------------------------------------ 取り込み
        static void ImportTests(SelfTestRunner r)
        {
            r.Section("profile picture: import");
            string d = r.NewDir("avatar");
            string target = Path.Combine(d, ProfileImage.FolderName, ProfileImage.FileName);

            r.Test("(1) a 300x200 PNG becomes the 256x256 copy", () =>
            {
                string src = MakePng(Path.Combine(d, "wide.png"), 300, 200);
                var res = ProfileImage.Import(src, target, L);
                r.Check("ok", res.Ok, res.Error + " / " + res.Reason);
                r.Check("the source's size is reported", res.Width == 300 && res.Height == 200, res.Width + "x" + res.Height);
                r.Check("avatar.png is there, in profile\\", File.Exists(target));
                r.Check("... and it is a PNG (the first 8 bytes)", HasPngSig(target));
                r.Check("... 256x256", PngSize(target) == new Size(ProfileImage.Size, ProfileImage.Size), PngSize(target).ToString());
                r.Check("... cut from the middle (the green band is in the centre)", PixelAt(target, 128, 128).G > 200 && PixelAt(target, 128, 128).R < 50);
                r.Check("the data: URL starts with the PNG prefix", res.DataUrl != null && res.DataUrl.StartsWith(ProfileImage.DataUrlPrefix, StringComparison.Ordinal));
                r.Check("... and carries the very bytes of the file", res.DataUrl != null && Convert.FromBase64String(res.DataUrl.Substring(ProfileImage.DataUrlPrefix.Length)).SequenceEqual(File.ReadAllBytes(target)));
                r.Check("no .tmp left", !File.Exists(target + ".tmp"));
                r.Check("the source is untouched", PngSize(src) == new Size(300, 200));
            });

            r.Test("(2) a 100x100 PNG is scaled up to 256x256", () =>
            {
                string src = MakePng(Path.Combine(d, "small.png"), 100, 100);
                var res = ProfileImage.Import(src, target, L);
                r.Check("ok", res.Ok, res.Error + " / " + res.Reason);
                r.Check("256x256", PngSize(target) == new Size(256, 256));
                r.Check("the size of the copy stays small", new FileInfo(target).Length <= ProfileImage.MaxStoredBytes);
            });

            r.Test("(3) random bytes named .png: refused, the old copy stays", () =>
            {
                string before = Sha(target);
                string junk = Path.Combine(d, "junk.png");
                var rnd = new Random(7);
                var bytes = new byte[4096];
                rnd.NextBytes(bytes);
                File.WriteAllBytes(junk, bytes);
                var res = ProfileImage.Import(junk, target, L);
                r.Check("not ok", !res.Ok);
                r.Equal("av_unreadable", S.T(L, "av_unreadable"), res.Error);
                r.Check("a reason for the log, without a path", !string.IsNullOrEmpty(res.Reason) && !res.Reason.Contains(d));
                r.Check("the copy did not change", Sha(target) == before);
                r.Check("no .tmp left", !File.Exists(target + ".tmp"));
            });

            r.Test("(4) a 21 MB file: refused before it is read", () =>
            {
                string before = Sha(target);
                string big = Path.Combine(d, "big.png");
                using (var fs = new FileStream(big, FileMode.Create, FileAccess.Write)) fs.SetLength(21L * 1024 * 1024);
                var res = ProfileImage.Import(big, target, L);
                r.Check("not ok", !res.Ok);
                r.Equal("av_toobig with the size", S.T(L, "av_toobig", "21.0 MB"), res.Error);
                r.Check("nothing written", Sha(target) == before && !File.Exists(target + ".tmp"));
                r.Check("exactly 20 MB is not too big (the limit itself)", ProfileImage.MaxFileBytes == 20L * 1024 * 1024);
            });

            r.Test("(5) a 9000x9000 header with no picture: refused, no copy", () =>
            {
                string alone = Path.Combine(d, "alone");
                Directory.CreateDirectory(alone);
                string t2 = Path.Combine(alone, ProfileImage.FileName);
                string huge = Path.Combine(d, "huge.png");
                File.WriteAllBytes(huge, HeaderOnlyPng(9000, 9000));
                var res = ProfileImage.Import(huge, t2, L);
                string toomany = S.T(L, "av_toomany", 9000, 9000), unreadable = S.T(L, "av_unreadable");
                r.Check("not ok", !res.Ok);
                r.Check("av_toomany (or av_unreadable when GDI+ throws first)", res.Error == toomany || res.Error == unreadable, res.Error);
                r.Info("9000x9000: " + res.Reason);
                r.Check("no copy was made", !File.Exists(t2) && !File.Exists(t2 + ".tmp"));
                r.Check("the pixel limit is 30 million", ProfileImage.MaxPixels == 30_000_000);
            });

            r.Test("(6) an .ico (Bitmap -> GetHicon -> Icon.Save) is taken in", () =>
            {
                string ico = Path.Combine(d, "mark.ico");
                using (var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); g.FillEllipse(Brushes.Orange, 4, 4, 56, 56); }
                    IntPtr h = bmp.GetHicon();
                    try
                    {
                        using (var icon = Icon.FromHandle(h))
                        using (var fs = File.Create(ico)) icon.Save(fs);
                    }
                    finally { Native.DestroyIcon(h); }
                }
                var res = ProfileImage.Import(ico, target, L);
                r.Check("ok", res.Ok, res.Error + " / " + res.Reason);
                r.Check("256x256 PNG", File.Exists(target) && HasPngSig(target) && PngSize(target) == new Size(256, 256));
                r.Check("the orange disc is in the middle", PixelAt(target, 128, 128).R > 200 && PixelAt(target, 128, 128).B < 50);
            });

            r.Test("(9) a WebP (RIFF....WEBP) is refused without decoding", () =>
            {
                string before = Sha(target);
                string webp = Path.Combine(d, "photo.webp");
                var bytes = Encoding.ASCII.GetBytes("RIFF").Concat(new byte[] { 0x24, 0, 0, 0 }).Concat(Encoding.ASCII.GetBytes("WEBPVP8 ")).Concat(new byte[24]).ToArray();
                File.WriteAllBytes(webp, bytes);
                var res = ProfileImage.Import(webp, target, L);
                r.Check("not ok", !res.Ok);
                r.Equal("av_webp", S.T(L, "av_webp"), res.Error);
                r.Check("the copy did not change", Sha(target) == before);
                r.Check("the file filter offers no WebP", !S.T(L, "av_filter").Contains("webp") && !S.T("en", "av_filter").Contains("webp") && !S.T("zh-CN", "av_filter").Contains("webp"));
            });

            r.Test("a file that is not there", () =>
            {
                var res = ProfileImage.Import(Path.Combine(d, "nothing.png"), target, L);
                r.Check("av_nofile", !res.Ok && res.Error == S.T(L, "av_nofile"), res.Error);
                r.Check("null too", !ProfileImage.Import(null, target, L).Ok);
            });

            r.Test("(7) delete: gone, and a second delete is still true", () =>
            {
                string error;
                r.Check("the copy is there first", File.Exists(target));
                r.Check("deleted", ProfileImage.Delete(target, out error) && error == null && !File.Exists(target));
                r.Check("deleting nothing is fine", ProfileImage.Delete(target, out error) && error == null);
            });
        }

        // ------------------------------------------------------------------ 起動時の読み込み
        static void LoadTests(SelfTestRunner r)
        {
            r.Section("profile picture: load at start");
            string d = r.NewDir("avatar-load");
            string target = Path.Combine(d, ProfileImage.FolderName, ProfileImage.FileName);
            r.Test("(8) nothing saved: null, no error", () =>
            {
                string error;
                r.Check("null", ProfileImage.Load(target, L, out error) == null && error == null, error);
            });
            r.Test("(8) a good copy comes back as its bytes", () =>
            {
                string src = MakePng(Path.Combine(d, "src.png"), 120, 90);
                r.Check("import ok", ProfileImage.Import(src, target, L).Ok);
                string error;
                var png = ProfileImage.Load(target, L, out error);
                r.Check("the bytes of the file, no error", png != null && error == null && png.SequenceEqual(File.ReadAllBytes(target)));
                r.Check("DataUrl has the prefix", ProfileImage.DataUrl(png).StartsWith(ProfileImage.DataUrlPrefix, StringComparison.Ordinal));
            });
            r.Test("(8) rubbish in avatar.png: null, av_broken, and the file goes", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, Encoding.ASCII.GetBytes("this is not a png at all"));
                string error;
                var png = ProfileImage.Load(target, L, out error);
                r.Check("null + av_broken", png == null && error == S.T(L, "av_broken"), error);
                r.Check("the broken copy is gone (it is not read again at every start)", !File.Exists(target));
            });
            r.Test("(8) a PNG signature with a broken body: the same", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, PngSig.Concat(new byte[200]).ToArray());
                string error;
                r.Check("null + av_broken, gone", ProfileImage.Load(target, L, out error) == null && error == S.T(L, "av_broken") && !File.Exists(target), error);
            });
            r.Test("(8) a good PNG of 3 MB: too big to be ours", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                NoisePng(target, 1024);   // ノイズは縮まない: 1024×1024×4 ≈ 4 MB
                long len = new FileInfo(target).Length;
                r.Check("the file really is over 2 MB", len > ProfileImage.MaxStoredBytes, len + " bytes");
                string error;
                r.Check("null + av_broken, gone", ProfileImage.Load(target, L, out error) == null && error == S.T(L, "av_broken") && !File.Exists(target), error);
            });
        }

        // ------------------------------------------------------------------ Bridge と settings.json
        static void BridgeAndSettingsTests(SelfTestRunner r)
        {
            r.Section("profile picture: bridge and settings");
            // (10)
            string[] cmds = { "profile.set", "profile.pickImage", "profile.clearImage" };
            r.Check("the three commands are the app's", cmds.All(c => Bridge.Classify(c) == Bridge.Kind.Supported), string.Join(",", cmds.Where(c => Bridge.Classify(c) != Bridge.Kind.Supported)));
            r.Check("none of them is 'later' any more", cmds.All(c => !Bridge.Later.ContainsKey(c)));
            r.Check("none of them waits for a long task (like pickSteam)", cmds.All(c => !Bridge.BusyGated.Contains(c)));
            r.Check("profileName / profileAvatar are not settings.set keys (profile.set owns them)", !Bridge.SettingKeys.Contains("profileName") && !Bridge.SettingKeys.Contains("profileAvatar"));
            // (11)
            r.Test("settings.json: profileName / profileAvatar", () =>
            {
                string d = r.NewDir("avatar-settings");
                string p = Path.Combine(d, "settings.json");
                var s = new ClientSettings();
                r.Check("the defaults: no name, picture 0, no profile", s.ProfileName == "" && s.ProfileAvatar == 0 && !s.HasProfile);
                s.Save(p);
                string json = File.ReadAllText(p);
                r.Check("the defaults write neither key", !json.Contains("profileName") && !json.Contains("profileAvatar"), json);
                r.Check("SetProfile(ほしぞら, 3)", s.SetProfile("ほしぞら", 3) && s.ProfileName == "ほしぞら" && s.ProfileAvatar == 3 && s.HasProfile);
                s.Save(p);
                var back = ClientSettings.Load(p);
                r.Check("comes back after Save -> Load", back.ProfileName == "ほしぞら" && back.ProfileAvatar == 3, back.ProfileName + "/" + back.ProfileAvatar);
                r.Check("17 characters are cut to 16", s.SetProfile("あいうえおかきくけこさしすせそたち", 0) && s.ProfileName.Length == 16 && s.ProfileName == "あいうえおかきくけこさしすせそた");
                r.Check("control characters are dropped", s.SetProfile("a\tb\nc", 1) && s.ProfileName == "abc");
                r.Check("avatar 6 is refused, nothing changes", !s.SetProfile("x", 6) && s.ProfileName == "abc" && s.ProfileAvatar == 1);
                r.Check("avatar \"x\" is refused", !s.SetProfile("x", "x") && s.ProfileAvatar == 1);
                r.Check("avatar -1 is refused", !s.SetProfile("x", -1));
                r.Check("avatar 2.5 is refused", !s.SetProfile("x", 2.5));
                r.Check("a JSON long / decimal / \"5\" are read like the volume", s.SetProfile("y", 5L) && s.ProfileAvatar == 5 && s.SetProfile("y", 4m) && s.ProfileAvatar == 4 && s.SetProfile("y", "2") && s.ProfileAvatar == 2);
                r.Check("name null means \"\"", s.SetProfile(null, 2) && s.ProfileName == "" && s.HasProfile);
                r.Check("name only: still a profile", s.SetProfile("z", 0) && s.HasProfile);
                r.Check("back to the defaults: no profile", s.SetProfile("", 0) && !s.HasProfile);
                s.Save(p);
                json = File.ReadAllText(p);
                r.Check("... and neither key is written again", !json.Contains("profileName") && !json.Contains("profileAvatar"), json);
                File.WriteAllText(p, "{\"close\":\"tray\",\"lang\":\"auto\",\"profileName\":12,\"profileAvatar\":9}", new UTF8Encoding(false));
                var bad = ClientSettings.Load(p);
                r.Check("a number for the name and 9 for the picture are ignored", bad.ProfileName == "" && bad.ProfileAvatar == 0 && !bad.HasProfile);
                File.WriteAllText(p, "{\"close\":\"tray\",\"lang\":\"auto\",\"profileName\":\"0123456789abcdefXYZ\",\"profileAvatar\":\"4\"}", new UTF8Encoding(false));
                var longName = ClientSettings.Load(p);
                r.Check("a long name in the file is cut, \"4\" reads as 4", longName.ProfileName == "0123456789abcdef" && longName.ProfileAvatar == 4);
            });
            r.Check("the copy lives under DataDir\\profile\\avatar.png", ProfileImage.FolderName == "profile" && ProfileImage.FileName == "avatar.png" && ProfileImage.Size == 256);
        }

        // ------------------------------------------------------------------ 言葉
        static void WordTests(SelfTestRunner r)
        {
            r.Section("profile picture: words");
            // (12)
            string[] keys = { "av_pick", "av_filter", "av_toobig", "av_toomany", "av_unreadable", "av_webp", "av_nofile", "av_write", "av_broken", "av_set", "av_cleared" };
            foreach (var l in Lang.Codes)
            {
                var missing = keys.Where(k => string.IsNullOrEmpty(S.T(l, k)) || S.T(l, k) == k || !S.Table[l].ContainsKey(k)).ToArray();
                r.Check("all " + keys.Length + " words are in " + l, missing.Length == 0, string.Join(",", missing));
            }
            r.Check("the size goes into av_toobig", Lang.Codes.All(l => S.T(l, "av_toobig", "12.3 MB").Contains("12.3 MB")));
            r.Check("the pixels go into av_toomany", Lang.Codes.All(l => S.T(l, "av_toomany", 8000, 6000).Contains("8000×6000")));
            r.Check("av_set names 256×256", Lang.Codes.All(l => S.T(l, "av_set", 300, 200).Contains("300×200") && S.T(l, "av_set", 300, 200).Contains("256×256")));
            r.Check("the file filter has two halves and the six kinds", Lang.Codes.All(l => S.T(l, "av_filter").Split('|').Length == 2 && S.T(l, "av_filter").Contains("*.png") && S.T(l, "av_filter").Contains("*.ico") && S.T(l, "av_filter").Contains("*.gif")));
            r.Check("the WebP refusal points at PNG / JPG", Lang.Codes.All(l => S.T(l, "av_webp").Contains("PNG") && S.T(l, "av_webp").Contains("JPG")));
        }
    }
}
