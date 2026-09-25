# コード署名について / Code signing

このページは、SignPath Foundation の無料コード署名に申し込む時に必要な説明です。`.github/workflows/build.yml` のコメントからここを指しています。
**オーナーが実際に行う手順（申し込みの順番・自分で作るもの・どこにも貼ってはいけないもの）は、このリポジトリには入れていません。**オーナーの手元の控えにあります。

> まだ署名はされていません。署名を受けたら、この行を README とサイトにも出します:
> **Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/)**

最後に中身を確かめた日: 2026-09-23（v1.0.0 のレビューで、ここに書いてあることを 1 つずつコードと突き合わせて直しました）。

---

## 日本語

### 1. 何のプログラムか

`StarPocket Client.exe` は、Among Us の**ホスト専用 MOD「PocketRoles」**を入れて起動するためのランチャーです。今まで PowerShell のスクリプト（`PocketRolesLauncher.ps1` と `aegis\Aegis.ps1`）で行っていたことを、1 つのネイティブアプリにまとめたものです。作っているのは StarPocket Games（個人）で、ソースはすべてこのリポジトリにあります。

- C# / WinForms / WebView2、.NET Framework 4.8、x86
- 版: **1.0.0**（`StarpocketClient.csproj` の `FileVersion` = `app.manifest` の `assemblyIdentity version` = exe の `FileVersion` = `1.0.0.0` = `src\AppInfo.cs` の `Version`。CI と自己テストが毎回この 4 つを突き合わせます）
- ライセンス: GPL-3.0-or-later（`LICENSE`・`NOTICE`）
- 管理者権限は使いません（`app.manifest` の `requestedExecutionLevel level="asInvoker"`）。インストール先もレジストリも、そのユーザーの分だけです（HKCU）。

### 2. ソースからのビルド

```
dotnet restore StarpocketClient.csproj
dotnet build   StarpocketClient.csproj -c Release -p:ContinuousIntegrationBuild=true
```

できるもの: `bin\Release\StarPocket Client.exe`

- 公開の GitHub Actions（`.github/workflows/build.yml`、`windows-latest`）で、push のたびに同じ手順でビルドしています。**秘密の値は 1 つも使っていません**（ワークフローに `secrets.` の字は 1 つもありません。権限は `contents: read` だけです）。
- 取ってくるものは NuGet の `Microsoft.Web.WebView2`（版を固定: 1.0.3485.44）と、`uses:` で指定した GitHub の action だけです。
  action は `actions/checkout@v4` のように**大きい版だけ**を書いています。これは動く目印なので、同じ行のままでも中身が入れ替わることがあります（コミットの SHA での固定は、まだ行っていません）。
- `ContinuousIntegrationBuild` と csproj の `PathMap` により、ビルドしたマシンのパスは exe に入りません（同じソースから同じものができます）。
- CI は、ビルドのあとに次の 3 つも行います。どれかが落ちればビルドは赤くなります。
  1. `bin` と `obj` を除く**すべての `.cs`** を読み、`Process.Start(`・`new Process`・`ShellExecute…(`・`CreateProcess…(`・`WinExec(`・`EntryPoint="ShellExecute…` が `src\Core\ShellOpen.cs` 以外に無いこと
  2. 画面を出さない自己テスト `--self-test`。件数は CI の「Self-test」の段が `RESULT PASS n/n` として出します（この文章には件数を書きません。作業のたびに増えるからです）
  3. exe の素性（名前・会社・版・マニフェストの版・`AppInfo.Version`・アイコンのファイル）

### 3. 署名してもらうもの

**`StarPocket Client.exe` の 1 つだけ**です。

