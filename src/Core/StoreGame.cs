// ============================================================================
//  StoreGame.cs … Microsoft Store / Xbox 版の Among Us が入っているかを見るだけ（2026-10-01 追加）
//
//  なぜ要るのか:
//    Store / Xbox (PC Game Pass) 版を持っている人がこのアプリを開くと、今までこう出ていました。
//      「Steam 版の Among Us が見つかりません。Among Us.exe があるフォルダを選んでください」
//    でも、その人に**選べるフォルダはありません**。Store 版のゲームは
//    C:\Program Files\WindowsApps\InnerSloth.LLC-AmongUs_… という Windows の保護フォルダーにあり、
//    中を見るだけでも所有権の変更（＝管理者権限）が要ります。
//    仮に指定できたとしても、Store 版は**コピーして動く作りではありません**（パッケージの権利確認が通らない）。
//    つまり、できないことを「やってください」と案内していました。
//
//  なぜ Store 版に対応しないのか（持ち主の判断、2026-10-01）:
//    1. **管理者権限が要る。** このアプリは app.manifest を asInvoker にして「管理者権限を使わない」と
//       docs\CODE-SIGNING.md にも配布ページにも書いています。Store 対応はその約束を捨てることになります。
//    2. **「あなたのゲームに触らない」という作りを壊す。** 今は別のコピーを作って元には触りません。
//       Store 版でやるなら WindowsApps の中を直接書き換えることになり、それは避けてきたやり方そのものです。
//    3. **Microsoft 自身が止めている。** 権限を変えると Store アプリが壊れることがある、と案内しています。
//       壊れたら、このアプリのせいにされます。
//    そして **Store / Xbox の人は、もう遊べます**。この MOD はホストだけが入れればよいので、
//    Steam 版のホストが立てた部屋に、今のゲームのまま部屋コードで入れます。
//
//  だからここでやるのは「**入っていることに気づいて、正しいことを言う**」だけです。
//
//  見方:
//    HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages
//    の下に、入っているパッケージの名前が並びます。**管理者権限なしで読めます**（2026-10-01 に実測。
//    この PC では 226 個読めました）。中身は見ません。名前があるかどうかだけです。
// ============================================================================

using System;
using Microsoft.Win32;

namespace Starpocket.Client.Core
{
    internal static class StoreGame
    {
        /// <summary>入っているパッケージの名前が並ぶ所（この利用者の分。管理者権限は要りません）。</summary>
        private const string PackagesKey =
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        /// <summary>Among Us のパッケージの名前の頭。Store 版も Xbox (PC Game Pass) 版も同じ発行者です。</summary>
        private const string Prefix = "InnerSloth.LLC-AmongUs";

        /// <summary>
        /// Microsoft Store / Xbox 版の Among Us が入っていれば、そのパッケージの名前。無ければ null。
        /// **読めなかった時も null**（分からないことを理由に、別のことを言わないため）。決して例外を投げません。
        /// </summary>
        internal static string Find()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(PackagesKey))
                {
                    if (k == null) return null;
                    foreach (var name in k.GetSubKeyNames())
                        if (name != null && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                            return name;
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>入っているか（<see cref="Find"/> が名前を返すか）。</summary>
        internal static bool Installed() => Find() != null;

        /// <summary>
        /// 「Steam 版が見つからない」時に出す文の鍵。Store / Xbox 版が入っている人には、
        /// 選べないフォルダを選べと言わず、**今のゲームのまま遊べる**ことを伝えます。
        /// 判断はここ 1 か所だけです（Installer と DevBuild の両方から呼びます）。
        /// </summary>
        internal static string NoGameKey() => Installed() ? "in_store_only" : "in_steam_notfound";

        /// <summary>
        /// パッケージの名前から版を取り出します（例 InnerSloth.LLC-AmongUs_1.2.3.0_x64__8wekyb3d8bbwe → 1.2.3.0）。
        /// 取り出せなければ null。画面に出すためだけの物で、これで何かを判断はしません。
        /// </summary>
        internal static string VersionOf(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return null;
            var parts = packageName.Split('_');
            return parts.Length >= 2 && parts[1].Length > 0 && char.IsDigit(parts[1][0]) ? parts[1] : null;
        }
    }
}
