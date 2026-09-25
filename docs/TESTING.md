# 配る前の試し方

リリースした zip を、**初めての人とまったく同じ順で**触って確かめるための手順です。
自分の PC には、もうこのアプリが入っています。そのまま開いても初めての人が見る画面は出てこないので、
**その 1 回の起動だけ、アプリに「何も無い状態」を見せます。**

すべて、**いま入っている物には一切触れません**。写しを取る必要も、あとで戻す必要もありません。

---

## 1. なぜこれで「初めての人」になるのか

このアプリは、自分の記録をどこに置くかを **`LOCALAPPDATA` 環境変数**で決めます
（`src\Core\ClientContext.cs` の `LocalAppDataDir()`。この変数を先に読み、空のときだけ Windows に聞きます）。

| 置くもの | 場所 |
|---|---|
| 設定 | `%LOCALAPPDATA%\StarPocket\Client\settings.json` |
| 最初の同意の答え | 同じフォルダの `consent.json` |
| 記録 | 同じフォルダの `client.log`・`launcher-state.json` |
| Aegis の記録 | `%LOCALAPPDATA%\PocketRoles\Aegis`（PowerShell 版と**共有**） |

ですので `LOCALAPPDATA` に空のフォルダを渡せば、アプリから見て**何も無い**＝初めて開いた人と同じです。

ゲームのフォルダは別に決まるので、`--game-dir` で使い捨ての場所を指します
（環境変数 `POCKETROLES_GAMEDIR` でも同じことができます）。

---

## 2. 用意

```powershell
# 配った zip を、まっさらなフォルダに展開する
$test = "$env:TEMP\spc-test"
Remove-Item $test -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$test\app", "$test\state", "$test\game" | Out-Null
Expand-Archive "StarPocketClient-unsigned.zip" -DestinationPath "$test\app"
```

**落とした物が本物か、先に確かめます。**

```powershell
(Get-FileHash "StarPocketClient-unsigned.zip" -Algorithm SHA256).Hash
```

リリースのページに書いてある値と、**一字一句同じ**であること。違ったら、そこで止めます。

---

## 3. まず、窓を出さずに自己テスト

```powershell
$env:LOCALAPPDATA = "$test\state"
& "$test\app\StarPocket Client.exe" --self-test "$test\report" | Out-Host
$code = $LASTEXITCODE
Get-Content "$test\report\self-test.txt" | Select-String "^RESULT|^FAIL"
```

- 窓もトレイも出ません。通信もしません。ゲームのフォルダも読みません
- `RESULT PASS 2361/2361`（数は版によって増えます）と、終了コード **0** であること
- **`| Out-Host` を省かないでください。** このアプリは Windows サブシステムのプログラムなので、
  出力をつながないと PowerShell が終わりを待たず、失敗を見落とします

---

## 4. 初めての人として開く

```powershell
$env:LOCALAPPDATA = "$test\state"
& "$test\app\StarPocket Client.exe" --game-dir "$test\game"
```

見るところ:

| | 見るもの |
|---|---|
| ① | **最初の同意の画面**が出るか（利用規約・プライバシーポリシー・遊び方のルール） |
| ② | その画面を日本語・简体中文・English の 3 つで見て、文が切れていないか |
| ③ | 画面の中のリンクが、全部生きているか |
| ④ | **「同意しない」で終わったとき、何も通信していないか** |
| ⑤ | 同意した後、`--game-dir` に渡した場所にゲームの**別のコピー**ができるか |
| ⑥ | Steam のフォルダが**触られていない**か（更新日時を見る） |
| ⑦ | BepInEx と MOD が入り、SHA-256 の照合が通るか |
| ⑧ | Aegis の点検が走るか |
| ⑨ | エラーコードの「詳しく見る」で <https://starpocketgames.com/codes/> が開くか |
| ⑩ | 設定 → アンインストールで、きれいに消えるか |

④ は、Windows のリソースモニターか、`netstat` で見ます。
**同意する前に外へ出る通信があってはいけません**（利用規約 第12条3項）。

---

## 5. 触っていないことを、あとで確かめる

```powershell
# 本物の設定が触られていないこと（更新日時が、始める前のままであること）
Get-ChildItem "$env:USERPROFILE\AppData\Local\StarPocket\Client" -File |
  Select-Object Name, LastWriteTime
```

`$env:LOCALAPPDATA` を書き換えたのは**その PowerShell の窓の中だけ**です。窓を閉じれば元に戻ります。
別の窓や、ふだんアプリを開くときには影響しません。

---

## 6. 片づけ

```powershell
Remove-Item "$env:TEMP\spc-test" -Recurse -Force
```

これだけです。レジストリにも、ほかの場所にも何も残りません。

---

## 7. 参加者側（スマホ・エミュレータ）

部屋を立てる人だけが MOD を入れます。**入ってくる人は普通の Among Us のまま**なので、
参加する端末には何も入れません。

見るのは、ホスト側からは分からないことだけです。

1. 部屋に入れるか
2. 1 ゲーム、最後まで回せるか
3. **役職の名前が、へんな四角（□□□）や英語になっていないか**
4. **チャットに日本語を打って、ちゃんと出るか**
5. 抜けて、入り直せるか

3 と 4 は、**本物の端末でしか出ない不具合**があります。エミュレータだけで済ませないでください。

### エミュレータでやる場合

```powershell
# MuMu Player 12 の例
$adb = "C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe"
& $adb connect 127.0.0.1:16416
& $adb -s 127.0.0.1:16416 shell "dumpsys package com.innersloth.spacemafia | grep versionName"
```

`versionName` が、MOD の対象にしている版と同じであること。違う版では試す意味がありません。

---

## 8. やってはいけないこと

- **公開の部屋で試さない。** 知らない人が入ってきます。非公開の部屋を使ってください
- **いま使っているゲームのフォルダを `--game-dir` に渡さない。** 使い捨ての場所を渡してください
- `$env:LOCALAPPDATA` を**システムの環境変数として**書き換えない。その窓の中だけにしてください
