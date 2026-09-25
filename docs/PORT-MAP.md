# StarPocket Client — 移植の対応表（PORT-MAP）

今の PowerShell 版（`PocketRolesLauncher.ps1` と `aegis\Aegis.ps1`）の機能を、StarPocket Client（`StarPocket Client.exe`）に移す時の対応表です。
v0.1 でどこまで移すか、v0.1 で移すものは何をそのとおりに写すかを書きます。

- 読んだもの（2026-09-22）: `HostRoles-v050`（ブランチ v0.5.5、コミット `eeab523`）の `PocketRolesLauncher.ps1`（2142 行）・`aegis\Aegis.ps1`（1549 行）・`aegis\definitions.txt(.sig)`・`PocketRoles Launcher.cmd`・`aegis\Aegis.cmd`・`NOTICE`、プロトタイプ `launcher-proto\index.html` と `SPEC.md`、用語集 `scratchpad\terms\glossary.tsv`。
- 行番号は上のコミットのもの。ずれたら関数名で探す。「ps1:」は `PocketRolesLauncher.ps1`、「Aegis:」は `aegis\Aegis.ps1`。
- 「同じ」＝ 判断と文言を変えずに C# に移す。見た目（窓・アニメーション）は変えてよいが、何を確かめて何を決めるかは変えない。
- 今の版の不具合は、v0.1 ではそのまま写し、9 章に書く（直すのは後の版）。例外は用語だけ（中文は公式の用語。9.3）。
- 見た目を作る所（通知の小窓、WebView2 が無い時の窓、タスクバーのバッジ、トレイの点）は apple-design スキル（ばね 170/26・270/18、押した瞬間に反応、途中で止められる動き）に従う。

---

## 1. 版ごとの範囲

| 版 | 中身 |
|---|---|
| **v0.1（今回）** | アプリの枠（1 つのプロセス・1 つだけ起動・WebView2 の画面・トレイ・タスクバー・設定の「閉じる」）＋ Aegis トレイの全部（起動時のスキャン、定義ファイルの取得と署名確認、ゲームの見張り、通知、状態）＋「mod 付きで起動」（確認・起動前スキャン・前回のログの保存・起動）＋フォルダやファイルを開く＋ 30 日たったログの自動削除＋ `--self-test` |
| **v0.1.1** | 設定 → PocketRoles の「起動するゲーム」（PocketRoles / ふつうの Among Us（Steam））。ふつうを選ぶと、プレイ（とトレイの「プレイ」）は `launchVanilla`（`steam://rungameid/945360` だけ。起動前スキャン・ログの保存・MOD のファイルには触らない）。プレイの字の下に小さくゲームの名前、起動のあとはゲームが動き出すまで（最長 30 秒）「プレイ中」。「更新してプレイ」は処理が終わった時の設定で起動する。選んだものは `settings.json` の `startGame`。設定の「修復」のボタンにダウンロードの絵（下向きの矢印が受け皿に入る形 `i-download-tray`。オーナー 2026-09-23）。わかっていること: Aegis の見張りはプロセスの名前だけで見るので、ふつうの Among Us も「監視中」になる（前のトレイと同じ。あとで exe の場所で見分ける） |
| **v0.2（済）** | インストール（5 手順・BepInEx・MOD 本体。robocopy ではなくアプリ自身がコピー）、Steam 版との同期、MOD の更新確認（GitHub API）、`pickSteam`、`launcher-state.json` の読み書きと今までのランチャーからの引き継ぎ（`.lnk` のリンク先から探す）。**13 章**。日ごとの zip・`--action` などの引数・ゲーム終了後に窓を戻す／起動後にトレイへ は v0.4 へ送りました |
| **v0.3（済）** | 報告 zip（伏せ字・証拠の記録 90 日・裏づけの 2 行・メールの下書き）、ひとり分の証拠 `exportOne`（画面のボタンだけ次回）、ショートカット作成（押した時だけ）、PC の起動時に開く（設定を入れた時だけ・既定オフ）、アンインストール（MOD 用のコピーはチェックボックス・既定は残す）、公開 CI でのビルド。**14 章**。開発モードの再ビルドと更新は v0.4 へ送りました |
| v0.4 | 開発モードの再ビルドと更新（`rebuild`・`devUpdate`）、ログの窓（`showLog`）とログのフォルダの大きさ、7 日より前のログの日ごとの zip、画面なしの実行（`--action`・`--tray`・`--scan-only`・`--autolaunch`・`--windowed`）、ゲーム終了後に窓を戻す／起動後にトレイへ（SPEC 5.4）、窓を隠している時のスキャンの小窓。**14.5** |
| 後 | Aegis BAN 管理（`AegisBan.ps1`、11514 行）、status.json・variants.json・titles.json（署名付き）、コミュニティ、プロフィール、クライアント自身の更新、ジャンプリスト。背景の動画は 2026-09-24 に取りやめ（持ち主「動画はいいや、要らない」。背景は自作の動く絵のまま） |

---

## 2. 機能の一覧（今 → v0.1 か後か）

### 2.1 ランチャー（`PocketRolesLauncher.ps1`）

| 今の機能 | 場所 | 扱い |
|---|---|---|
| 引数 `-AutoLaunch -Windowed -Action -SteamDir -GameDir -SourceDir -DesktopDir -CacheDir -Language -Friend` | ps1:9-20 | v0.1 は場所と言語の引数だけ受け付ける（`--game-dir`・`--steam-dir`・`--source-dir`・`--desktop-dir`・`--language`・`--friend`。`-GameDir` の書き方も）。ほかは v0.2 |
| 環境変数 `POCKETROLES_GAMEDIR` / `POCKETROLES_STEAMDIR` | ps1:60, 75 | v0.1（同じ） |
| AppUserModelID `wakayamachannel.PocketRoles.Launcher` | ps1:27-32 | v0.1 は新しい ID `StarPocketGames.Client`（今回の指示。SPEC とは違う。10 章） |
| モード判定（開発 / 友達） | ps1:58 | v0.1（3.2） |
| MOD 用のコピーの場所（OneDrive の規則） | ps1:59-74 | **v0.1・同じ**（3.1） |
| Steam 版の検出 | ps1:617-645 | **v0.1・同じ**（3.3。状態の判定に使う） |
| フォルダを選ぶ（Steam 版） | ps1:647-655 | v0.2（`pickSteam`） |
| 言語 ja / zh-CN / en と文言 | ps1:95-500, 1892-1904 | **v0.1・同じ決め方**（3.14）。文言は起動まわり（`la_*`・`in_firstrun`・`op_notfound`・`f_*`・`lg_*`）だけ移す |
| ゲームの版を読む（globalgamemanagers） | ps1:533-544 | **v0.1・同じ**（3.4） |
| DLL の版の文字列 | ps1:546-567 | v0.1（状態の表示に使う） |
| `launcher-state.json` の読み書き | ps1:569-584 | v0.1 は**読むだけ**（見つかった時。3.2）。書くのは v0.2 |
| `launcher.log`（`[HH:mm:ss] 文`、1 MB 超で起動時に削除） | ps1:505-514, 1907 | v0.1 は自分の `client.log`（同じ書式・同じ 1 MB の規則）。`launcher.log` には触らない。統合は v0.2 |
| 状態表示と警告の行 | ps1:1707-1753 | **v0.1**（判定は同じ。表示はプレイボタンの状態。3.4） |
| 処理中はボタンを全部無効 | ps1:596-600 | v0.1（長い処理は 1 つずつ。ほかの invoke は `busy`） |
| インストール 5 手順・robocopy・BepInEx・MOD 本体の 3 つの入手元・古い HostRoles.dll の削除・Aegis の指紋を書く・steam_appid.txt・ショートカット | ps1:666-1036 | v0.2（ショートカットは v0.3、押した時だけ） |
| 更新を確認（GitHub Releases） | ps1:743-772, 1039-1056 | v0.2 |
| Steam 版の同期（コピー + interop 削除） | ps1:1059-1070 | v0.2 |
| ゲームのログの保存（起動の直前） | ps1:1221-1262 | **v0.1・同じ**（3.7） |
| 30 日たったログ・報告 zip の自動削除 | ps1:1171-1217 | **v0.1・同じ**（3.7。ログを保存する以上、消す方も同じ時に入れる） |
| 7 日より前のログを日ごとの zip へ | ps1:1285-1380 | v0.2（場所を節約するだけ。zip にしなくても 30 日で消える） |
| ログのフォルダの大きさ（2 GB で橙） | ps1:1399-1434 | v0.2 |
| 報告 zip・伏せ字・証拠の記録・メール | ps1:1442-1704 | v0.3 |
| 開発: 再ビルド・更新（interop を作り直す） | ps1:1756-1819 | v0.3 |
| **mod 付きで起動**（確認 → 起動前スキャン → ログの保存 → 起動） | ps1:1822-1876 | **v0.1・同じ**（3.5・3.6）。途中で「更新しますか？」を聞く場面は v0.1 では案内だけ（3.5） |
| ウィンドウで起動（`-Windowed`） | ps1:1868-1871 | v0.1（`launchWindowed` の invoke だけ。引数 `--windowed` は v0.2） |
| バニラ起動 `steam://rungameid/945360` | ps1:2002 | **v0.1.1**（同じ URL・同じ文 `la_vanilla`。開発の道具ではなく、設定の「起動するゲーム」でプレイから。足したのは「Among Us がもう動いている」の確かめだけ） |
| 設定ファイル・ログ・README をメモ帳で開く、mod フォルダ・ログのフォルダを開く | ps1:1878-1890, 1436-1440, 2003-2018 | **v0.1・同じ**（3.12） |
| 画面なし実行 `-Action` | ps1:1912-1930 | v0.2（`--action`） |
| Aegis トレイを別プロセスで起動・`launcher.pid` | ps1:2077-2092 | **v0.1: 同じプロセスの中**。`launcher.pid` は書かない（10 章） |
| Aegis BAN 管理のショートカット | ps1:2094-2122 | 後（BAN 管理と一緒） |
| 開いた直後の処理（Aegis 開始 → 状態 → 30 日の削除 → ログ保存 → zip → 自動起動） | ps1:2124-2140 | v0.1 は「Aegis 開始 → 状態 → 30 日の削除 → ログ保存」の順（zip と自動起動は v0.2） |

### 2.2 Aegis トレイ（`aegis\Aegis.ps1`）

| 今の機能 | 場所 | 扱い |
|---|---|---|
| 1 つだけ（Mutex `Local\wakayamachannel.Aegis.AntiCheat`） | Aegis:1526-1528 | **v0.1**: アプリがこの Mutex を持つ（3.15） |
| データフォルダ `%LOCALAPPDATA%\PocketRoles\Aegis` | Aegis:24-27 | **v0.1・同じ**（3.8） |
| ゲームフォルダの既定（引数が無い時） | Aegis:33-39 | v0.1 はランチャーの規則に一本化（9.1 L-7） |
| 言語（`-Lang`、無ければ Windows の言語） | Aegis:40-43, 179 | v0.1 はアプリの言語（3.14）。言語を変えたらトレイ・通知にもすぐ反映 |
| 文言（ja / zh-CN / en） | Aegis:63-192 | **v0.1・同じ**（中文の用語だけ直す。9.3） |
| 定義ファイルの署名確認 | Aegis:270-374 | **v0.1・同じ**（3.10） |
| 定義ファイルの読み込み・取得・保存（古い版を拒む） | Aegis:376-593 | **v0.1・同じ**（3.10） |
| スキャン 13 項目 | Aegis:595-857 | **v0.1・同じ**（3.9） |
| スキャン画面（枠なしの窓） | Aegis:859-1095 | v0.1 は UI の Aegis パネルに出す（判定と文言は同じ）。窓を隠している時の小窓は v0.2（`--tray`・`--scan-only` と一緒） |
| 独自のトースト（右下・最大 4 枚） | Aegis:1097-1181 | **v0.1・同じ**（3.11） |
| トレイのアイコン・メニュー・ツールチップ | Aegis:1236-1251, 1398-1407 | v0.1: アイコン A 1 つにまとめる（3.13） |
| ゲームの見張り（1 秒ごと・ログの続き読み・通知） | Aegis:1273-1379 | **v0.1・同じ**（3.11） |
| 30 秒ごとのチートツールの確認 | Aegis:1296-1302 | **v0.1・同じ** |
| 状態の窓（記録 200 件） | Aegis:1409-1434 | v0.1: UI の Aegis パネル（`aegis` の event で渡す） |
| 終わり方（ランチャーの PID、手動なら 15 秒後） | Aegis:1304-1309 | v0.1: アプリが終わるまで動く。手動の 15 秒ルールは `--tray`（v0.2） |
| events.log の 30 日の整理 | Aegis:1451-1496, 1529 | **v0.1・同じ**（アプリ起動時、Aegis の Mutex を取った後） |
| 起動前スキャン `-PreLaunch`（終了コード 3） | Aegis:1503-1520, 1544 | **v0.1: アプリの中の関数**（3.6）。`prelaunch-result.txt` と `events.log` も同じく書く |
| `-ScanOnly` | Aegis:18, 1230 | invoke `aegis.scanOnly` は v0.1（3.6 の「新しく読み込んだ定義でスキャン」）。コマンドラインの `--scan-only` は v0.2 |
| 失敗の記録 `aegis.log`（Add-Type の失敗など） | Aegis:26-27, 1546-1549 | v0.1: 同じファイルに「Aegis failed: <例外>」 |

---

## 3. v0.1 でそのまま写す動き

### 3.1 MOD 用のゲームフォルダ（`Modded`）の決め方（ps1:59-74）

上から順に最初に当てはまるもの:

1. 引数 `--game-dir`（`-GameDir`）→ そのまま。
2. 環境変数 `POCKETROLES_GAMEDIR`（空でない時）→ そのまま。
3. 開発モード（3.2）→ `<Src の親>\Among Us PocketRoles`。そこに `Among Us.exe` が無ければ `<Desktop>\Among Us PocketRoles`（開発モードでは OneDrive の規則を使わない）。
4. 友達モード → `<Desktop>\Among Us PocketRoles`。ただし次の全部が当てはまる時は `%LOCALAPPDATA%\PocketRoles\Among Us PocketRoles`:
   - 引数 `--desktop-dir` が無い
   - 環境変数 `OneDrive` がある（`OneDriveCommercial` は見ない。今と同じ）
   - `Desktop` が `OneDrive.TrimEnd('\') + '\'` で始まる（大文字小文字を区別しない）
   - `<Desktop>\Among Us PocketRoles\Among Us.exe` が無い

- `Desktop` ＝ 引数 `--desktop-dir`、無ければ `Environment.GetFolderPath(Desktop)`（リダイレクト後の実際の場所）。
- そこから決まるパス（ps1:77-84）: `DllPath = Modded\BepInEx\plugins\PocketRoles.dll`、`CfgPath = Modded\BepInEx\config\jp.pocketroles.mod.cfg`、`LogPath = Modded\BepInEx\LogOutput.log`、`LogArchiveDir = Modded\BepInEx\PocketRoles\logs`。
- 起動前スキャンとトレイの見張りは、この `Modded` を使う（今のランチャーが `-GameDir $Modded` で Aegis に渡しているのと同じ）。
- 検出は関数にし、Desktop・環境変数・`LOCALAPPDATA`・引数を引数で受け取る（`--self-test` が偽のフォルダで試せるように）。

### 3.2 ランチャーのフォルダ（`Src`）・モード・状態ファイル

今は `Src` ＝ `-SourceDir`、無ければ ps1 のあるフォルダ。開発モード ＝ `-Friend` が無く、`Src\PocketRoles.csproj` がある時（ps1:55-58）。

v0.1 の `Src` の決め方（SPEC 10 章 #2 に合わせる）:
1. 引数 `--source-dir`。
2. exe のフォルダに `PocketRolesLauncher.ps1` か `launcher-state.json` があれば、exe のフォルダ（Setup のフォルダに exe を置いた形）。
3. exe のフォルダから上へ 4 階層まで `PocketRoles.csproj` を探し、あればそのフォルダ（開発）。
4. どれも無ければ exe のフォルダ（状態ファイル無し・友達モード）。
- 開発モード ＝ `--friend` が無く、`Src\PocketRoles.csproj` がある。
- 状態ファイル `Src\launcher-state.json` は、あれば**読むだけ**（UTF-8、BOM 付きもある。ps1 は `Set-Content -Encoding UTF8` で BOM 付きで書く）。v0.1 が使うキー: `steamDir`（3.3）、`lang`（3.14）、`lastBuiltGameVersion`（開発の「再ビルドが必要」）。v0.1 は書かない。
- デスクトップの `PocketRoles Launcher.lnk` のリンク先から探す引き継ぎ（SPEC 6.5）は v0.2。

### 3.3 Steam 版の検出（ps1:617-645）

1. `--steam-dir` → `POCKETROLES_STEAMDIR` → あればそのまま返す（存在は確かめない。今と同じ）。
2. 状態ファイルの `steamDir` に `Among Us.exe` があればそれ。
3. 候補のフォルダ（この順）:
   - `HKCU\Software\Valve\Steam` の `SteamPath`、`InstallPath`
   - `HKLM\SOFTWARE\WOW6432Node\Valve\Steam` の同じ 2 つ
   - `HKLM\SOFTWARE\Valve\Steam` の同じ 2 つ
   - 最後に `C:\Program Files (x86)\Steam`
   - 値の `/` は `\` に直す。重なりは除く（今は大文字小文字を区別して除いている）。
   - HKLM は `RegistryView.Registry64` で開く（今の ps1 は 64 bit の PowerShell で動くので 64 bit の見え方。x86 の exe でも同じにする）。
4. 候補ごとに、その候補と `steamapps\libraryfolders.vdf` の `"path"\s+"([^"]+)"`（UTF-8 で読む。`\\` → `\`）を順にライブラリとして並べる。
5. 最初に `<ライブラリ>\steamapps\common\Among Us\Among Us.exe` があるもの。無ければ見つからない。

### 3.4 状態（プレイボタンの状態）（ps1:533-544, 869-882, 1707-1753）

- ゲームの版: `<dir>\Among Us_Data\globalgamemanagers` を Latin-1（28591）で読み、正規表現 `20\d\d\.\d{1,2}\.\d{1,2}(?![\dfa-z])`（大文字小文字を区別）で最初に見つかった、`2022.` で始まらないもの。無ければ不明（null）。
- 入っているもの: `exe` = `Modded\Among Us.exe`、`bep` = `Modded\BepInEx\core\BepInEx.Core.dll` がある、`bepVer` = その ProductVersion、`bepOk` = `bepVer` に `6.0.0-be.735` を含む、`dll` = `DllPath` がある、`dllVer` = ProductVersion が数字で始まれば `+` の前、そうでなければ FileVersion、`interop` = `Modded\BepInEx\interop\Assembly-CSharp.dll` がある。
- `Installed = exe && bep && dll`
- `NeedsUpdate = steamVer と modVer が両方あり、違う`（Steam が見つからない時は false）
- 開発: `NeedsRebuild = (modVer があり、状態ファイルの lastBuiltGameVersion と違う) || !dll`。友達: false。
- 警告の行（今の色）→ プロトタイプの状態（`PSTATE`）:

| モード | 条件（上から） | 今の文（キー） | 色 | UI の状態 |
|---|---|---|---|---|
| 開発 | NeedsUpdate | `al_update` | 赤 | `devUpdate` |
| 開発 | NeedsRebuild | `al_rebuild` | 橙 | `devRebuild` |
| 開発 | それ以外 | `al_ok_dev` | 緑 | `ready` |
| 友達 | !Installed | `al_notinstalled` | 赤 | `install` |
| 友達 | NeedsUpdate | `al_gameupdated` | 橙 | `sync` |
| 友達 | それ以外 | `al_ok` | 緑 | `ready` |
| どちらも | 起動前スキャンで止めた（v0.1 で足す） | `la_aegis_block` | 赤 | `blocked`（新しい状態。7.3） |

- `steam running` ＝ 名前が `steam` のプロセスがある。`game running` ＝ 名前が `Among Us` のプロセスがある（どのフォルダの Among Us でも）。
- この結果は `status` の event で UI に送る（開いた時・起動の前後・ゲームが終わった時）。

### 3.5 mod 付きで起動（ps1:1842-1876）

今の順番（v0.1 も同じ順番）:

1. `Among Us` のプロセスがある → `la_running`（今はログだけ）。v0.1: `{ok:false, error:<la_running>}`。
2. 状態を計算し直す（3.4）。
3. 友達モードで `!Installed` → `la_notinstalled`（今はログだけ）。v0.1: `{ok:false, error:<la_notinstalled>, needs:"install"}`。
4. `steam` のプロセスが無い → `la_steam`（今はメッセージボックス）。v0.1: `{ok:false, error:<la_steam>}`（画面の中の知らせ。ダイアログは出さない）。
5. 聞く場面（今はダイアログ）:
   - 開発で NeedsUpdate → はい/いいえ/キャンセル `la_update_q`。はい＝開発の更新、キャンセル＝やめる、いいえ＝そのまま起動。
   - 開発で NeedsRebuild → はい/いいえ `la_rebuild_q`。はい＝再ビルド、いいえ＝そのまま起動。
   - 友達で NeedsUpdate → はい/いいえ/キャンセル `sync_q`（{0}=コピーの版、{1}=Steam の版）。はい＝同期、いいえ＝そのまま起動。
   - **v0.1**: 更新・再ビルド・同期はまだ無いので、`{ok:false, unsupported:true, needs:"syncSteam"|"devUpdate"|"rebuild"}` を返し、「今のランチャーで行ってください」と落ち着いた知らせを出す（プレイボタンはすでに `sync` などの状態なので、ふつうはここまで来ない）。「いいえ」でそのまま起動する道は v0.1 には作らない（10 章）。
