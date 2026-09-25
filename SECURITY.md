# セキュリティについて（Security policy）

[日本語](#日本語) | [English](#english)

## 日本語

### 直す版

直すのは、いちばん新しい版だけです。

### 知らせ方

セキュリティの問題（悪い人に使われるかもしれない弱いところ）を見つけたら、メールで知らせてください。

- あて先: `pocketroles.report+help@gmail.com`
- 件名: `[StarPocket Client] セキュリティ`
- 日本語・中文・English のどれでも大丈夫です。返事には数日かかることがあります。

**GitHub の Issue や Discord の公開のチャンネルには書かないでください。** 直す前に、悪い人に知られてしまうからです。

書いてほしいこと: 何が起きるか / 起こす手順 / 版（`StarPocket Client.exe` の版・Among Us の版）/ あればログや報告 zip。

**鍵・パスワード・Discord のウェブフックの URL は送らないでください。** 見つけた秘密は「どこにあったか」だけで十分です。

### たとえば、こんなこと

- 決められた 4 つのホスト（`api.github.com`・`github.com`・`objects.githubusercontent.com`・`builds.bepinex.dev`）以外からファイルを取らせる方法
- BepInEx の zip の SHA-256 の確認をすり抜けて、別のファイルをゲームのフォルダーに入れる方法
- 偽の Aegis の定義ファイル（署名の確認をすり抜ける、古い版に戻す）
- `src\Core\ShellOpen.cs` の一覧に無いものを起動させる方法（シェル・スクリプト・別の exe）
- アンインストールに、決められた場所の外を消させる方法
- 報告 zip やアプリのログに、フレンドコード・PUID・キー・ウェブフックの URL が残ってしまう
- 画面（WebView2）に、`app.starpocket.local` 以外の中身を読み込ませる方法

### ここでは受け付けないもの

- Among Us 本体や公式サーバーの問題 → Innersloth へ
- ゲームの中のチーター → ゲームの通報ボタン。ホスト・管理人は `pocketroles.report+host@gmail.com`
- BepInEx・WebView2 そのものの問題 → それぞれのプロジェクト・会社へ

### 知らせてもらったあと

1. 読んで、本当に起きるかを確かめます。
2. 直した版を出します。
3. 知らせてくれた人の名前は、その人がよいと言った時だけ出します。

コード署名（Windows の署名）は、まだしていません。予定は [docs/CODE-SIGNING.md](docs/CODE-SIGNING.md) にあります。

## English

### Supported versions

Only the newest release is fixed.

### How to report

Mail `pocketroles.report+help@gmail.com` with the subject `[StarPocket Client] Security`. Japanese, Chinese or English
are all fine; a reply can take a few days. **Please do not open a public issue or post in a public Discord channel**
before it is fixed.

Tell us what happens, how to make it happen, which version of `StarPocket Client.exe` (and of Among Us), and attach a
log or a report zip if you have one. **Never send keys, passwords or Discord webhook URLs** - where you found a secret
is enough.

Things we very much want to hear about: getting the app to fetch from a host outside its four-host allow-list; getting a
file past the pinned SHA-256 of the BepInEx zip; a forged or rolled-back Aegis definitions file; getting anything
started that is not on the list in `src/Core/ShellOpen.cs`; getting the uninstall to delete outside its allowed paths; a
friend code, PUID, key or webhook URL surviving in a report zip or in the app's log; loading anything but
`app.starpocket.local` into the WebView2 UI.

Not here: Among Us itself or its official servers (Innersloth); cheaters in a game (the in-game report button, or
`pocketroles.report+host@gmail.com` for hosts and admins); BepInEx or WebView2 themselves (their own projects).

After a report we check it, ship a fixed release, and credit the reporter only if they want that.
The app is not code-signed yet; the plan is in [docs/CODE-SIGNING.md](docs/CODE-SIGNING.md).
