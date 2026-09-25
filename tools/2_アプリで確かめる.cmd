@echo off
chcp 932 > nul
rem StarPocket Client の実機確認。配布元からファイルを 1 つ受け取って確かめ、すぐ消します。
rem ゲームのフォルダーには何も書きません。パスワードも聞きません。
rem
rem このファイルは CP932 で保存しています。chcp 65001 は exe の出力のために要りますが、
rem 下の日本語の echo より後に置いてください（先に置くと、CP932 のその行が UTF-8 として
rem 読まれて化けます）。exe が終わったら chcp 932 に戻します。戻さないと、そのあとの
rem 日本語の行が化けます。
rem
rem cmd.exe は窓のプログラムでも終わるまで待つので、黒い画面は exe が終わるまで開いたままです。
rem 最後の pause は、結果の番号を読む時間を作るため（と、exe が起動できなかったときに
rem 黒い画面が一瞬で消えないようにするため）です。
echo.
echo  ・配布元から 1 回だけダウンロードします（約 31 MB）
echo  ・ゲームのフォルダーには何も書きません。パスワードも聞きません
echo  ・終わったらダウンロードしたファイルは消します
echo.
echo 配布元のファイルを確かめています。30 秒ほどお待ちください...
echo この黒い画面は開いたままです。閉じないでください。
echo.
chcp 65001 > nul
set "EXE=%~dp0app\StarPocket Client.exe"
if not exist "%EXE%" set "EXE=%~dp0StarPocket Client.exe"
"%EXE%" --verify-download
set "RC=%errorlevel%"
chcp 932 > nul
echo.
echo 終わりました。結果の番号は %RC% です。
echo   0 ＝ 確かめられました
echo   1 ＝ 確かめられませんでした（もう一度どうぞ）
echo   2 ＝ 配らないでください
echo.
pause
exit /b %RC%