6. `interop` が無い → `in_firstrun` を記録（止めない）。
7. 起動前スキャン（3.6）。止めた時: 文 = `la_aegis_block` + 各行ごとに `"\n・" + 行`。今は記録＋メッセージボックス。v0.1: `{ok:false, blocked:true, data:{reasons:[行…], text:<文>}}`、プレイボタンを `blocked` の状態に、Aegis パネルに赤い行と直し方、タスクバーにバッジ。PowerShell の窓もダイアログも出さない。
8. 前回のログを保存（3.7 `Save-GameLog`。結果は見ない）。
9. `la_start` を記録。
10. ゲームを起動:
    - ファイル: `<Modded>\Among Us.exe`（フルパス）
    - 作業フォルダ: `<Modded>`
    - 引数: ふつうは**無し**。`launchWindowed` の時だけ `-screen-fullscreen 0 -screen-width 1600 -screen-height 900`
    - 起動のしかた: 今の `Start-Process` と同じく ShellExecute（`ProcessStartInfo.UseShellExecute = true`、動詞は既定）
    - 環境変数: 何も足さない・消さない（アプリの環境をそのまま引き継ぐ）。今のランチャーも何も足していない。違いは PowerShell 自身の `PSModulePath` だけで、ゲームには関係ない。**アプリの中で環境変数を設定しないこと**（WebView2 の設定は `CoreWebView2EnvironmentOptions` と userDataFolder の引数で渡し、`WEBVIEW2_*` 環境変数は使わない。使うとゲームに引き継がれる）。
    - BepInEx を読み込むのはゲームフォルダの `winhttp.dll`（Doorstop）と `doorstop_config.ini`、Steam の外で起動できるのは `steam_appid.txt`（945360。インストールが作る）。アプリは何もしない。
11. `la_started` を記録。`{ok:true, data:{text:<la_started>}}`。起動に成功したかは確かめない（今と同じ。例外の時だけ `err`）。

- 全体の例外は `err`（`エラー: {0}`）を記録して `{ok:false, error}`。
- 長い処理として 1 つずつ（ほかの invoke は `busy`）。
- 使う文（ps1 の同じキーをそのまま）: `la_running` `la_notinstalled` `la_steam` `la_update_q` `la_rebuild_q` `sync_q` `in_firstrun` `la_aegis_block` `la_start` `la_started` `game_running` `err`。

### 3.6 起動前スキャン（ps1:1822-1840、Aegis:1503-1520）

今の決まり:
- ランチャーが `powershell.exe … Aegis.ps1 -PreLaunch -GameDir <Modded> -Lang <言語>` を隠して起動し、最長 **60 秒**待つ。
  - 60 秒たっても終わらない → 止めない（起動する。Aegis は残ったまま）。
  - 終了コードが 3 以外 → 止めない。
  - 3 → `%LOCALAPPDATA%\PocketRoles\Aegis\prelaunch-result.txt` の空でない行を理由として止める。
  - `Aegis.ps1` が無い・起動に失敗 → 止めない。
- `-PreLaunch` の中身:
  - 定義ファイルを**新しく読み込む**（別プロセスなので毎回。取得はしない）。
  - 13 項目をスキャン。
  - `events.log` に `yyyy-MM-dd HH:mm:ss  pre-launch scan: blocked (<n>)` か `… pre-launch scan: ok` を追記。
  - `prelaunch-result.txt` を UTF-8（BOM 付き）で書き直す。中身は状態 4（赤）の行ごとに `<項目名>: <詳細>` と、直し方があれば ` → <直し方>`。止めない時も書く（空）。
  - 赤の項目が 1 つでもあれば 3、無ければ 0。
  - Aegis 自身の例外 → 0（止めない）、`aegis.log` に記録。
- 赤（止める）になる項目: MOD 本体の整合性・ほかのプラグイン・ゲームへの注入・カーネルの保護・実行中のチートツール（3.9 の「止める」）。項目の中で例外が出たら、その項目は黄（止めない）。

v0.1:
- 同じプロセスの中の関数。定義ファイルは毎回新しく読み込む（今の別プロセスと同じ結果。3.10 の「インスタンス」）。
- 別のスレッドで動かし、60 秒の期限も同じ（過ぎたら止めずに起動し、`client.log` に書く）。
- `events.log` と `prelaunch-result.txt` も今と同じく書く（今のランチャーと行き来しても読めるように）。
- 進み具合は `progress` の event `{task:"prelaunch", step, of:13, value}` とタスクバー（`TBPF_NORMAL`）。
- 今の小窓の待ち時間（最初 450 ms、1 項目 300〜460 ms、終わってから 0.9 秒 / 止めた時 9 秒）は見た目だけなので写さなくてよい。

### 3.7 ゲームのログの保存と 30 日の削除（ps1:1072-1262）

共通:
- ログの整理は名前付き Mutex `Local\PocketRolesLauncher.logs` の中で行う（今のランチャーと MOD も同じ名前）。
- 取れるまで最長 15 秒待つ（窓は固めない）。`AbandonedMutexException` は「取れた」とみなす。取れなければ何もしない。
- 例外は外に出さず、`client.log` にだけ書く（今の `Log-Quiet`）。

**Save-GameLog**（起動の直前。今は開いた時にも行う。v0.1 も両方）:
1. `LogOutput.log` が無い、または 0 バイト → 保存するものなし（true）。
2. MOD 用のコピーの `Among Us` が動いている → しない（false）。判定: 名前が `Among Us` のプロセスで、パスが `<Modded>\Among Us.exe`（フルパス、大文字小文字を区別しない）と同じもの。パスが読めないものも「動いている」に数える。パスは `QueryFullProcessImageName` で読む（`PROCESS_QUERY_LIMITED_INFORMATION` だけ。メモリは読まない）。
3. Mutex を取る。
4. 名前 ＝ `LogOutput-<ログの最終更新時刻 yyyy-MM-dd_HHmmss>.log`（InvariantCulture）、置き場所 ＝ `LogArchiveDir`。
   - 同じ名前がもうある → true。
   - `logs-<yyyyMMdd>*.zip` か `logs-<yyyy-MM>*.zip` の中に同じ名前の項目がある → true。
5. `FileShare.Read` で開く（ゲームが書いている間は失敗 → false）。
6. `<保存先>.<PID>.part` に書き、最終更新時刻を元のログと同じにしてから、保存先の名前に変える。
7. `lg_saved` を記録。失敗したら `.part` を消す。

**Remove-ExpiredLocalData**（開いた時。Save-GameLog より先）。消すもの:
- `LogOutput.log` 自体: 最終更新から 30 日以上、かつ MOD 用のコピーのゲームが動いていない時。
- `BepInEx\LogOutput-<時刻>.log`（よけておいたログ）: 名前の時刻から 30 日以上。
- `LogArchiveDir` の中は `Test-LogArchiveExpired` の規則（ファイルごと。1 つ失敗しても続ける）:
  - `LogOutput-<時刻>.log` → 名前の時刻から 30 日以上。
  - `*.part` → 最終更新から 1 日以上。
  - `logs-<yyyyMMdd>(-n).zip` → その日の終わりから 30 日以上（9999 年は消さない）。
  - `logs-<yyyy-MM>(-n).zip` → その月の終わりから 30 日以上（9999 年は消さない）。
  - ほかの名前は消さない。
- デスクトップの `PocketRoles-report-<yyyyMMdd-HHmm>.zip`: 名前の時刻から 30 日以上（名前がこの形のものだけ）。
- キャッシュ（`%TEMP%\PocketRolesLauncher`）の `report-<yyyyMMdd-HHmm>` フォルダ: 1 日以上。
- 消した数が 1 以上なら `lg_expired` を記録（キャッシュのフォルダは数えない）。

### 3.8 Aegis のデータフォルダ（Aegis:24-27 ほか）

`stateDir = %LOCALAPPDATA%\PocketRoles\Aegis`（今は `$env:LOCALAPPDATA`。起動時に作る。失敗は無視）。v0.1 も同じ場所を使う（今の ps1 と共有）。

| ファイル | 書く人 | 形 |
|---|---|---|
| `events.log` | トレイ・起動前スキャン | 行 `yyyy-MM-dd HH:mm:ss  <文>`（間は空白 2 つ）、UTF-8（新しく作る時だけ BOM）、改行 CRLF。30 日より古い行は起動時に消す（`EventsLog.Prune`: 時刻の無い行は前の行に従う・先頭は残す。消す行がある時だけ `events.log.tmp` → 上書き → tmp 削除。UTF-8 BOM 付き） |
| `definitions.txt` / `definitions.txt.sig` | 取得（3.10） | 正規化したバイト / 署名の文（前後の空白を除き末尾に `\n`、UTF-8 BOM 無し） |
| `mod-fingerprint.txt` | スキャン・ランチャー | `<sha256 小文字 64 桁>|<版>`（BOM 無し） |
| `prelaunch-result.txt` | 起動前スキャン | 3.6 |
| `launcher.pid` | 今のランチャー | v0.1 は書かない（10 章） |
| `aegis.log` | Aegis の失敗 | `yyyy-MM-dd HH:mm:ss <文>`、UTF-8 |

アプリ自身のデータ（新規・今回の指示）: `%LOCALAPPDATA%\StarPocket\Client\`
- `settings.json`（`{"close":"tray"|"quit","lang":"auto"|"ja"|"zh-CN"|"en"}`。UTF-8 BOM 無しで書き、BOM 付きも読める。v0.1.1: ふつうの Among Us を選んでいる間だけ `"startGame":"vanilla"`。無い・知らない値は PocketRoles。クライアントの色を選んだ時だけ `"accent":"#RRGGBB"`（14.4b）。v1.1: 設定 → 全般 → 起動時の動作の「起動のたびに Aegis のスキャンを出す」を切った時だけ `"startScan":false`（既定はオン。オーナー 2026-09-23「起動のたびに出す形に変えて」。`src\Shell\ClientApp.cs` の `UpdateScanCard`: 起動時のスキャンを右下の小窓に毎回出す。窓が出てから小窓が出る。問題なしなら 1.8 秒で消え、赤があれば小窓はクリックまで残り、画面の Aegis パネルも開く。パネルはこの設定を切っていても開く））
- `client.log`
- `WebView2\`（WebView2 のユーザーデータ。必ずここを指定する。指定しないと exe の隣に `.WebView2` ができる）

### 3.9 スキャン 13 項目（Aegis:595-857）

状態: 0 待ち、1 実行中、2 OK（緑）、3 注意（黄）、4 止める（赤）。

- 失敗した時の状態は、その項目が「止める」なら 4、それ以外は 3。
- 項目の中の例外 → 詳細 = 例外の文、直し方なし、「止める」を外して 3。
- 数: 注意 = 失敗の数、赤 = 4 の数。
- 読むのは、ファイル・誰でも読めるレジストリの値・プロセスの**名前**だけ。ほかのプロセスのハンドルは開かない。

| # | 項目（キー） | 読むもの | OK（詳細） | 失敗（詳細 / 直し方） | 止める |
|---|---|---|---|---|---|
| 1 | Aegis エンジン `engine` | 定義の状態 `SigState` | 0 → `engine.ok`（26, 版）。1 → `engine.nofile`（26）も OK | 2 → `engine.nosig`（黄） | いいえ |
| 2 | Among Us `game` | `Among Us.exe`、ゲームの版（3.4 と同じ読み方） | 版が `2026.8.18` → `game.ok`。版が読めない → `game.nover`（OK） | exe が無い → `game.none`。版が違う → `game.other`（版, 2026.8.18） | いいえ |
| 3 | BepInEx `bep` | `BepInEx\core\BepInEx.Core.dll` と `<Game>\winhttp.dll` の両方 | `bep.ok` | `bep.none` | いいえ |
| 4 | MOD 本体の整合性 `mod` | `plugins\PocketRoles.dll` の SHA-256（`FileShare.ReadWrite` で開く）と版（ProductVersion ?? FileVersion ?? "?" の `+` の前）、`mod-fingerprint.txt` | 前の記録なし → 記録して `mod.first`。ハッシュが同じ → `mod.same`。ハッシュが違い版も違う → 記録し直して `mod.update`（版, 前の版） | DLL が無い → `mod.none`（**止めない**に変える）。ハッシュが違い版が同じ → `mod.changed` / `mod.fix`（記録は書き直さない） | はい |
| 5 | ほかのプラグイン `plug` | `BepInEx\plugins` の中の `*.dll`（下のフォルダも）で `PocketRoles.dll` 以外（名前は大文字小文字を区別しない） | `plug.ok` | `plug.warn`（名前を `, ` でつなぐ）/ `plug.fix` | はい |
| 6 | ゲームへの注入 `inj` | ゲームフォルダ直下の `*.dll`。名前（小文字）が定義の `[dlls]` と同じか、`[dllwords]` の語を含む | `inj.ok` | `inj.warn` / `inj.fix` | はい |
| 7 | Aegis の設定 `cfg` | `BepInEx\config\jp.pocketroles.mod.cfg` の `[AntiCheat]`（`Detect` `AutoKick` `AnnounceKick` `Callout`。値が `false`（大文字小文字無視）だけオフ、無ければオン） | ファイル無し → `cfg.none`（OK）。`cfg.ok`（4 つのオン/オフ） | Detect がオフ → `cfg.off` | いいえ |
| 8 | BAN リスト `ban` | `BepInEx\PocketRoles\Banlist.txt`（UTF-8）。行の `//` 以降を捨て、前後の空白を除き、空・`;`・`#` で始まる行は数えない | `ban.ok`（人数）。いつも OK | — | いいえ |
| 9 | セキュアブート `sb` | `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State` の `UEFISecureBootEnabled` | キーが無い → `sb.unknown`（OK）。1 → `sb.on` | それ以外 → `sb.off` | いいえ |
| 10 | TPM `tpm` | `HKLM\SYSTEM\CurrentControlSet\Enum\ACPI\MSFT0101` の下のキーの数 > 0 | `tpm.ok` | `tpm.none` | いいえ |
| 11 | カーネルの保護 `kern` | `HKLM\SYSTEM\CurrentControlSet\Control` の `SystemStartOptions` を大文字にして空白で区切る。`TESTSIGNING`、`DEBUG` か `DEBUGPORT…`、`DISABLE_INTEGRITY_CHECKS`（表示は `NOINTEGRITYCHECKS`） | `kern.ok`（メモリ整合性がオンなら `kern.hvci` を足す。`…\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity` の `Enabled` = 1） | `kern.warn`（見つかったものを ` / ` でつなぐ）/ `kern.fix`（testsigning → debug → nointegritychecks の順で 1 つだけ） | はい |
| 12 | 脆弱ドライバーの遮断 `vdb` | `…\Control\CI\Config` の `VulnerableDriverBlocklistEnable`、メモリ整合性、`…\Control\CI\Policy` の `VerifiedAndReputablePolicyState`、ビルド番号（`SOFTWARE\Microsoft\Windows NT\CurrentVersion` の `CurrentBuildNumber`） | 値 1 → `vdb.on`。メモリ整合性オン → `vdb.hvci`。スマート アプリ コントロール 1 → `vdb.on`。値が無く build ≥ 22621 → `vdb.default` | それ以外 → `vdb.off` | いいえ（注意だけ） |
| 13 | 実行中のチートツール `tools` | 全プロセスの名前（空白・`-`・`_` を除き小文字）を定義の `[tools]` と比べる（`name*` は前方一致、ほかは完全一致）。同じ名前は 1 回 | `tools.ok` | `tools.warn` / `tools.fix`（名前を `, ` でつなぐ） | はい |

- HKLM はすべて `RegistryKey.OpenBaseKey(LocalMachine, RegistryView.Registry64)`。
- 状態の行の文（今の小窓の下の行。v0.1 は Aegis パネルの見出しに使う）:
  - `scanning`（n/13）
  - `done`
  - `done.warn`（注意の数）
  - 起動前だけ: `go`（止めない時）/ `blocked`（赤の数）
- 赤の行の直し方の見出しは `fix.head`。
- スキャンの時に読む・判断する所は、読む部分（ファイル・レジストリ・プロセス）と決める部分に分け、決める部分に値を渡して試せるようにする（`--self-test` は本物のレジストリやプロセスではなく、渡した値で試す）。

### 3.10 定義ファイル（Aegis:270-593）

**取得**（トレイの開始時に 1 回だけ。起動前スキャン・`scanOnly` の時は取らない）:
- データ: `https://raw.githubusercontent.com/wakayamachannel/PocketRoles/main/aegis/definitions.txt`
- 署名: 同じ URL に `.sig` を足したもの。データが取れた時だけ取りに行く。
- 裏のスレッド（`IsBackground`）で順に。TLS 1.2 を足す。
- 1 つにつき `Timeout = 8000 ms`・`ReadWriteTimeout = 8000 ms`。
- User-Agent `Aegis/1.0 (+https://github.com/wakayamachannel/PocketRoles)`（同じ）。
- 大きさの上限: データ 262144 バイト（`4 × 64 KiB`）、署名 4096 バイト。超えたら捨てる。
- HTTP の失敗・ネットの失敗・例外 → 何もしない（知らせない）。
- 取れたら `Store(データ, 署名を UTF-8 で読んだ文)`。この PC の情報は送らない（ふつうの GET だけ）。

**署名の確認** `Sig.Verify(canonical, sigText, trusted, revoked)`（何があっても例外を出さない）:
- 正規化: 先頭の UTF-8 BOM（EF BB BF）を除き、CRLF を LF に（CR だけの所は残す）。署名はこの正規化したバイト全体に対するもの。
- `sigText` が null か空白だけ → **Missing**。長さが 4096 文字を超える → **Invalid**。
- 行ごと（`\n` で分ける）:
  - 前後の空白と先頭の U+FEFF を除く。空行と `#` で始まる行は飛ばす。
  - `keyid=`（大文字小文字無視）→ 鍵の ID。
  - それ以外は base64 の署名（2 行目の署名が来たら Invalid、base64 でなければ Invalid）。
- 署名が無い・空 → Invalid。ID が失効した鍵 → Invalid。
- 信頼する鍵を順に: 失効したものは飛ばし、ID があればその ID の鍵だけ。`RSACryptoServiceProvider.FromXmlString` → 署名の長さが `(KeySize+7)/8` と同じ → `VerifyData(canonical, "SHA256", sig)`（PKCS#1 v1.5）。合えば **Ok**。どれも合わなければ Invalid。
- 暗号・形式・引数の例外は「合わない」扱い。

**信頼する鍵**（`src\Net\AegisRules.cs`・`aegis\AegisBan.ps1` と同じ一覧。Aegis:292-298 から**1 文字も変えずに**写す）:
- ID `91400fdf0f5af4ca`（SubjectPublicKeyInfo の SHA-256 = `91400fdf0f5af4ca9d4772e63e7840c2c88cee099ef70147a5583cc99d45fbaf`、RSA 3072、指数 AQAB）

```
<RSAKeyValue><Modulus>vy6G7MqjS1pVrs2kPhhIgm8kiHeYpLSegBOTWq2XnpxwJXspVovRsHZqtn9vFwd3Vb/zzJQqoa9uzhKjj5befeJArZXgn5gSwgbSKY2J3MC2gXVgHY/ELfwkgO2qCD6uXDEym4zGTe262sDKOJoD3xT2QHfNAEaxXlRFRnU0WPTL1Gca31TqVdo1Pxj9/ofCvpNsuTS834hMyeTSE/8qV+t6nKGmtCI93/0f4qhpXusWBDOyM04uM0pUTH3eKtku5hOU9H1TKkXbMvCavP2529JMu3lk8T1Y5gVdOllC33sSwL0ehqz3rdy1/t8mlQISwqL4EuS1ph+21oTbY6tjXQCRv7GvkRuxl91ZyyZkgJV4kwWXMCGYo5sIxuUN/RVtf9UB+qX/qYPHA94NKaV2Ee6AnUaEsiYmMykK9YYpJIWEwNNt8XIbYM/rlNlkckTUp3ND1O7XB7D6WP9TeAYDiqt5asXcNTLsciq27xndQSJRPybYTaZOcZ/RmCKKQ7Xp</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>
```

- 失効: `cedca02cae60f103`（Claude アプリの中で作られた最初の鍵）。
- 鍵の一覧は 1 つのファイル（例: `src\Aegis\TrustedKeys.cs`）にまとめる。`tools\sign-definitions.ps1 -Verify` と `build-release.ps1` は、今は `Aegis.ps1` から一覧を読んでいるので、後でこのファイルも読むように直す（v0.5.5 のリポジトリ側の作業。SPEC 6.3）。

**読み込み** `Load(bundledDir, stateDir)`:
- 同梱: `<exe>\aegis\definitions.txt`（今は `Aegis.ps1` の隣）。キャッシュ: `stateDir\definitions.txt`。
- それぞれ:
  - ファイルがあり、262144 バイト以下。
  - `.sig` があり 4096 バイト以下なら UTF-8 で読む（無ければ Missing）。
  - 署名が Ok の時だけ中身を読み取る。
