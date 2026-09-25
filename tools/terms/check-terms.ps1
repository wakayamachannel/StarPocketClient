# check-terms.ps1 — 公式の用語（ゲームの翻訳）と違う言い方を探します。
#
#   powershell -ExecutionPolicy Bypass -File tools\terms\check-terms.ps1 <ファイルかフォルダ> [...]
#   powershell -ExecutionPolicy Bypass -File tools\terms\check-terms.ps1 . -Allow tools\terms\check-terms.allow.tsv
#
# 見つけたら「ファイル:行: 言い方 → 公式の言い方（キー）」を出して終了コード 1、なければ 0。
# 直す先の言い方はこのファイルに書いてあります（用語集がなくても動きます）。用語集 glossary.tsv（run.ps1 がゲームから
# 取り出したもの。既定は隣の local\glossary.tsv、なければ隣の glossary.tsv）があれば、直す先が今のゲームの言葉と同じかも確かめます。
# 読むファイル: .json .cs .ps1 .psm1 .md .txt .html .htm .srt .tsv .cmd .csproj .yml（UTF-8、BOM あり・なしどちらでも）。
# 読まないもの: .git / bin / obj / node_modules、昔の記録（support\release-notes-*、support\design-*、DESIGN*.md、
#   support\verify-findings.md）、lang\defaults-history.tsv（ハッシュだけ）、tools\terms\（この道具）。-NoDefaultExcludes で全部読みます。
# 行を飛ばす: 行に「terms-ok」がある（例: プレイヤーが打つ言葉の一覧、古い名前の別名）、
#   .cs / .ps1 のコメント行（//・///・#・*）、-Allow の TSV（相対パスの一部 <TAB> 言い方 <TAB> 行に含まれる文字 <TAB> 理由）に合う行。
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Path = @('.'),
    [string]$Glossary = '',
    [string]$Allow = '',
    [switch]$NoDefaultExcludes
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Glossary) {
    foreach ($g in @((Join-Path $here 'local\glossary.tsv'), (Join-Path $here 'glossary.tsv'))) { if (Test-Path -LiteralPath $g) { $Glossary = $g; break } }
}
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }
$utf8 = New-Object System.Text.UTF8Encoding $false

# ------------------------------------------------------------------ 用語集（任意。glossary.tsv: concept key en zh-CN zh-TW ja）
$Official = @{}
if ($Glossary) {
    if (-not (Test-Path -LiteralPath $Glossary)) { Write-Output "用語集がありません: $Glossary"; exit 2 }
    foreach ($line in [IO.File]::ReadAllLines($Glossary, $utf8)) {
        $f = $line.Split("`t")
        if ($f.Length -lt 6 -or $f[1] -eq 'key') { continue }
        $Official[$f[1]] = @{ en = $f[2]; zh = $f[3]; tw = $f[4]; ja = $f[5] }
    }
}