| ファイル | 署名 |
|---|---|
| `StarPocket Client.exe` | **これを署名してもらいます** |
| `Microsoft.Web.WebView2.Core.dll`・`Microsoft.Web.WebView2.WinForms.dll`・`WebView2Loader.dll` | Microsoft が署名済み。こちらは触りません |
| `ui\`（HTML・JS・画像）、`aegis\definitions.txt(.sig)`、`LICENSE`、`NOTICE`、`licenses\` | データとテキスト。署名しません |
| MOD 本体の `PocketRoles.dll` | Among Us のゲームファイルが要るため公開 CI でビルドできません。**署名の対象にしません** |

CI が保存する成果物（`StarPocketClient-unsigned`）が、そのまま配布する形です（build.yml:107-121）。

### 4. SignPath の条件に対する答え（2026-09-23 に 1 つずつコードで確かめました）

| 条件 | このアプリでは | 証拠 |
|---|---|---|
| アンインストールがある | **アプリの設定 →「アンインストール」**から行えます。アプリ自身のデータが消せなかった時は、ほかには一切触りません。コマンドからも同じことができます（`"<exe>" --uninstall`、無人は `--uninstall --quiet`）。**今は「アプリと機能」に項目はできません**。zip を展開して使う形なので、セットアップのプログラムがまだ無く、項目を書く人がいないからです（9. を見てください） | `src\Core\Uninstaller.cs`（`Allowed` / `Plan` / `Run` / `DeleteTree`）、`src\Program.cs`（`--uninstall` と `FinishUninstall`）。自己テスト「uninstall」 |
| 断りなくシステムを変えない | ショートカットは設定のボタンを押した時だけ。「Windows の起動時に開く」は設定のスイッチを入れた時だけで**既定はオフ**（`HKCU\...\Run` の値 1 つ）。ファイルの関連付けは 1 つも作りません（`Software\Classes` を書く所はありません）。管理者権限は使いません。**アプリが自分で「アプリと機能」に項目を書くことはありません**（書くための関数 `AppsAndFeatures.Register` はありますが、呼んでいるのは自己テストだけです。セットアップを作る時にここから使います） | `src\Core\Shortcuts.cs`（`Create` / `AutoStart` / `AppsAndFeatures`）、`src\Shell\ClientApp.cs`（ボタンとスイッチ）、`app.manifest` |
| 断りなくデータを送らない | **アプリがネットに送るものはありません。** 通信はすべて GET で、送るのは User-Agent だけです（Cookie なし・本文なし・テレメトリなし）。ソースに `POST`・アップロード・メール送信のコードは 1 つもありません。報告 zip はユーザーのデスクトップに作るだけで、送るのは本人がメールソフトで添付して送信を押した時だけです（`mailto:` を開くだけ） | `src\Core\Downloads.cs:69,83`（GET だけ）、`src\Aegis\DefinitionsStore.cs:178,200`、`src\Core\ShellOpen.cs:46`（宛先は作者の 3 つだけ）`:156`（`MailTo`）、`src\Core\ReportBuilder.cs:79,304` |
| 公開 CI でソースからビルドできる | 上の 2. のとおりです。**公開リポジトリの GitHub Actions が、秘密の値を 1 つも使わずにソースから exe をビルドします**（`windows-latest`・`dotnet build -c Release`・ソースの確認・画面なしの自己テスト・exe の素性の確認。権限は `contents: read` だけ）。配るものは、その回の成果物 `StarPocketClient-unsigned` そのものです。ビルドの決まりはこのリポジトリの中にあるので、**上げた時点から実行の記録が残ります**（9. の 1.） | `.github\workflows\build.yml` |
| マルウェア／PUP のふるまいをしない | ほかのプログラムを起動するのは `src\Core\ShellOpen.cs` の 1 か所だけで、起動してよいものは決め打ちの一覧だけです（MOD 用のコピーの `Among Us.exe`、`steam://` の 2 つ、Microsoft の WebView2 のページ、ユーザーのフォルダとテキストファイル、プロジェクトの 3 つのメールアドレス宛の `mailto:`）。**シェルは一切起動しません**（`powershell.exe`・`cmd.exe`・`robocopy`・`wscript`・`schtasks`・`reg.exe` はどれも使いません。ファイルのコピーも zip の展開もレジストリもアプリの中で行います）。CI がソースを読んでこれを毎回確かめ、自己テストが**ビルドされた exe の中にそれらの名前が無いこと**を確かめます。自分自身を更新する仕組みはまだありません。開発モードだけの例外が 1 つあります（下の 8.） | `src\Core\ShellOpen.cs:40-46`（一覧）`:52`（`Allowed`）`:130`（`Start`）、`.github\workflows\build.yml:48-63`、`src\SelfTest\IdentitySelfTests.cs:92-102` |
| 独自アイコン | exe のアイコンは StarPocket のアイコン A（`assets\starpocket.ico`）です。MOD の乗組員アイコンは Innersloth のデザインなので exe には入れません（画面の中で MOD を指す時だけ、exe の外のファイル `ui\img\pocketroles-128.png` として使います）。自己テストが、exe の中にその絵のバイトが無いことと、exe の資源がアイコン A だけであることを確かめます | `StarpocketClient.csproj:22,67`、`src\SelfTest\IdentitySelfTests.cs:78-90`、`src\SelfTest\ShellSelfTests.cs:961-970` |
| ほかの人のものを正しく扱う | GPL-3.0 の本文（`LICENSE`）と `NOTICE`、WebView2 の BSD 3-clause の本文と NOTICE、歯車アイコンの元である Feather Icons の MIT の本文（`licenses\`）を exe の隣に置きます。自己テストが、その 5 つが本当に置かれているかを毎回確かめます | `StarpocketClient.csproj:70-76`、`src\SelfTest\ShellSelfTests.cs:999-1021` |

### 5. アプリがつなぐ先

**画面のボタンを押した時**（インストール・修復・更新の確認）、**または本人が `--action install` / `--action check` を実行した時**に、次の 4 つのホストからの **GET** を行います。`src\Core\Downloads.cs` の `WebFetch.AllowedUrl` が、https であることとこのホスト名であることを**コードで強制**します。リダイレクト先も同じ一覧を通ります（ファイルを落とす `Download` も、文章を取る `GetText` も、同じ `WebFetch.CheckArrivedFrom` を通ります）。

| ホスト | 何のため |
|---|---|
| `api.github.com` | MOD の最新リリースを読む |
| `github.com` / `objects.githubusercontent.com` | そのリリースの `PocketRoles-<版>.zip` |
| `builds.bepinex.dev` | BepInEx 6.0.0-be.735（ファイルの一覧のページと zip） |

**自動で行う通信は 1 つだけです。** Aegis の検知ルールの定義ファイル（`raw.githubusercontent.com` の `wakayamachannel/PocketRoles` の `main/aegis/definitions.txt` と `.sig`）を、**アプリを開いた時に 1 回**受け取ります。そのあとは、窓を開いた時かプレイを押した時に、前回から 12 時間以上たっていればもう一度受け取ります（`src\Aegis\AegisService.cs:129`・`:175`、`src\Shell\ClientApp.cs:339,768`）。1 つにつき 8 秒で打ち切り、受け取る大きさの上限は定義 256 KiB・署名 4 KiB、**署名を確かめてからでないと使わず、古い版には戻しません**（`src\Aegis\DefinitionsStore.cs`）。失敗しても何も言わず、同梱のファイルを使い続けます。

**どの通信でも、この PC の情報は送りません**（User-Agent だけの GET、Cookie なし、本文なし、テレメトリなし）。

BepInEx の zip は、**取ってきた後・展開する前に SHA-256 を確かめます**（`src\AppInfo.cs:68-83` の表と `src\Core\Installer.cs` の `CheckBepZip`）。値が合わなければ消して、ゲームには何も入れません。値を書いていない版は断ります（`docs\BEPINEX-PIN.md`）。

画面を描く Microsoft Edge WebView2 ランタイム（Microsoft の部品。アプリが起動する `msedgewebview2.exe`）は、Windows の設定と Microsoft のプライバシーに関する声明に従って Microsoft と通信することがあります。画面（`ui\`）自体はネットから何も読み込みません（`ui\index.html:4` の CSP と、`src\Shell\MainForm.cs:158` の仮想ホストで `app.starpocket.local` 以外を止めています）。

### 5b. コマンドの指定（全部）

`src\Core\CommandLine.cs` が読むものが、これで全部です。知らない言葉は**黙って無視せず**、使い方を出して終了コード 1 で終わります。

| 指定 | 何をするか |
|---|---|
| （何も付けない） | 画面を開きます |
| `--tray` | 画面を出さず、通知領域だけで動かします |
| `--autolaunch` | 画面を出さずに、すぐゲームを起動します |
| `--windowed` | ゲームを 1600x900 の窓で起動します（`--autolaunch` と一緒に、または画面から） |
| `--action install` | 画面を出さずにインストール（上の 4 つのホストへ通信します） |
| `--action check` | 画面を出さずに更新の確認（同じく通信します） |
| `--action report` | 画面を出さずに報告 zip をデスクトップに作ります（通信しません） |
| `--action status` | 今の状態を文字で出します（通信しません） |
| `--scan-only` | Aegis のスキャンを 1 回だけして終わります |
| `--uninstall` [`--quiet`] | アンインストール（`--quiet` は確認も出しません） |
| `--self-test <フォルダ>` | 画面もネットも使わない自己テスト。そのフォルダの中にしか書きません |
| `--game-dir <場所>` | MOD 用のゲームのコピーの場所（環境変数 `POCKETROLES_GAMEDIR` でも同じ） |
| `--steam-dir <場所>` | Steam 版 Among Us の場所（環境変数 `POCKETROLES_STEAMDIR` でも同じ） |
| `--desktop-dir <場所>` | デスクトップとして扱う場所 |
| `--language ja\|zh-CN\|en` | 最初の言語 |
| `--friend` | 開発モードに入りません（友達モードのまま） |
| `--devtools` | WebView2 の開発者ツール。**開発者用のビルドだけ**の指定です |
| ~~`--source-dir`~~ | **配布する Release の exe にはありません**（開発者用のビルドだけ。8. を見てください） |

### 6. アプリが置くもの

| 場所 | 中身 | アンインストールで |
|---|---|---|
| `%LOCALAPPDATA%\StarPocket\Client` | `settings.json`（閉じ方・言語・起動するゲーム・選んだ色・作者用の「開発用で動かす」）、`client.log`、WebView2 のプロファイル、このアプリ用の `launcher-state.json`、`mod-origin.txt`（このアプリが入れた `PocketRoles.dll` のハッシュと、配布版か開発ビルドか） | 消えます（最初に消します） |
| `%LOCALAPPDATA%\Programs\StarPocket Client` | アプリ本体（自分でそこに展開した場合） | 動いている exe は自分を消せないので、そのフォルダは残り、画面で名前を出して知らせます |
| `HKCU\...\Run` の値 1 つ | 「Windows の起動時に開く」を入れた時だけ | 消えます |
| デスクトップ／スタートメニューの `.lnk` | ボタンを押した時だけ | そのショートカットがこの exe を指している時だけ消えます |
| `<デスクトップ>\Among Us PocketRoles`（デスクトップが OneDrive の中にある時は `%LOCALAPPDATA%\PocketRoles\Among Us PocketRoles`） | MOD 用のゲームのコピー（約 1 GB）。Steam 版のコピー・BepInEx・`PocketRoles.dll`・`steam_appid.txt`・ゲームのログの保存・Aegis の証拠の記録が入ります。**この場所は `--game-dir` と環境変数 `POCKETROLES_GAMEDIR` で変えられます** | チェックボックス。**既定は残す**。消す時も、そのフォルダに `Among Us.exe` がある時だけです（無ければ「残した物」として名前を出します） |
| `%TEMP%\pocketroles-build.log` | 開発モードの「再ビルド」を押した時の、コンパイラの出力（配布する exe では作られません。8. を見てください） | 残ります（テキスト 1 つ） |
| `%LOCALAPPDATA%\PocketRoles\Aegis` | 今までの PowerShell 版と**共有**している記録。アプリも書きます: `events.log`（30 日で整理）・`definitions.txt(.sig)`（受け取った定義）・`mod-fingerprint.txt`・`prelaunch-result.txt`・`aegis.log` | **消しません**（PowerShell 版が動かなくなるため。`Uninstaller.Allowed` がコードで断ります） |
| `%TEMP%\PocketRolesLauncher` | ダウンロードの置き場（`<名前>.<プロセス番号>.part`・BepInEx の zip）と、報告 zip を作る時の作業フォルダ。PowerShell 版と共有 | 1 日たった作業フォルダはアプリが自分で消します |
| `%TEMP%\StarPocketClient-uninstall.log` | アンインストールの記録（消すフォルダの外に書きます） | 残ります（テキスト 1 つ） |
| `<デスクトップ>\PocketRoles-report-<日時>.zip`・`PocketRoles-evidence-<日時>.zip` | 押した時だけ作る報告用の zip | 触りません（30 日で自動で消えます） |
| ランチャーのフォルダの `launcher-state.json` | 今までのランチャーと共有している状態（入れた場所・版） | 触りません |

「アプリと機能」の項目（`HKCU\...\Uninstall\StarPocketClient`）は、この表にありません。**アプリは自分では書かないからです**。セットアップのプログラムを作った時に、そこが書きます（そのための関数はもう `src\Core\Shortcuts.cs` にあります）。何かが書いていた場合は、アンインストールの時に消します。

Steam と Steam 版の Among Us には、いかなる場合も触りません（`Uninstaller.Allowed` が Steam のフォルダの中を断ります）。

### 7. Aegis（同梱のチート対策）が読むもの

レビューの人がいちばん気にする所なので、先に書きます。Aegis は**ホスト自身の部屋を守るため**の確認で、読むのは次の 3 つだけです（`src\Aegis\AegisScan.cs`・`src\Core\Processes.cs`）。

- ファイル（MOD 用のゲームのコピーの中のファイルと、そのハッシュ）
- 誰でも読めるレジストリの値（セキュアブート・TPM・カーネルの起動オプション・脆弱ドライバーの遮断）
- **プロセスの名前**（`Process.GetProcessesByName`）。ゲームの exe の場所を確かめる時だけ `QueryFullProcessImageName`（`PROCESS_QUERY_LIMITED_INFORMATION` のみ）を使います

**ほかのプロセスのメモリは一切読みません。キー入力も画面も記録しません。ほかのプログラムを止めたり消したりもしません。** 見つけたことは画面と `events.log` に出すだけで、送りません。

### 8. 開発モードだけの例外（`dotnet.exe`）

作者の PC では、MOD 本体を作り直すボタンが出ます。コンパイルだけはアプリの中でできないので、ここだけ .NET SDK の `dotnet.exe` を起動します（`src\Core\ShellOpen.cs` の `OpenKind.Build`・`RunBuild`、`src\Core\DevBuild.cs`）。

**配布する Release の exe は、この道に入れません。** v0.4 のレビューまではそうではありませんでした。`--source-dir` が配布する exe にも残っていて、しかも exe の 4 階層上まで `PocketRoles.csproj` という名前のファイルを探していたので、その名前のファイルが入ったフォルダを指すだけで、誰の PC でも開発モードになりました。今は 2 つとも閉じてあります。

- `--source-dir` は**開発者用のビルド（DEBUG）にしかありません**。Release の exe にこの指定を書くと「分からない指定があります」と出て、終了コード 1 で終わります（黙って無視はしません）。
- exe の**上のフォルダを探すのをやめました**。ソースとして使えるのは exe と同じフォルダだけです。そこにファイルを置ける人は、exe そのものを差し替えられる人です。

正直に書いておくこと: **`dotnet build` は MSBuild を通すので、プロジェクトファイル（`.csproj`）に書かれていれば、別のプログラムを起動しえます。** つまりこの道は「`dotnet.exe` だけを起動する」ではなく「作者のプロジェクトファイルに書いてあることを行う」道です。だから配布する exe からは道ごと外してあります。

作者用のビルドで守っていること:

- 起動してよいのは、**絶対パスで、ファイル名が `dotnet.exe` のもの**だけ（`ShellOpen.Allowed` の `Build`）。`.cmd` や `.bat` の同名ファイルは通りません。
- **引数はコードの中に 3 語だけ**書いてあります（`build -c Release`）。画面からも設定からもファイルからも足せません。
- `UseShellExecute = false`・`CreateNoWindow = true`。シェルは通りません。黒い窓も出ません。出力はパイプで読みます。
- **20 分で打ち切ります。** 終わらないコンパイラは閉じて、その理由を 3 か国語で出します（v0.4 のレビュー。前は待ち続けていました）。
- `DOTNET_ROOT` と `PATH` は**子プロセスにだけ**渡します。アプリ自身の環境変数は変えません（PowerShell 版はここで自分に設定していて、あとで起動するゲームにまで引き継がれていました）。
- 作り直しの確認のためにゲームを 1 回起動し、**自分が起動したそのゲームだけ**を閉じます（ほかの人が起動したゲームには触りません）。

### 9. まだできていないこと（申し込みの前に必要）

足りないのは**オーナーにしかできないこと**と、まだ作っていないものです。手順はオーナーの手元の控えにあります。

1. **公開はこれからです（オーナーの操作）。** ソースも、公開 CI の決まり（`.github\workflows\build.yml`）も、このリポジトリに揃っています。残っているのは GitHub に上げる操作だけです（今は `git remote` が空です）。上げた時点で Actions が動き、そこから実行の記録が残ります。
2. **リリースもこれからです。** 署名してほしい形（CI が保存する `StarPocketClient-unsigned`）は、公開 CI がそのまま作ります。あとは 1 回リリースするだけです。SignPath は「署名してほしい形のものが、すでに配布されていること」を見ます。
3. **セットアップのプログラムがありません。** 今は zip を展開して使う形です。そのため「アプリと機能」に項目はできません（アンインストールは、アプリの設定の中と `--uninstall` から行います）。
4. **CI の action をコミットの SHA で固定していません**（今は `@v4` という大きい版だけ）。
5. サポートサイトの「プライバシー」と「コード署名について」のページ。
6. SignPath のアカウントと 2 要素認証。

---

## English (summary for reviewers)

**StarPocket Client** (version 1.0.0) is a launcher for *PocketRoles*, a host-only mod for the game Among Us. It replaces two
PowerShell scripts with one native Windows app, so it shows in Task Manager under its own name and icon instead of
"Windows PowerShell". Publisher: StarPocket Games (an individual). Licence: GPL-3.0-or-later.

- **Build from source:** `dotnet build StarpocketClient.csproj -c Release -p:ContinuousIntegrationBuild=true`
  → `bin\Release\StarPocket Client.exe`. This runs on every push in public GitHub Actions
  (`.github/workflows/build.yml`, `windows-latest`). **No secrets are used** (the workflow contains no `secrets.` at all
  and asks only for `contents: read`). The only downloads are the `Microsoft.Web.WebView2` NuGet package, pinned to an
  exact version in the project file, and the GitHub actions, which are named by major version (`@v4`) and are **not**
  pinned to a commit SHA yet. The build is deterministic and carries no build-machine paths (`PathMap`). The same
  workflow then greps every `.cs` file outside `bin` and `obj`, runs the app's headless self-tests (the step prints
  `RESULT PASS n/n`; the count is deliberately not quoted in this document, because it grows with every change) and
  checks the exe's name, company, version, manifest version, `AppInfo.Version` and icon.
- **Only the .exe is submitted for signing.** The three WebView2 DLLs beside it are Microsoft's own signed binaries; the
  mod's `PocketRoles.dll` needs the game's files, cannot be built in public CI, and is not part of this request.
- **Uninstall:** from the app's own Settings, and from the command line (`"<exe>" --uninstall`, silent
  `--uninstall --quiet`). The app's own data folder goes first; if that fails nothing else is touched, so the app is
  never half-removed (`src/Core/Uninstaller.cs`, `src/Program.cs`, with self-tests). **There is no Apps & Features
  entry today**: the app ships as a zip with no setup program, and the app never writes such an entry for itself.
- **No system change without asking:** shortcuts only on a button press (`Shortcuts.Create`, reached only from the
  page's `shortcut.create`); "start with Windows" only when the switch is turned on, off by default, one
  `HKCU\...\Run` value; the app never registers itself in Apps & Features (the function that would do it,
  `AppsAndFeatures.Register`, is called by nothing but the self-test, and is there for a future setup program); no file
  associations anywhere in the sources; `asInvoker`, never elevated; everything is per-user (HKCU).
- **Nothing is sent.** Every request is a plain GET with a User-Agent and no cookies, no body and no telemetry; there is
  no POST, no upload and no mail sending anywhere in the sources. Downloads happen on a button press, or when the user
  themselves runs `--action install` or `--action check`, and only from
  `api.github.com`, `github.com`, `objects.githubusercontent.com` and `builds.bepinex.dev`, enforced in code
  (`WebFetch.AllowedUrl`; redirects go through the same list for pages as well as files). The one automatic request is the anti-cheat's signed rule file from
  `raw.githubusercontent.com`, fetched once at start and at most every 12 hours, size-capped, signature-checked and
  never rolled back. The BepInEx zip's SHA-256 is pinned in the source and verified after download and before any
  unpacking. Report archives are written to the user's own Desktop; sending one is the user attaching it in their own
  mail client (`mailto:` only, to one of three project addresses).
- **No shell:** the app never starts `powershell.exe`, `cmd.exe`, `robocopy`, `wscript`, `schtasks` or `reg.exe`. File
  copying, zip extraction and registry work are all done in-process. `src/Core/ShellOpen.cs` is the only file that
  starts anything at all, against a fixed allow-list; CI fails the build if any other source file starts a program, and
  the self-test fails if those program names appear anywhere in the built exe. The app has no self-update mechanism.
- **Developer mode is not in the released build.** On the author's own machine a DEBUG build shows a "rebuild the mod"
  button that starts the .NET SDK's `dotnet.exe`, because compiling is the one thing the app cannot do inside itself.
  A Release build - the one that is published and submitted for signing - cannot reach it: `--source-dir` exists only
  in a DEBUG build (a Release build answers "this is not an option of this app" and exits 1), and the search for the
  mod's project file in folders above the exe has been removed, so the only possible source folder is the exe's own.
  We say plainly why that matters: `dotnet build` runs MSBuild, and a project file can tell MSBuild to start other
  programs, so this path is "do what the author's project file says", not "start `dotnet.exe` and nothing else". In
  the author's build it is still fenced: an absolute path whose file name is `dotnet.exe`, three fixed arguments
  written in the source (`build -c Release`) that no caller, page, file or setting can change, `UseShellExecute` off
  with no console window, output read back over pipes, a 20-minute limit after which the compiler is closed, and
  `DOTNET_ROOT`/`PATH` set on the child process only. The same flow starts the game once to regenerate the interop
  assemblies and closes only the run it started itself.
- **The bundled anti-cheat (Aegis)** reads files in the mod's own copy of the game, public registry values (Secure Boot,
  TPM, kernel boot options, the vulnerable-driver blocklist) and process *names*. It never reads another process's
  memory, never records input or the screen, and never stops or removes anything; findings stay on the PC.
- **Third-party components:** Microsoft Edge WebView2 (Microsoft's runtime, drawing the UI) and, downloaded at the
  user's request into the mod's own copy of the game, BepInEx. Licence texts ship beside the exe (`licenses\`, `NOTICE`,
  `LICENSE`), and a self-test fails the build when any of them is missing.