- 読めたもののうち版が一番大きいもの（同じ版なら先に読んだ同梱の方）を使い、`SigState = 0`。
- 読めたものが無い:
  - ファイルが 1 つでもあった → `SigState = 2`。無かった → `SigState = 1`。
  - 版 0、組み込みの一覧を使う:
    - tools: `cheatengine*, artmoney*, wemod, extremeinjector*, xenos, xenos64, ghinjector*, squalr, speedhack*, gameguardian, sickomenu*, amongusmenu*, reclass*`
    - dlls: `version.dll, dxgi.dll, d3d11.dll, dinput8.dll, winmm.dll, dsound.dll, xinput1_3.dll, xinput1_4.dll, xinput9_1_0.dll, opengl32.dll`
    - dllwords: `menu, cheat, inject`
- 使っているファイルの正規化したバイトを覚えておく（`inUse`。組み込みの時は null）。

**中身の読み取り** `ParseText`（64 KiB 文字以下）:
- 行ごとに、前後の空白と先頭の U+FEFF を除く。空行と `#` の行は飛ばす。
- `=` があり `[` で始まらず、`=` の前が `version`（大文字小文字無視）の行:
  - 最初の 1 回だけ整数として読む（読めなければ 0）。
  - 2 回目からは飛ばす。
- 見出し:
  - `[tools]` `[dlls]` `[dllwords]` はその一覧へ（大文字小文字を区別する完全一致）。
  - ほかの `[…]` は読まない（`[rules]` `[ngwords]` `[bans]` `[erase]` などは MOD 用）。
- 項目は小文字にする。3 つの合計が 500 を超えたら全体を捨てる。
- 版 ≤ 0、または tools が 0 件 → 捨てる。
- 捨てる項目:
  - tools: `*` を除いて 5 文字未満のもの。守るプロセスに当たるもの（`amongus steam steamwebhelper steamservice powershell explorer svchost system discord valorant valorantwin64shipping riotclientservices riotclientux vgc vgtray mumuplayer mumunxmain mumunxdevice chrome msedge obs64 claude`）。
  - dlls: `.dll` で終わらないもの。守る DLL（`winhttp.dll gameassembly.dll unityplayer.dll baselib.dll steam_api.dll steam_api64.dll d3dcompiler_47.dll`）。
  - dllwords: 4 文字未満のもの。守る DLL の名前に含まれる語。
- 捨てた後に tools が 0 件 → 全体を捨てる。
- 守るプロセスの一覧に StarPocket Client の名前を足すかは後で決める（今は足さない。足すと定義の意味が変わるため）。

**保存と「古い版に戻さない」規則** `Store(data, sigText)`（取得した時だけ）:
1. 正規化 → 署名が Ok でなければ保存しない。
2. 読み取れなければ保存しない。版が今使っている版より小さい → 保存しない。
3. 版が同じ → 今使っているもの（`inUse`）とバイトまで同じ時だけ次へ（組み込みを使っている時は保存しない）。
4. キャッシュにもう同じバイトがあり、その `.sig` も Ok → 何も書かずに true。
5. `definitions.txt.sig.tmp`（署名の文を trim + `\n`、UTF-8 BOM 無し）と `definitions.txt.tmp`（正規化したバイト）を書く。
6. `.sig` → 本体の順で「消してから移す」で置き換える。
- 取った新しい定義は、今は**次の Aegis の起動か、次の起動前スキャンから**使われる（トレイの「もう一度スキャン」は開始時に読んだものを使い続ける。9.2 A-1）。アプリは何日も開いたままになるので、v0.1 のレビューで「窓を開いた時・プレイの時に、前回の取得から 12 時間以上たっていればもう一度取る」を足した（新しい DefinitionsStore で。古い版に戻さない規則はそのまま）。

**作り**: 定義は静的なクラスではなく**インスタンス**にする（`DefinitionsStore`。信頼する鍵・失効の一覧・同梱のフォルダ・stateDir を受け取る）。
- トレイ: 開始時に作って `Load` → 取得 → `Store`。「もう一度スキャン」もこれを使う。
- 起動前スキャンと `aegis.scanOnly`: 毎回新しく作って `Load`（今の別プロセスと同じ結果になる）。
- `--self-test`: その場で作った鍵を渡して試す。

### 3.11 ゲームの見張りと通知（Aegis:1273-1396）

**見張り**（1 秒ごとのタイマー。v0.1 はトレイが動いている間ずっと）:
- ゲーム ＝ `Process.GetProcessesByName("Among Us")` が 1 つ以上（どのフォルダでも。9.2 A-11）。
- 見張っていない時にゲームが現れた:
  - 見張り開始。検知と退出の数を 0 に。
  - `gameSeenAt = 今`、`logPos = -2`、`logBaseline = 今の LogOutput.log の長さ`（無ければ 0）。
  - 読み残しとデコーダーを空に。
  - `AddEvent(b.watch0)`（トーストは出さない）。
- 見張り中にゲームが消えた:
  - 見張り終わり、`gameGoneAt = 今`。
  - `Toast(b.stop, 情報)`、`AddEvent(b.stop)`。
- 見張り中は毎回 `ReadLog`。
- 30 回ごと（約 30 秒）: チートツールの名前を確かめる（3.9 #13 と同じ比べ方）。
  - まだ知らせていない名前ごとに `tools.warn` を `AddEvent` し、`Toast(警告, 必ず出す)`。
  - 知らせた名前は覚え続ける（消さない）。
- アイコンとツールチップを直す。

**ログの続き読み** `ReadLog`（`<Modded>\BepInEx\LogOutput.log`）:
- 無ければ何もしない。`FileShare.ReadWrite | FileShare.Delete` で開く。
- `logPos == -2` の時:
  - `長さ < logBaseline` か、`最終更新 ≥ gameSeenAt − 2 秒` なら「書き直された」とみなし、0 から読む。
  - そうでなければ前回のゲームのログなので読まない。
- `長さ < logPos` → 0 から読み直す。`長さ == logPos` → 何もしない。
- 1 回に最大 4 MiB 読む。
- UTF-8 のデコーダーは続けて使う（読みの境目で切れた文字を保つ）。
- 前回の読み残し＋今回の文字 → 最後の `\n` までを行に分けて `Line()` へ。残りは次へ。
- 例外は無視。

**行の判断** `Line(line)`（上から最初に当てはまったもの）:

| 正規表現 | すること |
|---|---|
| `PocketRoles v([\d.]+) loaded` | MOD の版を覚え、`Toast(b.watch("v"+版), 情報)` |
| `CheatDetector: removing #\d+ (.+?) \(client \d+\) with a room ban: (\w+)` | 退出の数 +1、12 秒間は「警告」の表示。文 = `b.removed(名前.Trim(), ルール名)`。`AddEvent`、`Toast(警告, 必ず出す)` |
| `CheatDetector: (\[test\] )?(\w+) \((Certain\|Repeat\|Notice)\) #\d+ (.+?) \(client` | ルールが `Callout` `CalloutRepeat` `VoteCallout` なら**何もしない**（インポスターの名前を外に出さないため）。`[test] ` なら文の頭に `test`（`[テスト] `）を付け、検知の数は増やさない。文 = `b.flag(名前.Trim(), ルール名)`。`AddEvent`。段階が `Notice` 以外か `[test]` の時だけ `Toast(警告)`（間引きあり） |

- ルール名 ＝ `r.<Rule>` の文。無ければルールの名前そのもの（Aegis:155-177。数は `RuleCount = 26`）。

**AddEvent**:
- 画面用の一覧に `HH:mm:ss  <文>` を足す（最大 200 件、古いものから消す）。
- `events.log` に `yyyy-MM-dd HH:mm:ss  <文>` を追記する。
- 状態の画面が開いていれば更新（v0.1 は `aegis` の event）。

**Toast の間引き**（`Balloon`）:
- 「必ず出す」でないものは、前のトーストから 4 秒以内なら**出さない**（ためておかない）。
- 出したら前回の時刻を今に（「必ず出す」の時も）。
- 警告 → 黄（Amber `#FFBA30`→`#AA6000`）で 6000 ms。情報 → 緑（`#34D38C`→`#0A6E48`）で 3500 ms。

**開始時**: トレイを出した時にゲームが動いていなければ `Toast(b.ready, 情報)`（動いていれば、見張り開始の知らせに任せる）。すぐに 1 回見張る。

**アイコン**:
- 退出から 12 秒以内 → 警告。見張り中 → 見張り。それ以外 → 待機。
- ツールチップは 3.13。

**通知の文**（Aegis:129-149 と同じ。中文の用語は 9.3）:

| キー | ja | zh-CN | en |
|---|---|---|---|
| `b.ready` | 起動しました。ゲームを始めると監視します。 | 已启动。开始游戏后将进行监视。 | Ready. Watching starts when the game runs. |
| `b.watch` | 監視を始めました（PocketRoles {0}） | 开始监视（PocketRoles {0}） | Watching (PocketRoles {0}) |
| `b.watch0` | 監視を始めました | 开始监视 | Watching |
| `b.stop` | ゲームが終わりました。待機中です。 | 游戏已结束。待机中。 | The game closed. Standing by. |
| `b.removed` | {0} を退出させました（{1}） | 已移出 {0}（{1}） | Removed {0} ({1}) |
| `b.flag` | {0}: {1} | {0}: {1} | {0}: {1} |
| `test` | [テスト]  | [测试]  | [test]  |
| `tools.warn` | {0} が起動中（チートに使えるツール） | {0} 正在运行（可用于作弊的工具） | {0} is running (usable for cheating) |

### 3.12 トースト（Aegis 独自の小窓。Aegis:1097-1181）

Windows のバルーン通知やトースト通知は使わない。ゲーム中の「応答不可」でも見えるように、自前の小さな窓を出す。
- 400×78、主モニターの作業領域の右下（右と下に 18 px）。
- 開いている数に応じて上へ積む（間 10 px）。最大 4 枚で、5 枚目の前に一番古いものを閉じる。閉じたら並べ直す。
- 窓の形: フォーカスを取らない（`ShowWithoutActivation`、`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`）、タスクバーに出ない、最前面。
- 動き: 250 ms で不透明度 0.96 まで出る → 決めた時間そのまま → 30 ms ごとに 0.08 ずつ消える。押すと消える。
- 中身: 左に色の帯 4 px、盾の絵、「AEGIS」、文（はみ出したら「…」）。
- 見た目は apple-design に合わせて直してよい（大きさ・位置・枚数・時間・フォーカスを取らないことは変えない）。

### 3.13 トレイ（v0.1）

- アイコンは 1 つ（`starpocket.ico`、アイコン A。小さいサイズを選ぶ）。
- Aegis の状態は右下の小さな点で表す（青緑＝待機中、緑＝監視中、黄＝退出から 12 秒。SPEC 5.3。今の盾の色と同じ意味）。点は実行時に描く。
- ツールチップ（SPEC 5.3。63 文字を超えたら切る。.NET Framework の NotifyIcon は 64 文字以上で例外）:
  - ja: `StarPocket Client — Aegis 待機中` / `StarPocket Client — Aegis 監視中 · 検知 {0} · 退出 {1}`
  - zh-CN: `StarPocket Client — Aegis 待机中` / `StarPocket Client — Aegis 监视中 · 检测 {0} · 移出 {1}`
  - en: `StarPocket Client — Aegis standing by` / `StarPocket Client — Aegis watching · {0} flagged · {1} removed`
- 右クリックのメニュー（言語は 3.14。変えたらすぐ作り直す）:

| 項目 | ja | zh-CN | en | すること |
|---|---|---|---|---|
| 開く（太字・既定） | 開く | 打开 | Open | 窓を出して前へ |
| プレイ | プレイ | 开始游戏 | Play | 窓を出して、プレイボタンと同じ流れ（`launch`）。ゲーム中・未インストール・処理中は押せない |
| Aegis の状態 | Aegis の状態 | Aegis 状态 | Aegis status | 窓を出して Aegis パネルを開く |
| もう一度スキャン | もう一度スキャン | 重新扫描 | Scan again | トレイの定義でスキャン（`aegis.rescan` と同じ） |
| （区切り） | | | | |
| 終了 | 終了 | 退出 | Quit | ゲーム中なら確かめてから終了（下） |

- ダブルクリック → 開く（今の Aegis は「状態」。SPEC 5.3 に合わせる）。
- 終了の確かめ（ゲーム中だけ。SPEC 5.4）:
  - ja「ゲームが終わるまでの Aegis の見張りも止まります。終了しますか？」
  - zh-CN「游戏结束前 Aegis 的监视也会停止。要退出吗？」
  - en「Aegis will also stop watching until the game ends. Quit?」

### 3.14 言語の決め方（ps1:1892-1904）

今のランチャー:
1. `-Language` が `ja` / `zh-CN` / `en` のどれか → それ。
2. 状態ファイルの `lang` がそのどれか → それ。
3. Windows の表示言語（`CurrentUICulture.Name`）が `ja*` → ja、`zh*` → zh-CN（繁体字の Windows も zh-CN）、それ以外 → en。
- Aegis はランチャーから `-Lang` で受け取る（トレイは起動時の言語のまま）。
- 文言が無いキーは ja にし、それも無ければキーの名前を出す。

v0.1:
1. `--language`（`-Language`）。
2. `settings.json` の `lang` が `auto` 以外（UI の言語の欄。`zh` は `zh-CN` に直す）。
3. 状態ファイルの `lang`（3.2 で見つかった時）。
4. Windows の表示言語（上と同じ）。
- 決まった言語は、トレイ・トースト・スキャンの文・起動の文・WebView2 の UI（`lang` の event）の全部で使う。
- UI で言語を変えたら `setLang` で受け取り、`settings.json` に書いて、すぐ全部に反映する（`launcher-state.json` には書かない。v0.2 で決める）。
- 中文の UI の字体は Microsoft YaHei UI（今と同じ）。

### 3.15 Mutex と、今のアプリとの同居

| 名前 | 誰が | v0.1 |
|---|---|---|
| `Local\StarPocketGames.Client` | アプリ（1 つだけ起動） | 取れなければ、1 つ目を前に出して終わる（4.1）。`--self-test` はこの対象外 |
| `Local\wakayamachannel.Aegis.AntiCheat` | Aegis トレイ | アプリが持つ。今の `Aegis.ps1`（ランチャーが起動したもの・`Aegis.cmd`）は、アプリが動いていればすぐ終わる（今の仕組みのまま） |
| `Local\PocketRolesLauncher.logs` | ログの整理（ランチャー・MOD） | 同じ名前で使う（3.7） |

- 今の Aegis トレイが先に動いていて Mutex を取れない時:
  - アプリの Aegis の見張りは止めたまま。Aegis パネルに「古い Aegis トレイを終了してください」と出す（SPEC 5.1）。
  - 1 秒ごとの見張りのタイマーで `WaitOne(0)` を試し、取れたら開始する。
- 起動前スキャンは Mutex を使わない（今と同じ）。

---

## 4. v0.1 で新しく作るもの（アプリの枠）

### 4.1 プロセスと 1 つだけ起動
- exe ＝ `StarPocket Client.exe`。AssemblyTitle / FileDescription / Product ＝ `StarPocket Client`、Company ＝ `StarPocket Games`、ApplicationIcon ＝ `starpocket.ico` の写し（アイコン A、9 サイズ）。
- .NET Framework 4.8、WinForms、`OutputType=WinExe`。ビット数は SPEC 2 章の x86 を勧める（Among Us も 32 bit、`WebView2Loader.dll` は x86 用 1 つ）。
- ほかのプロセスのパスは `QueryFullProcessImageName`。HKLM は `Registry64`。
- ウィンドウを作る前に `SetCurrentProcessExplicitAppUserModelID("StarPocketGames.Client")`。
- 2 つ目の起動:
  - `AllowSetForegroundWindow(ASFW_ANY)` のあと、登録したメッセージ（`RegisterWindowMessage("StarPocketGames.Client.Show")`）を送る（`HWND_BROADCAST` か、1 つ目が名前付きイベントを待つ形）。
  - 1 つ目は窓を出して前へ。2 つ目はすぐ終わる。
- **Start with Windows はオフ**。v0.1 は Run キー・ショートカット・ファイルの関連付けを書かない（SignPath: 聞かずにシステムを変えない）。

### 4.2 窓と WebView2
- 枠なし、最初は 1280×720 で真ん中。最小化と閉じるは UI の中のボタン（`window.minimize` / `window.close`）。
- ドラッグ: プロトタイプは CSS の `app-region: drag`（index.html:165, 190）。`CoreWebView2Settings.IsNonClientRegionSupportEnabled = true`（新しめの SDK と ランタイムが要る）。使えない時は、その帯の mousedown を `window.drag` で受けて `ReleaseCapture` + `WM_NCLBUTTONDOWN(HTCAPTION)`。
- UI のファイルは exe の隣の `ui\`。`SetVirtualHostNameToFolderMapping("app.starpocket.local", <exe>\ui, Deny)`、`https://app.starpocket.local/index.html` を開く（名前は SPEC 2.3。遅い時は `.example` のような予約された名前を検討）。
- ネットに出さない:
  - `NavigationStarting` で `https://app.starpocket.local/` 以外は取り消す。
  - `NewWindowRequested` は `Handled = true`。
  - `AddWebResourceRequestedFilter("*", All)` で、この名前以外への要求は 403 を返す（プロトタイプの見本の URL も読みに行かない）。
- 設定:
  - 本番: `AreDevToolsEnabled=false`、`AreDefaultContextMenusEnabled=false`、`IsStatusBarEnabled=false`、`IsZoomControlEnabled=false`、`AreBrowserAcceleratorKeysEnabled=false`。
  - SDK にあれば `IsReputationCheckingRequired=false`（ローカルの画面だけなので SmartScreen に問い合わせない）。
- `WebMessageReceived` は `e.Source` が `https://app.starpocket.local/` で始まる時だけ受ける。本文は `WebMessageAsJson` を `JavaScriptSerializer`（System.Web.Extensions）で読む（ほかの NuGet は足さない）。
- 返事は `PostWebMessageAsJson` で、invoke 1 つにつき必ず 1 回（SPEC 3 章）。長い処理は 1 つずつ。処理中の invoke は `{ok:false, busy:true}`。
- **WebView2 が無い時**:
  - `CoreWebView2Environment.GetAvailableBrowserVersionString()` が例外（`WebView2RuntimeNotFoundException`）か null。
  - HTML を使わない小さな窓（3 言語）:
    - 説明: ja「画面の表示に Microsoft Edge WebView2 ランタイムが要ります。」
    - ボタン:「ダウンロードのページを開く」（Microsoft の公式ページ `https://developer.microsoft.com/microsoft-edge/webview2/` を既定のブラウザで開く。直接ダウンロードする fwlink は使わない。勝手に入れない）と「閉じる」。
    - 一文:「今までのランチャー（PocketRoles Launcher.cmd）も使えます」。
  - v0.1 はこの窓を閉じたら終わる。

### 4.3 閉じる・終了（設定 `close`）
- 既定は `tray`: ✕ で窓を隠す（`Hide()`、タスクバーから消える）。トレイと Aegis は動き続ける。
- `quit`: ✕ でアプリを終了（ゲーム中は 3.13 の確かめ）。
- `app.quit`・トレイの「終了」も 3.13 の確かめ → Aegis を止め、トレイのアイコンを消して終わる。
- UI からは `settings.set {key:"close", value:"tray"|"quit"}`。
- v1.2（持ち主 2026-09-24「× 押したときの挙動が落ちた感じ」）: ✕・トレイ退避・終了は 140 ms で薄くなってから消える（`src\Shell\CloseFade.cs`、窓側は `Form.Opacity`（`MainForm.FadeOut`）、ページ側は `host-v01.js` の縮み（`window` イベントの `{closing:true, ms}`））。Windows の「アニメーション効果」が OFF の時（＝ページの `prefers-reduced-motion`。両側が同じ元 `SPI_GETCLIENTAREAANIMATION` を見る）とシャットダウン時は即座。演出が失敗しても必ず閉じる（自己テスト「close fade」）。

### 4.4 タスクバー
- `ITaskbarList3` は、登録メッセージ `TaskbarButtonCreated` を受けてから使う。
- 進み具合の API（`SetProgressState` / `SetProgressValue`）を用意する。v0.1 で使うのは起動前スキャンと「もう一度スキャン」（n/13、`TBPF_NORMAL`）、終わったら `TBPF_NOPROGRESS`。インストール・更新は後の版がこれを使う。
- **オーバーレイのバッジ**（`SetOverlayIcon`）: Aegis が問題を見つけている間だけ出す。
  - 赤: 直前のスキャン（開始時・もう一度スキャン・起動前）に赤の行がある。次のスキャンで無くなるまで。
  - 黄: 退出から 12 秒。
  - どちらも無ければ消す。説明の文（アクセシビリティ）も付ける。
  - SPEC 5.2 の「見張り中は緑の盾」は任意。

### 4.5 `--self-test <tempdir>`（画面なし）
- 窓・トレイ・Mutex の取得・ネット・本物の `%LOCALAPPDATA%`・本物のゲームフォルダは使わない。書くのは `<tempdir>` の中だけ。
- WinExe なので、`AttachConsole(ATTACH_PARENT_PROCESS)` で呼んだ側の画面に出し、同じ内容を `<tempdir>\self-test.txt` にも書く。
- 出力:
  - 1 行ずつ `PASS <名前>` / `FAIL <名前>: <理由>` / `INFO <文>`。
  - 最後に `RESULT PASS <n>/<n>` か `RESULT FAIL <失敗数>/<n>`。
  - 終了コードは全部 PASS で 0、それ以外 1。