# ------------------------------------------------------------------ 公式と違う言い方（Bad = 正規表現、Good = 直す先、Key = 用語集のキー）
# Good が用語集の Key の値と違えば、最初に「用語集と違います」と出します（ゲームの更新で公式の言葉が変わったとき）。
$Deny = @(
    @{ Lang = 'zh'; Bad = '内鬼';          Key = 'Impostor';          Good = '伪装者';   Note = '伪装者狂粉・伪装者阵营なども同じ' }
    @{ Lang = 'zh'; Bad = '通风管';        Key = 'VentLabel';         Good = '通风口';   Note = '' }
    @{ Lang = 'zh'; Bad = '管道';          Key = 'VentLabel';         Good = '通风口';   Note = '在管道中 → 在通风口里、进入管道 → 钻进通风口' }
    @{ Lang = 'zh'; Bad = '跳管';          Key = '';                  Good = '钻通风口'; Note = '動詞は公式のクイックチャット「我没有钻通风口」「进出通风口」' }
    @{ Lang = 'zh'; Bad = '放逐';          Key = '';                  Good = '驱逐';     Note = '公式: {0} 遭到驱逐。（ExileTextNonConfirm）' }
    @{ Lang = 'zh'; Bad = '(被|或)投出';   Key = '';                  Good = '被驱逐 / 被投票驱逐'; Note = '「已投出的票」（票を入れた）は別の意味' }
    @{ Lang = 'zh'; Bad = '好友代码';      Key = 'FriendCodeLabel';   Good = '好友编号'; Note = '' }
    @{ Lang = 'zh'; Bad = '房间码';        Key = 'RoomCodeLabel';     Good = '房间代码'; Note = '' }
    @{ Lang = 'zh'; Bad = '幻影';          Key = 'PhantomRole';       Good = '幻象师';   Note = '' }
    @{ Lang = 'zh'; Bad = '隐身';          Key = 'PhantomAbility';    Good = '消失';     Note = '幻象师の能力の名前は 消失、説明は 隐形（隱身 は繁体字）' }
    @{ Lang = 'zh'; Bad = '噪音制造者';    Key = 'NoisemakerRole';    Good = '大嗓门';   Note = '' }
    @{ Lang = 'zh'; Bad = '追踪者';        Key = 'TrackerRole';       Good = '侦察员';   Note = '繁体字の公式は 追蹤者' }
    @{ Lang = 'zh'; Bad = '审判官';        Key = 'JudgeRole';         Good = '法官';     Note = '' }
    @{ Lang = 'zh'; Bad = '生命体征';      Key = 'VitalsSystem';      Good = '生命监测器'; Note = '' }
    @{ Lang = 'zh'; Bad = '快捷聊天';      Key = 'QuickChat';         Good = '快速聊天'; Note = '' }
    @{ Lang = 'zh'; Bad = '原版击杀距离';  Key = 'GameKillDistance';  Good = '击杀范围'; Note = '設定の名前（短 / 中 / 长）' }
    @{ Lang = 'zh'; Bad = '守护天使的守护'; Key = 'ProtectAbility';   Good = '守护天使的保护'; Note = '' }
    @{ Lang = 'zh'; Bad = '(尸体举报|举报尸体|不可能的举报)'; Key = ''; Good = '报告（尸体） / 通报发现尸体'; Note = '玩家を運営に通報するのは公式も 举报' }
    @{ Lang = 'zh'; Bad = '主持';          Key = 'HostNounLabel';     Good = '房主';     Note = 'ホストは 房主（公式は 主持人 もホスト全般に使うので、GM 機能の名前は GM）' }
    @{ Lang = 'zh'; Bad = '(未|已)登记';   Key = '';                  Good = '未注册 / 已注册'; Note = 'MOD 内の言い方をそろえる（MOD房间注册）' }
    @{ Lang = 'ja'; Bad = 'サイエンティスト'; Key = 'ScientistRole';  Good = '科学者';   Note = '' }
    @{ Lang = 'ja'; Bad = 'ヴァイパー';    Key = 'ViperRole';         Good = 'バイパー'; Note = '' }
    @{ Lang = 'ja'; Bad = 'クルーメイト';  Key = 'Crewmate';          Good = 'クルー';   Note = '' }
    @{ Lang = 'ja'; Bad = '守護天使の守護'; Key = 'ProtectAbility';   Good = '守護天使の護衛'; Note = '' }
)
$drift = 0
foreach ($d in $Deny) {
    $d.Rx = New-Object System.Text.RegularExpressions.Regex($d.Bad)
    if (-not $d.Key -or $Official.Count -eq 0) { continue }
    if (-not $Official.ContainsKey($d.Key)) { Write-Output ("用語集に {0} がありません（glossary-keys.tsv に足してください）" -f $d.Key); $drift++; continue }
    $g = $Official[$d.Key][$d.Lang]
    if ($g -and $d.Good.IndexOf($g, [StringComparison]::Ordinal) -lt 0 -and -not ($d.Lang -eq 'zh' -and $d.Key -eq 'HostNounLabel')) {
        Write-Output ("用語集と違います: {0} → {1}（{2} の公式は今「{3}」）" -f $d.Bad, $d.Good, $d.Key, $g); $drift++
    }
}

