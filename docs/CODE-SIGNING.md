# コード署名について / Code signing / 代码签名

[日本語](#日本語) | [English](#english-summary-for-reviewers) | [简体中文](#简体中文)

このページは、SignPath Foundation の無料のコード署名の審査のための説明です。`.github/workflows/build.yml` のコメントからここを指しています。
**オーナーが実際に行う手順（申し込みの順番・自分で作るもの・どこにも貼ってはいけないもの）は、このリポジトリには入れていません。**オーナーの手元の控えにあります。

> **いまの状態: 署名はしていません（無料の署名の申し込みは、今回は通りませんでした）。**
>
> SignPath Foundation の無料のコード署名（オープンソースのためのもの）に 2026-09-26 に申し込みました。
> 2026-10-02 に「まだ広く知られていないので、今回は承認できない（作りの良し悪しで決めたのではない）」という返事が来ました。広く使われるようになったら、申し込み直します。
> それまで、配っている `StarPocket Client.exe` には署名がありません。
>
> 承認されたら、SignPath の決まった一文（英語）を、このページの上・README・サイトに載せます。**今は載せていません**（署名を受けていると読まれないように、文はこのファイルの見えない注釈に控えてあります）。

<!-- 承認されたら載せる一文（承認の前に、見える所へ出さない）:
Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org)
-->

最後に中身を確かめた日: 2026-10-02（v1.1.2 のための見直し。1.〜9. と要約を、今のコードと 1 つずつ突き合わせて直しました）。

---

## 日本語

### 1. 何のプログラムか

`StarPocket Client.exe` は、Among Us の**ホスト専用 MOD「PocketRoles」**を入れて起動するためのランチャーです。今まで PowerShell のスクリプト（`PocketRolesLauncher.ps1` と `aegis\Aegis.ps1`）で行っていたことを、1 つのネイティブアプリにまとめたものです。作っているのは StarPocket Games（個人）で、ソースはすべてこのリポジトリにあります。

- C# / WinForms / WebView2、.NET Framework 4.8、x86
- 版: **1.1.2**（このページはこの版に合わせてあります。`StarpocketClient.csproj` の `FileVersion` = `app.manifest` の `assemblyIdentity version` = exe の `FileVersion` = `1.1.2.0`、`src\AppInfo.cs` の `Version` = `1.1.2`。CI と自己テストが毎回この 4 つを突き合わせます）
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
  action は `actions/checkout@11d5960a326750d5838078e36cf38b85af677262` のように**コミットの SHA で固定**しています（`@v4` は、その横のコメントに残してあるだけです）。`@v4` は動く目印なので、同じ行のままでも中身が入れ替わります。SHA なら入れ替わりません。**この 1 つのファイルだけから、だれでも同じ exe を建て直せます。** action を新しくする時は、新しい SHA を手で書き直します（手間ですが、それが狙いです）。
- `ContinuousIntegrationBuild` と csproj の `PathMap` により、ビルドしたマシンのパスは exe に入りません（同じソースから同じものができます）。
- CI は、ビルドのあとに次の 3 つも行います。どれかが落ちればビルドは失敗になります。
  1. `bin` と `obj` を除く**すべての `.cs`** を読み、`Process.Start(`・`new Process`・`ShellExecute…(`・`CreateProcess…(`・`WinExec(`・`EntryPoint="ShellExecute…` が `src\Core\ShellOpen.cs` 以外に無いこと
  2. 画面を出さない自己テスト `--self-test`。件数は CI の「Self-test」の段が `RESULT PASS n/n` として出します（この文章には件数を書きません。作業のたびに増えるからです）
  3. exe の素性（名前・会社・版・マニフェストの版・`AppInfo.Version`・アイコンのファイル）

  リリースのタグの時だけ、もう 1 つ確かめます: 画面のお知らせの欄に、MOD のリリースの公開日時が書いてあること（`build.yml:101-116`）。

### 3. 署名してもらうもの（承認されたら）

**`StarPocket Client.exe` の 1 つだけ**です。