- 試す内容は 8 章。

---

## 5. 起動の時の順番（v0.1）

1. 引数を読む。`--self-test` ならそれだけ（4.5）。
2. AppUserModelID を設定 → 1 つだけ起動の Mutex。
3. WebView2 があるか確かめる（無ければ 4.2 の窓）。
4. `client.log` が 1 MB を超えていれば消す。`Src`・モード・状態ファイル（読むだけ）・言語・`Modded`・Steam を決める。
5. 窓と WebView2 を作る（見えるのは UI の準備ができてから）。トレイのアイコンを出す。
6. Aegis の Mutex:
   - 取れたら順に:
     1. `EventsLog.Prune(stateDir, 30)`
     2. 定義を読み込む（トレイ用のインスタンス）
     3. 取得を裏で 1 回
     4. 開始時のスキャン（13 項目。結果は Aegis パネル・レールの点・赤ならバッジ。今の小窓の代わり）
     5. 見張りのタイマーを開始
     6. ゲームが動いていなければ `b.ready` のトースト
   - 取れなければ 3.15。
7. 状態（3.4）を `status` の event で送る。
8. `Remove-ExpiredLocalData` → `Save-GameLog`（3.7。ランチャーを開いた時と同じ）。

---

## 6. ウィンドウを閉じた後も残るもの・残さないもの

- 残す（今と同じ場所）: `%LOCALAPPDATA%\PocketRoles\Aegis\*`、`BepInEx\PocketRoles\logs\*`。
- 新しく作る: `%LOCALAPPDATA%\StarPocket\Client\`（`settings.json`・`client.log`・`WebView2\`）。
- 作らない: Run キー、ショートカット、関連付け、`launcher.pid`、`launcher-state.json` への書き込み、`launcher.log` への書き込み。

---

## 7. プロトタイプの invoke と v0.1 の扱い

### 7.1 UI → アプリ（`{type:"invoke", id, cmd, args}`）

「知らせ」＝ `{ok:false, unsupported:true, data:{cmd, version:"0.1"}}`。UI は落ち着いた知らせを出す（7.3）。

| cmd | どこから | v0.1 | 中身 |
|---|---|---|---|
| `launch` | プレイボタン、トレイの「プレイ」（v0.1.1: 設定の「起動するゲーム」が PocketRoles の時）、ツール（いつも MOD 付き） | **する** | 3.5（途中は `progress` の `prelaunch`、止めた時は `blocked`） |
| `launchWindowed` | ツール「ウィンドウで起動」 | **する** | 3.5 と同じで、引数だけ 1600×900 |
| `aegis.rescan` | Aegis パネル、トレイ | **する** | トレイの定義でスキャン。進み具合は `progress` `{task:"rescan", step, of:13}`、結果は `aegis` の event、返事は `{ok:true, data:{warnings, serious}}` |
| `aegis.scanOnly` | ツール「スキャンだけ」 | **する** | 定義を新しく読み込んでスキャン（取得しない。`-ScanOnly` と同じ）。アプリは終わらない |
| `aegis.events` | Aegis パネル「記録（events.log）」 | **する** | `notepad.exe "<stateDir>\events.log"`。無ければ `op_notfound` |
| `openModFolder` | ツール、設定 | **する** | `explorer.exe "<Modded>"`（今と同じく、あるかは確かめない） |
| `openLogsFolder` | ツール | **する** | `Modded` が無ければ `op_notfound(st_mod, Modded)`。あれば `LogArchiveDir` を作って `explorer.exe` で開く |
| `openConfig` | ツール | **する** | あれば `notepad.exe "<CfgPath>"`、無ければ `op_notfound(f_cfg, パス)`。アプリは中身を読まない |
| `openLog` | ツール | **する** | `notepad.exe "<LogPath>"` / `op_notfound(f_log, …)` |
| `openReadme` | ツール | **する** | 置き場所は開発なら `Src`、友達なら `Modded`。zh-CN は `README.zh-CN.md`→`README.md`、en は `README.en.md`→`README.md`、ja は `README.md`。無ければ `op_notfound(f_readme, …)` |
| `window.minimize` | 窓のボタン | **する** | 最小化 |
| `window.close` | 窓のボタン | **する** | 4.3 |
| `window.drag` | （SPEC の予備） | **する** | 4.2 |
| `app.quit` | プロフィールのメニュー | **する** | 4.3 |
| `settings.set` | 設定 | `close`・`startGame`（v0.1.1）・`accent`（14.4b）・`startScan`（v1.1）を**する** | `{key:"close", value:"tray"\|"quit"}`・`{key:"startGame", value:"pocketroles"\|"vanilla"}`・`{key:"accent", value:"default"\|"#RRGGBB"}`・`{key:"startScan", value:true\|false}`（JSON の真偽値。`"on"`/`"off"` の字でも可）を `settings.json` に（ほかの値は `invalid value`）。`accent` の返事には画面が塗る 11 個の値と、色を動かした時はその知らせが入ります。ほかのキーは知らせ（`autostart` などは UI 側で元に戻す。7.3） |
| `setLang` | （v0.1 で UI に足す） | **する** | `{lang:"auto"\|"ja"\|"zh"\|"en"}` → 3.14 |
| `install`（`{}` / `{resume:true}` / `{handoff:"steam"}`） | プレイボタン | 知らせ | v0.2 |
| `checkUpdate` | プレイボタン（アップデート）、ツール | 知らせ | v0.2 |
| `syncSteam` | プレイボタン（更新してプレイ）、設定「修復」 | 知らせ | v0.2 |
| `pickSteam` | 設定「Among Us の場所」 | 知らせ | v0.2 |
| `showLog` | ツール「進行ログ」 | 知らせ | v0.2（`launcher.log` の統合と一緒） |
| `rebuild` / `devUpdate` | プレイボタン（開発） | 知らせ | v0.3 |
| `launchVanilla` | プレイボタンとトレイの「プレイ」（設定の「起動するゲーム」がふつうの Among Us の時）、ツール（プレイと同じ起動の流れ） | **する**（v0.1.1） | 「Among Us がもう動いている」なら `la_running`。そうでなければ `steam://rungameid/945360` を開くだけ（ShellExecute）。状態の確かめ・起動前スキャン・ログの保存・MOD 用のコピーには触らない。Steam が閉じていても Steam が自分で起動する。返事は `{ok:true, data:{text:la_vanilla}}`。長い処理の 1 つ（`busy`） |
| `makeReport` / `mailBug` / `mailRequest` / `openReportFolder` | ツール | 知らせ | v0.3 |
| `shortcut.create` | 設定 | 知らせ | v0.3（v0.1 はショートカットを作らない） |
| `uninstall` | 設定（確認の後） | 知らせ | v0.3 |
| `aegis.banConsole` | Aegis パネル | 知らせ | 後 |
| `openExternal`（`site` `discord.invite` `legal.terms` `legal.privacy`。`video.intro` `video.howto` は 2026-09-24 に動画ごと無くなった） | ホーム・設定・コミュニティ | 知らせ | 後（行き先のページがまだ無い。許可リストで開く形は SPEC 2.3） |
| `recent.clear` / `player.vip` / `player.restrict` | コミュニティ・プロフィール | 知らせ | 後 |
| `profile.set` `{name, avatar:0-5}` | プロフィール（名前と組み込みの絵の番号） | **する**（v1.3） | `settings.json` の `profileName`（16 文字まで、空なら書かない）/ `profileAvatar`（0〜5、0 なら書かない）。同じ値なら書かない。返事は `{ok:true}`、0〜5 でない `avatar` は `{ok:false, error:"invalid value"}`。名前はログに書かない |
| `profile.pickImage` `{}` | プロフィールの絵の 7 個目「自分の画像を選ぶ」 | **する**（v1.3） | アプリの `OpenFileDialog`（PNG / JPG / ICO / BMP / GIF、20 MB・3000 万画素まで。WebP はこの版では読まない）→ 正方形に切って 256px に縮めた PNG の写しを `%LOCALAPPDATA%\StarPocket\Client\profile\avatar.png` に置く（元の画像は触らない・パスはログにも返事にも書かない）→ 返事 `{ok:true, data:{avatar:"data:image/png;base64,…"}}` / やめた時 `{ok:true, data:{cancelled:true}}` / 断り `{ok:false, error:<日本語の理由>}` / 二重 `{ok:false, busy:true}`。長い処理の `busy` には入れない（`pickSteam` と同じ）。`src\Core\ProfileImage.cs`、**19.** |
| `profile.clearImage` `{}` | 「画像をやめて元の絵に戻す」 | **する**（v1.3） | 写し `avatar.png` を消す（無くても ok）→ `{ok:true, data:{avatar:null}}` / 消せない時 `{ok:false, error:<日本語>}` |
| `setOption`・`status`（SPEC の一覧にだけある） | — | 知らせ | `settings.set` と `status` の event に置き換わった |
| 一覧に無い cmd | — | `{ok:false, error:"unknown command"}` | SPEC 3 章 |

UI の中だけで済み、アプリに送らないもの（プロトタイプの `LOCAL`）:
- `media.new`、`soon`、`status`（ライブラリを開く）、`aegis.status`（Aegis パネルを開く）
- `headless`、`cliHelp`、`aegis.trayOnly`（説明だけ）、`legal.third`

### 7.2 アプリ → UI（`{type:"event", name, data}`）

| name | v0.1 | data |
|---|---|---|
| `status` | 送る | `{mode:"dev"\|"friend", pstate, steamVersion, copyVersion, bepinex, dll, interop, steamRunning, gameRunning, warn:<警告の行の文>}` |
| `aegis` | 送る | `{state:"idle"\|"watching"\|"kicked"\|"off", detected, kicked, rows:[{key, title, detail, fix, state}], events:[文…新しい順], defs:{version, sig:0\|1\|2}, summary}` |
| `progress` | 送る | `{task:"prelaunch"\|"rescan", step, of:13, value}` |
| `game` | 送る | `{running}` |
| `lang` | 送る | `{lang:"ja"\|"zh"\|"en", pref}`（開いた時と、トレイ側で変わった時） |
| `shell` | 送る（ページが出来た時。読み直しのたびに） | `{version, app, dragRegion, close, startGame, autostart, accent, startScan, sound, volume, vars, mode, devFolder, devOn}` に加えて v1.3 の `avatar`（自分の画像の写し `"data:image/png;base64,…"` か `null`。毎回載せる）・`avatarError`（写しが読めなくて消した時だけ。日本語の一文）・`profile`（`{name, avatar:0-5}`。`settings.json` にある時だけ。無ければ UI が自分の分を `profile.set` で送る）。`settings.json` の値がページの保存に勝つ |
| `window` | 送る | `{visible}`（隠した時・戻した時。UI は動く絵などを止める／戻す）/ `{closing:true, ms}`（v1.2: 薄くする時だけ。UI は `ms` の間ほんの少し縮み、`{visible:true}` で戻す。アプリはこの返事を待たない） |
| `nav` | 送る（v0.1 で足す） | `{open:"d-aegis"}`（トレイの「Aegis の状態」）、`{run:"aegis.rescan"}`（トレイの「もう一度スキャン」）、`{run:"play"}`（トレイの「プレイ」。v0.1.1: UI が「起動するゲーム」を見て `launch` か `launchVanilla` を送る） |
| `media` `community` `recent` `titles` `log` | 送らない | 後の版 |

### 7.3 UI を app の `ui\` にする時の直し（exe の外のファイルだけ）
- **Google Fonts の `<link>` を 3 行消す**（index.html:944-946）。UI はネットに出ない。字体は CSS にある予備（Yu Gothic UI / Meiryo UI / Microsoft YaHei UI / system-ui）で出る。M PLUS 1・Montserrat の同梱は後（SIL OFL。ダウンロードは今回できない）。
- **クルーのアイコン**（Innersloth のデザイン）:
  - `<symbol id="pr-icon">` の中の `data:image/png;base64` の画像（index.html:977、`_src\pr-128.png`）を取り出して `ui\img\pocketroles-128.png` に置き、`href` をそのファイルに変える。
  - exe とそのリソースには入れない。後で、署名付きのタイトル一覧（titles.json）から取る形に替える。
- 中文の `内鬼` を `伪装者` に直す（index.html:2183「按设置补足内鬼人数」→「按设置补足伪装者人数」）。
- 本体の `<script>`（1 つ・インライン）はそのまま使い、つなぎの `ui\host-v01.js` を後ろに足す。
  - CSP は、インラインの script を止めないものにする（`script-src 'self' 'unsafe-inline'`）。ネットに出さないことは 4.2 の要求の遮断で守る。
- `host-v01.js` の仕事:
  - `report()` を差し替える:
    - `r.unsupported` → 知らせ ja「「{x}」はこの版（v0.1）ではまだ使えません。今までのランチャーで行えます。」／ zh-CN「此版本（v0.1）还不能使用“{x}”。请用原来的启动器进行。」／ en「“{x}” isn’t in this version (v0.1) yet. The current launcher still does it.」
    - `r.error` → その文をそのまま出す。
    - `r.blocked` → 状態を `blocked` にし、Aegis パネルを開く。
  - `PSTATE` に `blocked` を足す（アイコン `i-shield`、赤の点）:

    | | ja | zh-CN | en |
    |---|---|---|---|
    | ボタン | もう一度確かめる | 重新检查 | Check again |
    | 状態の行 | Aegis が起動を止めました | Aegis 已阻止启动 | Aegis stopped the launch |
    | その下 | 赤い項目を直してから押してください | 请先处理红色项目再按 | Fix the red items, then press |

    押すと `launch`（起動前スキャンからやり直す）。
  - `host:status` → `pstate` と下の行を本物に（見本の状態の切り替え `#protoBar`・`#stateSel` はアプリの中では隠す）。
  - `host:aegis` → 見本の `CHECKS` / `AEGIS_LOG` の代わりに本物の行と記録を出す（13 項目の並びと「止める」「注意だけ」の印はプロトタイプと同じ）。
  - `host:lang` → `setLangPref`。UI で言語を変えたら `setLang` を 1 回送る。
  - `settings.set` で知らせが返ったキーのうち `autostart` は、UI の値を元（オフ）に戻す。

---

## 8. `--self-test` で試すこと（すべて `<tempdir>` の中の偽物で）

1. **ゲームフォルダの決め方**（3.1）:
   - 引数 > 環境変数 > 開発（兄弟フォルダに exe がある／無い）> 友達。
   - OneDrive の規則（Desktop が OneDrive の中で Desktop にコピーが無い → LOCALAPPDATA、コピーがある → Desktop、`--desktop-dir` がある → Desktop）。
2. **Steam の検出**（3.3）:
   - 偽のルートと UTF-8（日本語のパス）の `libraryfolders.vdf`、`\\` の直し。
   - 最初に `Among Us.exe` があるライブラリを選ぶこと、状態ファイルの `steamDir`。
   - レジストリの代わりにルートの一覧を渡す。
3. **ゲームの版**: `2026.8.18` を拾う、`2022.x` を飛ばす、`2026.8.18f1` は拾わない、ファイル無し → null。
4. **状態の判定**（3.4）: Installed / NeedsUpdate / NeedsRebuild → `pstate` の 7 通り。
5. **スキャンの判断**（3.9。渡した値で）:
   - エンジン 0/1/2。版が違う。BepInEx（winhttp が無い）。
   - 指紋: 初回 / 同じ / 版が上がった / 同じ版で中身が違う → 赤、記録は書き直さない / DLL が無い → 止めない。
   - ほかのプラグイン（下のフォルダ・大文字小文字）、注入（`version.dll`、`xxmenu.dll`、`winhttp.dll` は無視）。
   - cfg（`Detect=False` → 黄、キーが無い → オン）、Banlist の数え方（`//`・`;`・`#`・空行）。
   - SecureBoot / TPM / カーネル（`TESTSIGNING`、`DEBUGPORT=…`、`DISABLE_INTEGRITY_CHECKS`、直し方の優先順）/ 遮断（値・メモリ整合性・スマート アプリ コントロール・build 22621）の値の組み合わせ。
   - ツール名の正規化と `*` の一致。項目の例外 → 黄で止めない。
   - 起動前の結果（赤があれば 3 相当、`prelaunch-result.txt` の行の形と BOM、`events.log` の行）。
6. **署名**（その場で RSA 3072 の鍵を作り、`Verify` の引数で渡す）:
   - 合う / 違う鍵 / keyid が違う / 失効した keyid / `.sig` 無し → Missing。
   - 署名 2 行 / 4096 文字超 / base64 でない / 長さが違う → Invalid。
   - CRLF と LF、BOM の有無で同じ結果。
7. **定義の読み込みと保存**（3.10。鍵を渡したインスタンスで）:
   - 同梱とキャッシュの新しい方、同じ版なら同梱。
   - 署名が合わないファイルだけ → SigState 2。ファイル無し → 1 と組み込みの一覧。
   - `ParseText` の規則（最初の `version=` だけ、500 件、5 文字未満・守るプロセス・守る DLL・4 文字未満の語を捨てる、`[rules]` などを飛ばす）。
   - `Store`: 新しい → 保存、古い → 拒む、同じ版で違うバイト → 拒む、同じバイト → true で書かない、組み込みの時に同じ版 → 拒む、書いた `.sig` の形。
8. **本物の鍵の一覧**:
   - `TrustedKeys` の各鍵の ID ＝ その鍵の SubjectPublicKeyInfo（DER）の SHA-256 の先頭 16 桁で、`91400fdf0f5af4ca…fbaf` と一致する。
   - 失効の一覧に `cedca02cae60f103` がある。
   - `INFO`: 同梱の `aegis\definitions.txt` が今の鍵で通るかを報告する（v0.5.5 の今は失効した鍵なので通らない。9.1 L-11）。
9. **ログの行の判断**（3.11。時計を渡す）:
   - 読み込み / 退出 / 検知（Certain・Repeat・Notice・[test]）/ 言い当て 3 種は何もしない、の各行 → 数・記録・トーストを出すか・「必ず出す」か。
   - 4 秒の間引き。12 秒の警告。
10. **ログの続き読み**: `logPos=-2` の規則（前回のログは読まない・短くなった・更新時刻）、行の途中で切れた読み、UTF-8 の文字が 2 回に分かれた読み、ファイルが短くなった時の読み直し。
11. **言語**（3.14）: 引数 / settings / 状態ファイル / `ja-JP` `zh-TW` `zh-CN` `en-US` `fr-FR` の組み合わせ、`zh` → `zh-CN`。
12. **events.log の整理**（30 日、時刻の無い行、先頭の行）と、ログの 30 日の規則（`Test-LogArchiveExpired` の名前ごと、9999 年）、`Save-GameLog`（名前、`.part`、すでにある・zip の中にある）。
13. **トレイのツールチップ**が 63 文字以下になること（3 言語・大きな数）。

---

## 9. 既知の不具合・癖（v0.1 ではそのまま。後の版で直す）

### 9.1 ランチャー
- **L-1** 起動前スキャンが 60 秒で終わらない時は「止めない」で起動し、PowerShell の Aegis（と最前面のスキャンの窓）は残る。
- **L-2** Aegis に渡す引数を `"<パス>"` で囲むだけなので、`\` で終わる `-GameDir`（例 `D:\AU\`）だと `\"` が引用符の逃がしになり、引数がずれる。同じプロセスの中の関数にすれば起きない（写さない）。
- **L-3** 「ゲームが動いている」は、どのフォルダの `Among Us` でも数える（Steam 版を動かしているだけでも「すでに起動しています」）。
- **L-4** 開発モードの再ビルドは、ランチャー自身のプロセスに `DOTNET_ROOT` と `PATH` を足す。その後に同じ窓から起動したゲームはそれを引き継ぐ。v0.1 はビルドしないので起きない（写さない）。
- **L-5** `$script:LauncherVersion` が `0.4.0` のまま（User-Agent と報告に出る）。
- **L-6** `PocketRoles Launcher.cmd` は引数を渡さないので、`-AutoLaunch` `-Windowed` をショートカットから使えない。
- **L-7** ゲームフォルダの規則がランチャーと手で起動した Aegis で違う（Aegis は OneDrive を見ず、LOCALAPPDATA に exe があればそちら。`POCKETROLES_GAMEDIR` も見ない）。v0.1 はランチャーの規則 1 つ。
- **L-8** ゲームの起動に成功したかは確かめず、いつも `la_started` を出す。
- **L-9** `Test-ModdedGameRunning` は `$p.Path`（MainModule。プロセスのメモリを読む権限で開く）を使う。v0.1 は `QueryFullProcessImageName` にする（結果は同じ）。
- **L-10** 繁体字の Windows も `zh-CN`（簡体字）になる。
- **L-11（リリース前に直すこと）** v0.5.5 の同梱の `aegis\definitions.txt.sig` は、失効した鍵 `cedca02cae60f103` の名前で署名されている。
  - 今の Aegis もアプリも、同梱のファイルを必ず拒む（エンジンの行が黄「定義の署名なし・不一致（組み込みの定義を使用）」になり、組み込みの一覧を使う）。
  - オーナーが今の鍵 `91400fdf0f5af4ca` で署名し直すまで続く（v0.5.5 の予定: version を上げて署名）。アプリは同じファイルをそのまま同梱する。

