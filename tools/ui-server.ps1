# tools\ui-server.ps1 - ui\ フォルダをそのまま出すだけの、見るため専用の小さなサーバー。
#
# 本物の Client は WebView2 の仮想ホスト（https://app.starpocket.local/）から ui\ を読みます。
# その形を手元のブラウザで再現するためだけの物です。アプリはこれを使いません。
# 外には出しません（127.0.0.1 だけ）。
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\ui-server.ps1 [-Port 8731]
# SPDX-License-Identifier: GPL-3.0-or-later
param([int]$Port = 8731, [string]$Root = '')

$ErrorActionPreference = 'Stop'
$root = if ($Root) { $Root } else { Join-Path (Split-Path -Parent $PSScriptRoot) 'ui' }
if (-not (Test-Path -LiteralPath $root)) { throw ('ui フォルダがありません: ' + $root) }

$types = @{
    '.html' = 'text/html; charset=utf-8'
    '.js'   = 'text/javascript; charset=utf-8'
    '.css'  = 'text/css; charset=utf-8'
    '.md'   = 'text/plain; charset=utf-8'
    '.json' = 'application/json; charset=utf-8'
    '.png'  = 'image/png'
    '.jpg'  = 'image/jpeg'
    '.svg'  = 'image/svg+xml'
    '.webm' = 'video/webm'
    '.woff2'= 'font/woff2'
}

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
Write-Host ("ui を出しています: http://127.0.0.1:$Port/index.html   （元: $root）")
Write-Host '止める時は、この窓を閉じるか Ctrl+C です。'

try {
    while ($listener.IsListening) {
        $ctx = $listener.GetContext()
        $rel = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath).TrimStart('/')
        if ($rel -eq '') { $rel = 'index.html' }
        $full = Join-Path $root ($rel -replace '/', '\')
        # ui\ の外へ出ない（..\ を書かれた時の守り）
        $ok = $false
        try {
            $fullResolved = [IO.Path]::GetFullPath($full)
            $rootResolved = [IO.Path]::GetFullPath($root)
            $ok = $fullResolved.StartsWith($rootResolved, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $fullResolved -PathType Leaf)
        } catch { $ok = $false }

        if ($ok) {
            $bytes = [IO.File]::ReadAllBytes($fullResolved)
            $ext = [IO.Path]::GetExtension($fullResolved).ToLowerInvariant()
            $ctx.Response.ContentType = $(if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' })
            $ctx.Response.StatusCode = 200
            $ctx.Response.ContentLength64 = $bytes.Length
            $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        } else {
            $b = [Text.Encoding]::UTF8.GetBytes('not found: ' + $rel)
            $ctx.Response.StatusCode = 404
            $ctx.Response.ContentType = 'text/plain; charset=utf-8'
            $ctx.Response.ContentLength64 = $b.Length
            $ctx.Response.OutputStream.Write($b, 0, $b.Length)
        }
        $ctx.Response.OutputStream.Close()
    }
} finally {
    try { $listener.Stop(); $listener.Close() } catch { }
}
