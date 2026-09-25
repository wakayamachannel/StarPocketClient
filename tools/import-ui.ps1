# tools\import-ui.ps1 - makes the app's ui\index.html from the prototype (design\launcher-proto\index.html), PORT-MAP 7.3.
#  - a standards-mode UTF-8 page with a Content-Security-Policy (the UI never loads anything from the internet)
#  - the Google Fonts links removed (the CSS falls back to Yu Gothic UI / Meiryo UI / Microsoft YaHei UI / system-ui)
#  - the PocketRoles crewmate icon (Innersloth's design) taken out of the page into ui\img\pocketroles-128.png: a separate
#    file next to the app, never inside "StarPocket Client.exe" (later it comes from the signed title manifest)
#  - the unofficial Chinese term fixed if an older prototype still has it (内鬼 -> 伪装者, PORT-MAP 9.3)
#  - small hooks for ui\host-v01.js (the v0.1 glue): window.spHost and a few one-line calls
# Every anchor must be found exactly once, else nothing is written (the prototype changed: update this script).
# Reads and writes UTF-8 with [IO.File] (never Get-Content / Set-Content, which garble BOM-less UTF-8 in PowerShell 5.1).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\import-ui.ps1 [-Prototype <index.html>] [-Out <folder>]
#   -Prototype defaults to design\launcher-proto\index.html in this repository (the prototype is kept here, so the UI can
#   be made again from the repository alone).
param(
    [string]$Prototype = '',
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Prototype) { $Prototype = Join-Path $repo 'design\launcher-proto\index.html' }
if (-not $Out) { $Out = Join-Path $repo 'ui' }
$utf8 = New-Object System.Text.UTF8Encoding($false)
$html = [IO.File]::ReadAllText($Prototype, $utf8)

function Replace-Once([string]$text, [string]$old, [string]$new, [string]$what) {
    $i = $text.IndexOf($old, [StringComparison]::Ordinal)
    if ($i -lt 0) { throw ('import-ui: not found: ' + $what) }
    if ($text.IndexOf($old, $i + $old.Length, [StringComparison]::Ordinal) -ge 0) { throw ('import-ui: found twice: ' + $what) }
    return $text.Substring(0, $i) + $new + $text.Substring($i + $old.Length)
}

# ---- 1. a real page: doctype, charset, CSP
if (-not $html.StartsWith('<title>')) { throw 'import-ui: the prototype no longer starts with <title>' }
$csp = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
       "media-src 'self'; font-src 'self'; connect-src 'self'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'"
$head = "<!doctype html>`n<html lang=`"ja`">`n<meta charset=`"utf-8`">`n" +
        "<meta http-equiv=`"Content-Security-Policy`" content=`"$csp`">`n" +
        "<!-- StarPocket Client v0.1 UI: made by tools/import-ui.ps1 from the prototype (design/launcher-proto/index.html). Edit the prototype, then run the script again. -->`n"
$html = $head + $html

# ---- 2. no Google Fonts
$html = Replace-Once $html "<link rel=`"preconnect`" href=`"https://fonts.googleapis.com`">`n" '' 'fonts.googleapis preconnect'
$html = Replace-Once $html "<link rel=`"preconnect`" href=`"https://fonts.gstatic.com`" crossorigin>`n" '' 'fonts.gstatic preconnect'
$m = [regex]::Match($html, '<link rel="stylesheet" href="https://fonts\.googleapis\.com/css2[^"]*">\n')
if (-not $m.Success) { throw 'import-ui: not found: Google Fonts stylesheet' }
$html = $html.Remove($m.Index, $m.Length)

# ---- 3. the crewmate icon becomes a separate file
$m = [regex]::Match($html, '<symbol id="pr-icon" viewBox="0 0 128 128"><image href="data:image/png;base64,([A-Za-z0-9+/=]+)"')
if (-not $m.Success) { throw 'import-ui: not found: pr-icon image' }
$png = [Convert]::FromBase64String($m.Groups[1].Value)
$imgDir = Join-Path $Out 'img'
[void][IO.Directory]::CreateDirectory($imgDir)
[IO.File]::WriteAllBytes((Join-Path $imgDir 'pocketroles-128.png'), $png)
$html = $html.Remove($m.Groups[1].Index - 'data:image/png;base64,'.Length, 'data:image/png;base64,'.Length + $m.Groups[1].Length).Insert($m.Groups[1].Index - 'data:image/png;base64,'.Length, 'img/pocketroles-128.png')
if ($html.Contains('data:image/png;base64')) { throw 'import-ui: another embedded PNG is left' }