### 9.2 Aegis
- **A-1** 「もう一度スキャン」は、トレイ開始時に読んだ定義を使い続ける。取得して保存した新しい定義は、次の起動前スキャンか次の起動から使われる（ファイルの頭の説明「次のスキャンから」と違う）。SPEC 7 章の改善は後の版。
- **A-2** 開始時のスキャンで見つけたチートツールを覚えないので、約 30 秒後にトレイがもう一度知らせる。知らせた名前は消さないので、閉じてまた開いたツールは 2 回目を知らせない。
- **A-3** 「必ず出す」でないトーストは、前のトーストから 4 秒以内なら捨てる（ためない）。Certain の検知が 4 秒の間に 2 つあると、2 つ目は記録だけ。
- **A-4** 指紋: 版の文字列が変われば、中身が違っても「更新」として受け入れる（版の情報を書き換えた DLL は通る）。初回はどの DLL でも記録する。
- **A-5** 版の文字列の作り方がランチャー（ProductVersion が数字で始まる時だけ使う）と Aegis（ProductVersion が null でなければそのまま）で違う。ランチャーが指紋を書いた直後の初回スキャンが「更新を確認」になることがある（害はない）。
- **A-6** カーネルの保護で複数見つかっても、直し方は 1 つだけ出す。
- **A-7** 項目の中の例外は黄で止めない（例: プロセス一覧が取れないとチートツールの確認は通る）。わざとの決まり（Aegis の失敗で起動を止めない）。
- **A-8** 定義の保存は `.sig` を先に置き換える（消してから移す）。途中で落ちると新しい `.sig` と古い本体になり、次はキャッシュが拒まれて同梱に戻る（安全側だが、取ったものは失う）。
- **A-9** `events.log` は、トレイと起動前スキャン（別プロセス）が鍵なしで追記する。まれに行が混ざる。v0.1 は同じプロセスなので、アプリの中ではロックして書く（ファイルの形は同じ）。
- **A-10** 対応するゲームの版 `2026.8.18`・ルール数 `26`・BepInEx `6.0.0-be.735` が、あちこちに直に書いてある。アプリでは定数を 1 か所にまとめる（値は同じ）。ゲームが更新されたら一緒に上げる。
- **A-11** 見張りは、どのフォルダの `Among Us` でも「監視中」になる（Steam 版でも。MOD 用のコピーのログは書き直されないので何も読まないが、表示は監視中）。
- **A-12** スキャンの各項目は UI のスレッドで動く（プロセス一覧・レジストリ・DLL の SHA-256 の間、画面が少し固まる）。v0.1 は別のスレッドで動かす（判断は同じ）。
- **A-13**（移植で見つけた・そのまま）`events.log`・`aegis.log`・状態の一覧の時刻は、`ToString("yyyy-MM-dd HH:mm:ss")` を**今の地域の設定**で書く。`:` は「時刻の区切り」の記号なので、区切りが `:` でない地域（例: 一部の北欧の設定）では `21.04.12` のようになる。30 日の整理（`EventsLog.Prune`）は `:` の形（InvariantCulture）でしか読まないので、その地域では行が**いつまでも消えない**（プレイヤー名の 30 日の決まりが効かない）。日本・中国・英語の設定では起きない。直すなら書く方も InvariantCulture にする（MOD と Aegis.ps1 も一緒に）。
- **A-14**（移植で見つけた・そのまま）署名の確認 `Sig.Verify` は「例外を出さない」と書いてあるが、鍵の XML が壊れていると `FromXmlString` が `XmlSyntaxException` を出し、それは catch の一覧（Cryptographic / Format / Argument）に無いので外に出る。鍵の一覧は組み込みなので実際には起きない（読み込みは catch で「通らない」、取得は取得側の catch で「保存しない」になる）。
- **A-15**（移植で見つけた・そのまま）「ほかのプラグイン」と「ゲームへの注入」は `Directory.GetFiles(…, "*.dll")` を使う。拡張子がちょうど 3 文字の形は「その 3 文字で**始まる**拡張子」にも当たる（.NET の決まり）ので、`BepInEx\plugins\Foo.dll_off` のように名前を変えて外したつもりのファイルも「見知らぬプラグイン」（赤）になる。

### 9.3 用語の直し（ここだけは写さずに直す）
中文は Among Us の公式の用語（ゲームから取り出した用語集 `glossary.tsv`。Innersloth の文なのでリポジトリには入れない。確かめる道具は `tools\terms\check-terms.ps1`）を使う。

| キー | 今（Aegis.ps1） | アプリ |
|---|---|---|
| `r.TaskImpostor` | 内鬼完成任务 | 伪装者完成任务 |
| `r.VentRole` | 不能用通风管却用了 | 不能用通风口却用了 |
| `r.VentFar` | 远离通风管进入通风管 | 远离通风口进入通风口 |
| `r.ReportForge` | 不可能的举报 | 不可能的报告 |
| `r.KillPhase` | 会议或放逐画面中击杀 | 会议或驱逐画面中击杀（Aegis の移植の時に `check-terms.ps1` で見つけた。公式は `ExileTextNonConfirm`「{0} 遭到驱逐。」） |

- プロトタイプの `内鬼` 1 か所も直す（7.3）。
- 「主持」（Aegis の `sub`・`st.about`）は公式にもある言葉（`HostHeader`）なので、そのままでよい。
- v0.5.5 の `lang\zh-CN.json` はまだ `内鬼` のまま（公式の用語に直すブランチ `zh-terms` は未マージ）。アプリの文言には使わない。
- マージの時は `tools\terms\check-terms.ps1 . -Allow tools\terms\check-terms.allow.tsv` をかける（このリポジトリに入れた。直す前の言葉を引用しているこの文書は allow に入れてある）。用語集 `glossary.tsv` はゲームから取り出した Innersloth の文なので、リポジトリには入れない（無くても動く）。

### 9.4 アプリの枠を作った時に見つけたこと（2026-09-22）
- **S-1** 今のランチャーは `Test-Path` を `-LiteralPath` なしで使う所がある（フォルダの決め方・Steam の検出・インストールの確認・`Open-File`）。パスに `[` `]` があるとワイルドカードとして読まれ、「無い」になることがある。アプリは文字どおりに確かめる（写さない。L-2・L-9 と同じ扱い）。
- **S-2** 「Steam を先に起動してください」は、今は小窓だけで `launcher.log` に残らない。アプリは画面の中の知らせに加えて `client.log` にも書く。
- **S-3** `launcher-state.json` は起動の時に 1 回だけ読む。今のランチャーは状態を出すたびに読み直す（開発モードの `lastBuiltGameVersion`）。アプリを開いたまま古いランチャーで再ビルドすると、アプリを開き直すまで「再ビルドが必要」のまま。
- **S-4** `System.Drawing.Icon`（.NET Framework）は `starpocket.ico` の 256 px（PNG）を読めず 128 px を選ぶ。窓・トレイ・バッジは 16〜64 px なので害はない（exe のアイコンとしては Windows が 256 px も使う）。
- **S-5** 窓の大きさは v0.1 では変えられない（1280×720 を画面の拡大率に合わせ、作業領域より大きければ縮める）。大きさを覚える・最小 1024×600（SPEC 5.4）は後の版。初めて閉じた時の「トレイで動いています」の知らせは v0.1 のレビューで入れた（1 回だけ。settings.json の trayHintShown）。
- **S-6** 更新が必要な時の「いいえ＝そのまま起動」（3.5 の 5）は作っていない（10 章の案どおり）。プレイボタンは `sync`・`devUpdate`・`devRebuild` の状態になり、押すと「今のランチャーで」の知らせ。

---

## 10. 今回の指示と SPEC.md の違い（オーナーに確かめること）

| 項目 | 今回の指示（v0.1 はこちら） | SPEC.md |
|---|---|---|
| exe の名前 | `StarPocket Client.exe` | `StarPocketClient.exe` |
| AppUserModelID | `StarPocketGames.Client` | `StarpocketGames.StarpocketClient`（決定と書いてある） |
| データの場所 | `%LOCALAPPDATA%\StarPocket\Client\` | `%LOCALAPPDATA%\StarpocketGames\Client\` |

その他の判断:
- 更新を聞く場面（3.5 の 5）で「いいえ ＝ そのまま起動」を v0.1 に作るか（案: 作らない。v0.2 で UI の確認ダイアログと一緒に）。
- `launcher.pid`: SPEC 7 章は「段階 1〜2 は書き続ける」だが、アプリが書くと、古いランチャーが起動した PowerShell の Aegis が、ランチャーを閉じた後も生き続ける（`Alive(LauncherPidFile())`）。そうなると、アプリは Aegis の Mutex をずっと取れない。案: 書かない。
- 開始時のスキャンの結果を窓で出すか（今は毎回、最前面の小窓）。案: UI の Aegis パネルとレールの点・バッジだけにし、小窓は `--tray` の時（v0.2）。
- 起動に成功したら窓を隠す／ゲームが終わったら窓を戻す（SPEC 5.4 の決定）は v0.2（v0.1 は窓をそのまま）。

---

## 11. Aegis の移植（v0.1 段階 2、2026-09-22）

読んだもの: `HostRoles-v050\aegis\Aegis.ps1`（ブランチ v0.5.5、`eeab523`、1549 行）。`Aegis.ps1` は窓を出すので**一度も動かしていない**。自己テストの答えは、すべてコードから決めた（同じ入力で ps1 が出す答え）。

### 11.1 移したもの（判断と文言は同じ）

| ps1 | アプリ | 中身 |
|---|---|---|
| `S`（文言） | `src\Aegis\AegisText.cs` | 窓だけで使うキー（`sub` `tip.*` `m.*` `st.*`）以外の 92 キーと、アプリの新しい 2 キー（`c.oldtray` `c.notstarted`）。ps1 と機械で比べて、違いは 9.3 の中文 5 つだけ（`check-terms.ps1` は 0 件）。行の詳細は「キー＋値」で持ち、言語を変えるとすぐ描き直す。 |
| `Sig` | `src\Aegis\DefinitionsSignature.cs` | 正規化・`keyid=`・失効・長さ・`RSACryptoServiceProvider.VerifyData(SHA256)`。鍵の一覧（`TrustedKeys`）は 1 文字も変えずに写した。 |
| `Defs` | `src\Aegis\DefinitionsStore.cs` | 読み込み（同梱が先・新しい方・署名が通るものだけ・組み込みの一覧）、`ParseText`、`Store`（古い版に戻さない・同じ版は同じバイトだけ・`.sig` → 本体の順）、取得（8 秒・大きさの上限・同じ User-Agent・データが取れた時だけ `.sig`）。インスタンス（3.10）。 |
| `Scanner` と `Splash` の判定 | `src\Aegis\AegisScan.cs` | 13 項目・同じ順番・同じ「止める」項目。例外は黄で止めない。レジストリとプロセスの名前は `IAegisSystem`（本物は `Registry64` と `Process.GetProcesses`）。 |
| `Tray.Poll` `ReadLog` `Line` `Balloon` | `src\Aegis\LogWatcher.cs` | 1 秒ごと・`logPos = -2` の規則・4 MiB・デコーダーを続けて使う・3 つの正規表現・言い当て 3 種は何もしない・`[test]`・4 秒の間引き・12 秒の警告・30 回ごとのツール確認。 |
| `Toast` | `src\Aegis\AegisToast.cs` | 400×78・右下 18 px・10 px 間隔・最大 4 枚・フォーカスを取らない・250 ms で 0.96・警告 6 秒 / 情報 3.5 秒・30 ms ごとに 0.08 で消える・押すと消える。 |
| `EventsLog.Prune`・`AddEvent`・`Write-AegisLog` | `src\Aegis\EventsLog.cs` | 同じ行の形・同じ文字コード（新しいファイルだけ BOM）・同じ 30 日の規則。 |
| `Entry.Run`・`Entry.PreLaunch`・`Tray` | `src\Aegis\AegisService.cs` | Mutex → 整理 → 定義 → 取得（裏で 1 回）→ 開始時のスキャン → 見張り → `b.ready`（ゲームが無い時）。起動前スキャン: 新しく読み込んだ定義・`events.log` の 1 行・`prelaunch-result.txt`（BOM 付き）・赤があれば止める・失敗は `aegis.log` で止めない。 |
| （シェル側） | `IAegisService.cs`・`ClientApp.cs` | `AegisFactory.Create` が `AegisService` を返す（作れない時だけ `AegisStub`）。`ScanSummary.Error`（古いトレイが動いている時などの知らせ）を足した。 |
| 同梱の定義 | `aegis\definitions.txt(.sig)` | v0.5.5 のファイルをバイトのまま。exe の隣の `aegis\` に置く（csproj）。 |

プレイボタンの「止めた」状態・トレイの点とツールチップ・タスクバーのバッジ（赤＝直前のスキャンに赤、黄＝退出から 12 秒）・UI の Aegis パネル（13 行・記録・見出し）は、段階 1 のつなぎがそのまま使う。

### 11.2 ps1 と違うところ（わざと。判断は変えていない）

| # | ps1 | アプリ | 理由 |
|---|---|---|---|
| G-1 | 開始時のスキャンを最前面の小窓で見せる（毎回） | UI の Aegis パネル・レールの点・赤ならバッジ。小窓は出さない | 10 章の案（オーナー確認待ち） |
| G-2 | 項目ごとに 300〜460 ms 待つ（見た目のため）、UI のスレッド | 待たない、別のスレッド（A-12） | 見た目だけの待ち |
| G-3 | 盾のトレイアイコン・メニュー（状態 / もう一度スキャン / 終了）・ダブルクリックで状態の窓 | アイコン A に点・3.13 のメニュー・ダブルクリックで窓 | 3.13・SPEC 5.3 |
| G-4 | ランチャーを閉じてゲームも無ければ終わる（手動なら 15 秒後） | アプリが終わるまで動く | 1 つのプロセス |
| G-5 | 言語は起動時のまま | アプリの言語。変えるとすぐ全部に | 3.14 |
| G-6 | 起動前スキャンが 60 秒で終わらない時、別プロセスはそのまま続き、あとで `events.log` と `prelaunch-result.txt` を書く | 次の項目の前で止め、何も書かない（ゲームはもう起動している） | 止めない判断は同じ（L-1）。起動後に「止めた」と書くと紛らわしい |
| G-7 | 起動前スキャンは別プロセスなので、トレイのスキャンと同時に動くことがある | スキャンは 1 つずつ（起動前スキャンは前のスキャンの終わりを待つ。60 秒の中） | `mod-fingerprint.txt` の読み書きが重ならない |
| G-8 | 起動前スキャンの結果は小窓だけ | Aegis パネルの行・見出し・バッジにも出る | 7.2 |
| G-9 | 取得の前に `SecurityProtocol |= Tls12` | Windows に任せる（.NET 4.8 の既定 `SystemDefault` は TLS 1.2 以上）。既定でない時だけ Tls12 を足す | `SystemDefault` に足すと TLS 1.2 だけになるため |
| G-10 | `-ScanOnly` はトレイが動いていると何もせずに終わる | `aegis.scanOnly`・`aegis.rescan` は、アプリの Aegis が動いていれば実行、古いトレイが動いている時・開始前は落ち着いた知らせ（`c.oldtray` / `c.notstarted`） | アプリ自身がトレイなので |
| G-11 | 通知の小窓（四角・GDI+ の ClearType） | 角丸・影・ばねで出る・並び直しもばね・押した瞬間に消え始める・マウスを乗せると時間が止まり、消えかけも戻る | apple-design。大きさ・位置・枚数・時間・フォーカスは同じ |
| G-12 | 見張りのタイマーの中の例外は WinForms の「ハンドルされない例外」の窓 | `client.log` に書いて続ける | 窓を出さない |
| G-13 | 取得の結果はどこにも書かない | `client.log` に 1 行（保存した / しなかった / 失敗の種類） | 自分のログだけ |
| G-14 | 小窓の大きさは Windows が拡大（ぼやける） | 画面の拡大率に合わせて描く（PerMonitorV2） | 見た目だけ |
| G-15 | `-Lang` が空なら ja | アプリの言語（ランチャーはいつも `-Lang` を渡していたので同じ結果） | — |
| G-16 | `events.log` はトレイと起動前スキャンが鍵なしで追記（A-9） | アプリの中では 1 つの鍵で書く | 形は同じ |

### 11.3 ps1 の不具合でそのまま写したもの

9.2 の A-1〜A-8・A-10・A-11 と、今回見つけた A-13（地域の設定の時刻の区切り）・A-14（壊れた鍵の XML で例外）・A-15（`*.dll` が `.dll_off` にも当たる）。自己テストはこの 3 つも「今と同じ」として確かめる。

### 11.4 自己テスト（Aegis の分 260 件。全体は 482 件）

- 文言（全キー 3 言語・公式の中文・ルール名が無い時・`zh` の扱い）
- 署名（合う・`keyid=` 無し・違う鍵・別の信頼する鍵の id・知らない id・失効（id あり / なし）・Claude アプリの鍵・無し / 空白 → Missing・2 行・base64 でない・4096 文字超・1 バイト短い / 長い・中身 1 バイト違い・BOM と CRLF・壊れた鍵の XML）
- 本物の鍵（RSA 3072・AQAB・SubjectPublicKeyInfo の SHA-256 が `91400fdf…fbaf`・id がその先頭 16 桁・失効の一覧）と、同梱の定義の結果（INFO: 失効した鍵の署名なので通らない。L-11）
- 定義の形（最初の `version=` だけ・大文字小文字・500 件・64 KiB・短い名前・守るプロセス / DLL・`[rules]` などを飛ばす）
- 読み込み（無し → 1・同梱・キャッシュが新しい・同じ版は同梱・信頼しない鍵・失効・`.sig` 無し → 2・`.sig` 4096 バイト超・256 KiB 超）
- 保存（Load の前・古い・同じ版で違うバイト・同じバイト・署名なし / 違う / 失効・読めない・新しい → 正規化したバイトと `.sig` の形・tmp が残らない・使っている版は変わらない・すでにある時は書かない・組み込みの時）
- 取得（順番と上限・大きすぎ・`.sig` だけ大きすぎ・ネットの失敗・違う鍵・読み込みの上限）
- スキャン 13 項目の各判断（2.2 の表と 3.9 のとおり。例外は黄）と、全部通した時の数・中止
- 起動前スキャン（止める / 止めない・`prelaunch-result.txt` と `events.log` のバイト・進み具合・言語を変えた時・失敗しても止めない・期限・`GameLauncher` と通しで）
- ゲームの見張り（前回のログを読まない・書き直されたログ・読み込み / 退出 / Certain / Notice / `[test]` / 言い当て 3 種 / 知らないルール・12 秒・4 秒の間引き・行の途中・UTF-8 の文字の途中・短くなったログ・ゲームの終わりと次のゲーム・30 回ごとのツール）
- `events.log`（BOM・CRLF・30 日・時刻の無い行・ちょうど 30 日・何も消さない時は触らない）と `aegis.log`
- 通知の絵（窓を作らずに PNG に描いて、大きさ・角の透明・色の帯・紺の地を確かめる）

### 11.5 実機で確かめること（自己テストではできない）

- 古い Aegis トレイ（`Aegis.cmd`）が先に動いている時: アプリの Aegis パネルが「古い Aegis トレイ…」になり、古い方を終了すると 1 秒以内に見張りが始まる。逆に、アプリが先なら `Aegis.cmd` はすぐ終わる。
- 通知の小窓: ゲーム中（ボーダーレス全画面）に前に出る・フォーカスを取らない・押すと消える・4 枚まで・並び直しが滑らか・拡大率 100% / 150%。
- 定義の取得: 開始時に 1 回と、12 時間以上たってから窓を開くかプレイを押した時にもう 1 回・8 秒で打ち切り・保存は次の起動前スキャンから使われる（`client.log` の「Aegis definitions download: …」）。
- プレイで止めた時: プレイボタンが「確かめて起動」・Aegis パネルに赤い行と直し方・タスクバーの赤いバッジ。直して押すと起動する。

## 12. v0.1 のレビューで直したこと（2026-09-22）

| 見つけたこと | 直したこと |
|---|---|
| 定義の取得がプロセスごとに 1 回だけ（アプリは何日もトレイで動く） | 窓を開いた時・プレイの時に、前回の取得から 12 時間以上たっていれば同じ取得をもう一度（毎回新しい `DefinitionsStore` で。古い版に戻さない規則はそのまま）。`IAegisService.RefreshDefinitionsIfStale` |
| パスに使えない文字（`"` `<` `>` や縦線など）で起動の途中に落ちる | `GameFolders.Join`（落ちない Join-Path）でゲームのフォルダ・Src・`launcher-state.json`・Steam を組み立て、「見つからない」として続ける。`client.log` への書き込みと例外の記録を最初に用意。Aegis のスキャンは `Aegis.ps1` と同じ `Path.Combine` のまま（そのフォルダでは「Aegis failed」で、ゲームは止めない） |
| 終わる途中の 2 回目の起動が何も起こさない | 終わり始めたら（Aegis とトレイを片付けた直後、WebView2 の片付けの前に）単一起動の Mutex と合図を手放す（`SingleInstance.ReleaseEarly`） |
| 管理者として動いている Client があると、普通に開いた方が落ちる | Mutex と合図の作成で「アクセス拒否」などを捕まえ、「ほかの Client が動いている」として 0 で終わる |
| `--language` を付けると設定で言語を変えられない | 選んだら `--language` を外す（`ClientContext.ChooseLanguage`。ランチャーのコンボボックスと同じ） |
| 止められた時のボタン「もう一度確かめる」が起動もする | 文言を「確かめて起動 / 重新检查并启动 / Check and play」に |
| WebView2 の DLL のライセンス本文を配っていない | `licenses\Microsoft.Web.WebView2-LICENSE.txt`・`-NOTICE.txt` をパッケージから exe の隣へ。NOTICE から指す |
| 閉じてもトレイで動き続けることを知らせない（9.4 S-5） | 初めてトレイに入った時に 1 回だけ、アプリの名前とアイコン A の小窓で知らせる（`settings.json` の `trayHintShown`） |
| `--self-test` が `<dir>\work` を確かめずに消す | 自己テストが作った印（`.starpocket-selftest`）がある時だけ消す。無ければ何も消さずに FAIL |
| Release の exe にビルドした PC のパスが入る | `PathMap` で `C:\src\StarPocketClient` に置き換え |
| README の「ネットは定義の取得だけ」が WebView2 ランタイムの通信にふれていない | アプリ自身の通信と、WebView2 ランタイム（Microsoft の部品）の通信を分けて書いた |
| UI の元（プロトタイプ）と用語の確認がリポジトリに無い | `design\launcher-proto\index.html`（中文は公式の用語に直したもの）・`tools\terms\check-terms.ps1`（+ allow）を入れた。`import-ui.ps1` の既定はリポジトリの中のプロトタイプ。用語集 `glossary.tsv` はゲームの文なので入れない |
| トレイの「もう一度スキャン」が、窓を隠していると何も見えない | 「プレイ」と同じく窓を出し、ページのスキャン（パネル・行の動き・終わりの知らせ）を 1 回動かす |
| exe だけをコピーすると何も出ずに落ちる / 「ランタイムを入れて」と出る | exe の隣のファイル（WebView2 の DLL 3 つと `ui\index.html`）を最初に確かめ、足りなければ「zip の中身をすべて展開して」と出す |
| Aegis の「赤」がタスクバーのバッジにしか出ない | トレイの点も赤にし、ツールチップを「Aegis が問題を見つけました」に |
| WebView2 が無い時の窓が Mutex を持ったまま | 窓は Mutex を取る前に出す。「もう一度確かめる」で見つかれば、そのまま起動を続ける |
| 設定の一部が「保存されません」なのにスイッチがそのまま残る | アプリ側の設定は既定値に戻す（開いた時にも）。ページだけで効く配信モードには知らせを出さない（背景の動画は 2026-09-24 に取りやめ、音量は v1.2 からアプリが持つ） |
| 古い Aegis トレイが見張っている時、ツールチップが「停止中」 | 「古い Aegis トレイが見張っています」に |
| WebView2 の窓の文言（どれを押すか書いていない・見出しと本文が同じ） | 見出しを短く、本文に「Evergreen Bootstrapper」をダウンロードして入れ、「もう一度確かめる」と書いた |
| 終了の確認の en / zh-CN があいまい | en「Quitting also stops Aegis from watching the rest of this game. Quit?」、zh-CN「退出后，Aegis 将不再监视本局游戏。要退出吗？」 |