# ------------------------------------------------------------------ 読むファイル
$Exts = @('.json', '.cs', '.ps1', '.psm1', '.md', '.txt', '.html', '.htm', '.srt', '.tsv', '.cmd', '.csproj', '.yml')
$DefaultExcludes = @('\.git\', '\bin\', '\obj\', '\node_modules\', '\support\release-notes-', '\support\design-', '\DESIGN', '\support\verify-findings.md', '\lang\defaults-history.tsv', '\tools\terms\', '\check-terms.ps1', '\check-terms.allow.tsv')
function Excluded([string]$full) {
    if ($NoDefaultExcludes) { return $false }
    foreach ($x in $DefaultExcludes) { if ($full.IndexOf($x, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true } }
    return $false
}

$files = New-Object System.Collections.Generic.List[string]
foreach ($p in $Path) {
    if (-not (Test-Path -LiteralPath $p)) { Write-Output "見つかりません: $p"; exit 2 }
    $item = Get-Item -LiteralPath $p
    if ($item.PSIsContainer) {
        foreach ($f in (Get-ChildItem -LiteralPath $item.FullName -Recurse -File)) {
            if ($Exts -notcontains $f.Extension.ToLowerInvariant()) { continue }
            if (Excluded $f.FullName) { continue }
            $files.Add($f.FullName)
        }
    } else {
        $files.Add($item.FullName)   # 名前で指定したファイルは除外リストに関係なく読む
    }
}
$root = if ($Path.Count -eq 1 -and (Test-Path -LiteralPath $Path[0] -PathType Container)) { (Get-Item -LiteralPath $Path[0]).FullName.TrimEnd('\') + '\' } else { '' }
function Rel([string]$full) { if ($root -and $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { return $full.Substring($root.Length) } return $full }

# ------------------------------------------------------------------ 許可リスト（相対パスの一部 <TAB> 言い方 <TAB> 行に含まれる文字 <TAB> 理由）
$AllowRows = @()
if ($Allow) {
    if (-not (Test-Path -LiteralPath $Allow)) { Write-Output "許可リストがありません: $Allow"; exit 2 }
    foreach ($line in [IO.File]::ReadAllLines($Allow, $utf8)) {
        if (-not $line -or $line.StartsWith('#')) { continue }
        $f = $line.Split("`t")
        if ($f.Length -lt 3) { continue }
        $AllowRows += @{ File = $f[0].Replace('/', '\'); Term = $f[1]; Text = $f[2] }
    }
}
function Allowed([string]$rel, [string]$term, [string]$text) {
    foreach ($a in $AllowRows) {
        if ($rel.IndexOf($a.File, [StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
        if ($a.Term -and $term.IndexOf($a.Term, [StringComparison]::Ordinal) -lt 0) { continue }
        if ($a.Text -and $text.IndexOf($a.Text, [StringComparison]::Ordinal) -lt 0) { continue }
        return $true
    }
    return $false
}

# ------------------------------------------------------------------ 探す
$hits = 0; $hitFiles = @{}; $skipped = 0
foreach ($file in $files) {
    $ext = [IO.Path]::GetExtension($file).ToLowerInvariant()
    $code = ($ext -eq '.cs' -or $ext -eq '.ps1' -or $ext -eq '.psm1')
    $lines = [IO.File]::ReadAllLines($file, $utf8)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $text = $lines[$i]
        $hasAny = $false
        foreach ($d in $Deny) { if ($d.Rx.IsMatch($text)) { $hasAny = $true; break } }
        if (-not $hasAny) { continue }
        if ($text.IndexOf('terms-ok', [StringComparison]::OrdinalIgnoreCase) -ge 0) { $skipped++; continue }
        if ($code) {
            $t = $text.TrimStart()
            if ($t.StartsWith('//') -or $t.StartsWith('#') -or $t.StartsWith('*') -or $t.StartsWith('<#')) { $skipped++; continue }
        }
        $rel = Rel $file
        foreach ($d in $Deny) {
            foreach ($m in $d.Rx.Matches($text)) {
                if (Allowed $rel $m.Value $text) { $skipped++; continue }
                $at = $m.Index
                $from = [Math]::Max(0, $at - 16); $len = [Math]::Min($text.Length - $from, $m.Length + 32)
                $ctx = $text.Substring($from, $len).Trim()
                $key = if ($d.Key) { '（' + $d.Key + '）' } else { '' }
                $note = if ($d.Note) { '  ※' + $d.Note } else { '' }
                Write-Output ("{0}:{1}: {2} → {3}{4}{5}`n    …{6}…" -f $rel, ($i + 1), $m.Value, $d.Good, $key, $note, $ctx)
                $hits++
                $hitFiles[$rel] = $true
            }
        }
    }
}

Write-Output ''
$gl = if ($Glossary) { "用語集: $Glossary" } else { '用語集: なし（直す先はこのファイルの表）' }
Write-Output ("読んだファイル: {0}  公式と違う言い方: {1} 件（{2} ファイル）  飛ばした行（terms-ok・コメント・許可リスト）: {3}  {4}" -f $files.Count, $hits, $hitFiles.Count, $skipped, $gl)
if ($drift -gt 0) { Write-Output ("用語集と違う直す先: {0} 件（上を見て、このファイルの表を直してください）" -f $drift) }
if ($hits -gt 0 -or $drift -gt 0) { exit 1 }
exit 0