# ---- 4. official Chinese term (the repository's copy of the prototype already has it; an older prototype does not)
if ($html.Contains('按设置补足内鬼人数')) { $html = Replace-Once $html '按设置补足内鬼人数' '按设置补足伪装者人数' 'zh term' }   # terms-ok
if ($html.Contains('内鬼')) { throw 'import-ui: 内鬼 is still in the page' }   # terms-ok

# ---- 5. hooks for host-v01.js (each a single call that does nothing outside the app)
$html = Replace-Once $html "function report(r, label){`n" ("function report(r, label){`n" +
    "  if (window.spHostReport && window.spHostReport(r, label)) return;   /* v0.1: host-v01.js */`n") 'report()'
$html = Replace-Once $html "function renderChecks(state){`n" ("function renderChecks(state){`n" +
    "  if (window.spHostRenderChecks && window.spHostRenderChecks(state)) return;   /* v0.1: real rows */`n") 'renderChecks()'
$html = Replace-Once $html "function renderAegisLog(){`n" ("function renderAegisLog(){`n" +
    "  if (window.spHostRenderAegisLog && window.spHostRenderAegisLog()) return;   /* v0.1: real events.log lines */`n") 'renderAegisLog()'
$html = Replace-Once $html "function setLangPref(p){`n" ("function setLangPref(p){`n" +
    "  if (window.spHostLangPref) window.spHostLangPref(p);   /* v0.1: tell the app (settings.json) */`n") 'setLangPref()'
$html = Replace-Once $html 'running = false; setTaskbar(0.62); renderPlay();' `
    'running = !!(window.spHostGameRunning && window.spHostGameRunning()); setTaskbar(0); renderPlay();   /* v0.1: the real game state */' 'launch() end'
$html = Replace-Once $html "  renderPlog(); syncThumb(false);`n}`n" ("  renderPlog(); syncThumb(false);`n" +
    "  if (window.spHostAfterRenderPlay) window.spHostAfterRenderPlay();   /* v0.1 */`n}`n") 'renderPlay() end'
$html = Replace-Once $html 'running = false; renderPlay();   /* plain: back to Play */' `
    'running = !!(window.spHostGameRunning && window.spHostGameRunning()); renderPlay();   /* v0.1.1: the real game state */' 'launchPlain() end'
$hooks = @'
runSplash();
/* ---------- StarPocket Client v0.1: what ui/host-v01.js may use (added by tools/import-ui.ps1; not part of the prototype) ---------- */
window.spHost = {
  T, t, L, toast, bridge, run, host, cmdLabel, PSTATE, CHECKS, AEGIS_LOG, PREF_DEFAULTS, prefs,
  renderPlay, renderChecks, renderAegisLog, refreshSettings, setLang, openLayer, closeAll, show, plainOn, openGameSetting, plainStarted,
  /* v1.3: プロフィールの絵（ui/host-v01.js が "shell" イベントのアプリの写しを入れる） */
  profile,
  /* v1.0: the app replaces the prototype's sample state with the real one (ui/host-v01.js) */
  setSampleState, renderTitles,
  /* the colour: the glue puts the app's own eleven values back on <html> on every "shell" event, so a page WebView2
     reloaded by itself never keeps the colour it started the day with (ui/host-v01.js) */
  acc: ACC,
  savePrefs: () => store.setJson('sp.prefs', prefs),
  get pstate(){ return pstate; }, set pstate(v){ pstate = v; },
  get busy(){ return busy; },
  get plainHolding(){ return !!plainHold; },
  get running(){ return running; }, set running(v){ running = v; },
  get lang(){ return lang; },
  get langPref(){ return langPref; }, set langPref(v){ langPref = v; store.set('sp.langPref', v); }
};
})();
</script>
'@
$html = Replace-Once $html "runSplash();`n})();`n</script>`n" ($hooks.Replace("`r`n", "`n")) 'end of the script'

# ---- 6. the glue script after the prototype's own
$html = $html.TrimEnd("`n") + "`n<script src=`"host-v01.js`"></script>`n"

[void][IO.Directory]::CreateDirectory($Out)
[IO.File]::WriteAllText((Join-Path $Out 'index.html'), $html, $utf8)
Write-Host ('ui\index.html: ' + $html.Length + ' chars; ui\img\pocketroles-128.png: ' + $png.Length + ' bytes')