---

## 13. v0.2（インストール・同期・更新の確認）

`PocketRolesLauncher.ps1` の「入れる・直す・合わせる」をアプリの中に移しました。外のプログラムは一切呼びません（robocopy も wscript も使いません。`src\Core\ShellOpen.cs` が、アプリが何かを起動する唯一の場所です）。

| 移したもの | 今の場所 | 元（ps1） |
|---|---|---|
| インストール 5 手順（Steam 版を探す → ゲームのコピー → BepInEx → MOD 本体 → 仕上げ）。途中で失敗しても、その手順からやり直せます | `src\Core\Installer.cs` | 666-1036 |
| ファイルのコピー（robocopy の代わり。同じ判断で、同じものだけコピーします） | `src\Core\FileCopy.cs` | 同上 |
| ダウンロードと zip の展開（BepInEx・MOD の zip） | `src\Core\Downloads.cs`・`ZipFiles.cs` | 707-741 |
| 更新の確認（GitHub Releases） | `src\Core\ReleaseInfo.cs`・`Installer.CheckUpdate` | 743-772, 1039-1056 |
| Steam 版との同期（コピー + interop の削除） | `Installer.SyncGameCopy` | 1059-1070 |
| Steam のフォルダを選ぶ | `ClientApp.PickSteam` | 647-655 |
| `launcher-state.json` の読み書き | `src\Core\LauncherStateFile.cs` | 569-584 |

### 13.1 今までのランチャーからの引き継ぎ

アプリがランチャーのフォルダの中にある時（`PocketRolesLauncher.ps1` か `launcher-state.json` が隣にある時）と開発モードの時は、**同じ `launcher-state.json` を読み書き**します。そうでない時はアプリ自身の `%LOCALAPPDATA%\StarPocket\Client\launcher-state.json` を使い、**初めてそれが無い時に一度だけ**、今までのランチャーのものを丸ごと写します（デスクトップのショートカット `PocketRoles Launcher.lnk` の指す先から探します。`migratedFrom`・`migratedAt` を足すだけで、**元のファイルは変えも消しもしません**）。ショートカットは `IShellLink` で読みます（`wscript.exe` は使いません）。

### 13.2 長い処理の決まり

一度に 1 つだけ（`ClientApp.TryBeginTask`）。途中の進み具合はページの進行カード（`host:progress`）とタスクバーのボタンに出ます。処理が終わると状態を取り直します。

### 13.3 「修復」の状態（R）

- ゲームのコピーはあるが、BepInEx か MOD が足りない → プレイのボタンが「修復」になり、同じ手順を足りない所から行います（`LaunchStatus.Repair = "files"`）。
- Aegis が `PocketRoles.dll` の変更を見つけて起動を止めた（R-41）→ 「修復」で MOD を入れ直します（`Repair = "mod"`）。

どちらの時も、ページは引数なしの `install` を送り、**どちらの修復かはアプリが決めます**（`ClientApp.InstallMode`。決める場所を 1 つにするため）。Steam 版のゲームには触りません。

---

## 14. v0.3（報告 zip・ショートカット・アンインストール・CI）

### 14.1 報告 zip とひとり分の証拠

MOD 側のブランチ `evidence-90d`（コミット **`34506c3`**。2026-09-23 03:10 時点の先頭）の決まりと文言に合わせました（オーナーの決定 2026-09-23「A」「B」）。C# が合わせているのはこのコミットです（`FromLiveLog` の裏づけの見出し・`Get-EraseCutoffs`・`Get-EnforcedEvidenceIds`・秒まで書く zip の名前・`ex_warn` がそろっています。v0.3 の報告に書いた `18c8231` は 1 つ前のコミットで、書きまちがいでした）。

| 決まり | 今の場所 |
|---|---|
| ログの伏せ字（%USERPROFILE%・API キー・Discord の webhook・PUID・フレンドコード・記録を消すためのコード・短いハッシュ） | `src\Core\Masking.cs` |
| 証拠の記録の伏せ字（`player.hash` をフレンドコードのハッシュから **PUID のハッシュ**へ。形が違うファイルは入れない） | `Mask.ConvertToReportEvidence` |
| 検知の文・裏づけの 2 行・コードの読み取り（MOD と BAN 管理と同じ規則） | `src\Core\EvidenceText.cs` |
| 証拠の記録は **90 日**、報告 zip はその 90 日分（200 件・5 MB のまま） | `src\Core\EvidenceStore.cs` |
| ログを消す直前に、残る記録を裏づける 2 行を `evidence\<id>.log` に残す | `EvidenceStore.SaveBackingFrom`（`GameLogs.RemoveExpired` から） |
| 報告 zip の中身（今のログ・`launcher-state.json`・アプリのログ・過去のログ 3 件（1 件 8 MB まで）・証拠の記録・`system.txt`） | `src\Core\ReportBuilder.cs` |
| **入れないもの**: MOD の `.cfg`（Discord の webhook が入っています）・`report-mail.json`・鍵・パスワード（名前と場所の両方で外します） | `ReportBuilder.CopyMasked` |
| ひとり分の証拠 zip（コード・フレンドコード・証拠 ID から。フレンドコードもそのハッシュも入れません） | `ReportBuilder.ExportOne` |
| zip は 30 日で自動で消えます（報告 zip も、ひとり分の zip も。名前の日時で消します） | `GameLogs.RemoveExpired` |
| メールは `mailto:` を開くだけ。**アプリは何も送りません**。宛先は作者の 3 つだけ | `ShellOpen.MailTo`（`OpenKind.Mail`） |

**次の UI の取り込みで直すこと**（プロトタイプ `design\launcher-proto\index.html` 側）:

1. ~~ひとり分の証拠のボタンがまだありません~~ → **済**（2026-09-23）。設定 → PocketRoles →「困ったとき」の、報告 zip のすぐ下に行を足しました（`TOOLS` の `exportOne`。`ask` があるので入力欄つきで描かれます）。入れたものは**そのままアプリに渡します**。形が違う時・記録が無い時の言葉はアプリに 3 言語そろっているので（`Strings.cs` の `ex_bad`・`ex_none`）、ページ側には 1 文字も書きません。入れたものはどこにも保存しません（人の記録を消すためのコードだからです）。
2. ~~`LONG_CMDS` に `uninstall` と `exportOne` を足す~~ → **済**（`ui\index.html` とプロトタイプの両方。2026-09-23 のレビューで先に入れました）。あわせて `ui\host-v01.js` の `reportFlow` から「まだ処理が続いています」の逃げ道を外しました。30 分待って返事が無いのは**本当に失敗**で、「まだ続いています」は嘘になるからです（アプリが自分で閉じるアンインストールだけは、今までどおりそう言います）。

**この行について、2026-09-23 のレビューで直したこと。**

- **作っているあいだ、画面が黙っていました。** アプリはぜんぶ終わるまで返事をしないので、ログの多い PC では数分間なにも起きません。ボタンは押せたままで、もう一度押すと「ほかの処理の途中です」とだけ出ました。→ 押した時にボタンを押せなくして `aria-busy` を付け、アプリと同じ言葉（`rp_creating` / `ex_creating`）をトーストで出し、終わったら必ず戻します。
- **打ち間違いが 3.6 秒で消えていました。** `ex_bad` は日本語で 70 文字あり、読み返す所もありません（うまくいった時だけ消えないダイアログが出るので、成功と失敗であつかいが逆でした）。→ `r.error` があるものは、うまくいった時と同じダイアログで出します（「やめる」は隠し、閉じるだけ）。「ほかの処理の途中」「返事がありません」はトーストのままです（何も詳しく言わないので）。
- **30 分たったあとに届いた返事を捨てていました。** zip はデスクトップにできているのに、ホストには「応答がありません」しか残りませんでした。→ 時間切れでも `pending` から消さず、遅れて届いたら `host:late` として出し、うまくいった時と同じダイアログを見せます。
- **行の説明が、zip を作ったあとの説明と逆のことを言っていました。**「フレンドコードもそのハッシュも入りません」で終わっていたので「ハッシュは入らない」と読めるのに、直後の `ex_warn` は「PUID のハッシュが入っています」と言います。「1 人分の記録**だけ**」も本当ではなく、`system.txt` と `launcher-state.json` も入ります。→ `ex_msg` と同じ対（「人を見分けるのは PUID のハッシュと記録を消すためのコードだけで、フレンドコードもそのハッシュも入りません」）にし、入るものを 3 言語とも先に書きました。
- **配信モードでも、この欄だけ相手のフレンドコードがそのまま映っていました**（同じ画面のコミュニティ検索は切ってあります）。→ 配信モードのあいだは伏せ字（`-webkit-text-security:disc`）にし、モードを入れた瞬間に欄と `askValues` を空にします。欄は使えるままなので、配信中でも zip は作れます。

### 14.2 ショートカット（押した時だけ）

`src\Core\Shortcuts.cs`。`shortcut.create` の時だけ `IShellLink` で `.lnk` を書きます（シェルもスクリプトも使いません）。起動時・インストール後・更新後には**何も作りません**。消す時は、**その `.lnk` がこの exe を指している時だけ**消します（同じ名前の他人のショートカットは触りません）。

### 14.3 PC の起動時に開く

`AutoStart`（`Shortcuts.cs`）。`HKCU\...\Run` の値 1 つだけで、**設定のスイッチを入れた時だけ**書きます。切ると消します。既定はオフ。スイッチの表示はレジストリの今の状態から作ります（ページの記憶ではありません）ので、他所で消されていても正しく出ます。

### 14.4 アンインストール

`src\Core\Uninstaller.cs`。設定 →「アンインストール」から、そして「アプリと機能」の項目（`"<exe>" --uninstall`、無人は `--uninstall --quiet`）からも行えます。引数は `src\Core\CommandLine.cs` が読み、`src\Program.cs` が窓も WebView2 も開かずに実行します。

**いつ消すか**（2026-09-23 のレビューで変えたところ）。窓が開いている間は、アプリ自身が自分のデータフォルダを掴んでいます（WebView2 のプロファイルの `LOCK`・`client.log`）。そこを消そうとすると必ず途中で止まるので、**窓の中の「アンインストール」は聞くだけ**にして、答えを覚えてアプリを閉じ、**`Application.Run` が終わって WebView2 を手放した後に `Program.FinishUninstall` が消します**（WebView2 のプロセスは少し遅れて離すので、数回やり直します）。`--uninstall` の方は、何も開く前に実行します（ほかの Client が動いていたら断ります）。

- **順番**: まずアプリ自身のデータ（`%LOCALAPPDATA%\StarPocket\Client`）。**ここが消せなかったら、ほかには一切触りません**（レジストリもショートカットも MOD のコピーも）。「アプリと機能」の項目だけ消えて中身が残る、という中途半端な状態を作らないためです。もう一度押せば最初からやり直せます。
- 次に、アプリが作ったショートカット → （チェックを入れた時だけ）MOD 用のゲームのコピー → 「Windows の起動時に開く」の値 → 「アプリと機能」の項目。
- **チェックボックス**: MOD 用のゲームのコピー（約 1 GB。設定と記録が入っています）。**既定は残す**。ゲームが動いている間はアンインストールを始めません（インストール・同期・更新の確認と同じ規則）。
- 決して触らないもの: Steam と Steam 版の Among Us、今までのランチャーとトレイが使う **`%LOCALAPPDATA%\PocketRoles\Aegis`** とその親フォルダ自身、デスクトップの報告 zip、ほかのフォルダ。消す前に必ず `Uninstaller.Allowed` を通しますし、`Plan` も `Allowed` を通したものしか並べません（確認の画面が、あとで断られるものを約束することはありません）。
  - 守る範囲を `%LOCALAPPDATA%\PocketRoles` 全体から `\Aegis` に絞りました。デスクトップが OneDrive の中にある PC では MOD 用のゲームのコピーが `%LOCALAPPDATA%\PocketRoles\Among Us PocketRoles` に置かれるので、広すぎるとチェックボックスが絶対に効かなかったためです。
- 動いている exe 自身は消せないので、**アプリのフォルダは必ず残ります**。`%LOCALAPPDATA%\Programs` に入れた時は「閉じたあとに消してください」、自分で好きな所に展開した時は「ご自分で置いた場所なので、いらなければ消してください」と、別の言葉で名前を出して知らせます。
- フォルダを消す時は `Uninstaller.DeleteTree` を使います。`Directory.Delete(path, true)` は途中にジャンクション（フォルダのリンク）があると「アクセスが拒否されました」で止まって半分だけ消えますが、これはリンクそのものだけを外して（リンク先には触らずに）先へ進みます。パスの比較は 8.3 の短い名前も展開してから行います（`C:\PROGRA~2\...` と `C:\Program Files (x86)\...` を別物と見ないため）。

### 14.4b クライアントの色

クライアントのアクセント色は**使う人が選べます**（設定 → 全般 →「いろ」）。既定は StarPocket の金 `#F7C548` で、`settings.json` には `"default"` として残します（ブランドの色が変わってもついてこられるように）。選べるのは、ならんだ 7 色か、好きな `#RRGGBB` の 1 色です。画面が使う 11 個の値（`--acc-*`）は、その 1 色から `src\Core\AccentColor.cs` が計算します。赤は「うまくいかなかった」を表す色（`--danger*`）として取ってあるので、アクセントには使いません。

**（1）赤をやめたところ・残したところ。** 51 か所を 1 つずつ見て、3 つに分けました。

| | 何 | どうしたか |
|---|---|---|
| **アクセント** | プレイのボタン、主ボタン、タブの下線、更新の進み具合、「NEW」の札、動作環境の「推奨/快適」の帯、パッチノートの見出しとその「未公開・作業中」、アイコン選択の枠、ヒーロー下の赤い光 | StarPocket の色へ。**人が選べます**（下） |
| **意味の赤** | 失敗した手順、状態のランプ、Aegis の「止める」「見つかった」「直し方」、「入室制限」ON、消す時の危険なボタン、閉じるボタンのホバー、「配信中」 | **赤のまま**。赤は「何かがおかしい」を表す唯一の色で、MOD 側も止まった・失敗に赤を使っています。専用の `--danger*` に分けました |
| **PocketRoles の絵** | クルーメイトのアイコン、ロゴの変種、役職カード、アバター | **そのまま**（ブランドの決定: PocketRoles はポケットのクルーメイトの顔を持ち続ける） |

`--accent-text` が 1 つでアクセントとエラーの両方に使われていたので、そこは分けないと片方を直すともう片方が壊れました。`--crew-deep` はどこからも使われていなかったので消しました。

**（2）色の決め方（`src\Core\AccentColor.cs`）。** 人が選ぶのは**色 1 つ**だけで、画面が使う 11 個の値はそこから計算します。目分量はどこにもありません。

- `--acc-fill` 面 / `--acc-hi` 上端 / `--acc-deep` 下端 / `--acc-sub` プレイの中の小さな札 / `--acc-ink` 面の上の文字（紺か白）
- `--acc-l-line` `--acc-d-line` 2px の線・枠・点（明暗それぞれ）/ `--acc-l-text` `--acc-d-text` 面のない所の文字 / `--acc-*-soft` 薄い地

満たす決まり（自己テストが全色で測ります）: 面とその濃淡の上の文字 **4.5 以上**、面はヒーロー（`#0B0C22`）の上で **3 以上**、線は `#E0E2EA`（明るい側でいちばん暗い面）と `#303030`（暗い側でいちばん明るい面）の上で **3 以上**、文字は同じ 2 面の上で **4.5 以上**。

読みにくい色は**断りません。黙って受け取りもしません**。明るさだけを 1% ずつ、**必要なぶんだけ**動かして、動かした時は知らせます。金は白の上では文字にならないので、**面は金・その上は紺、線と文字は濃い金**という使い分けになります（設定画面がもともと採っていた作法と同じです）。

**赤だけは色合いも動かします（2026-09-23 のレビュー）。** 画面の「うまくいかなかった」の色（`--danger` `#B02230`・`--danger-text` `#A8202E` / `#FF7A85`・`--danger-dot` `#E63946`）はどれも色相 352〜357 度にあるので、**345〜20 度はアクセントに使えません**。そこに入る色も断らず、帯の外へ 10 度ぶん出した所（335 度か 30 度の近い方）へ移して `ac_red` で知らせます。直す前は、赤を選ぶと「パッチノートの未読の点」（`--accent-line`）と「止まった・失敗したランプ」（`--danger-dot`）が**まったく同じ `#E63946`** になり、ホストにはどちらの意味か分かりませんでした。ならんだ 7 色はどれもこの帯の外です（いちばん近い「もも」で 340 度）。

**プレイのボタンは、マウスを乗せても色が変わりません（同レビュー）。** 11 個の値は**乗せる前の色**で測って決めるので、乗せた時に塗りを明るくすると、誰も測っていない色の上に文字が乗ることになります（`filter:brightness(1.06) saturate(1.05)` は、ならんだ色のうち「こん」と「むらさき」で文字の比を 4.72 → 4.27、4.61 → 4.15 に落としていました）。今は影を深くして、ヒーローの上に同じ色の淡い輪を出すだけです。`tools\uitest` が、ホバーの規則を読んで**その filter をかけた色でもう一度測り直します**。

**（3）どこに残るか・ちらつかないか。** `settings.json` の `accent`（`"default"` か `"#RRGGBB"`。既定は `"default"` で、**何も書きません**——あとでブランドの色が変わってもついてこられるように）。`MainForm.InitializeWebViewAsync` が `AddScriptToExecuteOnDocumentCreatedAsync` で、**ページのどのスクリプトより前・最初の描画より前に** 11 個を `<html>` に置きます。`NavigationCompleted` で送っていたら「元の色 →（数フレーム後）選んだ色」が必ず見えていました。変えた時は `settings.set` の返事に入っている 11 個をそのまま当てるので、読み込み直しはありません。

色を変えた時は、**最初の描画のスクリプトも入れ替えます**（`MainForm.SetBootScriptAsync`）。WebView2 は描画プロセスが落ちた時に自分でページを読み込み直すので、入れ替えないと「起動した時の色」に戻ったまま、設定だけが選んだ色を指し続けました。あわせて `shell` イベントに 11 個を載せ、`host-v01.js` が**毎回**当て直します。どちらか片方が効かなくても、画面は `settings.json` のとおりになります。

**（4）画面。** 設定 → 全般 →「いろ」。ならんだ 7 色（**金が既定**で、名前にもそう書いてあります）＋「すきな色をえらぶ」＋「元にもどす」。8 つは**ラジオの決まり通り**に動きます（Tab の止まり場は選ばれている 1 つだけ、左右上下キーで移ってえらぶ）。「すきな色をえらぶ」はふつうのボタンで、ブラウザの色の欄は**その外に隠して**置いてあります——欄をボタンの中に重ねていた時は、ボタン自身にフォーカスが行かず（枠が見えない）、ラジオの中にフォーカスできる部品がある形にもなっていました。

