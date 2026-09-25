# StarPocket Client

**English summary** (the rest of this file is in Japanese; a longer English section for reviewers is in [`docs/CODE-SIGNING.md`](docs/CODE-SIGNING.md)):

- StarPocket Client is the launcher app of StarPocket Games for *PocketRoles*, a host-only mod for the Steam version of Among Us.
- It installs the mod into a separate copy of the game, keeps that copy in sync with the Steam copy, launches it, and runs the Aegis pre-play checks in the same process.
- It is one native Windows process (C# WinForms + WebView2, .NET Framework 4.8, x86) that Task Manager shows as "StarPocket Client", not a PowerShell script.
- The app sends nothing: every request is a plain GET. Downloads (the mod, BepInEx) happen only on a button press, from an allow-list of https hosts enforced in `src\Core\Downloads.cs`; the one automatic request is Aegis's signed definitions file from GitHub.
- The BepInEx zip's SHA-256 is pinned in `src\AppInfo.cs` and checked before anything is unpacked (`docs\BEPINEX-PIN.md`).
- Build: `dotnet build -c Release` on Windows with the .NET SDK 8 (details under "ビルド" below). Running the app needs the Microsoft Edge WebView2 runtime.
- Licence: GPL-3.0-or-later (`LICENSE`, `NOTICE`). Third-party notices and what the app downloads on request are listed in `NOTICE`.
- This project is not affiliated with or endorsed by Innersloth LLC; the disclaimer and the Among Us Mod Policy link are at the end of this file.
- The exe is not code-signed yet (`docs\CODE-SIGNING.md`).

StarPocket Games のランチャーアプリです（`StarPocket Client.exe`）。今の PowerShell 版（PocketRoles Launcher と Aegis のトレイ）を 1 つのアプリにまとめます。タスクマネージャーには「Windows PowerShell」ではなく「StarPocket Client」と出ます。

- C# WinForms + WebView2、.NET Framework 4.8（Windows 10 1903 以降・11 に最初から入っている）、x86
- 画面は `ui\`（プロトタイプの HTML）を WebView2 で表示します。画面はネットに出ません（`app.starpocket.local` 以外への読み込みはすべて止めます）
- **アプリがネットに送るものはありません**（受け取るだけです）。自動でつなぐのは Aegis の定義ファイルの取得だけです（アプリを開いた時に 1 回と、そのあと窓を開いた時かプレイを押した時に前回から 12 時間以上たっていればもう 1 回。GitHub の `wakayamachannel/PocketRoles` の `main/aegis/definitions.txt` と `.sig` を受け取る。1 つにつき 8 秒で打ち切り。確かめ方は PowerShell 版の `Aegis.ps1`（PocketRoles リポジトリの `aegis\Aegis.ps1`。このリポジトリには入っていません）と同じ。この PC の情報は送りません）
- そのほかのダウンロード（MOD の更新の確認・MOD 本体・BepInEx）は、**ボタンを押した時だけ**です。取りに行ってよいのは https の 4 つのホスト（`api.github.com`・`github.com`・`objects.githubusercontent.com`・`builds.bepinex.dev`）だけで、これはコードで止めています（`src\Core\Downloads.cs`）
- `"StarPocket Client.exe" --verify-download` は、配布元から BepInEx の zip を 1 つだけ受け取り、書き留めてある SHA-256 と同じか・中身を取り出せるかを確かめて、すぐ全部消します。**ゲームのフォルダーには一切触れません**（作業用は `%TEMP%\StarPocket-verify` だけ。終わったら丸ごと消します）。公開の前に 1 回だけ手で走らせるものです（[`docs/BEPINEX-PIN.md`](docs/BEPINEX-PIN.md)）
- 画面を描く Microsoft Edge WebView2 ランタイム（Microsoft の部品。アプリが起動する `msedgewebview2.exe`）は、Windows の診断データの設定と Microsoft のプライバシーに関する声明に従って、Microsoft と通信することがあります（自分の更新の確認など）。画面（`ui`）そのものはネットから何も読み込みません
- ライセンス: GPL-3.0-or-later（`LICENSE`・`NOTICE`）
- 行動規範: [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) ／ セキュリティの知らせ方: [`SECURITY.md`](SECURITY.md)
- コード署名（Windows の署名）は**まだしていません**。説明は [`docs/CODE-SIGNING.md`](docs/CODE-SIGNING.md) です

## v0.1 の中身

| 入っているもの | 場所 |
|---|---|
| 1 つのプロセス・1 つだけ起動（2 回目は 1 つ目の窓を前に出して終わる） | `src\Shell\SingleInstance.cs` |
| 枠なしの窓 + WebView2（ui フォルダを仮想ホストで表示、外への移動・新しい窓・ダウンロードを止める） | `src\Shell\MainForm.cs` |
| UI とのやりとり（v0.1 の命令と「この版ではまだ使えません」） | `src\Shell\Bridge.cs`・`ClientApp.cs`・`ui\host-v01.js` |
| トレイ（開く / プレイ / Aegis の状態 / もう一度スキャン / 終了、3 言語、Aegis の状態の点） | `src\Shell\TrayController.cs` |
| タスクバー（進み具合 ITaskbarList3・Aegis が問題を見つけた時のバッジ） | `src\Shell\Taskbar.cs` |
| 閉じるボタン（既定はトレイへ。設定で「アプリを終了」）。初めてトレイに入った時に 1 回だけ「通知領域で動いています」の知らせ | `settings.json`（`%LOCALAPPDATA%\StarPocket\Client`）・`src\Shell\ClientApp.cs` |
| v1.1: 起動のたびに Aegis のスキャン（13 項目）を右下の小窓に出す。窓が出てから小窓が出て、問題なしなら 1.8 秒で自分で消える。赤い項目があれば小窓はクリックするまで残り、画面の Aegis パネルも開く。設定 → 全般 → 起動時の動作で切れる（既定はオン。切っても赤い項目の時はパネルは開く）。`settings.json` の `startScan` | `src\Shell\ClientApp.cs`（`UpdateScanCard`）・`ScanPopup.cs`・`src\Core\ClientSettings.cs`・`ui\` |
| WebView2 が無い時の窓（Microsoft の公式ページを開くボタンと「もう一度確かめる」。勝手に入れない） | `src\Shell\WebView2Gate.cs` |
| exe の隣のファイルが足りない時（exe だけをコピーした時など）の知らせ | `src\Core\PackageFiles.cs`・`src\Program.cs` |
| 「mod 付きで起動」（今のランチャーと同じフォルダの決め方・確認・引数） | `src\Core\GameLauncher.cs`・`GameFolders.cs`・`InstallStatus.cs` |
| v0.1.1: 設定 → PocketRoles の「起動するゲーム」（PocketRoles / ふつうの Among Us（Steam））。ふつうを選ぶと、プレイとトレイの「プレイ」は `steam://rungameid/945360` を開くだけ（MOD なし。起動前スキャン・ログの保存・MOD 用のコピーには触らない）。プレイの字の下に小さくゲームの名前、その下の行に、どちらが起動するかと「変更」。起動のあとは、ゲームが動き出すまで（最長 30 秒）「プレイ中」。選んだものは `settings.json` の `startGame`。設定の「修復」は、下向きの矢印が受け皿に入る絵 | `src\Core\ClientSettings.cs`・`GameLauncher.cs`（`LaunchVanilla`）・`src\Shell\Bridge.cs`・`ui\` |
| 前回のログの保存と 30 日の削除（今のランチャーと同じ規則・同じ Mutex） | `src\Core\GameLogs.cs` |
| Aegis（PowerShell 版の `Aegis.ps1`（PocketRoles リポジトリの `aegis\Aegis.ps1`）のトレイと起動前スキャンを同じプロセスの中へ）: 起動時のスキャン 13 項目、署名付きの定義ファイル（同梱・キャッシュ・起動時に 1 回の取得と、12 時間たった後に窓を開いた時かプレイの時の取り直し、古い版に戻さない）、ゲーム中のログの見張りと Aegis 独自の通知、events.log の 30 日の整理、起動前スキャン（赤があればプレイを止める） | `src\Aegis\`（`AegisService.cs` が入口。シェルとの境目は `IAegisService.cs`） |
| 同梱の定義ファイル（MOD のリリースと同じバイト）。**このリポジトリには入れていません**（リリースの添付として配っています。下の「Aegis」） | `aegis\definitions.txt`・`aegis\definitions.txt.sig`（exe の隣に置く） |
| v1.3: フレンド欄のプロフィールの絵に自分の画像（PNG / JPG / ICO / BMP / GIF、20 MB・3000 万画素まで。WebP はこの版では読まない）。アプリが画像を選ぶ窓を開き、正方形 256px の PNG の写しを `%LOCALAPPDATA%\StarPocket\Client\profile\avatar.png` に置く（元の画像は触らない・どこにも送らない）。「画像をやめて元の絵に戻す」で写しを消す。読めない画像は元の絵のままで理由を出す | `src\Core\ProfileImage.cs`・`src\Shell\ClientApp.cs`・`ui\` |
| 画面なしの自己テスト | `src\SelfTest\` |

## v0.2 の中身（インストール・同期・更新）

| 入っているもの | 場所 |
|---|---|
| インストールと修復（5 手順: Steam 版を探す → ゲームのコピー → BepInEx → MOD 本体 → 仕上げ。失敗した手順から続けられます）。コピーもダウンロードも展開もアプリ自身が行います（robocopy も PowerShell も呼びません） | `src\Core\Installer.cs`・`FileCopy.cs`・`Downloads.cs`・`ZipFiles.cs` |
| ダウンロードは https の 4 つのホストからだけ。さらに **BepInEx の zip は、そのファイルの SHA-256 が書き留めてある値と同じかどうかを、展開する前に確かめます**。違えば消して、ゲームには何も入れません。値が書いていない版は断ります（「確認できないけれど、とりあえず入れる」はしません） | `src\AppInfo.cs`（`BepSha256`）・`src\Core\FileHash.cs`・`Installer.CheckBepZip`・**`docs\BEPINEX-PIN.md`**（値の変え方） |
| Steam 版との同期、MOD の更新の確認（GitHub Releases） | `Installer.SyncGameCopy`・`CheckUpdate`・`src\Core\ReleaseInfo.cs` |
| `launcher-state.json` の読み書きと、今までのランチャーからの一度きりの引き継ぎ（デスクトップのショートカットの指す先から探します。元のファイルは変えません） | `src\Core\LauncherStateFile.cs`・`ShellLink.cs` |
| 外のプログラムを起動する唯一の場所（ゲーム・`steam://` の 2 つ・Microsoft のページ・フォルダ・テキストファイル・作者へのメールだけ） | `src\Core\ShellOpen.cs` |

## v0.3 の中身（報告 zip・ショートカット・アンインストール）

| 入っているもの | 場所 |
|---|---|
| 報告 zip（今のログ・`launcher-state.json`・アプリのログ・過去のログ 3 件・Aegis の証拠の記録・`system.txt`）。ログの中の PUID・フレンドコード・記録を消すためのコード・API キー・Discord の webhook・Windows のユーザー名は伏せます。MOD の `.cfg` や鍵は**入れません** | `src\Core\ReportBuilder.cs`・`Masking.cs` |
| Aegis の証拠の記録は直近 90 日分（200 件・5 MB まで）。ログは 30 日で消えるので、消す直前に、残る記録を裏づけるログの 2 行を `evidence\<id>.log` に残します（MOD 側のブランチ `evidence-90d` の決まり） | `src\Core\EvidenceStore.cs`・`EvidenceText.cs` |
| ひとり分の証拠の zip（記録を消すためのコード・フレンドコード・証拠 ID から探します。フレンドコードもそのハッシュも入りません。作者にだけ送るものです） | `ReportBuilder.ExportOne` |
| 報告 zip とひとり分の zip は 30 日で自動で消えます | `src\Core\GameLogs.cs` |
| メールは `mailto:` を開くだけです。**アプリがネットに送るものはありません**（宛先は作者の 3 つだけ） | `ShellOpen.MailTo` |
| ショートカット（デスクトップ / スタートメニュー）は**ボタンを押した時だけ**作ります。消す時は、その `.lnk` がこの exe を指している時だけ | `src\Core\Shortcuts.cs` |
| 「Windows の起動時に開く」は、設定のスイッチを入れた時だけ `HKCU\...\Run` に値を 1 つ書きます。既定はオフで、切ると消します | `AutoStart`（`Shortcuts.cs`） |
| アンインストール（設定 →「アンインストール」。コマンドからも `"<exe>" --uninstall`、無人は `--uninstall --quiet`）。アプリ自身のデータ・アプリが作ったショートカット・起動時の値を消します。MOD 用のゲームのコピーはチェックボックス（**既定は残す**。そのフォルダに `Among Us.exe` がある時だけ）。Steam と Steam 版の Among Us、今までのランチャーとトレイが使う記録には**触りません**。セットアップのプログラムがまだ無いので、**「アプリと機能」に項目はできません** | `src\Core\Uninstaller.cs`・`src\Program.cs` |
| 公開の GitHub Actions でソースからビルド（秘密の値なし・署名なし）。ソースを読んで「`ShellOpen.cs` 以外でプログラムを起動していない」ことも確かめます | `.github\workflows\build.yml`・`docs\CODE-SIGNING.md` |

**アンインストールのしかた**（2026-09-23）。窓が開いている間は、アプリ自身が自分のデータフォルダを掴んでいます（WebView2 のプロファイルとログ）。そこを消そうとすると必ず途中で止まるので、窓の中の「アンインストール」は**聞くだけ**にして、アプリを閉じたあと（WebView2 を手放したあと）に消します。コマンドからの `--uninstall` は、何も開く前に実行します（ほかの Client が動いている時とゲームが動いている時は断ります）。**アプリ自身のデータが消せなかった時は、ほかには一切触りません**（記録だけ消えて中身が残る、という中途半端な状態を作らないため）。動いている exe は自分を消せないので、アプリのフォルダは必ず残り、名前を出して知らせます。

ファイルの関連付けは、どの版でも書きません。開発モードの再ビルド・ログの窓・画面なしの実行・BAN 管理は後の版です（`docs\PORT-MAP.md` の 1 章と 14.5 章）。2026-09-23 のレビューで直したことは `docs\PORT-MAP.md` の 15 章にまとめてあります。

## ビルド

```
dotnet build -c Release
```

必要なもの:

- Windows（WinForms + WebView2 のアプリなので、ビルドも Windows で行います。公開の CI は `windows-latest`）
- .NET SDK 8（`.github\workflows\build.yml` は `8.0.x` に固定。`dotnet` が PATH に無い置き方をしているときは、その `dotnet.exe` をフルパスで呼びます）
- .NET Framework 4.8 の参照アセンブリ（Visual Studio か「.NET Framework 4.8 Developer Pack」に入っています。無い PC では .NET SDK が NuGet から `Microsoft.NETFramework.ReferenceAssemblies.net48` を自動で取ってくるので、SDK だけでもビルドできます）
- NuGet に届くこと（`Microsoft.Web.WebView2` のパッケージ。版は csproj に固定してあります）
- 動かすには Microsoft Edge WebView2 ランタイム（Windows 11 には最初から入っています。無いときは、アプリが Microsoft の公式ページを開くボタンを出します。勝手には入れません）

できるもの: `bin\Release\StarPocket Client.exe` と、隣の `ui\`・WebView2 の DLL・`LICENSE`・`NOTICE`・`licenses\`（WebView2 のライセンスの本文と NOTICE、歯車アイコンの元である Feather Icons の MIT の本文）。定義ファイルをこのリポジトリに入れていないので、**`aegis\` はビルドでは作られません**（リリースのときに、添付の `definitions.txt` と `.sig` を exe の隣の `aegis\` に置きます。下の「Aegis」）。

Release の exe には、ビルドした PC のフォルダの名前が入りません（`PathMap` で `C:\src\StarPocketClient` に置き換え）。CI では `-p:ContinuousIntegrationBuild=true` を付けます。

## 自己テスト（窓もトレイも出さない）

```
"bin\Release\StarPocket Client.exe" --self-test <フォルダ>
```

ネットにも本物のゲームフォルダにも触らず、書くのは `<フォルダ>` の中だけです。`PASS` / `FAIL` の行と `<フォルダ>\self-test.txt` を出し、全部通れば終了コード 0 です。作業用の `<フォルダ>\work` は、前の自己テストが作ったもの（中に `.starpocket-selftest` がある）だけを消して作り直します。そうでない `work` があれば、何も消さずに `FAIL` で終わります。

exe はウィンドウのあるプログラムなので、PowerShell から呼ぶ時は出力をつながないと終わるのを待ちません。待たせるには `& ".\bin\Release\StarPocket Client.exe" --self-test <フォルダ> | Out-Host` のように書きます（CI もこの形です）。

最後の組（`src\SelfTest\IdentitySelfTests.cs`）は、ビルドされた exe そのものを読みます: タスクマネージャーに出る名前・会社・版、exe の中のマニフェストの版（csproj と同じか）、アイコン（`RT_GROUP_ICON`）、そして `powershell.exe`・`cmd.exe`・`robocopy` などの名前が exe の中に 1 つも無いこと。

## UI の作り直し

`ui\index.html` は、このリポジトリの中のプロトタイプ `design\launcher-proto\index.html` から作ります。プロトタイプを直したら:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\import-ui.ps1
```

Google Fonts を外し、クルーのアイコン（Innersloth のデザイン）を `ui\img\pocketroles-128.png` に取り出し（exe の中には入れない）、`ui\host-v01.js` 用のつなぎを足します（古いプロトタイプに非公式の中文の用語が残っていれば、公式のものに直します）。

## 用語の確認（マージの時）

中文は Among Us の公式の用語を使います。マージの前に:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\terms\check-terms.ps1 . -Allow tools\terms\check-terms.allow.tsv
```

0 件なら終了コード 0 です。直す先の言葉は `check-terms.ps1` の中の表にあり、用語集がなくても動きます（ゲームから取り出した用語集 `glossary.tsv` は Innersloth の文なので、このリポジトリには入れません。手元にあれば `-Glossary <ファイル>` で、直す先が今のゲームの言葉と同じかも確かめます）。

## Aegis

PowerShell 版の Aegis、つまり PocketRoles リポジトリ（`wakayamachannel/PocketRoles`）の `aegis\Aegis.ps1`（v0.5.5）の判断と文言をそのまま C# に移しました（`docs\PORT-MAP.md` 11 章）。**`Aegis.ps1` はこのリポジトリには入っていません**（定義ファイルもこのリポジトリには入れていないので、`aegis\` はこのリポジトリには存在しません。下の定義ファイルの行）。

| ファイル | 中身 | 元（PocketRoles の `Aegis.ps1`） |
|---|---|---|
| `src\Aegis\AegisService.cs` | 開始（Mutex・整理・定義・取得・開始時のスキャン・見張り）、起動前スキャン、もう一度スキャン、状態 | `Entry`・`Tray` |
| `src\Aegis\AegisScan.cs` | スキャン 13 項目（レジストリとプロセスの名前は `IAegisSystem` 経由） | `Check`・`Scanner`・`Splash` の判定 |
| `src\Aegis\DefinitionsSignature.cs` | 署名の確認と信頼する鍵の一覧 | `Sig` |
| `src\Aegis\DefinitionsStore.cs` | 定義の読み込み・取得・保存（古い版に戻さない） | `Defs` |
| `src\Aegis\LogWatcher.cs` | ゲームの見張り・ログの続き読み・行の判断・通知の間引き | `Tray.Poll`・`ReadLog`・`Line`・`Balloon` |
| `src\Aegis\AegisToast.cs` | Aegis 独自の通知の小窓（apple-design） | `Toast` |
| `src\Aegis\EventsLog.cs` | `events.log`・`aegis.log` | `EventsLog`・`AddEvent`・`Write-AegisLog` |
| `src\Aegis\AegisText.cs` | 文言（ja / zh-CN / en。中文は公式の用語） | `S` |

- データは今と同じ `%LOCALAPPDATA%\PocketRoles\Aegis`（PowerShell 版の `Aegis.ps1` と共有）。Mutex も同じ名前なので、古い Aegis トレイとは同時に動きません（古い方が先に動いていれば、アプリの Aegis はそれが終わるまで待ちます）。
- 自己テスト（`src\SelfTest\AegisSelfTests.cs`）は、偽のレジストリ・偽のプロセス一覧・その場で作った署名の鍵・偽のダウンロードで、判断の道すじを 1 つずつ確かめます。
- **定義ファイル（`aegis\definitions.txt` と `.sig`）はこのリポジトリには入れず、リリースの添付として配っています**（検知する名前の一覧を公開リポジトリに置かないため。`.gitignore`）。exe の隣の `aegis\` に置くと同梱として使われ、無ければアプリは組み込みの一覧（26 件）で動き、GitHub から取得した署名付きの定義があればそちらを使います（`src\Aegis\DefinitionsStore.cs`）。

## Innersloth について（免責）

This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.

上の英文は `NOTICE` の「Among Us disclaimer」と同じものです。このアプリと PocketRoles は Innersloth LLC とは無関係で、Innersloth の承認も後援も受けていません。公式サーバーで使うときは Innersloth の Among Us Mod Policy に従います: https://www.innersloth.com/among-us-mod-policy/
