# tools\uitest\run.ps1 - the UI test: ui\index.html + ui\host-v01.js, checked in a real browser engine.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\uitest\run.ps1 [-Group all|base] [-Shots <folder>]
#
# NO WINDOW EVER OPENS and nothing is ever played: headless Edge (--headless=new --mute-audio), a throwaway profile
# inside a temp folder, and the network switched off at the resolver (only 127.0.0.1 is allowed through, which is the
# little file server this script starts for the ui folder - the page itself asks for nothing from the internet).
#
# The page is set up exactly as the app sets it up:
#   - the eleven --acc-* values are put on <html> when the document is created, before any of the page's own script
#     runs (Page.addScriptToEvaluateOnNewDocument here; AddScriptToExecuteOnDocumentCreatedAsync in MainForm.cs), so
#     this test also shows that the colour cannot flash;
#   - window.chrome.webview is stubbed, so ui\host-v01.js runs the way it does inside the app.
# Then tools\uitest\checks.js is loaded and asked for its list.
#
# Every language (ja / zh / en) is run in both themes. Prints "PASS name" / "FAIL name: why" and
# "RESULT PASS n/n"; the exit code is 0 only when everything passed.
# -Shots writes a PNG of the game page and of the settings page (ja, both themes, plus the colour picker).
param(
    [ValidateSet('all', 'base')][string]$Group = 'all',
    [string]$Shots = '',
    [string]$ShotPrefix = '',
    # another ui folder to look at instead of this repository's (used to picture an older build beside this one)
    [string]$Ui = '',
    [int]$Port = 0
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }
$utf8 = New-Object System.Text.UTF8Encoding $false
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ui = if ($Ui) { (Resolve-Path $Ui).Path } else { Join-Path $repo 'ui' }
if (-not (Test-Path (Join-Path $ui 'index.html'))) { throw "no index.html in $ui - run tools\import-ui.ps1 first" }
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (-not (Test-Path $edge)) { $edge = 'C:\Program Files\Microsoft\Edge\Application\msedge.exe' }
if (-not (Test-Path $edge)) { throw 'Microsoft Edge was not found' }
$work = Join-Path ([IO.Path]::GetTempPath()) ('sp-uitest-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
[void][IO.Directory]::CreateDirectory($work)
if ($Shots) { [void][IO.Directory]::CreateDirectory($Shots) }

# ---------------------------------------------------------------- a file server for the ui folder, loopback only
# (the page asks for nothing from the internet; this only exists because a page opened from file:// has no origin of
#  its own, and index.html carries a Content-Security-Policy of default-src 'self')
if ($Port -eq 0) {
    $probe = New-Object Net.Sockets.TcpListener ([Net.IPAddress]::Loopback), 0
    $probe.Start(); $Port = ([Net.IPEndPoint]$probe.Server.LocalEndPoint).Port; $probe.Stop()
}
$serve = {
    param($listenerPort, $root)
    $types = @{ '.html' = 'text/html; charset=utf-8'; '.js' = 'text/javascript; charset=utf-8'; '.png' = 'image/png'; '.css' = 'text/css; charset=utf-8' }
    $l = New-Object Net.Sockets.TcpListener ([Net.IPAddress]::Loopback), $listenerPort
    $l.Start()
    while ($true) {
        $c = $l.AcceptTcpClient()
        try {
            $s = $c.GetStream()
            $buf = New-Object byte[] 4096
            $n = $s.Read($buf, 0, $buf.Length)
            $req = [Text.Encoding]::ASCII.GetString($buf, 0, $n)
            $path = '/'
            if ($req -match '^GET\s+(\S+)') { $path = $matches[1] }
            $path = ($path -split '\?')[0]
            if ($path -eq '/') { $path = '/index.html' }
            # only files inside the ui folder, no walking out of it
            $full = [IO.Path]::GetFullPath((Join-Path $root ($path.TrimStart('/') -replace '/', '\')))
            if (-not $full.StartsWith([IO.Path]::GetFullPath($root)) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) {
                $head = [Text.Encoding]::ASCII.GetBytes("HTTP/1.1 404 Not Found`r`nContent-Length: 0`r`nConnection: close`r`n`r`n")
                $s.Write($head, 0, $head.Length)
            } else {
                $body = [IO.File]::ReadAllBytes($full)
                $ct = $types[[IO.Path]::GetExtension($full).ToLower()]
                if (-not $ct) { $ct = 'application/octet-stream' }
                $head = [Text.Encoding]::ASCII.GetBytes("HTTP/1.1 200 OK`r`nContent-Type: $ct`r`nContent-Length: $($body.Length)`r`nCache-Control: no-store`r`nConnection: close`r`n`r`n")
                $s.Write($head, 0, $head.Length); $s.Write($body, 0, $body.Length)
            }
            $s.Flush()
        } catch { } finally { $c.Close() }
    }
}
$serverJob = Start-Job -ScriptBlock $serve -ArgumentList $Port, $ui
Start-Sleep -Milliseconds 900

$pass = 0; $fail = 0
function Line([string]$s) { Write-Output $s }

$profDir = Join-Path $work 'profile'
$edgeArgs = @(
    '--headless=new', '--mute-audio', '--hide-scrollbars', '--no-first-run', '--no-default-browser-check',
    '--disable-extensions', '--disable-background-networking', '--disable-sync', '--no-pings',
    '--disable-features=Translate,MediaRouter',
    # every name but the loopback file server fails to resolve: this test can reach nothing at all (quoted, the value
    # has spaces in it and Start-Process would otherwise split it and Edge would refuse to start)
    '"--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1"',
    "--user-data-dir=$profDir", "--remote-debugging-port=0", '--window-size=1280,860', 'about:blank')
$proc = Start-Process -FilePath $edge -PassThru -WindowStyle Hidden -ArgumentList $edgeArgs
$devPortFile = Join-Path $profDir 'DevToolsActivePort'
$devPort = 0
for ($i = 0; $i -lt 100 -and $devPort -eq 0; $i++) {
    Start-Sleep -Milliseconds 200
    if (Test-Path $devPortFile) { try { $devPort = [int]((Get-Content $devPortFile)[0]) } catch { } }
}
if ($devPort -eq 0) { throw 'headless Edge did not start' }

try {
    $list = $null
    for ($i = 0; $i -lt 50 -and -not $list; $i++) { Start-Sleep -Milliseconds 200; try { $list = Invoke-RestMethod "http://127.0.0.1:$devPort/json/list" } catch { } }
    $page = @($list | Where-Object { $_.type -eq 'page' })[0]
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $ct = [Threading.CancellationToken]::None
    $ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, $ct).Wait()
    $script:nextId = 0
    $buf = New-Object byte[] 4194304
    function Recv {
        $ms = New-Object IO.MemoryStream
        do { $seg = New-Object ArraySegment[byte] -ArgumentList @(, $buf); $r = $ws.ReceiveAsync($seg, $ct).Result; $ms.Write($buf, 0, $r.Count) } while (-not $r.EndOfMessage)
        return [Text.Encoding]::UTF8.GetString($ms.ToArray())
    }
    function Cdp([string]$method, [string]$params = '{}') {
        $script:nextId++; $id = $script:nextId
        $b = [Text.Encoding]::UTF8.GetBytes('{"id":' + $id + ',"method":"' + $method + '","params":' + $params + '}')
        $ws.SendAsync((New-Object ArraySegment[byte] -ArgumentList @(, $b)), 'Text', $true, $ct).Wait()
        while ($true) { $m = Recv; if ($m -match ('^\{"id":' + $id + '[,}]')) { return $m } }
    }
    function Eval([string]$expr) {
        $p = '{"expression":' + (ConvertTo-Json $expr -Compress) + ',"returnByValue":true,"awaitPromise":true}'
        return Cdp 'Runtime.evaluate' $p
    }
    # the value the expression gave back, unwrapped from the protocol's own envelope
    function EvalValue([string]$expr) {
        $o = (Eval $expr) | ConvertFrom-Json
        if ($o.result -and $o.result.exceptionDetails) { throw ('the page threw: ' + $o.result.exceptionDetails.text + ' ' + $o.result.exceptionDetails.exception.description) }
        if ($o.result -and $o.result.result) { return $o.result.result.value }
        return $null
    }
    Cdp 'Page.enable' | Out-Null
    Cdp 'Runtime.enable' | Out-Null

    $checksJs = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'checks.js'), $utf8)
    $hostJs = [IO.File]::ReadAllText((Join-Path $ui 'host-v01.js'), $utf8)

    # what the app puts on the document before it has drawn anything (MainForm.AddScriptToExecuteOnDocumentCreatedAsync)
    # plus the message channel the app gives the page
    $boot = @'
window.__errors = []; window.__sent = []; window.__listeners = [];
/* what the app answers, per command, when a check needs something other than a plain "ok":
   window.__reply.exportOne = { answer:{ ok:false, error:'...' }, delayMs:200 }, or 'never' for an app gone quiet. */
window.__reply = {};
window.addEventListener('error', e => window.__errors.push(String(e.message)));
window.chrome = window.chrome || {};
window.chrome.webview = {
  postMessage(m){
    window.__sent.push(JSON.parse(JSON.stringify(m)));
    if (m.type !== 'invoke') return;
    /* the app answers every invoke exactly once. settings.set for the colour answers the way ClientApp.SetAccent
       does: the eleven values, and the flags that say what was done to the colour. */
    let res = { ok: true };
    if (m.cmd === 'settings.set' && m.args && m.args.key === 'accent') {
      try {
        const A = window.spReview.acc, v = m.args.value, d = A.derive(v);
        res = { ok: true, data: { accent: v, vars: A.vars(v), adjusted: d.adjusted, hueMoved: d.hueMoved,
          lightMoved: d.lightMoved, lighter: d.lighter, chosen: d.chosen, fill: d.fill } };
      } catch (e) { res = { ok: true }; }
    }
    const scripted = window.__reply[m.cmd];
    if (scripted === 'never') return;
    let wait = 0;
    if (scripted && typeof scripted === 'object') {
      if (scripted.answer) res = JSON.parse(JSON.stringify(scripted.answer));
      wait = scripted.delayMs || 0;
    }
    setTimeout(() => window.__emit(Object.assign({ type: 'result', id: m.id }, res)), wait);
  },
  addEventListener(t, f){ if (t === 'message') window.__listeners.push(f); }
};
window.__emit = m => window.__listeners.forEach(f => f({ data: m }));
'@
    Cdp 'Page.addScriptToEvaluateOnNewDocument' ('{"source":' + (ConvertTo-Json $boot -Compress) + '}') | Out-Null

    $langs = @('ja', 'zh', 'en')
    # light, dark, and Windows high contrast with "reduce motion" turned on
    $modes = @(
        @{ n = 'light'; scheme = 'light'; motion = 'no-preference'; forced = 'none' },
        @{ n = 'dark';  scheme = 'dark';  motion = 'no-preference'; forced = 'none' },
        @{ n = 'hc';    scheme = 'light'; motion = 'reduce';        forced = 'active' })
    foreach ($lang in $langs) {
        foreach ($mode in $modes) {
            $scheme = $mode.n
            Cdp 'Emulation.setDeviceMetricsOverride' '{"width":1280,"height":860,"deviceScaleFactor":1,"mobile":false}' | Out-Null
            Cdp 'Emulation.setEmulatedMedia' ('{"features":[{"name":"prefers-color-scheme","value":"' + $mode.scheme + '"},{"name":"prefers-reduced-motion","value":"' + $mode.motion + '"},{"name":"forced-colors","value":"' + $mode.forced + '"}]}') | Out-Null
            Cdp 'Page.navigate' ('{"url":"http://127.0.0.1:' + $Port + '/index.html"}') | Out-Null
            for ($k = 0; $k -lt 80; $k++) {
                Start-Sleep -Milliseconds 150
                if ((Eval 'document.readyState') -match '"value":"complete"') { break }
            }
            Start-Sleep -Milliseconds 900
            # the app's own start: the shell event (with the colour), then the language
            Eval ("window.__emit({type:'event',name:'shell',data:{version:'0.3',app:'0.3.0',dragRegion:true,close:'tray',startGame:'pocketroles',autostart:false,accent:'default',mode:'friend'}});") | Out-Null
            Eval ("window.__emit({type:'event',name:'lang',data:{pref:'" + $lang + "',lang:'" + $lang + "'}});") | Out-Null
            Eval ("window.spReview.skipSplash(); window.spReview.sample('latest');") | Out-Null
            Start-Sleep -Milliseconds 500
            Eval ('window.__hostSource = ' + (ConvertTo-Json $hostJs -Compress) + ';') | Out-Null
            Eval $checksJs | Out-Null
            $json = EvalValue ('window.__spChecks(' + (ConvertTo-Json $Group -Compress) + ', ' + (ConvertTo-Json $mode.n -Compress) + ').then(r => JSON.stringify(r))')
            if (-not $json) { Line ("FAIL [" + $lang + "/" + $scheme + "] the checks did not run"); $fail++; continue }
            $rows = $json | ConvertFrom-Json
            foreach ($row in $rows) {
                if ($row.ok) { $pass++; Line ("PASS [" + $lang + "/" + $scheme + "] " + $row.n) }
                else { $fail++; Line ("FAIL [" + $lang + "/" + $scheme + "] " + $row.n + ": " + $row.why) }
            }

            # pictures of the game page and of Settings → 全般, in Japanese, light and dark. -Ui lets an older build be
            # pictured the same way for comparison; anything that build does not have is simply skipped.
            if ($Shots -and $lang -eq 'ja' -and $mode.forced -eq 'none') {
                function Shot([string]$name) {
                    # a toast left over from a check would be in the picture, so it is taken off first
                    try { Eval 'document.getElementById("toast").style.opacity = 0;' | Out-Null } catch { }
                    Start-Sleep -Milliseconds 150
                    $r = Cdp 'Page.captureScreenshot' '{"format":"png"}'
                    if ($r -match '"data":"([^"]+)"') {
                        $p = Join-Path $Shots ($ShotPrefix + $name + '.png')
                        [IO.File]::WriteAllBytes($p, [Convert]::FromBase64String($matches[1])); Line ("INFO shot " + $p)
                    }
                }
                function Try_([string]$js) { try { Eval $js | Out-Null } catch { } }
                Try_ 'if (window.spReview.accent) window.spReview.accent("default"); window.spHost.closeAll(); window.spHost.show("game");'
                Start-Sleep -Milliseconds 800
                Shot ("game-" + $scheme)
                # パッチノートのタブ（2026-09-24: hero が「プレイの帯」にたたまれ、その下からパッチノートが始まる）
                Try_ 'document.getElementById("tab-pn").click();'
                Start-Sleep -Milliseconds 800
                Shot ("game-notes-" + $scheme)
                Try_ 'document.getElementById("tab-ov").click();'
                Try_ 'window.spHost.show("home");'
                Start-Sleep -Milliseconds 800
                Shot ("home-" + $scheme)
                Try_ 'if (window.spReview.setPage) window.spReview.setPage("general"); else window.spHost.openLayer("m-settings");'
                Start-Sleep -Milliseconds 800
                Shot ("settings-" + $scheme)
                # Settings -> PocketRoles, scrolled to the help rows (the report zip and one player's evidence)
                Try_ 'if (window.spReview.setPage) window.spReview.setPage("pocketroles"); document.getElementById("setBody").scrollTop = (document.getElementById("tools-help") || {offsetTop:0}).offsetTop - 24;'
                Start-Sleep -Milliseconds 800
                Shot ("settings-tools-" + $scheme)
                if ($Group -eq 'all') {
                    Try_ 'if (window.spReview.setPage) window.spReview.setPage("general");'
                    Start-Sleep -Milliseconds 400
                    # a colour picked by hand, not one of the ready-made ones
                    Try_ 'window.spReview.accent("#00A6FB");'
                    Start-Sleep -Milliseconds 800
                    Shot ("settings-custom-" + $scheme)
                    Try_ 'window.spHost.closeAll(); window.spHost.show("game");'
                    Start-Sleep -Milliseconds 800
                    Shot ("game-custom-" + $scheme)
                }
            }
        }
    }
    $ws.Dispose()
} finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -Confirm:$false -ErrorAction SilentlyContinue }
    Get-CimInstance Win32_Process -Filter "Name='msedge.exe'" | Where-Object { $_.CommandLine -like "*$profDir*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -Confirm:$false -ErrorAction SilentlyContinue }
    if ($serverJob) { Stop-Job $serverJob -ErrorAction SilentlyContinue; Remove-Job $serverJob -Force -ErrorAction SilentlyContinue }
    try { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue } catch { }
}

$total = $pass + $fail
if ($fail -eq 0) { Line "RESULT PASS $total/$total"; exit 0 }
Line "RESULT FAIL $fail/$total"
exit 1