色を動かした時の文は**番号ではなく色で**言います（「えらんだ色より少し明るくしました」＋小さな色の丸 2 つ。16 進の番号は丸の名前と title に残ります）。色の欄はドラッグのあいだ**色だけが追いかけ**、手が止まって 250 ミリ秒たってから **1 回だけ**アプリに送ります（前は `input` のたびに設定画面を作り直していたので、色ダイアログが最初のひと動かしで閉じ、`settings.json` が 100 回以上書き替わりました）。

`@media (forced-colors: active)` で、Windows のハイコントラストではアクセントが system の色に席をゆずります。**色見本の丸だけ**が `forced-color-adjust:none` で本当の色を出し（色を選ぶ画面なので）、まわりのボタンは 8 つとも system が描きます。選ばれているものは `outline:3px solid Highlight` で示します——枠の太さ 1px の差しか残らず、40px の四角 8 つのどれが今の色か分からなくなっていました。Aegis のランプ（`.aegis-dot`）も本当の色を保ち、見えない文字（`#aegisDotText`）を添えたので、色だけに頼りません。

**（5）ずれない仕組み。** 同じ計算がページ側（`ACC`）にもあります（プロトタイプ単体でも動くように）。`src\SelfTest\AccentSelfTests.cs` と `tools\uitest\checks.js` が**同じ 14 色の表**で両方を測るので、どちらかが動いたらそちら側の検査が落ちて、どの値が変わったかを言います。`--focus` は**絶対にアクセントに繋ぎません**（選び方によってフォーカス枠が消えるため）。

### 14.5 まだ PowerShell 版にしかないもの

オーナーがいつ今までの 2 つを消せるかの目安です。

| まだランチャー（`PocketRolesLauncher.ps1`）だけ | 次の版 |
|---|---|
| ~~開発モードの「再ビルド」「更新（interop の作り直し）」（`rebuild`・`devUpdate`。ps1:1756-1819）~~ | **済（v0.4。16 章）** |
| ~~ログの窓（`showLog`）、ログのフォルダの大きさの表示と 2 GB の注意（ps1:1399-1434）~~ | **済（v0.4。16 章）** |
| ~~7 日より前のログを日ごとの zip にまとめる（ps1:1285-1380。まとめなくても 30 日で消えます）~~ | **済（v0.4。16 章）** |
| ~~画面なしの実行（`-Action`・`-AutoLaunch`・`-Windowed`・`-Tray`・`-ScanOnly` の引数）~~ | **済（v0.4。17 章）** |
| Aegis の BAN 管理（`aegis\AegisBan.ps1`）・鍵の道具・返信の下書き | ずっと先（別のアプリ） |

| まだ Aegis トレイ（`aegis\Aegis.ps1`）だけ | 次の版 |
|---|---|
| ~~`-Tray`・`-ScanOnly` でのコマンドラインからの起動と、手動起動の 15 秒の決まり~~ | **済（v0.4。17 章）** |

窓を隠している時のスキャンの小窓（v0.2 の予定だったもの）も **v0.4 で作りました（17.4）**。

**これで、ランチャーとトレイの 2 つの PowerShell は、BAN 管理を除いて全部アプリの中にあります。**

### 14.6 CI（署名の条件）

`.github\workflows\build.yml`。`windows-latest` で `dotnet build -c Release`、ソースの確認、自己テスト、exe の素性の確認、成果物の保存まで行います。**秘密の値は使いません。署名もまだしません。** SignPath の条件のうち「公開の CI でソースからビルドできること」がこれで満たせます。MOD の DLL は Among Us のファイルが要るので公開の CI では作れません（exe だけを先に署名してもらいます）。申し込みに出す説明は `docs\CODE-SIGNING.md` です。

確かめていること（2026-09-23 のレビューで足したもの）:

1. **ほかのプログラムを起動している所が `src\Core\ShellOpen.cs` 以外に無いこと**。`src` の `.cs` を全部読んで、`Process.Start(`・`ShellExecute…(`・`CreateProcess…(`・`WinExec(` が 1 つでもよそにあれば赤くします（v0.2 で `notepad.exe`・`explorer.exe` を名前で呼んでいた所が残っていたのと同じ漏れ方を止めるため）。`WebView2Gate` の「ページを開く」も `ShellOpen.WebPage` を通るようにしました。
2. **自己テストが本当に待たれること**。exe は窓のあるプログラムなので、PowerShell は出力をつながない限り終わるのを待ちません。以前はこの段が先へ進んでしまい、自己テストが落ちても赤くなりませんでした。`| Out-Host` を足し、報告ファイルが無ければその場で失敗にします。
3. **版とアイコン**。`FileVersion` が csproj の `FileVersion` と同じで、`app.manifest` の `assemblyIdentity version` もそれと同じであること、`ApplicationIcon` が `assets\starpocket.ico`（アイコン A）でファイルが実在すること。アイコンが本当に exe に入ったかは自己テスト（`src\SelfTest\IdentitySelfTests.cs`）が exe の `RT_GROUP_ICON` を読んで確かめます。

**2026-09-23（今日の作業のあと）の見直し。** 条件を 1 つずつ、書類ではなく**今のコード**に当てて確かめ直しました（アンインストール・断りなく変えない・送らない・公開 CI・マルウェアのふるまい・独自アイコン・ほかの人のライセンス）。コードの側はすべて満たしています。`docs\CODE-SIGNING.md` を今のアプリに合わせて書き直し（つなぐ先・置くもの・署名するのは exe だけ・Aegis が読むものの説明）、オーナーが行う手順は、リポジトリには置かずオーナーの手元の控えに分けました。リポジトリの決まりとして `CODE_OF_CONDUCT.md` と `SECURITY.md` を足しました。**残っているのはオーナーにしかできないこと**です: ① Client のリポジトリを GitHub に公開する（ビルドの決まりは `.github\workflows\build.yml` にあり、上げた時点から Actions が動きます。今は `git remote` が空です）② 公開してから 1 回リリースする ③ サイトのプライバシーとコード署名のページ ④ SignPath のアカウントと 2 要素認証。

---

## 15. v0.3 のレビューで直したこと（2026-09-23）

「シェルを使わない・署名の条件」「正しさ」「ファイルの安全」の 3 つの目で見直して出た 22 件です。

### 15.1 アンインストール（いちばん大きい直し）

| 見つかったこと | 直し方 |
|---|---|
| **「アプリと機能」から消せなかった。** `--uninstall` という引数を誰も読んでおらず、押すとアプリが開くだけ。すでに開いていると、1 つ目の窓を前に出して**成功のふりをして終了**していた（Windows からは消えたように見える） | `CommandLine` が `--uninstall` / `--quiet` を読み、`Program` が窓も WebView2 も開かずに実行。ほかの Client が動いていたら断る。`--uninstall`・`-uninstall`・`/uninstall`・`--quiet`・`--silent` の自己テスト |
| **最後まで終わらなかった。** 動いているアプリが、自分の WebView2 のプロファイルと `client.log` を開いたまま自分のデータフォルダを消そうとするので必ず途中で止まる。しかも止まったあとも「アプリと機能」の項目とショートカットだけは消えて、中途半端に残った | 窓の中では**聞くだけ**にして、`Application.Run` が終わって WebView2 を手放した後に `Program.FinishUninstall` が消す（数回やり直す）。データフォルダが消せなかったら**ほかには一切触らない** |
| **自分のログでデータフォルダが戻っていた。** 消した直後に `uninstall: removed …` を `DataDir\client.log` へ書くので、フォルダが作り直されていた | アンインストールのログは `%TEMP%\StarPocketClient-uninstall.log`（データフォルダの外） |
| **デスクトップが OneDrive の中にある PC では、MOD のコピーを消すチェックが絶対に効かなかった。** 守る範囲が `%LOCALAPPDATA%\PocketRoles` 全体で、その中にコピーが置かれるため。確認の画面は「消します」と出すのに、実行が断って「一部失敗」になっていた | 守る範囲を `\Aegis` とその親フォルダ自身に絞る。`Plan` も `Allowed` を通ったものしか並べない。OneDrive の場合の自己テストを追加 |
| ゲームが動いていてもアンインストールが始まった（マップされた `GameAssembly.dll` ごと消しにいく） | インストール・同期・更新の確認と同じ `GameRunning()` の確認を追加 |
| 自分で展開したフォルダが、何も言われずに残っていた | どちらの場合もフォルダ名を出して知らせる（言葉は別々。`un_selffolder` / `un_leftfolder`） |
| 途中にジャンクションがあると `Directory.Delete(path, true)` が半分で止まる。8.3 の短い名前で書かれたパスが別物に見える | `Uninstaller.DeleteTree`（リンクはリンクだけ外す）。`Inside` / `SameOrInside` は長い名前に直してから比べる |
| アンインストールだけ `%LOCALAPPDATA%` を別の所から取っていた | `ClientContext.LocalAppData` 1 か所から。`ClientContext.NewUninstaller` |

### 15.2 画面（`ui\host-v01.js`）

- **キャンセルしたアンインストールが、あとから実行されることがあった。** 確認の窓を「やめる」で閉じても「はい」を押した時に動く中身が残っていて、次に別の確認（「最近の記録を今すぐ消す」）で「消す」を押すと、その残っていた中身＝アンインストールが動いていた（MOD のコピーのチェックも残ったまま出ていた）。窓が閉じた時・別の窓に覆われた時・Escape・「やめる」・スクリム、どれでも中身を捨てるようにし、押された時にも「今出ているのは自分が出した窓か」を確かめる。
- **タスクマネージャーの説明が間違っていた。** 「これから」の欄に `robocopy.exe` と `dotnet.exe` が残っていた（v0.2 からコピーはアプリの中。ビルドはアプリにはできません）。3 言語とも「ほかのプログラムは動きません／コピーも展開もアプリの中で行います」に。同期の手順の「（robocopy）」も消しました。
- `LONG_CMDS` に `uninstall` と `exportOne`。

### 15.3 インストールとダウンロード

| 見つかったこと | 直し方 |
|---|---|
| **取得先が縛られていなかった。** 「このホストだけ」と書いてあるのにコードに確認が無く、BepInEx のページから拾ったリンクが `http://` でも別のホストでも、そのまま取りに行って中身をゲームに展開していた | `WebFetch.AllowedUrl` が https と 4 つのホスト名を強制（リダイレクト先も）。拾ったリンクもここを通らなければ使わない |
| **「修復」が BepInEx を直せなかった。** 展開が途中で止まると壊れたファイルが残るのに、次からは版の確認が「もう入っています」と言って飛ばすので、フォルダごと消す以外に直せなかった | 展開中だけ置く目印（`.starpocket-unpacking`）。残っていたら次回は版に関係なく入れ直す。「修復」も同じ |
| zip がファイルの上に直接書かれていた（途中で止まると半分だけ新しいファイルが残る） | `<file>.part` に書いてから置き換え（`FileCopy` と同じやり方） |
| 空き容量を確かめずに約 1 GB のコピーを始めていた | コピーの前に確認し、足りなければ必要量と空きを出して止める |
| コピーに失敗しても手順 3・4・5 に進み、`launcher-state.json` に「入っています」と書いていた（今までのランチャーもこのファイルを読みます） | コピーが失敗したらそこで中止。どの手順でも失敗があれば `lastCheck` しか書かない |
| `Setup` の大文字小文字の見方がランチャーと違った（`…-setup.zip` を MOD として入れてしまう余地） | `ReleaseInfo.LooksLikeSetup`（大文字小文字を無視）に統一 |
| 「フォルダを選ぶ」で MOD 用のコピー自身を選べた（そのあと更新が出なくなる）。そのための `CheckPickedSteam` が書かれているのに呼ばれていなかった | `ClientApp.PickSteam` が `CheckPickedSteam` / `RememberSteam` を通る |
| `%TEMP%` を今までのランチャーと共有しているのに、半分のファイルの名前が同じだった | `<名前>.<プロセス番号>.part`。失敗したら消す |

### 15.4 記録と報告 zip

- **フレンドコードの伏せ字の式がランチャーと 1 文字ちがっていた**（先読みの否定に全角 ＃ が入っていた）。全角 ＃ の直後だけ伏せ損ねて、報告 zip にフレンドコードが残る可能性があった。ランチャー（`ps1:1562`）と同じに戻し、テストを追加。
- **報告 zip に今のログが 2 回入っていた。** `Save-GameLog` が今のログの複製を作るので、それが「いちばん新しい過去のログ」として選ばれていた。前の試合のログも 3 件ではなく 2 件になっていた。ランチャーと同じく、今のログの複製と、時刻が読めない名前を外し、名前の降順で選ぶ。
- **`launcher-state.json` が一瞬読めなかっただけで、昔の内容に戻ることがあった。** 「ファイルが無い」と「今は読めない」を区別していなかったため、ウイルス対策や OneDrive の一瞬のロックで、今までのランチャーの古い内容に上書きされ得た。`Unreadable` を足し、その時は書き込みも引き継ぎもしない。読む大きさに上限、書き損ねた `.tmp` は消す、読み書きに名前付きロック（`Local\PocketRolesLauncher.state`）。

### 15.5 素性の確認（タスクマネージャーの件）

- `app.manifest` の版が 0.2.0.0 のままだった → 0.3.0.0 にし、**exe の中のマニフェストを読んで csproj の `FileVersion` と一致するか自己テストが確かめる**（`src\SelfTest\IdentitySelfTests.cs`）。
- `ShellOpen.cs` が「`IdentitySelfTests.cs` が確かめています」と書いていたのに、そのファイルが無かった → 本当に作り、exe の名前・会社・版・マニフェスト・アイコン（`RT_GROUP_ICON`）・**exe の中にシェルや道具の名前が 1 つも無いこと**を確かめる。CI はソースを読んで `Process.Start` などが `ShellOpen.cs` 以外に無いことを確かめる。
- CI の自己テストが待たれていなかった（落ちても赤くならなかった）→ `| Out-Host`。版とアイコンの確認も追加。`docs\CODE-SIGNING.md` を作成（CI のコメントが指していたのに無かった）。

### 15.6 BepInEx のファイルそのものを確かめる（オーナー 2026-09-23）

15.3 で取得先のホストは 4 つに縛りましたが、**ファイルの中身は確かめていませんでした**。
取得先が同じでも、置いてあるファイルが差し替えられたり、通信が途中で切れたりすれば、別の中身がゲームのフォルダーに展開され、ゲームの中でプログラムとして動きます。
オーナーの判断（2026-09-23）で、確認を入れました。

| 決めたこと | どうしたか |
|---|---|
| ファイルの SHA-256 を書き留めておく | `src\AppInfo.cs` の `BepZipSha256`（版 → 値の表）。取得先の住所のすぐ隣に置く。行のコメントに**ファイル名・バイト数・取ってきた場所・確かめた日**を書く |
| いつ確かめるか | ダウンロードの**直後**、**展開の前**（`Installer.CheckBepZip`）。`%TEMP%` に残っていたファイルも、使う前に同じように確かめる（このフォルダーは PowerShell 版ランチャーと共用） |
| 合わなかったら | ファイルを消す。展開しない。3 言語で理由を出す（`in_hash_bad`）。本来の値と届いた値も `client.log` に残す（`in_hash_detail`）。住所が 2 つあるので 2 つ目も試す |
| 確かめる順番 | **SHA-256 が先、zip を開くのは後**。途中で切れたファイルを「壊れた zip」として開くことすらしない |
| 値を書いていない版だったら | **断る**（`in_bep_nohash`）。ネットにもつながない。「確認できないが、とりあえず入れる」という道はわざと作っていない |
| 値の書き忘れを防ぐ | 自己テストが `AppInfo.BepInExVersion` の値が表にあるか、64 文字の小文字で書かれているか、取得先の住所とキャッシュのファイル名がその版を指しているかを毎回確かめる。版だけ上げると**ビルドが赤くなる** |

- 今の値: BepInEx `6.0.0-be.735`（`BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.735+5fef357.zip`、31,305,993 バイト、SHA-256 `9cd83eae4d47ab07e4ad7f4d98a0085f60fb4b61957857ff197c8729cf1bc483`、2026-09-23 に `builds.bepinex.dev` から取得して確認）。
- **配布元は zip の SHA-256 を公開していません**（ページに出ている短い英数字はソースの印であって zip の値ではない）。突き合わせる相手がいないので、この値は「その日に公式の配布元が配っていたファイル」を書き留めたものです。念のため、2 つの別々の道具で同じ値になること、zip 自身が持つ CRC がすべて合うことを確かめました。
- **新しいビルドに乗りかえる時の手順は `docs\BEPINEX-PIN.md`。** 値は推測できないので、**誰かが 1 回だけダウンロードして読む**必要があります。自動の作業に頼む時は、その回ごとに「1 回だけダウンロードしてよい」と伝えてください。
- 自己テスト: 「BepInEx: the file itself」42 件（正しいファイル・差し替えられたファイル・途中で切れたファイル・値が無い版・キャッシュに誰かが置いたファイル）。全体は 1150 件 → **1192 件**。画面のテストは 1515 件のまま。

---

## 16. v0.4（その 1）：ランチャーにしか無かった 3 つ

`PocketRolesLauncher.ps1` の最後の 3 つをアプリに移しました。**これで「ログの窓」「再ビルド」「更新」のために PowerShell 版を開く用事は無くなります。**

| 移したもの | 今の場所 | 元（ps1） |
|---|---|---|
| 進行ログの画面（`showLog`）。別の窓ではなく、**アプリの中のページ** | `ClientApp.ShowLog` + `ClientLog.Tail` + `ui\host-v01.js` の `#sp-log` | 窓の下のログ欄 |
| ログのフォルダの大きさ・2 GB の注意（`Get-LogArchiveInfo`・`Format-Size`・`Update-LogSizeLabel`） | `GameLogs.ArchiveInfo` / `GameLogs.FormatSize` / `ClientApp.SendLogSize` | 1399-1434 |
| 7 日より前のログを日ごとの zip へ（`Compress-OldGameLogs`・`Add-LogsToDayZip`） | `GameLogs.CompressOldLogs` / `AddLogsToDayZip` | 1285-1380 |
| ログをよけておく（`Move-GameLogAside`。更新の [3/4] で使います） | `GameLogs.MoveGameLogAside` | 1265-1283 |
| 開発モードの「再ビルド」（`Invoke-Build`） | `DevBuild.Rebuild` | 1756-1778 |
| 開発モードの「更新」（`Invoke-Update`） | `DevBuild.DevUpdate` | 1780-1819 |

### 16.1 ログの画面（`showLog`）

- `%LOCALAPPDATA%\StarPocket\Client\client.log` の**新しい方から** 400 行・末尾 256 KB までを読み、アプリの中のページに出します。新しいものが下です。
- **処理中でも開けます**（`Bridge.BusyGated` に入れていません）。ランチャーのログ欄も処理中に読めましたし、長い処理を眺めるためのページだからです。開いている間は 2 秒ごとに読み直します。
- **Windows のアカウント名は出しません**（`Mask.Home`）。このページは配信中に映ります。
- 省いた行があるときは、そう書きます（`lg_log_more`）。ログが無いときは「まだ記録はありません」。

### 16.2 ログのフォルダの大きさ

- `Refresh-Status` の最後で数えるのと同じく、状態を取り直すたびに裏で数えて、設定 → PocketRoles の「ログのフォルダを開く」の横に `ログ: 123 MB` を出します。説明はアプリの言葉（`lg_tip`）。
- 2 GB を超えたら**赤**（`--danger-text`。アクセントではありません。14.4b の決まり）にして、説明を `lg_big` に変え、**開いた時に 1 回だけ**ログにも書きます（ps1 の `Update-LogSizeLabel -Announce` と同じ）。
- 数え方も見せ方も ps1 と同じです。`Format-Size` は 0 → `0 MB`、1 バイト → `1 KB`、1 MB → `1.0 MB`、10 MB 以上は小数なし、1 GB 以上は `1.0 GB`。

### 16.3 日ごとの zip

- アプリを開いた時、`30 日の削除 → ログの保存 → 日ごとの zip` の順で 1 回だけ（ps1:2135 と同じ順番）。
- **日数はどこも変えていません。** zip は場所を節約するだけで、その日から 30 日で zip ごと消えます（`ArchiveExpired`）。
- ログのロックは**待ちません**（`Enter-LogLock 0`）。ほかのランチャーの窓が使っていたら、次の起動に回します。
- 1 回の起動で 256 MB まで。512 MB より大きいログは zip にしません（ただし 1 個目はいつも入れます。そうしないとフォルダが永久に減らないため）。
- 既にある zip は**絶対に開き直しません**。新しい zip を自分のプロセス番号の `.tmp` に作り、読み直して中身の長さを確かめ、`logs-yyyyMMdd.zip` → `-2` → `-3` と**空いている名前に付け替えて**から、元のログを消します。途中で失敗した時は `.tmp` だけを消すので、**ログが片方だけ消えることはありません**。
- 読めないログ（誰かが開いている）は、そのログだけ残して残りは zip に入ります。死んだプロセスが残した `logs-<日>.<番号>.tmp` は次の起動で片づけます。

### 16.4 開発モードの「再ビルド」「更新」

**これがアプリの中で唯一、ゲーム以外のプログラムを起動する所です。** コンパイルだけはアプリ自身にはできません。それでも**シェルは使いません**。詳しくは `src\Core\ShellOpen.cs` の頭に書きましたが、要点は:

