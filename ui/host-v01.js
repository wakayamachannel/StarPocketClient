/* StarPocket Client - the glue between the prototype UI (index.html) and the app (PORT-MAP 7.3).
   Loaded after the prototype's own script; uses the hooks tools/import-ui.ps1 adds (window.spHost, window.spHost*).
   Outside the app (no window.chrome.webview) it does nothing, so the page stays the prototype.
   What it changes:
   - commands this version does not have yet answer with a calm "not in this version" notice instead of "failed"
     (the version comes from the app's "shell" event, so the notice never names an older one)
   - the play button follows the app's "status" event, with a new "blocked" state when Aegis stops a start, and (v0.2)
     a "repair" state: the same install steps, from where they stopped, with the Steam version left alone
   - the Aegis panel shows the app's rows (13 checks, same order as the prototype's CHECKS) and events.log lines
   - the language comes from the app (settings.json); a change in Settings goes back to it (setLang)
   - settings the app does not keep yet stay at their defaults; the page's own one (streamer) says nothing
   - the tray's "scan again" runs the page's own scan flow (nav run aegis.rescan); its "play" runs the page's PLAY (nav run
     play: the game Settings → 起動するゲーム names)
   - 起動するゲーム (v0.1.1): the app keeps it (settings.json startGame) and says it in the "shell" event; plain Among Us
     makes PLAY the normal Play that sends launchVanilla (the mod's states and warnings do not apply to it); after it
     PLAY stays 「プレイ中」 until the "game" / "status" event says Among Us runs (at most 30 s: Steam can be slow)
   - the prototype-only bar is hidden; the drag strips use window.drag when WebView2 cannot drag by itself
   - v1.1: 起動のたびに Aegis のスキャンを出す (startScan) is the app's (settings.json, said in the "shell" event); a red row in
     the start scan makes the app send nav open d-aegis, which the handler below already answers
   - v1.2: 優しく閉じる（"window" closing）
   - v1.3: プロフィールの絵（"shell" の avatar / avatarError / profile）
   SPDX-License-Identifier: GPL-3.0-or-later */
