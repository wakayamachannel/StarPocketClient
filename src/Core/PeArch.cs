// ============================================================================
//  PeArch.cs … exe / dll が 32bit なのか 64bit なのかを見る（PORT-MAP 外・2026-10-01 追加）
//
//  なぜ要るのか:
//    2026-09-29 の Among Us（2026.9.29 / v19.0.0）から、本体が 32bit → **64bit** になりました。
//    BepInEx は winhttp.dll という名前でゲームに割り込みます。Windows は、**ゲームと種類の違う dll を
//    読み込みません**。つまり 32bit の winhttp.dll が入ったままだと、64bit のゲームでは
//    「読み込みに失敗する」のではなく **何も起きません**。MOD は無いのと同じになります。
//
//    しかもそれは、ファイルの有無からは見えません。winhttp.dll も BepInEx\core も、ちゃんとそこに在るからです。
//    この部品はその一点 ──「ゲームと同じ種類か」── だけを答えます。
//
//  読み方:
//    PE ファイルの先頭 0x3C に「PE ヘッダの位置」が 4 バイトで入っていて、そこから
//    署名 "PE\0\0"（4 バイト）の次の 2 バイトが Machine（種類）です。それ以上は読みません。
//    読めない・PE でない場合は Unknown(0) を返します。**この部品は決して例外を投げません。**
// ============================================================================

using System;
using System.IO;

namespace Starpocket.Client.Core
{
    internal static class PeArch
    {
        internal const ushort Unknown = 0x0000;
        internal const ushort X86 = 0x014C;
        internal const ushort X64 = 0x8664;
        internal const ushort Arm64 = 0xAA64;

        /// <summary>この exe / dll の種類。読めなければ <see cref="Unknown"/>（＝判断しない）。</summary>
        internal static ushort Machine(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Unknown;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < 0x40) return Unknown;
                    var head = new byte[0x40];
                    if (fs.Read(head, 0, 0x40) != 0x40) return Unknown;
                    if (head[0] != (byte)'M' || head[1] != (byte)'Z') return Unknown;      // DOS ヘッダでない
                    int at = head[0x3C] | (head[0x3D] << 8) | (head[0x3E] << 16) | (head[0x3F] << 24);
                    if (at < 0 || at + 6 > fs.Length) return Unknown;
                    fs.Position = at;
                    var pe = new byte[6];
                    if (fs.Read(pe, 0, 6) != 6) return Unknown;
                    if (pe[0] != (byte)'P' || pe[1] != (byte)'E' || pe[2] != 0 || pe[3] != 0) return Unknown;
                    return (ushort)(pe[4] | (pe[5] << 8));
                }
            }
            catch (Exception) { return Unknown; }
        }

        /// <summary>画面とログに出す短い名前。訳しません（32bit / 64bit は 3 言語とも同じ書き方）。</summary>
        internal static string Name(ushort machine)
        {
            switch (machine)
            {
                case X86: return "32bit";
                case X64: return "64bit";
                case Arm64: return "ARM64";
                default: return "?";
            }
        }

        /// <summary>
        /// <paramref name="dll"/> が <paramref name="exe"/> に割り込める種類か。
        /// **どちらか一方でも読めなかったら true を返します**（＝分からないことを理由に、動いている物を壊さない）。
        /// </summary>
        internal static bool Matches(string exe, string dll)
        {
            ushort a = Machine(exe), b = Machine(dll);
            if (a == Unknown || b == Unknown) return true;
            return a == b;
        }
    }
}