- 起動してよいのは `dotnet.exe` **だけ**（絶対パス・その名前ちょうど。`.cmd` や `.bat` の同名は断ります）。
- 引数は `ShellOpen.cs` の中に **`build -c Release` と書いてあるだけ**です。呼ぶ側もページもファイルも、1 文字も足せません。
- `UseShellExecute` は **false**（cmd.exe を通しません）、窓も出しません、出力はパイプで読みます。
- `DOTNET_ROOT` と `PATH` は**子プロセスにだけ**渡します。ps1 は自分自身に設定していたので、そのあと起動したゲームにも付いていっていました（3.5 で禁じていることです）。
- 友達モードでは、そもそもこのボタンがありません（`ClientApp.DoDev` も断ります）。CI の「`ShellOpen.cs` 以外でプログラムを起動していないこと」はそのまま通ります。

「更新」の [3/4] はゲームを 1 回起動して BepInEx に interop を作らせます（最長 420 秒、その後 5 秒待って閉じます）。その前に前回のログを保存し、保存できなければ**消さずによけます**（`Move-GameLogAside`）。ゲームが自分で終わっても、interop があれば成功です。

### 16.5 文言

`lg_size`・`lg_tip`・`lg_big`・`lg_zipped`・`sdk_missing` は ps1 と同じ文です。ps1 が日本語だけを直接書いていた開発モードの文（`dev_*`）と、新しい画面の文（`lg_log_*`・`dev_only`）は、日本語・中文・English の 3 つを書きました。

### 16.6 テスト

- 自己テスト: 「logs folder size」「day zips」「log moved aside」「log page」「the compiler entry」「rebuild」「developer update」「developer words」を追加。**1192 件 → 1386 件。**
- 画面のテスト（画面なしの Edge）: ログのページ・大きさの行・2 GB の赤・設定を開き直しても消えないこと。**1515 件 → 1713 件。**
- 動かしていないもの: 本物のコンパイラ、本物のゲーム、本物の Steam。すべて差し替え可能な口（`RunBuild`・`StartInterop`・`Sleep`・`Now`）越しに試しています。

---

## 17. v0.4（その 2）：窓を出さない実行と、トレイの動き

今までのランチャーとトレイにあって、アプリに無かった最後の部分です。**これでオーナーは PowerShell の 2 つを消せます**（BAN 管理だけは別のアプリとして残ります）。

### 17.1 コマンドラインの指定

`src\Core\CommandLine.cs`（読み取り）と `src\Core\Startup.cs`（何をするかを決める）。決める所はファイルも窓も触らないので、自己テストが全部の書き方を試せます。

| 書き方 | すること | 終了コード |
|---|---|---|
| （なし） | 今までどおり画面を開きます | 0 |
| `--tray` | 画面を出さず、通知領域だけで動きます（`Aegis.ps1 -Tray`） | 0 |
| `--autolaunch` | 画面を出さずに、すぐゲームを起動します（`-AutoLaunch`） | 0 |
| `--windowed` | ゲームを 1600x900 の窓で起動します（`-Windowed`）。この回の起動は全部この形 | 0 |
| `--scan-only` | Aegis のスキャンを 1 回して終わります（`-ScanOnly`） | 0 / 3 |
| `--verify-download [--quiet]` | 配布元から BepInEx の zip を 1 つ受け取り、書き留めてある SHA-256 と中身を確かめ、作業用フォルダーに展開して全部消します。**ゲームのフォルダーには触れません**（`src\Core\BepVerifier.cs`・`docs\BEPINEX-PIN.md` 手順 9）。結果は日本語 1 文の小窓（`--quiet` で出しません） | 0 / 1 / **2** |
| `--action install\|check\|report\|status` | 画面なしで 4 つの仕事（`-Action`） | 0 / 1 |
| `--uninstall [--quiet]` | 今までどおり（v0.3） | 0 / 1 / 1602 |
| `--self-test <フォルダ>` | 今までどおり | 0 / 1 |

- 書き方は `--tray` / `-Tray` / `/tray`、大文字小文字は問いません。値は `--action install` でも `--action=install` でも。
- **分からない言葉は、必ず止めます。** `--trey` と書いたら、何が分からなかったかを出して 1 で終わります。**黙って画面を開くことはしません**（`--uninstall` がそうなっていて、Windows には「消せました」と伝わっていた不具合と同じ形です。15.1）。
  - 知らない指定・指定でない言葉・値の無い指定（`--action` のあと何も無い）・2 つのモードを一緒に書いた時（`--tray --scan-only`）・ゲームを起動しない所での `--windowed` も、全部これです。
  - 出す先は、呼んだ人の黒い画面。黒い画面が無い時（ショートカットの書き間違いなど）だけ、小さな窓で知らせます。
- `--autolaunch` は **窓を出しません**。起動したらすぐ通知領域に入る決まり（SPEC 5.4）なので、出してすぐ隠すより、最初から出さない方が素直です。

### 17.2 すでに動いている時（1 つだけ起動の落とし穴）

2 つ目を起動した時、**指定ごとに違うことをします**。「1 つ目に伝えて 0 で終わる」は、伝えたことが本当に仕事になる時だけです。

| 指定 | 動いている時 | 終了コード |
|---|---|---|
| （なし） | 1 つ目の窓を前に出します | 0 |
| `--tray` | **窓は出しません。**「もう通知領域にいます」と言うだけ | 0 |
| `--autolaunch` | 1 つ目に「プレイ」を頼みます（新しい名前付きイベント `…Client.Play` / `…Client.PlayWindowed`）。**届いた時だけ** 0、届かなければ理由を出して 1 | 0 / 1 |
| `--action` | **1 つだけ起動の対象外**（SPEC 5.1）。自分で仕事をします。`install` と `check` は名前付き Mutex `…Client.Task` を取り、取れなければ「作業中です」で 1 | 0 / 1 |
| `--scan-only` | 同じく対象外。読むだけなので、そのままスキャンします | 0 / 3 |
| `--verify-download` | 同じく対象外。**`…Client.Task` も取りません。**ゲームのフォルダーを触らないので、Client が開いていても、インストール中でも、ゲームが起動中でも、そのまま自分で確かめます | 0 / 1 / 2 |
| `--uninstall` | 断ります（v0.3 のまま） | 1 |

- 開いているアプリは、長い処理（インストール・同期・更新の確認・報告 zip・アンインストール）の間ずっと `…Client.Task` を持ちます。だから `--action install` は「作業中です」になります。
- `--verify-download` を「1 つだけ起動」の外に置いたのは、15.1 と同じ穴をふさぐためです。動いている Client に仕事を渡す経路が無ければ、**何も確かめずに 0 を返す経路も存在しません。**公開の前に 1 回だけ走らせるコマンドで、これが起きては意味がありません。自己テスト「--verify-download」A-5 がここを固定しています。
- Mutex はスレッドのものなので、自己テストは**別のスレッド**で 2 つ目を試しています（同じスレッドだと自分に許可を出してしまい、何も確かめられません）。

### 17.3 ゲームの前後の窓（SPEC 5.4）と、トレイの 15 秒

- ゲームが起動できたら、**出ていた窓だけ**が通知領域に入ります。ゲームが終わったら、`SW_SHOWNOACTIVATE` で戻します（前にあるものを押しのけません）。
- もともと出ていなかった窓（`--tray`・`--autolaunch`・自分で閉じた後）は、**そのまま出しません**。
- トレイのメニューの「プレイ」も、窓が出ていない時は窓を出さずにそのまま起動します（出してすぐ隠すのを避けるため）。
- **15 秒の決まり**（`Aegis.ps1` の手動起動）: `--tray` で始めたアプリは、1 回でもゲームを見張った後、ゲームが終わって 15 秒たつと自分で終わります。**窓を一度でも開いたら、この決まりは止まります**（そこからはその人のアプリです）。

### 17.4 窓を隠している時のスキャンの小窓

`src\Shell\ScanPopup.cs`。v0.2 の予定でしたが、今まで画面のパネルにしかありませんでした。

- 画面右下に、Aegis の通知と同じ形の小さなカード。13 項目が進むところ、止める項目の理由と直し方まで出します。
- 出るのは**窓が出ていない時だけ**（通知領域にいる時、`--tray`・`--autolaunch`・`--scan-only`）。窓を開いたら消えて、パネルに戻ります。
- フォーカスは取りません（ゲームの前でも邪魔しません）。押すと消えます。
- 消えるまでの時間は `Aegis.ps1` の窓と同じ: 起動を止めた時 9 秒、起動してよい時 0.9 秒、注意がある時 4.2 秒、何も無い時 1.8 秒。
- 出ている間は、通知の小窓がその上に積まれます。
- **絵は窓を作らずに描けます**（`ScanPopup.Draw`）。自己テストはその絵の色と大きさを見ています。テストでは窓は 1 つも開きません。

### 17.4b 画面を出さない起動では WebView2 を後回しにします

`--tray`・`--autolaunch` では、**誰かが窓を求めるまで WebView2 を作りません**（`ClientApp.StartWebView`）。見えないページのために `msedgewebview2.exe` がいくつも立ち上がるのは、PC の起動時に入る `--tray` では大きすぎる負担だからです。その間にページへ伝えたいことは溜めておき（`SendEvent` の待ち行列）、ページの用意ができた時にまとめて送ります。トレイの「プレイ」「もう一度スキャン」も、ページを通さずに動きます。

### 17.5 テスト

- 自己テスト: 「arguments」「arguments not understood」「a Client is already running」「one Client」「--action」「--scan-only」「scan card」「window and tray」を追加。**1386 件 → 1607 件。**
- 画面のテスト（画面なしの Edge）: **1713 件のまま**（`ui` は変えていません）。用語の確認: **0 件**。
- 実際の exe でも確かめたもの（窓は出ません）: `--action status`、`--trey`、`--action`（値なし）、`--action frobnicate`、`--tray --scan-only`、`--windowed --action status`、指定でない言葉、`--uninstall --trey`。場所は全部作り物のフォルダで、`LOCALAPPDATA` と `TEMP` も別の所に向けています。
- 動かしていないもの: 本物の `--action install` / `check`（ネットとコピーが要るため）、`--scan-only` の本物の窓、`--tray` と `--autolaunch` の本物の起動。窓と通信の部分は差し替え可能な口（`IScanView`・`TakeLock`・`Install`/`Check`/`Report`・`SingleInstance.Names`）越しに試しています。

### 17.6 これから確かめること（実機）

1. `--tray` で始めて、ゲームを 1 回遊んで、15 秒後に本当に終わるか。
2. プレイの後に窓が通知領域へ入り、ゲーム終了で戻るか（前にあるウィンドウを押しのけないか）。
3. `--scan-only` の小窓の見え方（文字の大きさ・止める項目の色）。
4. ショートカットに `--autolaunch` を付けた時の、2 回目の起動（すでに動いている時にプレイが伝わるか）。

---

## 18. v0.4 のレビューで直したこと（2026-09-23）

3 人のレビューが出した指摘を、1 つずつ本物のファイルと突き合わせて直しました。**直したものには必ずテストを足しています。**

### 18.1 アプリの側（v0.4 そのもの）

| 見つかったこと | 直し方 |
|---|---|
| **アンインストールの途中に「黙って成功」が戻っていました。** アンインストールは窓のアプリと同じ鍵（mutex）を取るので、その数秒だけ「1 つ目」になります。そのとき Show / Play / PlayWindowed の**合図の口まで作っていた**のに、受け取る人は誰もいませんでした。別の起動が `--autolaunch` で合図を送ると「届いた」と判定され、終了コード 0 で終わるのに、ゲームは 1 度も始まりません。v0.4 で潰したはずの形（15.1）が別の顔で残っていました | `SingleInstance.Acquire(listen:)` を足しました。**答えるつもりがある時だけ**口を作ります（窓のアプリは `true`、アンインストールは `false`）。答えない側には合図が届かないので、`--autolaunch` は「伝えられませんでした」＋終了コード 1 になります。自己テスト追加 |
| **`--action` を 2 回書くと、黙って後ろの方が走りました。** 違うモードを 2 つ並べた時は必ず止めるのに、同じ指定に違う値を 2 つ書いた時だけ、後の値で上書きされていました | 値付きの指定が**違う値で** 2 回来たら `CommandLine.Twice` に記録し、`Startup` がほかの書き間違いと同じ「使い方」エラー（メッセージ＋終了コード 1）にします。**同じ値を 2 回**書くのは間違いではないので、今までどおり動きます。自己テスト追加 |
| **版が 3 か所とも 0.3.0 のままでした。** exe の版・ログの 1 行目・`--action` の見出し・報告 zip が、v0.4 の作業中ずっと 0.3.0 と名乗っていました | `csproj`・`app.manifest`・`AppInfo.Version`・`AppInfo.UiVersion` を **0.4.0** に。自己テストは「だいたい合っている」ではなく**ぴったり同じ**かを見るようにしました |
| **コンパイラを待つ時間に上限がありませんでした。** 止まったままの `dotnet build` を永遠に待ち、窓は「再ビルド」のまま動かせなくなります | **20 分**で打ち切り、コンパイラを閉じて、その理由を ja / zh-CN / en で出します（`dev_build_timeout`）。自己テスト追加 |
| **ログの画面が 2 秒ごとにログのフォルダを数え直していました。** しかも窓の本体（UI スレッド）の上で。同じ値は裏のスレッドがすでに出しています | `RefreshLogSize` が出した最後の値を覚えておき、`ShowLog` はそれを返します。まだ 1 度も数えていない時だけ、裏で数えるよう頼みます。画面のテスト追加 |

### 18.2 署名の条件にかかわるところ

| 見つかったこと | 直し方 |
|---|---|
| **「開発モードは作者の PC だけ」が事実ではありませんでした。** 配布する Release の exe にも `--source-dir` が残っていて、しかも exe の 4 階層上まで `PocketRoles.csproj` を探していました。その名前のファイルが入ったフォルダを指すだけで、誰の PC でも開発モードになり、画面に「再ビルド」が出ます。`dotnet build` は MSBuild を通すので、そのプロジェクトファイルに書いてあれば別のプログラムも起動します。「シェルは一切起動しません」がコードを 1 行も変えずに破れました | **2 つとも閉じました。** `--source-dir` は開発者用のビルド（DEBUG）にしかありません（Release では「分からない指定があります」＋終了コード 1。黙って無視はしません）。上のフォルダを探すのもやめ、ソースになれるのは **exe と同じフォルダだけ**です。そこにファイルを置ける人は exe そのものを差し替えられる人です。`CODE-SIGNING.md` 8. に「`dotnet build` は別のプログラムを起動しうる」と正直に書きました。自己テスト追加 |
| **CI の「シェルを起動していない」検査に穴が 2 つありました。** `Process.Start(` という書き方しか見ておらず、`ShellOpen.cs` 自身が使う `new Process { … }` ＋ `p.Start()` はどのファイルに書かれても素通り。しかも `src` フォルダしか読まないのに、この csproj は**リポジトリ直下の `.cs` も一緒にビルド**します | 正規表現に `new Process`・`CreateProcess…`・`EntryPoint="ShellExecute…` を足し、読む範囲を `bin` と `obj` を除く**全部**に広げました。`.Start()` だけの書き方は**足していません**（スレッドやタイマーの起動が 13 か所あり、鳴りっぱなしの警報は切られるからです）。実際に漏らす書き方のファイルを置いて、前の検査は素通り・新しい検査は 3 件とも見つけることを確かめました |
| **リダイレクトの確認が半分だけでした。** ファイルを落とす `Download` は行き先を確かめますが、文章を取る `GetText` は確かめていません。`GetText` が読むのは `builds.bepinex.dev` の一覧ページ（こちらが管理していないサーバー）です | 両方が同じ `WebFetch.CheckArrivedFrom` を通るようにしました。自己テスト追加 |
| **MOD 用コピーの場所が、中身を確かめずに消されていました。** 場所は `--game-dir` と環境変数 `POCKETROLES_GAMEDIR`（PowerShell 版と同じ名前）で変えられるので、古い設定が残っている PC では別のフォルダが丸ごと消される筋道がありました | `Uninstaller.Allowed` に「そのフォルダに `Among Us.exe` があること」を足しました。無ければ消さず、「残した物」として名前を出します。自己テスト追加 |
| **「アプリと機能」の項目を書くセットアップがありませんでした。** 書く関数はありますが、呼んでいるのは自己テストだけです。つまり配る zip を展開した人の PC には、その項目は永久にできません | 作るのは後回しにして、**文章のほうを事実に合わせました**。`CODE-SIGNING.md` の 4.・6.・英語のまとめと `README.md` から「アプリと機能」を外し、9.（まだできていないこと）に「セットアップが無い」を足しました |
| 申し込み書の自己テストの件数（1192）がもう合っていませんでした | 件数を文章から外し、「CI の Self-test の段が出す `RESULT PASS n/n` を見てください」に変えました |
| `@v4` の action を「固定（pinned）」と書いていましたが、これは動く目印です | 「大きい版だけ指定しています」に書き換え、9. に「SHA で固定していない」を足しました |
| 6. の表に `%TEMP%\pocketroles-build.log` がありませんでした | 1 行足しました |
| **オーナー用の申し込み手順（リポジトリの外の控え）の 1.→2. がそのままでは動きませんでした。** 既定のブランチ `main` は v0.1 のままで、CI も署名の説明も `client-native` にしかありません。書いてあるとおりに公開すると、Actions は 1 度も動かず、2. の「緑になるまで待つ」がいつまでも来ません | 手順 **0b** を足しました（コミットする → `main` に合流させるか既定のブランチを変える → 公開前に 4 つのファイルがあることを目で確かめる → `client-vanilla` をどうするか決める） |

### 18.3 テスト

- 自己テスト: **1618 件 → 1701 件**（警告 0・エラー 0）。
- 画面のテスト（画面なしの Edge）: **1713 件 → 1725 件**。
- 用語の確認: **0 件**。
- CI のソース検査: 緑。わざと漏らすファイルを置いて、前の検査との差も確かめました。

---

## 19. v1.3 アイコンの画像

フレンド欄のプロフィールの絵（組み込みの 6 つ）に、7 個目として「自分の画像を選ぶ」を足しました。`src\Core\ProfileImage.cs`（窓を出さない純粋ロジック）と `src\Shell\ClientApp.cs`（選ぶ窓と返事）、ページ側は `design\launcher-proto\index.html` → `ui\index.html` の `profile` と `ui\host-v01.js` の `shell` イベント。

- **上限は 3 つ**: ファイル 20 MB（全バイトを読むので、それ以上は読まずに断る）、画素 3000 万（寸法だけ先に読み、大きすぎる絵はデコードしない。GDI+ は 1 画素 4 バイトで持つので 120 MB が上限）、写し 2 MB（256×256 の PNG は大きくても 260 KB ほど。それ以上の `avatar.png` は誰かが置き換えたものなので読まない）。画面の文（`acct.customNote`）と `README.md` も同じ数を言います。
- **置き場所**: `%LOCALAPPDATA%\StarPocket\Client\profile\avatar.png`（`ClientContext.AvatarPath`。`settings.json` と同じフォルダの下）。元の画像は触らず、写しだけを置きます。書く時は `settings.json` と同じ `.tmp` → `File.Replace` なので、途中で落ちても半分のファイルは残りません。アンインストールはこのフォルダごと消します。名前と組み込みの絵の番号は `settings.json` の `profileName` / `profileAvatar`（`profile.set`）。
- **読める形式**: GDI+ が読む PNG / JPG / BMP / GIF と `.ico`（256 に一番近い絵）。JPG の EXIF の向きは直します。**WebP はこの版では読みません**: WIC（PresentationCore）を参照すると `csproj`（署名の条件が書いてあるファイル）に手を入れることになるので、`RIFF…WEBP` の署名を見て `av_webp`（「PNG か JPG にしてから選んでください」）で断ります。ファイルの種類の一覧（`av_filter`）にも WebP は出しません。
- **壊れた時の動き**: 読めない画像を選んだ時は元の絵のまま（既にあった写しも変えない）で、理由を日本語で toast に（`av_unreadable` / `av_toobig` / `av_toomany` / `av_webp` / `av_nofile` / `av_write`）。起動時に写しが読めない（2 MB 超・PNG の署名違い・GDI+ で開けない）時は写しを消して元の絵に戻り、`shell` イベントの `avatarError`（`av_broken`）で 1 回だけ言います（壊れたファイルを毎回の起動で読み続けない）。
- **外へ送る道が無い**: ページには `data:` の URL で渡すだけで、ページはネットに出ません（`MainForm` が `app.starpocket.local` 以外を全部 403 にする）。選んだ元の画像のパスはアカウント名が入るので、ログにも返事にも書きません（ログは「avatar: refused (too big (21.0 MB))」のように理由だけ）。
- **`data:` の URL**: ページの CSP は `img-src` に `data:` を許しているので、`<image href="data:image/png;base64,…">`（`#av-custom`）で描けます。ページのファイルには `data:image/png;base64` の綴りを書きません（`tools\import-ui.ps1` が「埋め込み PNG が残っている」と止まるため）。接頭辞を丸ごと確かめるのは `host-v01.js` と C#（`ProfileImage.DataUrlPrefix`）だけです。
- 自己テスト: `ProfileSelfTests`（取り込み・起動時の読み込み・消す・Bridge の表・`settings.json`・3 言語の言葉）。件数は `RESULT PASS n/n` を見てください。