(() => {
'use strict';
const H = window.spHost;
if (!H || !(window.chrome && window.chrome.webview)) return;
/* v1.0: inside the app from the first frame. sp-live is what the styles below key on to hide the prototype's samples
   (the wallpaper plate of the start screen, the proto bars, the 「見本」 badges); put on here rather than with the first
   "status" event, because the start screen is already playing before that event arrives. */
document.documentElement.classList.add('sp-live');

/* ---------- texts (ja / zh / en; zh uses the official Among Us terms) ---------- */
const X = {
  ja: {
    'v01.unsupported': '「{x}」はこの版（v{v}）ではまだ使えません。今までのランチャーで行えます。',
    'v01.unsupported0': 'この機能はこの版（v{v}）ではまだ使えません。今までのランチャーで行えます。',
    'v01.setting': 'この設定はこの版（v{v}）ではまだ保存されません。',
    'v01.external': 'このページはまだ用意できていません（v{v}）。',
    'ps.blocked': 'Aegis が起動を止めました',
    'ps.m.blocked': '赤い項目を直してから押してください',
    'ps.b.blocked': '確かめて起動',
    'ps.o.blocked': 'Aegis が起動を止めた（赤）',
    /* v0.2: the copy is there but not whole, or PocketRoles.dll has changed (R-41): the same install steps, from where
       it stopped. The Steam version is never touched. */
    'ps.repair': '修復が必要です',
    'ps.m.repair': 'MOD 用のコピーを直します。Steam 版のゲームはそのままです。',
    'ps.b.repair': '修復',
    /* v0.3 */
    'v03.shortcut': 'デスクトップにショートカットを作りました。',
    'v03.working': 'まだ処理が続いています。終わるまでお待ちください。',
    /* the same words the app uses while it makes a zip (Strings.cs rp_creating / ex_creating): the page shows them
       the moment the button is pressed, because the app cannot answer until the whole job is done */
    'v03.reporting': '報告 zip を作成中...',
    'v03.exporting': 'その人の証拠の zip を作っています...',
    'v03.close': '閉じる',
    /* v0.4: the log page and the developer tools. Everything the page SHOWS comes from the app in the app's own three
       languages; these are only the words this file needs before an answer has arrived. */
    'v04.logTitle': '進行ログ',
    'v04.logNone': 'まだ記録はありません。',
    'toast.scanDone': 'スキャンが終わりました。結果は Aegis の状態に出ています。',
    'toast.launched': '起動しました。左上に "PocketRoles v..." と表示されれば mod が有効です。',
    /* v1.0: the library's 状態 table with the app's real findings (the page's own lib.* words cover the rest) */
    'lib.notRunning': '未起動',
    'lib.absent': 'なし',
    'lib.bepOther': '別のバージョン',
    'aegis.off': 'Aegis は動いていません',
    'aegis.idle': 'いまは待機中 — ゲームを始めると監視します',
    'aegis.watching': '監視中（ゲーム中） · 検知 {d} · 退出 {k}',
    'aegis.kicked': '退出させました · 検知 {d} · 退出 {k}',
    'aegis.defs0': '定義ファイル v{v} · 署名 OK · 検知ルール {r}',
    'aegis.defs1': '組み込みの定義 · 検知ルール {r}',
    'aegis.defs2': '定義の署名なし・不一致（組み込みの定義を使用） · 検知ルール {r}',
    'aegis.noEvents': 'まだ記録はありません',
    /* v1.0: the community column with nothing recorded yet, and Settings → Among Us の場所 while the app has said only
       "found" and not yet which folder */
    'comm.empty': 'まだ記録はありません',
    'v10.steamFound': 'Steam の Among Us（自動で見つけました）'
  },
  zh: {
    'v01.unsupported': '此版本（v{v}）还不能使用“{x}”。请用原来的启动器进行。',
    'v01.unsupported0': '此版本（v{v}）还不能使用这个功能。请用原来的启动器进行。',
    'v01.setting': '此版本（v{v}）还不会保存这个设置。',
    'v01.external': '这个页面还没有准备好（v{v}）。',
    'ps.blocked': 'Aegis 已阻止启动',
    'ps.m.blocked': '请先处理红色项目再按',
    'ps.b.blocked': '重新检查并启动',
    'ps.o.blocked': 'Aegis 阻止了启动（红）',
    'ps.repair': '需要修复',
    'ps.m.repair': '将修复模组用副本。Steam 版游戏保持不变。',
    'ps.b.repair': '修复',
    'v03.shortcut': '已在桌面创建快捷方式。',
    'toast.scanDone': '扫描完成。结果显示在 Aegis 状态中。',
    'v03.working': '仍在处理中。请等待完成。',
    'v03.reporting': '正在生成报告 zip...',
    'v03.exporting': '正在生成此人的证据 zip...',
    'v03.close': '关闭',
    'v04.logTitle': '运行日志',
    'v04.logNone': '还没有记录。',
    'toast.launched': '已启动。左上角显示 "PocketRoles v..." 即表示 mod 生效。',
    'lib.notRunning': '未运行',
    'lib.absent': '无',
    'lib.bepOther': '其他版本',
    'aegis.off': 'Aegis 未运行',
    'aegis.idle': '现在待机 — 开始游戏后开始监视',
    'aegis.watching': '监视中（游戏中） · 检测 {d} · 移出 {k}',
    'aegis.kicked': '已移出玩家 · 检测 {d} · 移出 {k}',
    'aegis.defs0': '定义文件 v{v} · 签名 OK · 检测规则 {r}',
    'aegis.defs1': '内置定义 · 检测规则 {r}',
    'aegis.defs2': '定义没有签名或不一致（使用内置定义） · 检测规则 {r}',
    'aegis.noEvents': '还没有记录',
    'comm.empty': '还没有记录',
    'v10.steamFound': 'Steam 的 Among Us（已自动找到）'
  },
  en: {
    'v01.unsupported': '“{x}” isn’t in this version (v{v}) yet. The current launcher still does it.',
    'v01.unsupported0': 'This isn’t in this version (v{v}) yet. The current launcher still does it.',
    'v01.setting': 'This setting isn’t saved in this version (v{v}) yet.',
    'v01.external': 'That page isn’t ready yet (v{v}).',
    'ps.blocked': 'Aegis stopped the launch',
    'ps.m.blocked': 'Fix the red items, then press',
    'ps.b.blocked': 'Check and play',
    'ps.o.blocked': 'Aegis stopped the launch (red)',
    'ps.repair': 'Needs a repair',
    'ps.m.repair': 'Puts the mod copy right. The Steam version is left as it is.',
    'ps.b.repair': 'Repair',
    'v03.shortcut': 'A desktop shortcut was created.',
    'toast.scanDone': 'Scan finished. See Aegis status for the results.',
    'toast.launched': 'Launched. The mod is active when "PocketRoles v..." appears at the top-left.',
    'v03.working': 'Still working. Please wait until it finishes.',
    'v03.reporting': 'Creating the report zip...',
    'v03.exporting': "Creating the zip of that player's evidence...",
    'v03.close': 'Close',
    'v04.logTitle': 'Task log',
    'v04.logNone': 'Nothing recorded yet.',
    'lib.notRunning': 'not running',
    'lib.absent': 'missing',
    'lib.bepOther': 'different version',
    'aegis.off': 'Aegis isn’t running',
    'aegis.idle': 'Idle right now — starts watching when you start the game',
    'aegis.watching': 'Watching (in game) · {d} flagged · {k} removed',
    'aegis.kicked': 'Removed a player · {d} flagged · {k} removed',
    'aegis.defs0': 'Definitions v{v} · signature OK · {r} detection rules',
    'aegis.defs1': 'Built-in definitions · {r} detection rules',
    'aegis.defs2': 'Definitions unsigned or mismatched (built-in list in use) · {r} detection rules',
    'aegis.noEvents': 'Nothing recorded yet',
    'comm.empty': 'Nothing recorded yet',
    'v10.steamFound': "Steam's Among Us (found automatically)"
  }
};

/* ---------- styles for what the prototype has no look for yet ---------- */
const css = document.createElement('style');
css.textContent = [
  '#protoBar{display:none!important}',
  /* v1.0: the library's 状態 table - a BepInEx that is not the one the app expects, in the same amber as a "warn" row */
  '.sp-kv-warn{color:#B87A06}',
  '.checks li.warn .st{background:rgba(244,186,69,.2);color:#B87A06}',
  /* "found something" and "this is how to put it right" are faults, so they keep the danger red, never the accent */
  '.checks li.bad .st{background:var(--danger-soft);color:var(--danger-text)}',
  '.checks li .st .icon{width:12px;height:12px}',
  '.checks li .sp-d,.checks li .sp-fix{display:block;font-size:12px;line-height:1.45;margin-top:2px}',
  '.checks li .sp-d{color:var(--muted)}',
  '.checks li .sp-fix{color:var(--danger-text)}',
  'html.sp-live #d-aegis .sample-tag{display:none}',
  /* v1.0: the 「見本」 badges of the prototype have no place in the real app */
  'html.sp-live .sample,html.sp-live .sample-tag{display:none}',
  /* v1.0: buttons whose target does not exist yet (markSoon, below). Only this file disables them, so the prototype never
     meets these rules: no hover, no pointer, quiet colours - a button that cannot be pressed must not look pressable */
  '.media-card:disabled{cursor:default;opacity:.72}',
  '.media-card:disabled:hover .mthumb > svg{scale:1}',
  '.btn-join:disabled,.btn-join:disabled:hover{cursor:default;opacity:.5}',
  '.mini-btn:disabled,.mini-btn:disabled:hover{cursor:default;background:transparent;color:var(--muted);opacity:.5}',
  '.set-legal button:disabled,.set-legal button:disabled:hover,.legal-bottom button:disabled,.linkbtn:disabled{cursor:default;color:var(--set-faint);text-decoration:none}',
  /* v1.0: the rest of what only the prototype should show. The second proto bar under the home news (the first is
     #protoBar above), the 「見本」 badge of the community title, and the start screen's sample wallpaper plate with its
     two labels: the window is not see-through in this version, so the logo plays over the window's own night colour
     (MainForm.Night, the colour WebView2 paints before the page is there - no flash either way). */
  'html.sp-live .proto-bar{display:none!important}',
  'html.sp-live .sample-mini{display:none}',
  'html.sp-live .plate,html.sp-live .plate-tag,html.sp-live .plate-cap{display:none}',
  'html.sp-live .splash{background:#0B0C22}',
  /* the community column: the app keeps no recent players, rooms or Discord count yet (Bridge.Later), so the page's
     lists are handed empty ones (window.spHostRecent) and the count lines, left empty, take their green dot with them */
  'html.sp-live #dcCount:empty,html.sp-live #dcMini:empty{display:none}',
  /* v0.4: the log page. A page inside the app, in the same sheet colours as the settings dialogs - the launcher
     opened a second window for this, and a second window is the one thing this app is not allowed to be. */
  '#sp-log .sp-logcard{width:min(760px,100%);max-height:min(76vh,720px);display:grid;grid-template-rows:auto auto 1fr auto auto;gap:10px;background:var(--set-nav);color:var(--set-text);border-color:var(--set-line)}',
  '#sp-log h2{margin:0;font-size:16px}',
  '#sp-log .sp-logsub{margin:0;font-size:12px;line-height:1.55;color:var(--muted)}',
  '#sp-log .sp-loglines{margin:0;padding:8px 10px;list-style:none;overflow:auto;border:1px solid var(--set-line);border-radius:10px;background:var(--set-raised);font-size:12px;line-height:1.6;min-height:120px}',
  '#sp-log .sp-loglines li{display:flex;gap:10px;padding:1px 0;word-break:break-word}',
  '#sp-log .sp-loglines time{flex:none;color:var(--muted);font-variant-numeric:tabular-nums;font-feature-settings:"tnum"}',
  '#sp-log .sp-loglines span{white-space:pre-wrap}',
  /* the "nothing recorded yet" line (li.none): its span keeps a width of its own and never breaks inside the phrase, so
     a box squeezed narrow (break-word above lets every other line break anywhere) cannot put one character per line */
  '#sp-log .sp-loglines li.none span{min-width:min-content;word-break:keep-all;overflow-wrap:normal}',
  '#sp-log .sp-logfoot{margin:0;display:flex;flex-wrap:wrap;gap:6px 14px;align-items:baseline;font-size:12px;color:var(--muted)}',
  '#sp-log .sp-logfoot code{font-size:11px;word-break:break-all}',
  '#sp-log .sp-logmore{margin:0;font-size:12px;color:var(--muted)}',
  '#sp-log .dlg-acts button{background:var(--set-raised);border-color:var(--set-edge);color:var(--set-text)}',
  '#sp-log .dlg-acts button:hover{background:var(--set-raised-hi)}',
  /* 「ログ: 123 MB」 beside 「ログのフォルダを開く」; past 2 GB it is the danger colour, never the accent - it is a
     "look at this", and red is the only colour in this app that means that */
  '.sp-logsize{display:inline-block;margin-left:6px;font-size:12px;color:var(--muted);font-variant-numeric:tabular-nums}',
  '.sp-logsize[data-big="1"]{color:var(--danger-text);font-weight:700}',
  '@media (forced-colors: active){.sp-logsize[data-big="1"]{color:LinkText}}',
  /* v1.1: developer mode. The 「開発」 chip in the title bar (index.html #tbMode) and the developer-tagged tool rows
     (再ビルド, 更新, the ban console) exist only while the app says it runs against the author's working copy of the mod
     (sp-dev, from the "shell" / "status" events' mode). On every other PC they are not there at all - a row whose only
     answer would be 「開発モードでだけ使えます」 must not be drawn. The prototype outside the app keeps its samples. */
  'html.sp-live:not(.sp-dev) #tools-dev{display:none}',
  'html.sp-live:not(.sp-dev) .tool:has(> b > .tag.dev){display:none}',
  /* which PocketRoles.dll is in the copy, under its version in the library's 状態 table (the app's own words) */
  '.sp-origin{display:block;font-size:11px;line-height:1.4;color:var(--muted);margin-top:2px}',
  /* v1.2: 優しく閉じる。窓を薄くするのはアプリ側（Form.Opacity）で、ページは #app が「ほんの少し縮む」だけ。長さ（--sp-leave-ms）は
     アプリが "window" イベントで言う。index.html には書かない（自己診断が「無いこと」を見る） */
  'html.sp-live .app.sp-leave{opacity:.7;scale:.985;transition:opacity var(--sp-leave-ms,140ms) cubic-bezier(.2,.8,.2,1),scale var(--sp-leave-ms,140ms) cubic-bezier(.2,.8,.2,1)}'
].join('\n');
document.head.append(css);

/* ---------- state from the app ---------- */
let hostPstate = null, gameRunning = false, lastWarn = '', aegisData = null, applyingLang = false, dragFallback = false;
/* the version the app tells us in its "shell" event: the "not in this version" notices name it instead of a fixed 0.1 */
let appVer = '0.1';
const tv = (key, vars) => H.t(key, Object.assign({ v:appVer }, vars || {}));
/* v1.1: the author's switch (Settings → PocketRoles → 開発). What the app said: whether the working copy of the mod was
   found on this PC (only then is the block drawn - index.html devBlock asks spHostDev), its folder (the account name
   already taken out by the app), the setting as saved, and what is in the game copy right now (from "status"). */
let devInfo = { available:false, on:false, folder:'', origin:'' };
window.spHostDev = () => devInfo;
/* the 「開発」 chip and the developer rows follow what this run really IS (the app's "mode"), not the setting */
function applyMode(mode){
  if (mode !== 'dev' && mode !== 'friend') return;
  const dev = mode === 'dev';
  document.documentElement.classList.toggle('sp-dev', dev);
  const chip = document.getElementById('tbMode'); if (chip) chip.hidden = !dev;
}
const ROW_CLASS = { 0:'', 1:'run', 2:'ok', 3:'warn', 4:'bad' };
const DOT = { idle:'#2EC4B6', watching:'#18C37E', kicked:'#F4BA45' };

/* one of the page's own custom properties, as the browser worked it out */
function cssVar(name){
  try { return getComputedStyle(document.documentElement).getPropertyValue(name).trim(); }
  catch (err) { return ''; }
}
function icon(id){
  const NS = 'http://www.w3.org/2000/svg';
  const s = document.createElementNS(NS, 'svg'); s.setAttribute('class', 'icon');
  const u = document.createElementNS(NS, 'use'); u.setAttribute('href', '#' + id); s.append(u); return s;
}
/* v1.0: 「更新を確認」 found a newer PocketRoles. The app's status has no state for that (the launcher's warning line had
   none), so it is kept here and laid over 準備 OK until the DLL on disk is not the one that was looked at any more
   (updated - here, or by the old launcher - or removed), or an answer says the copy is current. */
let updateAvail = null;   /* { version, dll: the installed DLL version when it was found } */
let lastDll = '', lastStatus = null;
const livePstate = () => hostPstate === 'ready' && updateAvail ? 'update' : hostPstate;
function restorePstate(){ if (hostPstate && !H.busy && H.PSTATE[hostPstate]) { H.pstate = livePstate(); H.renderPlay(); } }
/* the page's hook: every checkUpdate answer comes through here - the tools list's look ({}), the play button's install
   ({install:true}) - so the button is put back on the app's own state at once, not on the page's guess */
window.spHostUpdateAnswer = r => {
  if (!r || !r.ok) return;
  const d = r.data || {};
  if (d.available) updateAvail = { version:String(d.version || ''), dll:lastDll };
  else if (d.updated || d.upToDate) updateAvail = null;
  restorePstate();
  if (lastStatus) applyRealState(lastStatus);   /* the MOD card and the library follow */
};

/* settings the app acts on but v0.1 does not keep yet: they stay at their defaults (a switch never shows a choice that
   does nothing), also when an older run left another value in this page's storage */
/* v0.3: "autostart" left this list - the app really keeps it now (one HKCU Run value), and the shell event says what
   Windows will do, so the switch shows the truth even when the value was removed somewhere else. */
const APP_SIDE = ['hwaccel', 'nAegisA', 'nAegisS', 'nUpdateA', 'nUpdateS', 'nNewsA', 'nNewsS', 'prLang', 'channel', 'autoUpdate', 'autoLaunch', 'chatTranslate'];
/* ページだけで効く設定（配信モード）: 「保存されません」の知らせは出さない。背景の動画は無くなり（持ち主 2026-09-24「動画はいいや、
   要らない」）、音量は v1.2 からアプリが settings.json に持つので、ここに残るのは配信モードだけ */
const PAGE_ONLY = new Set(['streamer']);
function resetAppSide(){
  const D = H.PREF_DEFAULTS; if (!D) return;
  let changed = false;
  for (const k of APP_SIDE) if (Object.hasOwn(D, k) && H.prefs[k] !== D[k]) { H.prefs[k] = D[k]; changed = true; }
  if (changed) { H.savePrefs(); H.refreshSettings(); }
}

/* ---------- hooks the prototype calls ---------- */
window.spHostGameRunning = () => gameRunning;
/* the mod copy's warning line is the tooltip of the state line; not while PLAY starts plain Among Us (it is not about that game) */
window.spHostAfterRenderPlay = () => { const el = document.getElementById('playState'); if (el) el.title = H.plainOn() ? '' : lastWarn; };

/* v1.0: the Discord count. The app looks nothing up (nothing is sent anywhere yet), so the prototype's 128 must not be
   shown as if it were real: null makes the page write 準備中 with no "online" lamp, and a dash in the strip
   (community.render in index.html). When the app really asks Discord one day, this returns that number. */
window.spHostDcOnline = () => null;

/* ---------- v1.0: buttons that lead nowhere yet ----------
   openExternal is "later" in this app (Bridge.cs), so each of these, pressed, only said 「このページはまだ用意できていません」.
   A button that can only apologise is worse than none. These stay where they are, cannot be pressed, and say 準備中 in
   place of what they promised: the two legal lines (planned pages; they open once they are public) and Discord (no
   invite; its count is spHostDcOnline above).
   2026-09-24: 動画の 2 つ（hero の「今すぐ見る」と「遊び方の動画」のカード。ここで隠す・準備中にしていた）はページ自体から
   無くなったので、ここからも消えた（持ち主「動画はいいや、要らない」）。
   Labels go through data-i18n / data-i18n-attr, so a change of language keeps them. Idempotent: the settings body is
   rebuilt every time it opens and watched by a MutationObserver, which calls this again. */
   2026-09-26: **表は空になりました。** openExternal が本当に動くようになったからです（Bridge.Supported、
   AppInfo.ExternalPage）。Discord も、製品サイトも、利用規約も、プライバシーポリシーも、行き先は全部
   公開されています。ページが渡すのは名前だけで、URL は C# 側の表が決めます。
   また押せなくする物が出てきたら、ここに名前を足してください。仕組みは残してあります。 */
const SOON = {};
function markSoon(root){
  for (const [cmd, s] of Object.entries(SOON)) for (const b of root.querySelectorAll('button[data-cmd="' + cmd + '"]')) {
    if (!b.disabled) b.disabled = true;
    if (s.label) {
      for (const svg of b.querySelectorAll('svg')) svg.remove();   /* the "opens outside" arrow of the privacy page: nothing opens */
      if (b.dataset.i18n !== s.label) b.dataset.i18n = s.label;
      const want = H.t(s.label); if (b.textContent !== want) b.textContent = want;
    }
    /* the strip's icon button: its name and tooltip say why (the 「参加する」 beside the 準備中 line needs no more) */
    if (s.attr && b.dataset.i18nAttr && b.dataset.i18nAttr !== s.attr) {
      b.dataset.i18nAttr = s.attr;
      for (const pair of s.attr.split(';')) { const [a, k] = pair.split(':'); b.setAttribute(a, H.t(k)); }
    }
  }
  /* the strip's count shows a dash: its tooltip must not call that "the number of people online (sample)", and since
     2026-09-26 it must not say Discord is coming either - Discord is up; this app simply does not ask it for a number. */
  const mini = root.querySelector('#dcMini');
  if (mini && mini.dataset.i18nAttr !== 'title:comm.noCount') { mini.dataset.i18nAttr = 'title:comm.noCount'; mini.title = H.t('comm.noCount'); }
}
/* v1.0: the community column's lists. The prototype fills them with sample people, rooms and a Discord count; the app
   keeps none of these yet (recent.clear / player.vip / player.restrict are "later" in Bridge.cs), so it gets empty lists,
   no count, and its own "nothing recorded yet" line - never a person who was not in the room. */
window.spHostRecent = () => ({ people:[], rooms:[], online:null, empty:H.t('comm.empty') });

/* answers that are not "ok" (report() of the prototype) */
window.spHostReport = (r, label) => {
  if (!r || r.ok || r.prototype) return false;
  const d = r.data || {};
  /* v1.1: the developer switch was refused (no working copy, a running game, busy): the page had already flipped it,
     and every answer to that switch names the setting as it really stands */
  if (d.key === 'devBuild' && typeof d.devBuild === 'boolean' && H.prefs.devBuild !== d.devBuild) { H.prefs.devBuild = d.devBuild; H.savePrefs(); H.refreshSettings(); }
  if (r.unsupported) {
    if (d.cmd === 'settings.set') {
      if (PAGE_ONLY.has(d.key)) return true;   /* the page already applied it */
      H.toast(tv('v01.setting'));
      resetAppSide();
    } else if (d.cmd === 'openExternal') H.toast(tv('v01.external'));
    else {
      const x = r.needs ? H.cmdLabel(r.needs) : label;
      H.toast(x && x !== d.cmd && x !== r.needs ? tv('v01.unsupported', { x }) : tv('v01.unsupported0'));
    }
    restorePstate();
    return true;
  }
  if (r.blocked) {
    if (!H.busy) { H.pstate = 'blocked'; H.renderPlay(); }
    H.toast(H.t('toast.blocked'));
    H.closeAll(); H.openLayer('d-aegis');
    return true;
  }
  if (r.error) { H.toast(String(r.error)); restorePstate(); return true; }
  return false;   // busy / timeout: the prototype's own words
};

/* the Aegis panel: the app's 13 rows (in the prototype's CHECKS order) and its events.log lines */
window.spHostRenderChecks = () => {
  const ul = document.getElementById('checks'); if (!ul) return true;
  ul.textContent = '';
  const rows = (aegisData && aegisData.rows) || [];
  H.CHECKS.forEach((c, i) => {
    const row = rows[i];
    const li = document.createElement('li'); li.className = row ? (ROW_CLASS[row.state] ?? '') : '';
    const st = document.createElement('span'); st.className = 'st';
    if (li.className === 'ok') st.append(icon('i-check'));
    else if (li.className === 'warn' || li.className === 'bad') st.append(icon('i-warn'));
    const n = document.createElement('span'); n.textContent = row && row.title ? row.title : H.L(c.n);
    if (row && row.detail) { const s = document.createElement('small'); s.className = 'sp-d'; s.textContent = row.detail; n.append(s); }
    if (row && row.fix && row.state === 4) { const s = document.createElement('small'); s.className = 'sp-fix'; s.textContent = row.fix; n.append(s); }
    const tag = document.createElement('span');
    if (c.stop) { tag.className = 'stop'; tag.textContent = H.t('aegis.stop'); } else if (c.warn) { tag.className = 'warnonly'; tag.textContent = H.t('aegis.warnOnly'); }
    li.append(st, n, tag); ul.append(li);
  });
  renderHero();
  return true;
};
window.spHostRenderAegisLog = () => {
  const ul = document.getElementById('aegisLog'); if (!ul) return true;
  ul.textContent = '';
  const lines = (aegisData && aegisData.events) || [];
  const add = (time, text, none) => {
    const li = document.createElement('li'); if (none) li.className = 'none';   /* .log li.none: never one character per line */
    const tm = document.createElement('time'); tm.className = 'num'; tm.textContent = time;
    const s = document.createElement('span'); s.textContent = text;
    li.append(tm, s); ul.append(li);
  };
  if (!lines.length) add('—', H.t('aegis.noEvents'), true);
  for (const line of lines.slice(0, 50)) { const m = /^(\d\d:\d\d:\d\d)\s+([\s\S]*)$/.exec(line); if (m) add(m[1], m[2]); else add('', line); }
  return true;
};
function renderHero(){
  const d = aegisData; if (!d) return;
  const hero = document.querySelector('#d-aegis .aegis-hero'); if (!hero) return;
  const state = hero.querySelector('.state'), sub = hero.querySelector('.state-sub'), meta = hero.querySelector('.state-meta');
  if (state) state.textContent = d.state === 'off' ? H.t('aegis.off') : H.t('aegis.state');
  if (sub) sub.textContent = H.t({ idle:'aegis.idle', watching:'aegis.watching', kicked:'aegis.kicked' }[d.state] || 'aegis.off', { d:d.detected || 0, k:d.kicked || 0 });
  const defs = d.defs || {};
  const line = H.t('aegis.defs' + (defs.sig === 0 ? 0 : defs.sig === 2 ? 2 : 1), { v:defs.version || 0, r:d.rules || 26 });
  if (meta) meta.textContent = d.summary ? d.summary + ' · ' + line : line;
  /* something serious found: the same red the state lamps use, taken from the page's own token so there is one red */
  const found = d.serious > 0;
  const color = found ? (cssVar('--danger-dot') || '#E63946') : (DOT[d.state] || '#8E93A8');
  const shield = hero.querySelector('.shield-big path'); if (shield) shield.setAttribute('fill', color);
  const dot = document.querySelector('.rail-btn[data-open="d-aegis"] .aegis-dot'); if (dot) dot.style.background = color;
  /* the lamp says it by colour alone, so the words beside it say it too: a screen reader reads them, and in Windows
     high contrast a colour is the system's to choose. data-i18n so a change of language redraws them. */
  const dotText = document.getElementById('aegisDotText');
  if (dotText) { dotText.dataset.i18n = found ? 'aegis.dotFound' : 'aegis.dotOk'; dotText.textContent = H.t(dotText.dataset.i18n); }
}

/* language: the app decides (settings.json -> launcher-state.json -> Windows); the Settings list sends changes back */
window.spHostLangPref = p => {
  if (applyingLang) return;
  H.bridge.invoke('setLang', { lang:p }).then(r => { if (r && !r.ok && r.error) H.toast(String(r.error)); });
};

/* ---------- events from the app ---------- */
const on = (name, f) => document.addEventListener('host:' + name, e => { try { f(e.detail || {}); } catch (err) { console.error(err); } });

on('shell', d => {
  if (d.version) appVer = String(d.version);
  resetAppSide();
  if (d.close === 'tray' || d.close === 'quit') { H.prefs.close = d.close; H.savePrefs(); H.refreshSettings(); }
  /* 起動するゲーム: the app's settings.json wins over this page's own storage */
  if (d.startGame === 'pocketroles' || d.startGame === 'vanilla') { H.prefs.startGame = d.startGame; H.savePrefs(); H.refreshSettings(); H.renderPlay(); }
  /* v0.3: "start with Windows" is the registry, not this page's storage */
  if (typeof d.autostart === 'boolean' && H.prefs.autostart !== d.autostart) { H.prefs.autostart = d.autostart; H.savePrefs(); H.refreshSettings(); }
  /* v1.1: the start scan's card on every start (Settings → 全般 → 起動時の動作): the app's settings.json wins over this
     page's storage, like startGame. The card itself and the panel that opens for a red row are the app's (ClientApp). */
  if (typeof d.startScan === 'boolean' && H.prefs.startScan !== d.startScan) { H.prefs.startScan = d.startScan; H.savePrefs(); H.refreshSettings(); }
  /* v1.2: 音（Aegis が見つけた時の 1 つ）の入切と音量。アプリの settings.json が勝つ（startScan と同じ）。アプリは両方を
     「shell」で、起動時のスキャンの結果を運ぶ最初の「aegis」より前に送ってくるので、鳴る時にはもう本当の値になっている */
  if (typeof d.sound === 'boolean' && H.prefs.sound !== d.sound) { H.prefs.sound = d.sound; H.savePrefs(); H.refreshSettings(); }
  if (Number.isInteger(d.volume) && d.volume >= 0 && d.volume <= 100 && H.prefs.volume !== d.volume) { H.prefs.volume = d.volume; H.savePrefs(); H.refreshSettings(); }
  /* v1.3: プロフィールの絵。"avatar" はアプリの写し（data: の PNG、256×256）か null で、ページが 4 か所に一度に描く。avatarError は
     ディスクの写しが使えなかった理由（元の絵に戻っていて、理由は 1 回だけ言う）。"profile"（name, avatar 0-5）は settings.json のもので、
     startGame と同じくページの保存に勝つ。アプリにまだ無い時はページが自分の分を渡す */
  if (H.profile && Object.hasOwn(d, 'avatar')) H.profile.setCustom(typeof d.avatar === 'string' && d.avatar.startsWith('data:image/png;base64,') ? d.avatar : null);
  if (d.avatarError) H.toast(String(d.avatarError));
  if (H.profile) H.profile.applyRemote(d.profile && typeof d.profile === 'object' ? d.profile : null);
  /* v1.1: the developer switch. devFolder is null on every PC but the one where the working copy of the mod was found,
     and then the block is not drawn at all; the setting (devOn) is the app's, like startGame. "mode" is what this run
     really is - the chip and the developer rows follow that. */
  applyMode(d.mode);
  if (Object.hasOwn(d, 'devFolder')) {
    devInfo.available = !!d.devFolder;
    devInfo.folder = d.devFolder ? String(d.devFolder) : '';
    if (typeof d.devOn === 'boolean') { devInfo.on = d.devOn; if (H.prefs.devBuild !== d.devOn) { H.prefs.devBuild = d.devOn; H.savePrefs(); } }
    H.refreshSettings();
  }
  /* v0.3.1: the Client's colour. The app has already put the eleven values on <html> before the first paint, so this
     normally changes nothing on the screen - but WebView2 reloads the page by itself when its render process dies,
     and a page that came back that way is painted by whatever script the app had registered. Putting them on again
     here, EVERY time, is what makes the window always agree with settings.json. */
  if (typeof d.accent === 'string' && (d.accent === 'default' || /^#[0-9A-Fa-f]{6}$/.test(d.accent))) {
    if (H.acc) H.acc.apply(d.accent, d.vars && d.accent !== 'default' ? d.vars : null);
    if (H.prefs.accent !== d.accent) { H.prefs.accent = d.accent; H.savePrefs(); H.refreshSettings(); }
  }
  if (d.dragRegion === false && !dragFallback) {
    dragFallback = true;
    /* older WebView2 runtime: the page asks the app to move the window */
    document.addEventListener('mousedown', e => {
      if (e.button !== 0 || !e.target.closest('.titlebar, .band')) return;
      if (e.target.closest('button, a, input, select, textarea, [data-press], [role="button"]')) return;
      H.bridge.invoke('window.drag');
    });
  }
});
on('lang', d => {
  applyingLang = true;
  try {
    if (d.pref) H.langPref = d.pref;
    if (d.lang && d.lang !== H.lang) H.setLang(d.lang);
    H.refreshSettings();
  } finally { applyingLang = false; }
});
/* v1.0: the library's 状態 table (Steam 版 / MOD 用のコピー / BepInEx / PocketRoles.dll / interop / Steam クライアント).
   The page fills it from its sample manifest every time renderLibrary runs (a sample state, a change of language), and
   in the app the 「見本」 badge is hidden - so the sample numbers would pass for the person's own PC. Here the app's
   "status" event is kept and written over the table after every renderLibrary (the page's spHostAfterRenderLibrary
   hook). A field the app did not send is 「—」, never a guess. Outside the app nothing here runs: the prototype keeps
   its sample. What "status" carries (ClientApp.StatusData / InstallStatus.cs): steamFound (the Steam copy's folder was
   found), steamVersion (its game version, null when it could not be read), copyVersion (the game version inside the
   mod copy, null when there is no copy), bepinex (BepInEx.Core.dll's ProductVersion, null when missing) + bepinexOk (it
   is the one this app installs), dll (PocketRoles.dll's version, null when missing), interop
   (BepInEx\interop\Assembly-CSharp.dll is there), steamRunning (steam.exe runs). */
let libStatus = null;
function kvText(id, text, ok){
  const el = document.getElementById(id); if (!el) return null;
  el.textContent = text;
  el.classList.toggle('ok-t', !!ok);
  return el;
}
function renderLibraryLive(){
  const d = libStatus;
  if (!d) {
    /* sp-live before the first status (the Aegis event can come first): dashes, never the sample */
    if (document.documentElement.classList.contains('sp-live')) {
      for (const id of ['kvSteam', 'kvCopy', 'kvBep', 'kvDll', 'kvInterop', 'kvClient']) { const el = kvText(id, '—', false); if (el) delete el.dataset.i18n; }
    }
    return;
  }
  const str = v => (v == null || v === '') ? '' : String(v);
  /* Steam 版: the folder was not found at all, or its version could not be read */
  kvText('kvSteam', d.steamFound === false ? H.t('lib.notFound') : (str(d.steamVersion) || '—'), false);
  /* MOD 用のコピー: the game version inside the copy (null: no copy) */
  kvText('kvCopy', str(d.copyVersion) || '—', false);
  /* BepInEx: its version and whether it is the one this app installs; the "+commit" part of the ProductVersion is left off */
  const bep = kvText('kvBep', '', false);
  if (bep) {
    const v = str(d.bepinex).split('+')[0];
    if (!v) bep.textContent = '—';
    else {
      bep.textContent = v + ' · ';
      const s = document.createElement('span');
      if (d.bepinexOk === true) { s.className = 'ok-t'; s.textContent = 'OK'; }
      else { s.className = 'sp-kv-warn'; s.textContent = H.t('lib.bepOther'); }
      bep.append(s);
    }
  }
  /* PocketRoles.dll: the version in the file (null: no file) */
  const dll = str(d.dll);
  const dllEl = kvText('kvDll', dll ? (/^v/i.test(dll) ? dll : 'v' + dll) : '—', false);
  /* v1.1: and which one it is - the release this app installed, the author's own build, or something else's (the app's
     words; the release and the developer build share this one copy on the author's PC, so it has to be said) */
  if (dllEl && d.dllOriginText) { const o = document.createElement('small'); o.className = 'sp-origin'; o.textContent = String(d.dllOriginText); dllEl.append(o); }
  /* interop / Steam クライアント: yes / no, or a dash when the app did not say. data-i18n stays on the Steam row so the
     page's own language pass writes the same word (the same trick as aegisDotText) */
  if (typeof d.interop === 'boolean') kvText('kvInterop', H.t(d.interop ? 'lib.present' : 'lib.absent'), d.interop);
  else kvText('kvInterop', '—', false);
  const cl = kvText('kvClient', '—', false);
  if (cl) {
    if (typeof d.steamRunning === 'boolean') { cl.dataset.i18n = d.steamRunning ? 'lib.running' : 'lib.notRunning'; cl.textContent = H.t(cl.dataset.i18n); cl.classList.toggle('ok-t', d.steamRunning); }
    else delete cl.dataset.i18n;
  }
}
window.spHostAfterRenderLibrary = () => { renderLibraryLive(); };
/* registered before the page's own status handler below: by the time that one calls setSampleState (which re-runs
   renderLibrary), the real values are already here */
on('status', d => { libStatus = d; renderLibraryLive(); });

on('status', d => {
  lastWarn = d.warn || '';
  gameRunning = !!d.gameRunning;
  if (d.pstate && H.PSTATE[d.pstate]) hostPstate = d.pstate;
  if (gameRunning) H.plainStarted();
  /* after launchVanilla PLAY waits in 「プレイ中」 until the game runs (Steam can be slow): "not running yet" does not end it */
  /* v1.0: the DLL is not the one 「更新を確認」 looked at any more (updated - here or by the old launcher - or removed): that answer is stale */
  if (updateAvail && (!d.installed || (d.dll || '') !== updateAvail.dll)) updateAvail = null;
  lastDll = d.dll || ''; lastStatus = d;
  /* v1.1: the mode this run is in, and what is in the copy now (Settings → PocketRoles → 開発 shows it too) */
  applyMode(d.mode);
  if (Object.hasOwn(d, 'dllOriginText')) { const o = d.dllOriginText ? String(d.dllOriginText) : ''; if (o !== devInfo.origin) { devInfo.origin = o; H.refreshSettings(); } }
  if (!H.busy) { if (hostPstate) H.pstate = livePstate(); if (!H.plainHolding) H.running = gameRunning; }
  applyRealState(d);
  applyLivePaths(d);   /* the real versions first: renderPlay then writes them under the play button */
  H.renderPlay();
});

/* v1.0: the page ships SAMPLE numbers (「未インストール · 約 1.0 GB」, 「v0.5.5 · …」, a made-up maintenance time).
   In the app they have to be what the app actually found, or the window tells the person things about their own PC
   that are not true. The app already sends all of it in "status"; until now only three fields were used. */
let liveOn = false;
/* v1.0: Settings → Among Us の場所. The prototype writes its sample path there (INST.path + 「（見本）」). In the app the box
   shows the folder itself when the "status" event carries it (steamDir / modDir - ClientApp.StatusData does not send
   them yet), and until then only what is true: found, not found, or nothing - never a folder it made up. Settings is
   rebuilt every time it opens, so this is put back whenever the row appears again (the same observer as the log size). */
let livePaths = null;
function applyLivePaths(d){
  livePaths = { steamFound: typeof d.steamFound === 'boolean' ? d.steamFound : null, steamDir: d.steamDir ? String(d.steamDir) : '', modDir: d.modDir ? String(d.modDir) : '' };
  applyPaths();
}
function applyPaths(){
  if (!livePaths) return;
  const p = document.getElementById('set-place-path');
  if (p) {
    const want = livePaths.steamDir || (livePaths.steamFound === false ? H.t('lib.notFound') : livePaths.steamFound ? H.t('v10.steamFound') : '');
    if (p.textContent !== want) p.textContent = want;   /* only on a change: the observer below must not loop */
  }
  const c = document.getElementById('set-copy-path');
  if (c && c.tagName === 'CODE' && c.textContent !== livePaths.modDir) c.textContent = livePaths.modDir;
}
function applyRealState(d){
  if (!liveOn) {
    liveOn = true;
    document.documentElement.classList.add('sp-live');
    const mc = document.getElementById('maintChip'), mb = mc && mc.closest('button');
    if (mb) mb.hidden = true;   /* the sample maintenance window: there is no real one to show yet */
  }
  /* the versions the page writes into its own texts (the line under the play button, the MOD card, the library's rows):
     the installed PocketRoles.dll, the Steam game and the mod copy, as the app found them (ClientApp.StatusData). The
     status event has no "latest", so none is given and the page names none (verVars in index.html shows "—" for a
     value it was not given, never the prototype's sample). */
  H.bridge.versions = { dll:d.dll || '', steam:d.steamVersion || '', copy:d.copyVersion || '', latest:'' };
  /* the MOD card, the rail and the library all follow the page's own state, so give it the real one (this redraws them
     with the versions above; the play line is drawn by renderPlay, which the "status" handler calls right after) */
  /* the app's status never says "update" (LaunchStatus has no such state): 「更新を確認」 found one → updateAvail (above) */
  if (H.setSampleState) H.setSampleState(!d.installed ? 'none' : updateAvail ? 'update' : 'latest');
}
on('game', d => {
  gameRunning = !!d.running;
  if (gameRunning) H.plainStarted();
  if (!H.busy && !H.plainHolding) { H.running = gameRunning; H.renderPlay(); }
});
/* v1.2: 音は 1 つ、Aegis が見つけた時（持ち主 2026-09-24）。"aegis" イベントの scan（終わったスキャンの通し番号、AegisService）が
   進み、その結果が赤（serious > 0）だった時に 1 回だけ鳴らす。見張りの 1 秒ごとの更新は同じ番号を何度も運んでくるので、番号で
   「新しい結果」を見分ける。

   **最初の 1 通では鳴らさない**（aegisFirst）。最初のイベントは「今どうなっているか」の知らせで、起動時のスキャンは窓が出る前に
   終わっていることがある（ClientApp.FirstShow）。ここで鳴らすと、実際には**窓が出た時に鳴る＝起動音**になってしまう。
   持ち主は 2026-09-24 に「起動の音とかは要らない。アンチチートで見つかった時だけやる」と決めました。
   WebView2 がページを自分で読み直した時も、これで昔の赤い結果を鳴らし直しません。
   起動時のスキャンで見つかった分は、窓が出た時にカードで知らせています（ClientApp.ShowWaitingStartCard）ので、取りこぼしません。
   鳴らす本体は index.html の spSound（入切と音量はそちらが見る） */
let lastScan = -1;
let aegisFirst = true;
on('aegis', d => {
  aegisData = d;
  document.documentElement.classList.add('sp-live');
  H.renderChecks(); H.renderAegisLog();
  const scan = Number.isInteger(d.scan) ? d.scan : -1;
  if (scan > lastScan) {
    if (!aegisFirst && scan > 0 && d.phase === 'done' && (d.serious || 0) > 0 && window.spSound) { try { window.spSound.play('found'); } catch (err) {} }
    lastScan = scan;
  }
  aegisFirst = false;
});
on('progress', d => {
  if (d.task !== 'rescan' && d.task !== 'scanOnly') return;   /* prelaunch: the play button's own handler */
  const li = document.querySelectorAll('#checks li')[(d.step || 0) - 1];
  if (li) li.className = 'run';
});
on('nav', d => {
  if (d.open === 'd-aegis') { H.closeAll(); H.openLayer('d-aegis'); }
  /* the tray's "play": the page's PLAY flow (PocketRoles, or plain Among Us when Settings → 起動するゲーム says so) */
  if (d.run === 'play' || d.run === 'launch') H.run('play');
  /* the tray's "scan again": the panel opens and the page sends aegis.rescan once (rows, then "scan finished" or busy) */
  if (d.run === 'aegis.rescan') H.run('aegis.rescan');
});
/* v1.2: 優しく閉じる（持ち主 2026-09-24 「× 押したときの挙動が落ちた感じ」）。窓を薄くするのはアプリ側（MainForm.FadeOut / CloseFade.cs）。ここは「ほんの少し縮む」だけ。アプリはこの返事を待たない（ページが固まっていても窓は消える）。「動きを減らす」（Windows のアニメーション効果 OFF ＝ prefers-reduced-motion）なら何もしない。窓が戻る時（visible:true）に外す。 */
on('window', d => {
  const app = document.getElementById('app');
  if (!app) return;
  if (d.closing === true) {
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    const ms = Math.min(160, Math.max(0, Math.round(Number(d.ms) || 0)));
    if (!ms) return;
    app.style.setProperty('--sp-leave-ms', ms + 'ms');
    app.classList.add('sp-leave');
  } else if (d.visible === true) {
    app.classList.remove('sp-leave');
    app.style.removeProperty('--sp-leave-ms');
  }
});

/* ---------- v0.3: the report zip, the shortcut button and the uninstall ----------
   All three answer with words the APP wrote (one set of texts in three languages, the same as the launcher's), so this
   file only places them. The confirm layer of the page (#m-confirm) is reused, with a checkbox added for the uninstall.
   The dialog is opened directly (not through the page's confirmDlg), so the page's own "yes" handler finds nothing to
   run and only closes the layer; the handler below does the work. */
const dlg = { layer:document.getElementById('m-confirm'), title:document.getElementById('cfTitle'), body:document.getElementById('cfBody'), yes:document.getElementById('cfYes') };
const dlgCancel = dlg.layer ? dlg.layer.querySelector('.dlg-acts [data-close]') : null;
let dlgThen = null, dlgExtra = null, dlgShown = null;

/* nothing of a dialog that is gone may survive: not what it was going to run, not the checkbox it added, and not a
   「やめる」 button another question of the page still needs.
   Without this, cancelling the uninstall and then pressing "yes" in ANOTHER question of the page ran the uninstall. */
function clearDialog(){
  dlgThen = null; dlgShown = null;
  if (dlgExtra) { dlgExtra.remove(); dlgExtra = null; }
  if (dlgCancel) dlgCancel.hidden = false;
}

function showDialog({ title, body, yes, danger = true, extra = null, then = null, onlyClose = false }){
  if (!dlg.title) return;
  clearDialog();
  dlg.title.textContent = title || '';
  dlg.body.textContent = body || '';
  dlg.yes.textContent = yes || '';
  dlg.yes.classList.toggle('danger', !!danger);
  /* a dialog that only tells you something has nothing to cancel: 「やめる」 beside 「閉じる」 reads as a choice */
  if (dlgCancel) dlgCancel.hidden = !!onlyClose;
  if (extra) { dlgExtra = extra; dlg.body.after(extra); }
  dlgThen = then;
  dlgShown = { title:dlg.title.textContent, body:dlg.body.textContent, yes:dlg.yes.textContent };
  H.closeAll(); H.openLayer('m-confirm');
  requestAnimationFrame(() => dlg.yes.focus({ preventScroll:true }));
}

/* the dialog on screen is still the one this file opened (the page reuses #m-confirm for its own questions) */
function ours(){
  return !!dlgShown && dlg.title.textContent === dlgShown.title && dlg.body.textContent === dlgShown.body && dlg.yes.textContent === dlgShown.yes;
}
if (dlg.yes) dlg.yes.addEventListener('click', () => {
  if (!ours()) { clearDialog(); return; }   /* someone else's question: the page's own handler answers it */
  /* the row this dialog added (the uninstall's checkbox) is taken off the page now but handed to the action, so what
     the person ticked is read from the row they actually saw and not looked up by id afterwards */
  const f = dlgThen, extra = dlgExtra;
  clearDialog();
  if (f) setTimeout(() => f(extra), 0);
}, true);
if (dlg.layer) {
  /* 「やめる」 and the scrim */
  dlg.layer.addEventListener('click', e => { if (e.target.closest && e.target.closest('[data-close]')) clearDialog(); }, true);
  /* and anything else that closes or covers the layer (Escape, closeAll, another layer on top) */
  new MutationObserver(() => { if (dlg.layer.hidden || dlg.layer.inert) clearDialog(); })
    .observe(dlg.layer, { attributes:true, attributeFilter:['hidden', 'inert'] });
}
document.addEventListener('keydown', e => { if (e.key === 'Escape') clearDialog(); }, true);

/* a checkbox row for the uninstall dialog, in the page's own settings style */
function checkboxRow(id, label){
  const wrap = document.createElement('label');
  wrap.className = 'sp-dlg-check';
  wrap.style.cssText = 'display:flex;gap:8px;align-items:flex-start;margin:12px 0 0;font-size:13px;line-height:1.5;cursor:pointer';
  const box = document.createElement('input');
  box.type = 'checkbox'; box.id = id; box.style.cssText = 'margin-top:2px;flex:none';
  const text = document.createElement('span'); text.textContent = label;
  wrap.append(box, text);
  return wrap;
}

/* an answer that is not "ok": the notice spHostReport has for it, else the page's own words for busy / timed out /
   failed. Nothing is ever swallowed - a task that can really fail must not be reported as "still working". */
function failToast(r, cmd){
  if (window.spHostReport(r, H.cmdLabel(cmd))) return;
  H.toast(H.t(r && r.busy ? 'toast.busy' : r && r.timeout ? 'toast.timeout' : 'toast.failed', { x:H.cmdLabel(cmd) }));
}

/* the zip is made, and the words for it. Pulled out of reportFlow because an answer that arrives after the page gave
   up waiting (host:late, below) has to be shown exactly the same way. */
function reportDone(r){
  const d = r.data || {};
  showDialog({
    title:d.title, body:[d.text, d.evidence, d.autoDelete].filter(Boolean).join('\n\n'), yes:d.open, danger:false,
    then:() => H.bridge.invoke('openReportFolder'),
  });
}

/* the report zip / one player's evidence: the app's own words, then "open the folder".
   Both are in LONG_CMDS (index.html), so they are given half an hour rather than fifteen seconds - a 1 GB read is
   slower than that. A timeout after half an hour really is a failure, and is said so.
   While it runs the button is held and the app's own "making it now" line is shown: the app cannot answer until the
   whole job is done, and three silent minutes with a button that still presses look like a button that did nothing. */
async function reportFlow(cmd, args, btn){
  if (btn) { if (btn.disabled) return; btn.disabled = true; btn.setAttribute('aria-busy', 'true'); }
  H.toast(tv(cmd === 'makeReport' ? 'v03.reporting' : 'v03.exporting'));
  let r;
  try { r = await H.bridge.invoke(cmd, args || {}); }
  finally { if (btn) { btn.disabled = false; btn.removeAttribute('aria-busy'); } }
  if (!r || !r.ok) {
    /* a mistyped code is the commonest failure of all, and the app's answer for it is two lines long. A notice that
       fades after three and a half seconds cannot be read twice, and the person has nowhere to look it up: it gets
       the same dialog the zip that worked gets. busy and "no answer" stay as notices - they name no detail. */
    if (r && r.error && !r.unsupported && !r.blocked) {
      showDialog({ title:H.cmdLabel(cmd), body:String(r.error), yes:tv('v03.close'), danger:false, onlyClose:true });
    } else failToast(r, cmd);
    return r;
  }
  reportDone(r);
  return r;
}

/* half an hour went by, the page said "no answer", and then the app answered after all: the zip really is on the
   desktop, so its words are shown rather than dropped (the warning about who it may be sent to is in them) */
document.addEventListener('host:late', e => {
  const d = e.detail || {};
  if (d.cmd !== 'makeReport' && d.cmd !== 'exportOne') return;
  if (d.result && d.result.ok) reportDone(d.result);
});

/* the uninstall: ask the app what it would remove, show it with the checkbox, then do it */
async function uninstallFlow(){
  const plan = await H.bridge.invoke('uninstall', {});
  if (!plan || !plan.ok) { failToast(plan, 'uninstall'); return; }
  const d = plan.data || {};
  const row = checkboxRow('sp-un-mod', d.delModLabel || '');
  showDialog({
    title:d.title, body:[d.text, d.keepsModCopy].filter(Boolean).join('\n\n'), yes:d.yes, extra:row,
    then:async extra => {
      const box = extra ? extra.querySelector('input[type="checkbox"]') : null;
      const modCopy = !!(box && box.checked);
      /* the uninstall is the one place "still working" is the truth: the app answers, then closes and deletes its own
         folder, so a silent half hour means it is gone, not that it failed */
      const done = await H.bridge.invoke('uninstall', { confirm:true, modCopy });
      if (done && done.timeout) { H.toast(tv('v03.working')); return; }
      if (!done || !done.ok) { failToast(done, 'uninstall'); return; }
      const r = done.data || {};
      /* the app answers at once and then closes: it does the deleting once this window and WebView2 are gone (a
         running app cannot delete the folder it keeps its own settings and browser profile in). The words are the
         app's; leftText names the folder the person has to remove themselves. */
      H.toast(r.leftText || r.text || '');
    },
  });
}

/* ---------- v0.4: the app's own log page (showLog) and the logs-folder size ----------
   The launcher showed its progress in a box at the bottom of its window and opened a SECOND window for the rest. This
   app has one process and one window, so the log is a page inside it. Everything written on it - the title, what it
   is, "nothing yet", "older lines left out", the size line and its tooltip - is the app's own wording in all three
   languages (Strings.cs lg_log_* and lg_size / lg_tip / lg_big); this file only places it. */
let logLayer = null, logPoll = 0, lastLogs = null;

function logPage(){
  if (logLayer) return logLayer;
  const el = document.createElement('div');
  el.className = 'layer';
  el.id = 'sp-log';
  el.hidden = true;
  const scrim = document.createElement('div'); scrim.className = 'scrim'; scrim.setAttribute('data-close', '');
  const card = document.createElement('div');
  card.className = 'dlg sp-logcard';
  card.setAttribute('data-card', '');
  card.tabIndex = -1;
  card.setAttribute('role', 'dialog');
  card.setAttribute('aria-modal', 'true');
  card.setAttribute('aria-labelledby', 'sp-log-title');
  const h = document.createElement('h2'); h.id = 'sp-log-title';
  const sub = document.createElement('p'); sub.className = 'sp-logsub'; sub.id = 'sp-log-sub';
  const list = document.createElement('ul'); list.className = 'sp-loglines'; list.id = 'sp-log-lines';
  list.tabIndex = 0;   /* a box that scrolls must be reachable by keyboard (WCAG 2.1.1) */
  list.setAttribute('role', 'log');
  list.setAttribute('aria-labelledby', 'sp-log-title');
  const more = document.createElement('p'); more.className = 'sp-logmore'; more.id = 'sp-log-more'; more.hidden = true;
  const foot = document.createElement('p'); foot.className = 'sp-logfoot'; foot.id = 'sp-log-foot';
  const acts = document.createElement('div'); acts.className = 'dlg-acts';
  const close = document.createElement('button'); close.id = 'sp-log-close'; close.setAttribute('data-close', '');
  close.textContent = tv('v03.close');
  acts.append(close);
  card.append(h, sub, list, more, foot, acts);
  el.append(scrim, card);
  document.body.append(el);
  logLayer = el;
  /* the page stops asking the app for the log as soon as it is off the screen */
  new MutationObserver(() => { if (el.hidden || el.inert) stopLogPoll(); }).observe(el, { attributes:true, attributeFilter:['hidden', 'inert'] });
  return el;
}

function stopLogPoll(){ if (logPoll) { clearInterval(logPoll); logPoll = 0; } }

/* what the app answered, drawn. Missing pieces simply do not appear: an app that answers a bare {ok:true} (an older
   build, or the test harness) still gets a page with a title and no made-up content. */
function drawLog(d){
  const el = logPage();
  d = d || {};
  document.getElementById('sp-log-title').textContent = d.title || tv('v04.logTitle');
  const sub = document.getElementById('sp-log-sub');
  sub.textContent = d.sub || '';
  sub.hidden = !d.sub;
  const list = document.getElementById('sp-log-lines');
  const wasAtEnd = list.scrollHeight - list.scrollTop - list.clientHeight < 24;
  list.textContent = '';
  const lines = Array.isArray(d.lines) ? d.lines : [];
  if (!lines.length) {
    const li = document.createElement('li'); li.className = 'none';   /* .sp-loglines li.none: never one character per line */
    const s = document.createElement('span'); s.textContent = d.none || tv('v04.logNone');
    li.append(s); list.append(li);
  }
  for (const line of lines) {
    const li = document.createElement('li');
    const m = /^\[(\d\d:\d\d:\d\d)\]\s?([\s\S]*)$/.exec(String(line));
    if (m) { const tm = document.createElement('time'); tm.textContent = m[1]; li.append(tm); }
    const s = document.createElement('span'); s.textContent = m ? m[2] : String(line);
    li.append(s); list.append(li);
  }
  /* the newest line is at the bottom, so that is where the page sits - unless the reader has scrolled up */
  if (wasAtEnd) list.scrollTop = list.scrollHeight;
  const more = document.getElementById('sp-log-more');
  more.textContent = d.more || '';
  more.hidden = !d.more;
  const foot = document.getElementById('sp-log-foot');
  foot.textContent = '';
  if (d.path) { const c = document.createElement('code'); c.textContent = String(d.path); foot.append(c); }
  if (d.logs) { lastLogs = d.logs; foot.append(sizeSpan(d.logs)); applyLogSize(); }
  const close = document.getElementById('sp-log-close');
  close.textContent = d.close || tv('v03.close');
}

/* 「ログ: 123 MB」 with the app's tooltip (lg_tip, or the 2 GB line past 2 GB) */
function sizeSpan(logs){
  const s = document.createElement('span');
  s.className = 'sp-logsize';
  s.textContent = logs.text || '';
  if (logs.tip) s.title = logs.tip;
  s.dataset.big = logs.big ? '1' : '0';
  return s;
}

/* the same line beside 「ログのフォルダを開く」 in Settings → PocketRoles. The settings tree is rebuilt every time the
   sheet opens, so this is put back whenever the row appears again. */
function applyLogSize(){
  if (!lastLogs) return;
  const row = document.getElementById('tool-openLogsFolder');
  if (!row) return;
  const label = row.closest('.tool') && row.closest('.tool').querySelector('b');
  if (!label) return;
  let s = label.querySelector('.sp-logsize');
  const want = lastLogs.text || '';
  const big = lastLogs.big ? '1' : '0';
  if (s && s.textContent === want && s.dataset.big === big && s.title === (lastLogs.tip || '')) return;   /* nothing changed: no loop */
  if (!s) { s = sizeSpan(lastLogs); label.append(s); return; }
  s.textContent = want;
  s.title = lastLogs.tip || '';
  s.dataset.big = big;
}

async function askLog(){
  const r = await H.bridge.invoke('showLog');
  if (!r || !r.ok) return r;
  drawLog(r.data || {});
  return r;
}

async function openLogPage(){
  const el = logPage();
  drawLog({});                    /* the frame is on screen at once, even on a PC where reading the log is slow */
  H.closeAll();
  H.openLayer('sp-log');
  const r = await askLog();
  if (!r || !r.ok) { failToast(r, 'showLog'); return; }
  /* the log page is the one place that is NOT held while a long task runs (Bridge.BusyGated), because watching a long
     task go by is what it is for: while it is open it asks again every two seconds */
  stopLogPoll();
  logPoll = setInterval(() => { if (el.hidden || el.inert) { stopLogPoll(); return; } askLog(); }, 2000);
}

/* the app tells the page the size whenever it works out the status (ps1: the end of Refresh-Status) */
on('logs', d => { lastLogs = d || null; applyLogSize(); });

/* Settings is rebuilt from scratch every time it opens, so the size line is put back when the row reappears */
(() => {
  const body = document.getElementById('setBody');
  if (!body) return;
  new MutationObserver(() => { applyLogSize(); applyPaths(); }).observe(body, { childList:true, subtree:true });
})();

/* the settings buttons: taken over before the prototype's own handler sees the click */
document.addEventListener('click', e => {
  const btn = e.target.closest && e.target.closest('button[data-cmd]');
  if (!btn) return;
  const cmd = btn.getAttribute('data-cmd');
  if (cmd === 'uninstall') { e.preventDefault(); e.stopPropagation(); uninstallFlow(); return; }
  /* v0.4: the log is a page in this window, not a second one */
  if (cmd === 'showLog') { e.preventDefault(); e.stopPropagation(); openLogPage(); return; }
  if (cmd === 'makeReport') { e.preventDefault(); e.stopPropagation(); reportFlow('makeReport', {}, btn); return; }
  /* ひとり分の証拠: the box beside the button says whose. Whatever was typed goes straight to the app, even when it is
     empty or the wrong shape: the app already has the words for that (ex_bad / ex_none) in all three languages, so
     they are written in ONE place and this file never has to guess what a code looks like. */
  if (cmd === 'exportOne') {
    e.preventDefault(); e.stopPropagation();
    const box = btn.dataset.ask ? document.getElementById(btn.dataset.ask) : null;
    reportFlow('exportOne', { who:(box && box.value || '').trim() }, btn);
    return;
  }
  if (cmd === 'shortcut.create') {
    e.preventDefault(); e.stopPropagation();
    H.bridge.invoke('shortcut.create', { where:'desktop' }).then(r => {
      if (r && r.ok) H.toast(tv('v03.shortcut'));
      else window.spHostReport(r, H.cmdLabel('shortcut.create'));
    });
  }
}, true);

/* ---------- start: the new state and texts, then everything drawn once in the current language ---------- */
H.PSTATE.blocked = { icon:'i-shield', dot:'red', btn:'ps.b.blocked', noSel:true };
/* v0.2: the app's "repair" state. task:'install' on purpose - the page sends the same 'install' invoke with no mode and
   the app decides "repair" or "repairMod" from the status it last sent (one decision, in one place: ClientApp.InstallMode).
   noSel: it is not one of the prototype's preview states. */
H.PSTATE.repair = { icon:'i-download-tray', dot:'orange', btn:'ps.b.repair', task:'install', noSel:true };
/* the size / time estimate under the button comes from the prototype's sample title list, not from the app: until the
   signed title manifest is there, the button shows no made-up MB (PORT-MAP 1, "later") */
delete H.PSTATE.install.sub; delete H.PSTATE.needGame.sub; delete H.PSTATE.update.sub;
for (const l of Object.keys(X)) if (H.T[l]) Object.assign(H.T[l], X[l]);
markSoon(document);   /* v1.0: before the first redraw below, so the swapped labels come out in the right language */
H.setLang(H.lang);
})();