| ファイル | 署名 |
|---|---|
| `StarPocket Client.exe` | **これを署名してもらいます** |
| `Microsoft.Web.WebView2.Core.dll`・`Microsoft.Web.WebView2.WinForms.dll`・`WebView2Loader.dll`（`runtimes\win-x86\native\` にも同じもの） | Microsoft が署名済み。こちらは触りません |
| `StarPocket Client.exe.config`、`ui\`（HTML・JS・画像・同梱の文書）、`aegis\definitions.txt(.sig)`、`LICENSE`、`NOTICE`、`licenses\` | データとテキスト。署名しません |
| MOD 本体の `PocketRoles.dll` | Among Us のゲームファイルが要るため公開 CI でビルドできません。**署名の対象にしません** |

CI が保存する成果物（`StarPocketClient-unsigned`）が、そのまま配布する形です（build.yml:148-162）。

### 4. SignPath の条件に対する答え（2026-10-02 に、v1.1.2 のコードで 1 つずつ確かめ直しました）

| 条件 | このアプリでは | 証拠 |
|---|---|---|
| アンインストールがある | **アプリの設定 →「アンインストール」**から行えます。アプリ自身のデータが消せなかった時は、ほかには一切触りません。コマンドからも同じことができます（`"<exe>" --uninstall`、無人は `--uninstall --quiet`）。**今は「アプリと機能」に項目はできません**。zip を展開して使う形なので、セットアップのプログラムがまだ無く、項目を書く人がいないからです（9. を見てください） | `src\Core\Uninstaller.cs`（`Allowed` / `Plan` / `Run` / `DeleteTree`）、`src\Program.cs`（`--uninstall` と `FinishUninstall`）。自己テスト「uninstall」 |
| 断りなくシステムを変えない | ショートカットは設定のボタンを押した時だけ。「Windows の起動時に開く」は設定のスイッチを入れた時だけで**既定はオフ**（`HKCU\...\Run` の値 1 つ）。ファイルの関連付けは 1 つも作りません（`Software\Classes` を書く所はありません。読むのは `src\Core\StoreGame.cs` の 1 か所だけで、Microsoft Store / Xbox 版の Among Us が入っているかを、パッケージの名前で見るだけです）。管理者権限は使いません。**アプリが自分で「アプリと機能」に項目を書くことはありません**（書くための関数 `AppsAndFeatures.Register` はありますが、呼んでいるのは自己テストだけです。セットアップを作る時にここから使います） | `src\Core\Shortcuts.cs`（`Create` / `AutoStart` / `AppsAndFeatures`）、`src\Shell\ClientApp.cs`（ボタンとスイッチ）、`app.manifest` |
| 断りなくデータを送らない | **アプリがネットに送るものはありません。** 通信はすべて GET で、送るのは User-Agent だけです（Cookie なし・本文なし・テレメトリなし）。最初の画面（利用規約などへの同意）に答えるまでは、どこにもつなぎません（5.）。ソースに `POST`・アップロード・メール送信のコードは 1 つもありません。報告 zip はユーザーのデスクトップに作るだけで、送るのは本人がメールソフトで添付して送信を押した時だけです（`mailto:` を開くだけ）。MOD のチャット翻訳と自動通報（どちらも MOD の機能）は、最初の画面で本人が選んだ答えを、インストールの時に MOD の設定ファイルへ書くだけです | `src\Core\Downloads.cs:69`（`AllowedUrl`）`:94,114`（`GetText`・`Download`。どちらも GET）、`src\Aegis\DefinitionsStore.cs:178,200`、`src\Core\ShellOpen.cs:96`（宛先は作者の 3 つだけ）`:176`（`MailTo`）、`src\Core\ReportBuilder.cs:79,304`、`src\Core\Installer.cs:767-784`（`ApplyConsentToMod`） |
| 公開 CI でソースからビルドできる | 上の 2. のとおりです。**公開リポジトリの GitHub Actions が、秘密の値を 1 つも使わずにソースから exe をビルドします**（`windows-latest`・`dotnet build -c Release`・ソースの確認・画面なしの自己テスト・exe の素性の確認。権限は `contents: read` だけ）。配るものは、その回の成果物 `StarPocketClient-unsigned` そのものです。ビルドの決まりはこのリポジトリの中にあり、リポジトリは公開しているので、実行の記録はだれでも見られます | `.github\workflows\build.yml` |
| マルウェア／PUP のふるまいをしない | ほかのプログラムを起動するのは `src\Core\ShellOpen.cs` の 1 か所だけで、起動してよいものは決め打ちの一覧だけです（MOD 用のコピーの `Among Us.exe`、`steam://` の 2 つ、アプリの中に書いてある web ページ（Microsoft の WebView2 のページ・Discord の招待・製品のサイト・利用規約／プライバシーポリシー／遊び方のルールのページ。画面は住所を渡せず、名前を送るだけです）、ユーザーのフォルダとテキストファイル、プロジェクトの 3 つのメールアドレス宛の `mailto:`、そしてこのアプリ自身（開発のスイッチを切り替えた時と、MOD 用のコピーの場所を変えた時に、引数なしで開き直すだけ））。**シェルは一切起動しません**（`powershell.exe`・`cmd.exe`・`robocopy`・`wscript`・`schtasks`・`reg.exe` はどれも使いません。ファイルのコピーも zip の展開もレジストリもアプリの中で行います）。CI がソースを読んでこれを毎回確かめ、自己テストが**ビルドされた exe の中にそれらの名前が無いこと**を確かめます。自分自身を更新する仕組みはまだありません。開発モードだけの例外が 1 つあります（下の 8.） | `src\Core\ShellOpen.cs:4-32`（一覧）`:87-96`（決め打ちの値）`:102`（`Allowed`）`:285`（`Start`）、`src\AppInfo.cs:76-113`（web ページ）、`.github\workflows\build.yml:52-85`、`src\SelfTest\IdentitySelfTests.cs:95-105` |
| 独自アイコン | exe のアイコンは StarPocket のアイコン A（`assets\starpocket.ico`）です。MOD の乗組員アイコンは Innersloth のデザインなので exe には入れません（画面の中で MOD を指す時だけ、exe の外のファイル `ui\img\pocketroles-128.png` として使います）。自己テストが、exe の中にその絵のバイトが無いことと、exe の資源がアイコン A だけであることを確かめます | `StarpocketClient.csproj:22,68`、`src\SelfTest\IdentitySelfTests.cs:80-93`、`src\SelfTest\ShellSelfTests.cs:1393-1402` |
| ほかの人のものを正しく扱う | GPL-3.0 の本文（`LICENSE`）と `NOTICE`、WebView2 の BSD 3-clause の本文と NOTICE、歯車アイコンの元である Feather Icons の MIT の本文（`licenses\`）を exe の隣に置きます。自己テストが、その 5 つが本当に置かれているかを毎回確かめます | `StarpocketClient.csproj:70-76`、`src\SelfTest\ShellSelfTests.cs:1404-1426` |

### 5. アプリがつなぐ先

**最初の画面（利用規約・プライバシーポリシー・遊び方のルールへの同意）に答えるまでは、アプリはどこにもつなぎません**（画面を描く WebView2 ランタイムのことは、この節の最後に書きました）。`--tray`・`--autolaunch` で始めた時もその画面を先に出し、`--action install`・`--action check`・`--verify-download` は何もせずに終了コード 1 で終わります（`src\Program.cs:65-70`、`src\Shell\ClientApp.cs:159-178`）。

**画面のボタンを押した時**（インストール・修復・更新の確認など、MOD や BepInEx を入れる作業）、**または本人が `--action install` / `--action check` / `--verify-download` を実行した時**に、次の 5 つのホストからの **GET** を行います。`src\Core\Downloads.cs` の `WebFetch.AllowedUrl` が、https であることとこのホスト名であることを**コードで強制**します。リダイレクト先も同じ一覧を通ります（ファイルを落とす `Download` も、文章を取る `GetText` も、同じ `WebFetch.CheckArrivedFrom` を通ります）。

| ホスト | 何のため |
|---|---|
| `api.github.com` | MOD の最新リリースを読む |
| `github.com` / `objects.githubusercontent.com` / `release-assets.githubusercontent.com` | そのリリースの `PocketRoles-<版>.zip`（GitHub は、リリースのファイルを後ろの 2 つの名前のどちらかから渡します） |
| `builds.bepinex.dev` | BepInEx 6.0.0-be.735 の 64bit 用（`win-x64`。ファイルの一覧のページと zip） |

**自動で行う通信は 1 つだけです。** Aegis の検知ルールの定義ファイル（`raw.githubusercontent.com` の `wakayamachannel/PocketRoles` の `main/aegis/definitions.txt` と `.sig`）を、**アプリを開いた時に 1 回**（最初の画面に答えた後）受け取ります。そのあとは、窓を開いた時かプレイを押した時に、前回から 12 時間以上たっていればもう一度受け取ります（`src\Aegis\AegisService.cs:130`・`:187`、`src\Shell\ClientApp.cs:829,1959`）。1 つにつき 8 秒で打ち切り、受け取る大きさの上限は定義 256 KiB・署名 4 KiB、**署名を確かめてからでないと使わず、古い版には戻しません**（`src\Aegis\DefinitionsStore.cs`）。失敗しても何も言わず、同梱のファイルを使い続けます。

**どの通信でも、この PC の情報は送りません**（User-Agent だけの GET、Cookie なし、本文なし、テレメトリなし）。

BepInEx の zip は、**取ってきた後・展開する前に SHA-256 を確かめます**（`src\AppInfo.cs:169-187` の表と `BepSha256`、`src\Core\Installer.cs` の `CheckAndExpandBep`）。値が合わなければ消して、ゲームには何も入れません。値を書いていない版は断ります（`docs\BEPINEX-PIN.md`）。v1.1.2 から入れるのは 64bit 用です（Among Us 2026.9.29 が 64bit になったため）。前の 32bit 用が入っている時は、ゲームと種類が違うことに気づいて入れ直します（`src\Core\PeArch.cs`）。

画面を描く Microsoft Edge WebView2 ランタイム（Microsoft の部品。アプリが起動する `msedgewebview2.exe`）は、Windows の設定と Microsoft のプライバシーに関する声明に従って Microsoft と通信することがあります。画面（`ui\`）自体はネットから何も読み込みません（`ui\index.html:4` の CSP と、`src\Shell\MainForm.cs:363-370` の仮想ホストで `app.starpocket.local` 以外を止めています）。

### 5b. コマンドの指定（全部）

`src\Core\CommandLine.cs` が読むものが、これで全部です。知らない言葉は**黙って無視せず**、使い方を出して終了コード 1 で終わります。

| 指定 | 何をするか |
|---|---|
| （何も付けない） | 画面を開きます |
| `--tray` | 画面を出さず、通知領域だけで動かします |
| `--autolaunch` | 画面を出さずに、すぐゲームを起動します |
| `--windowed` | ゲームを 1600x900 の窓で起動します（`--autolaunch` と一緒に、または画面から） |
| `--action install` | 画面を出さずにインストール（上の 5 つのホストへ通信します） |
| `--action check` | 画面を出さずに更新の確認（同じく通信します） |
| `--action report` | 画面を出さずに報告 zip をデスクトップに作ります（通信しません） |
| `--action status` | 今の状態を文字で出します（通信しません） |
| `--scan-only` | Aegis のスキャンを 1 回だけして終わります（通信しません） |
| `--verify-download` | このアプリが SHA-256 を書いてある BepInEx の zip を `builds.bepinex.dev` から 1 回受け取り、確かめ、`%TEMP%` の中で展開してみてから全部消します。ゲームのフォルダには触りません。公開の前に作者が確かめるためのものです |
| `--uninstall` [`--quiet`] | アンインストール（`--quiet` は確認も出しません） |
| `--self-test <フォルダ>` | 画面もネットも使わない自己テスト。そのフォルダの中にしか書きません |
| `--game-dir <場所>` | MOD 用のゲームのコピーの場所（環境変数 `POCKETROLES_GAMEDIR` でも同じ。どちらも設定の「場所を変える」より強い） |
| `--steam-dir <場所>` | Steam 版 Among Us の場所（環境変数 `POCKETROLES_STEAMDIR` でも同じ） |
| `--desktop-dir <場所>` | デスクトップとして扱う場所 |
| `--language ja\|zh-CN\|en` | 最初の言語 |
| `--friend` | 開発モードに入りません（友達モードのまま） |
| `--devtools` | WebView2 の開発者ツール。**開発者用のビルドだけ**で効きます（Release の exe では受け付けますが、何も起きません） |
| ~~`--source-dir`~~ | **配布する Release の exe にはありません**（開発者用のビルドだけ。8. を見てください） |

### 6. アプリが置くもの

| 場所 | 中身 | アンインストールで |
|---|---|---|
| `%LOCALAPPDATA%\StarPocket\Client` | `settings.json`（閉じ方・言語・起動するゲーム・色・音・プロフィールの名前・MOD 用のコピーの場所・作者用の「開発用で動かす」とそのフォルダ など）、`consent.json`（最初の画面の答え）、`client.log`、WebView2 のプロファイル、`profile\avatar.png`（本人が選んだプロフィールの絵の写し）、このアプリ用の `launcher-state.json`、`mod-origin.txt`（このアプリが入れた `PocketRoles.dll` のハッシュと、配布版か開発ビルドか） | 消えます（最初に消します） |
| `%LOCALAPPDATA%\Programs\StarPocket Client` | アプリ本体（自分でそこに展開した場合） | 動いている exe は自分を消せないので、そのフォルダは残り、画面で名前を出して知らせます |
| `HKCU\...\Run` の値 1 つ | 「Windows の起動時に開く」を入れた時だけ | 消えます |
| デスクトップ／スタートメニューの `.lnk` | ボタンを押した時だけ | そのショートカットがこの exe を指している時だけ消えます |
| `<デスクトップ>\Among Us PocketRoles`（デスクトップが OneDrive の中にある時は `%LOCALAPPDATA%\PocketRoles\Among Us PocketRoles`。設定（PocketRoles → Among Us の場所）の「場所を変える」で選んだフォルダにもできます） | MOD 用のゲームのコピー（約 1 GB）。Steam 版のコピー・BepInEx・`PocketRoles.dll`・`steam_appid.txt`・MOD の設定ファイル（最初の画面の 2 つの答え、チャット翻訳と自動通報を書きます）・ゲームのログの保存・Aegis の証拠の記録が入ります。**この場所は `--game-dir` と環境変数 `POCKETROLES_GAMEDIR` でも変えられます**（こちらが設定より強い）。「場所を変える」は、新しい場所へ写し終えて確かめてから元のコピーを消します（同じドライブなら名前を変えるだけ。元のコピーのゲームが動いている時は消しません） | チェックボックス。**既定は残す**。消す時も、そのフォルダに `Among Us.exe` がある時だけです（無ければ「残した物」として名前を出します） |
| `%TEMP%\pocketroles-build.log` | 開発モードの「再ビルド」を押した時の、コンパイラの出力（配布する exe では作られません。8. を見てください） | 残ります（テキスト 1 つ） |
| `%LOCALAPPDATA%\PocketRoles\Aegis` | 今までの PowerShell 版と**共有**している記録。アプリも書きます: `events.log`（30 日で整理）・`definitions.txt(.sig)`（受け取った定義）・`mod-fingerprint.txt`・`prelaunch-result.txt`・`aegis.log` | **消しません**（PowerShell 版が動かなくなるため。`Uninstaller.Allowed` がコードで断ります） |
| `%TEMP%\PocketRolesLauncher` | ダウンロードの置き場（`<名前>.<プロセス番号>.part`・BepInEx の zip）と、報告 zip を作る時の作業フォルダ。PowerShell 版と共有 | 1 日たった作業フォルダはアプリが自分で消します |
| `%TEMP%\StarPocket-verify` | `--verify-download` の時だけ作る作業フォルダ（受け取った BepInEx の zip と、試しに展開した中身） | 終わる時にアプリが自分で消します |
| `%TEMP%\StarPocketClient-uninstall.log` | アンインストールの記録（消すフォルダの外に書きます） | 残ります（テキスト 1 つ） |
| `<デスクトップ>\PocketRoles-report-<日時>.zip`・`PocketRoles-evidence-<日時>.zip` | 押した時だけ作る報告用の zip | 触りません（30 日で自動で消えます） |
| ランチャーのフォルダの `launcher-state.json` | 今までのランチャーと共有している状態（入れた場所・版） | 触りません |

「アプリと機能」の項目（`HKCU\...\Uninstall\StarPocketClient`）は、この表にありません。**アプリは自分では書かないからです**。セットアップのプログラムを作った時に、そこが書きます（そのための関数はもう `src\Core\Shortcuts.cs` にあります）。何かが書いていた場合は、アンインストールの時に消します。

Steam と Steam 版の Among Us には、いかなる場合も触りません（`Uninstaller.Allowed` が Steam のフォルダの中を断ります）。

### 7. Aegis（同梱のチート対策）が読むもの

レビューの人がいちばん気にする所なので、先に書きます。Aegis は**ホスト自身の部屋を守るため**の確認で、読むのは次の 3 つだけです（`src\Aegis\AegisScan.cs`・`src\Core\Processes.cs`）。

- ファイル（MOD 用のゲームのコピーの中のファイルと、そのハッシュ）
- 誰でも読めるレジストリの値（セキュアブート・TPM・カーネルの起動オプション・脆弱ドライバーの遮断）
- **プロセスの名前**（`Process.GetProcesses` の名前だけ。ゲームを探す時は `Process.GetProcessesByName`）。ゲームの exe の場所を確かめる時だけ `QueryFullProcessImageName`（`PROCESS_QUERY_LIMITED_INFORMATION` のみ）を使います

**ほかのプロセスのメモリは一切読みません。キー入力も画面も記録しません。ほかのプログラムを止めたり消したりもしません。** 見つけたことは画面と `events.log` に出すだけで、送りません。v1.1.2 から、MOD 用のコピーの中のファイルが変わった時にも同じ確認をもう一度します（`src\Aegis\AegisAutoScan.cs`。読むものは同じです）。

### 8. 開発モードだけの例外（`dotnet.exe`）

作者の PC では、MOD 本体を作り直すボタンが出ます。コンパイルだけはアプリの中でできないので、ここだけ .NET SDK の `dotnet.exe` を起動します（`src\Core\ShellOpen.cs` の `OpenKind.Build`・`RunBuild`、`src\Core\DevBuild.cs`）。

v0.4 のレビューまでは、**誰の PC でもこの道に入れました**。`--source-dir` が配布する exe にも残っていて、しかも exe の 4 階層上まで `PocketRoles.csproj` という名前のファイルを探していたので、その名前のファイルが入ったフォルダを指すだけで開発モードになりました。今は 2 つとも閉じてあります。

- `--source-dir` は**開発者用のビルド（DEBUG）にしかありません**。Release の exe にこの指定を書くと「分からない指定があります」と出て、終了コード 1 で終わります（黙って無視はしません）。
- exe の**上のフォルダを探すのをやめました**。ソースとして使えるのは exe と同じフォルダだけです。そこにファイルを置ける人は、exe そのものを差し替えられる人です。

**exe と同じフォルダのほかに、配布する exe でこの道に入る方法が 1 つだけあります**（最初に公開した v1.0.0 から、この道がありました。この節は v1.1.0（2026-09-26）で直すまで「入れません」と書いたままで、実態と違っていました）。次の **3 つが全部そろった時だけ**です。

1. 利用者が、アプリの中の設定（**設定 → PocketRoles → 開発**）でスイッチを入れる。または自分の `%LOCALAPPDATA%` の `settings.json` に `"devBuild": true` と書く
2. そのフォルダが、**`PocketRoles.csproj` と `PocketRolesLauncher.ps1` の両方**を持っている（片方だけのフォルダは通りません。v0.4 で問題になった形がこれです）
3. そのフォルダが、次のどれかである
   - **利用者がアプリの中のフォルダ選択で選んだ場所**（v1.1.0 から。`settings.json` の `"devSource"`。書くのは `pickModSource` だけで、それは**その場で開く Windows のフォルダ選択**です）
   - 自分のデスクトップの `PocketRoles.lnk` か `PocketRoles Launcher.lnk`（今までのランチャーのショートカット）が指す先の入ったフォルダ
   - 自分のデスクトップの `HostRoles`

**引数・ページ・外のファイルからは指定できません。** コマンドラインにこのフォルダを渡す方法はなく、画面の中の JavaScript も場所を渡しません（`pickModSource` という**命令の名前だけ**を送ります）。設定ファイルに書ける人は、同じ場所にある「スタートアップ」フォルダにも書ける人です。**署名済みの exe が、その人に新しい力を与えることはありません。**

v1.1.0 でフォルダ選択を足したのは、作業コピーをデスクトップの外に置くと開発モードが黙って消えていたためです（持ち主、2026-09-26）。**通す条件は 1 つも緩めていません。**場所の決め方が「固定の 2 か所」から「利用者が選んだ 1 か所＋固定の 2 か所」に増えただけです。

正直に書いておくこと: **`dotnet build` は MSBuild を通すので、プロジェクトファイル（`.csproj`）に書かれていれば、別のプログラムを起動しえます。** つまりこの道は「`dotnet.exe` だけを起動する」ではなく「作者のプロジェクトファイルに書いてあることを行う」道です。だから配布する exe からは道ごと外してあります。

作者用のビルドで守っていること:

- 起動してよいのは、**絶対パスで、ファイル名が `dotnet.exe` のもの**だけ（`ShellOpen.Allowed` の `Build`）。`.cmd` や `.bat` の同名ファイルは通りません。
- **引数はコードの中に 3 語だけ**書いてあります（`build -c Release`）。画面からも設定からもファイルからも足せません。
- `UseShellExecute = false`・`CreateNoWindow = true`。シェルは通りません。黒い窓も出ません。出力はパイプで読みます。
- **20 分で打ち切ります。** 終わらないコンパイラは閉じて、その理由を 3 か国語で出します（v0.4 のレビュー。前は待ち続けていました）。
- `DOTNET_ROOT` と `PATH` は**子プロセスにだけ**渡します。アプリ自身の環境変数は変えません（PowerShell 版はここで自分に設定していて、あとで起動するゲームにまで引き継がれていました）。
- 作り直しの確認のためにゲームを 1 回起動し、**自分が起動したそのゲームだけ**を閉じます（ほかの人が起動したゲームには触りません）。

### 9. 申し込みの結果と、まだできていないこと

**申し込み:** 2026-09-26 に SignPath Foundation の無料のコード署名に申し込み、2026-10-02 に「まだ広く知られていないので、今回は承認できない（作りの良し悪しで決めたのではない）」という返事が来ました。広く使われるようになったら、申し込み直します。手順はオーナーの手元の控えにあります。

**まだできていないこと**（コードで確かめたもの）:

1. **セットアップのプログラムがありません。** 今は zip を展開して使う形です。そのため「アプリと機能」に項目はできません（アンインストールは、アプリの設定の中と `--uninstall` から行います。項目を書く関数 `AppsAndFeatures.Register` を呼んでいるのは、自己テストだけです）。

**承認されたらすること:** SignPath 側の準備（SignPath のアカウントでも 2 段階認証を使います）と、上の見えない注釈に控えた一文を、このページの上・README・サイトに出すこと。

**済んだもの**（2026-10-02 時点）:

- 公開: <https://github.com/wakayamachannel/StarPocketClient>（GPL-3.0、Actions が動いています）
- CI の action は、すべてコミットの SHA で固定済みです。
- サイトのページ: [プライバシーポリシー](https://starpocketgames.com/privacy.ja.html)・[コード署名について](https://starpocketgames.com/code-signing.ja.html)・[利用規約](https://starpocketgames.com/terms.ja.html)・[遊び方のルール](https://starpocketgames.com/rules.ja.html)・[エラーコード](https://starpocketgames.com/codes/)（3 言語）
- GitHub の 2 要素認証。
- リリース: `v1.0.0` から `v1.1.1` までを出しました。このページは次の `v1.1.2` に合わせてあります。配るのは、**公開 CI がそのタグから建てた** `StarPocketClient-unsigned` です（手元の PC で建てた物は配りません）。

---

## English (summary for reviewers)

**StarPocket Client** (version 1.1.2) is a launcher for *PocketRoles*, a host-only mod for the game Among Us. It replaces two
PowerShell scripts with one native Windows app, so it shows in Task Manager under its own name and icon instead of
"Windows PowerShell". Publisher: StarPocket Games (an individual). Licence: GPL-3.0-or-later.

- **Signing status: not signed.** We applied to SignPath Foundation's free code signing for open-source projects on
  2026-09-26. On 2026-10-02 the answer was that the project is not yet widely known enough to be approved this time
  (not a judgement on its quality). We will apply again once it is more widely used. Until then the released exe
  carries no signature, and the attribution line SignPath asks for is kept in a hidden comment of this file and shown
  nowhere.
- **Build from source:** `dotnet build StarpocketClient.csproj -c Release -p:ContinuousIntegrationBuild=true`
  → `bin\Release\StarPocket Client.exe`. This runs on every push in public GitHub Actions
  (`.github/workflows/build.yml`, `windows-latest`). **No secrets are used** (the workflow contains no `secrets.` at all
  and asks only for `contents: read`). The only downloads are the `Microsoft.Web.WebView2` NuGet package, pinned to an
  exact version in the project file, and the GitHub actions, which are **pinned to full commit SHAs** (the major version
  is kept beside each one as a comment only), so the workflow file alone is enough to rebuild the same exe: a moving
  `@v4` tag can change under you, a SHA cannot. The build is deterministic and carries no build-machine paths
  (`PathMap`). The same
  workflow then greps every `.cs` file outside `bin` and `obj`, runs the app's headless self-tests (the step prints
  `RESULT PASS n/n`; the count is deliberately not quoted in this document, because it grows with every change) and
  checks the exe's name, company, version, manifest version, `AppInfo.Version` and icon. On a release tag it also
  checks that the in-app news carries the mod release's publication time.
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
  associations anywhere in the sources (the one place that reads `Software\Classes` is `src/Core/StoreGame.cs`, which
  only looks for the name of a Microsoft Store / Xbox package of Among Us and writes nothing); `asInvoker`, never
  elevated; everything is per-user (HKCU). The mod's game copy moves only when the user presses Settings (PocketRoles →
  Among Us location) → Change location, and the old copy is deleted only after the new one has been copied and checked.
- **Nothing is sent.** The app itself connects to nothing until the first-run screen (Terms of Use, Privacy Policy,
  Play Rules) has been answered. Every request is a plain GET with a User-Agent and no cookies, no body and no telemetry; there is
  no POST, no upload and no mail sending anywhere in the sources. Downloads happen on a button press, or when the user
  themselves runs `--action install`, `--action check` or `--verify-download`, and only from
  `api.github.com`, `github.com`, `objects.githubusercontent.com`, `release-assets.githubusercontent.com` and
  `builds.bepinex.dev`, enforced in code
  (`WebFetch.AllowedUrl`; redirects go through the same list for pages as well as files). The one automatic request is the anti-cheat's signed rule file from
  `raw.githubusercontent.com`, fetched once at start and at most every 12 hours, size-capped, signature-checked and
  never rolled back. The BepInEx zip (the 64-bit build since v1.1.2) has its SHA-256 pinned in the source and verified
  after download and before any unpacking. Report archives are written to the user's own Desktop; sending one is the
  user attaching it in their own mail client (`mailto:` only, to one of three project addresses). The mod's chat
  translation and automatic report (mod features) are only set the way the person answered on the first-run screen,
  written into the mod's config at install.
- **No shell:** the app never starts `powershell.exe`, `cmd.exe`, `robocopy`, `wscript`, `schtasks` or `reg.exe`. File
  copying, zip extraction and registry work are all done in-process. `src/Core/ShellOpen.cs` is the only file that
  starts anything at all, against a fixed allow-list (the mod copy's `Among Us.exe`, two `steam://` URLs, a fixed list
  of web pages the page can only name, never pass, the user's own folders and text files, `mailto:` to three project
  addresses, and this exe itself, with no arguments, to reopen after the developer switch or a move of the mod copy);
  CI fails the build if any other source file starts a program, and
  the self-test fails if those program names appear anywhere in the built exe. The app has no self-update mechanism.
- **Developer mode is off by default and cannot be switched on from outside the app.** A "rebuild the mod" button
  starts the .NET SDK's `dotnet.exe`, because compiling is the one thing the app cannot do inside itself. Nothing a
  caller hands the exe can reach it: `--source-dir` exists only in a DEBUG build (a Release build answers "this is not
  an option of this app" and exits 1), and the search for the mod's project file in folders above the exe has been
  removed. Besides the exe's own folder (a `PocketRoles.csproj` beside the exe, which only someone who could replace the
  exe itself can put there), a released build has exactly one way in, and it needs all three of: the person turning the
  switch on inside the app (Settings → PocketRoles → 開発 / Developer, or `"devBuild": true` in their own
  `settings.json`); a folder holding
  BOTH `PocketRoles.csproj` and `PocketRolesLauncher.ps1` (a lone project file - the v0.4 shape - is refused); and that
  folder being one they chose in the app's own folder dialog (`"devSource"`, written only by `pickModSource`), or the
  folder holding the target of their Desktop's `PocketRoles.lnk` or `PocketRoles Launcher.lnk`, or `<Desktop>\HostRoles`.
  The page never passes a path - it sends the name of the
  command and nothing else - and whoever can write those settings can already write the Startup folder beside them.
  (Until v1.1.0, 2026-09-26, this paragraph said a released build could not reach developer mode at all; that was
  already untrue in v1.0.0, the first release.)
  We say plainly why that matters: `dotnet build` runs MSBuild, and a project file can tell MSBuild to start other
  programs, so this path is "do what the author's project file says", not "start `dotnet.exe` and nothing else". In
  the author's build it is still fenced: an absolute path whose file name is `dotnet.exe`, three fixed arguments
  written in the source (`build -c Release`) that no caller, page, file or setting can change, `UseShellExecute` off
  with no console window, output read back over pipes, a 20-minute limit after which the compiler is closed, and
  `DOTNET_ROOT`/`PATH` set on the child process only. The same flow starts the game once to regenerate the interop
  assemblies and closes only the run it started itself.
- **The bundled anti-cheat (Aegis)** reads files in the mod's own copy of the game, public registry values (Secure Boot,
  TPM, kernel boot options, the vulnerable-driver blocklist) and process *names*. It never reads another process's
  memory, never records input or the screen, and never stops or removes anything; findings stay on the PC. Since
  v1.1.2 it also scans again when files in the mod's copy change; what it reads is the same.
- **Third-party components:** Microsoft Edge WebView2 (Microsoft's runtime, drawing the UI) and, downloaded at the
  user's request into the mod's own copy of the game, BepInEx. Licence texts ship beside the exe (`licenses\`, `NOTICE`,
  `LICENSE`), and a self-test fails the build when any of them is missing.

---

## 简体中文

（给审核者的摘要。内容和上面的 English 相同，详细说明在前面的日语部分。）

**StarPocket Client**（版本 1.1.2）是 Among Us 的**仅房主需要安装的模组 PocketRoles** 的启动器。它把两个 PowerShell 脚本
合成了一个原生的 Windows 应用，所以在任务管理器里显示的是它自己的名字和图标，而不是“Windows PowerShell”。
发布者：StarPocket Games（个人）。许可证：GPL-3.0-or-later。

- **签名状态：没有签名。** 我们于 2026-09-26 向 SignPath Foundation 申请了面向开源项目的免费代码签名。
  2026-10-02 收到回复：因为项目还不够广为人知，这次无法批准（不是对质量的判断）。等使用的人多了，我们会再次申请。
  在那之前，发布的 exe 没有签名。SignPath 要求显示的那句话保存在本文件看不见的注释里，哪里都没有显示。
- **从源代码构建：** `dotnet build StarpocketClient.csproj -c Release -p:ContinuousIntegrationBuild=true`
  → `bin\Release\StarPocket Client.exe`。每次 push 都会在公开的 GitHub Actions（`.github/workflows/build.yml`、
  `windows-latest`）里用同样的步骤构建。**不使用任何秘密的值**（工作流里没有一处 `secrets.`，权限只有 `contents: read`）。
  下载的只有固定版本的 NuGet 包 `Microsoft.Web.WebView2`，以及用**完整的提交 SHA 固定**的 GitHub action
  （大版本号只作为注释写在旁边）。所以只凭这一个工作流文件，任何人都能重新构建出同样的 exe：会移动的 `@v4` 标签可能
  在你不知道时变掉，SHA 不会。构建是确定性的，exe 里不含构建机器的路径（`PathMap`）。同一个工作流还会读取 `bin` 和
  `obj` 以外的所有 `.cs` 文件，运行应用的无画面自检（这一步输出 `RESULT PASS n/n`；本文不写数量，因为每次修改都会增加），
  并检查 exe 的名称、公司、版本、清单的版本、`AppInfo.Version` 和图标。打发布标签时，还会检查应用内的新闻里写着模组发布的公开时间。
- **只提交 .exe 签名。** 旁边的 3 个 WebView2 DLL 是 Microsoft 自己签过名的文件；模组的 `PocketRoles.dll` 需要游戏的文件，
  不能在公开的 CI 里构建，不在这次申请的范围内。
- **卸载：** 可以在应用的设置里卸载，也可以用命令行（`"<exe>" --uninstall`，不询问的方式是 `--uninstall --quiet`）。
  先删除应用自己的数据文件夹；如果失败，就不再碰其他任何东西，所以应用不会只被删掉一半（`src/Core/Uninstaller.cs`、
  `src/Program.cs`，有自检）。**现在“应用和功能”里没有条目**：应用以 zip 的形式发布，没有安装程序，应用自己也不会写这个条目。
- **不经同意不改动系统：** 快捷方式只在按下按钮时创建（`Shortcuts.Create`，只能从页面的 `shortcut.create` 到达）；
  “随 Windows 启动”只在打开开关时生效，默认关闭，只是 `HKCU\...\Run` 的 1 个值；应用不会把自己登记到“应用和功能”
  （写条目的函数 `AppsAndFeatures.Register` 只有自检在调用，是为将来的安装程序准备的）；源代码里没有任何文件关联
  （读取 `Software\Classes` 的只有 `src/Core/StoreGame.cs` 一处，只看有没有 Microsoft Store / Xbox 版 Among Us 的包名，
  不写任何东西）；`asInvoker`，从不提升权限；一切都只针对当前用户（HKCU）。模组用的游戏副本只在用户按下
  设置（PocketRoles → Among Us 的位置）→“更改安装位置”时才会移动，旧副本要等新副本复制并核对完之后才删除。
- **不发送任何东西。** 在回答第一个画面（使用条款、隐私政策、游戏规则）之前，应用本身不连接任何地方。所有请求都是只带
  User-Agent 的普通 GET，没有 Cookie、没有正文、没有遥测；源代码里没有 POST、上传或发送邮件的代码。下载只在按下按钮时，
  或用户自己运行 `--action install`、`--action check`、`--verify-download` 时进行，而且只从 `api.github.com`、`github.com`、
  `objects.githubusercontent.com`、`release-assets.githubusercontent.com` 和 `builds.bepinex.dev` 下载，这由代码强制
  （`WebFetch.AllowedUrl`；无论是页面还是文件，重定向的目标也要通过同一个列表）。唯一自动进行的请求，是从
  `raw.githubusercontent.com` 获取反作弊（Aegis）带签名的规则文件：启动时 1 次，之后最多每 12 小时 1 次，有大小上限，
  核对签名，绝不退回旧版本。BepInEx 的 zip（从 v1.1.2 起是 64 位版）的 SHA-256 写在源代码里，下载后、解压前核对。
  报告 zip 只写到用户自己的桌面；要发送，是用户自己在邮件软件里添加附件（只是打开 `mailto:`，收件人是项目的 3 个地址之一）。
  模组的聊天翻译和自动举报（都是模组的功能），只是在安装时把用户在第一个画面里的回答写进模组的设置文件。
- **不启动 shell：** 应用从不启动 `powershell.exe`、`cmd.exe`、`robocopy`、`wscript`、`schtasks` 或 `reg.exe`。复制文件、
  解压 zip 和注册表操作都在应用自己的进程里完成。会启动其他东西的只有 `src/Core/ShellOpen.cs` 这一个文件，而且只能启动
  固定列表里的东西（模组副本的 `Among Us.exe`、2 个 `steam://` 地址、固定列表里的网页（页面只能说名字，不能传地址）、
  用户自己的文件夹和文本文件、发往项目 3 个地址的 `mailto:`，以及在切换开发开关或移动模组副本之后，不带参数地重新打开这个 exe 本身）。
  如果其他源文件启动程序，CI 会让构建失败；如果构建出的 exe 里出现这些程序名，自检会失败。应用没有自我更新的机制。
- **开发模式默认关闭，不能从应用外部打开。**“重新构建模组”的按钮会启动 .NET SDK 的 `dotnet.exe`，因为编译是应用自己
  唯一做不到的事。调用者交给 exe 的任何东西都到不了这里：`--source-dir` 只存在于 DEBUG 构建里（Release 构建会回答
  “这不是本应用的选项”并以退出代码 1 结束），在 exe 上层文件夹里寻找模组项目文件的做法也已经去掉。除了 exe 自己的文件夹
  （exe 旁边放着 `PocketRoles.csproj`，而能放这个文件的人本来就能替换 exe 本身）以外，发布的构建只有一条路进入，
  而且必须同时满足 3 个条件：用户在应用里打开开关（设置 → PocketRoles → 开发，或者在自己的 `settings.json` 里写
  `"devBuild": true`）；那个文件夹里**同时**有 `PocketRoles.csproj` 和 `PocketRolesLauncher.ps1`（只有项目文件的文件夹，
  也就是 v0.4 时的那种情况，会被拒绝）；并且那个文件夹是用户在应用自己的文件夹选择框里选的（`"devSource"`，只由
  `pickModSource` 写入），或者是桌面上 `PocketRoles.lnk` 或 `PocketRoles Launcher.lnk` 所指目标所在的文件夹，或者是
  `<桌面>\HostRoles`。页面从不传路径，只发送命令的名字；能写这些设置的人，本来也能写旁边的“启动”文件夹。
  （在 v1.1.0（2026-09-26）之前，这一段写着发布的构建完全进不了开发模式；其实在第一个发布版 v1.0.0 里就已经不是这样了。）
  我们如实说明这一点为什么重要：`dotnet build` 会运行 MSBuild，而项目文件可以让 MSBuild 启动其他程序，所以这条路是
  “执行作者的项目文件里写的内容”，而不是“只启动 `dotnet.exe`”。在作者的构建里，它仍然有限制：文件名为 `dotnet.exe`
  的绝对路径；写在源代码里的 3 个固定参数（`build -c Release`），调用者、页面、文件和设置都改不了；`UseShellExecute`
  关闭，不显示控制台窗口；输出通过重定向的标准输出读取；20 分钟后关闭编译器；`DOTNET_ROOT`/`PATH` 只设置给子进程。同一个流程会
  启动 1 次游戏来重新生成 interop 程序集，并且只关闭它自己启动的那一次。
- **内置的反作弊（Aegis）** 读取的是模组自己的游戏副本里的文件、谁都能读的注册表值（安全启动、TPM、内核启动选项、
  易受攻击驱动程序阻止列表）以及进程的*名字*。它从不读取其他进程的内存，从不记录键盘输入或屏幕，也从不停止或删除任何东西；
  发现的结果只留在这台电脑上。从 v1.1.2 起，模组副本里的文件发生变化时，它也会再检查一次；读取的东西相同。
- **第三方组件：** Microsoft Edge WebView2（Microsoft 的运行时，用来绘制界面），以及按用户要求下载到模组自己的游戏副本里的
  BepInEx。许可证文本放在 exe 旁边（`licenses\`、`NOTICE`、`LICENSE`），缺少任何一个时，自检都会让构建失败。
