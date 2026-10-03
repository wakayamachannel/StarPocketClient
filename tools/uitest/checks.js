/* tools\uitest\checks.js - what the UI test looks at, inside the page itself.
   Run by tools\uitest\run.ps1 in a headless Edge (muted, throwaway profile, no network). No window ever opens.

   The page is loaded exactly the way the app loads it: the eleven --acc-* values are put on <html> before the
   document's own script runs (the same first-paint injection MainForm does), and window.chrome.webview is stubbed, so
   ui\host-v01.js runs too. Nothing here reads a screenshot: every colour check is a measured contrast ratio taken from
   the browser's own computed styles.

   window.__spChecks(group) returns [{ n, ok, why }] - group 'base' is everything that was already true before the
   recolour, 'all' adds what the recolour and the two new rows brought.
   SPDX-License-Identifier: GPL-3.0-or-later */
(() => {
'use strict';
const H = window.spHost, SR = window.spReview;

/* ---------- measuring ---------- */
/* a colour as the browser gives it back: rgb()/rgba() from a computed style, or #RRGGBB from a custom property */
const rgb = s => {
  s = (s || '').trim();
  const h = /^#([0-9a-fA-F]{6})$/.exec(s);
  if (h) return [1, 3, 5].map(i => parseInt(h[1].substr(i - 1, 2), 16)).concat([1]);
  const m = /rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)(?:[,/\s]+([\d.]+))?/.exec(s);
  return m ? [+m[1], +m[2], +m[3], m[4] === undefined ? 1 : +m[4]] : null;
};
const chan = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
const lum = c => 0.2126 * chan(c[0]) + 0.7152 * chan(c[1]) + 0.0722 * chan(c[2]);
const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const over = (fg, bg) => fg[3] >= 1 ? fg : [0, 1, 2].map(i => Math.round(fg[i] * fg[3] + bg[i] * (1 - fg[3])));
/* what is really behind an element: the first painted background up the tree, with any see-through layers mixed in */
function bgOf(el){
  const stack = [];
  for (let e = el; e; e = e.parentElement) {
    const c = rgb(getComputedStyle(e).backgroundColor);
    if (!c || c[3] === 0) continue;
    stack.push(c);
    if (c[3] >= 1) break;
  }
  let out = stack.length && stack[stack.length - 1][3] >= 1 ? stack.pop() : [255, 255, 255, 1];
  while (stack.length) out = over(stack.pop(), out);
  return out;
}
/* a gradient's own stops, so the PLAY button is measured at its lightest and its darkest, not at some average */
function gradientStops(el){
  const bi = getComputedStyle(el).backgroundImage, out = [];
  for (const m of bi.matchAll(/rgba?\([^)]*\)/g)) { const c = rgb(m[0]); if (c) out.push(c); }
  return out;
}
/* every surface an element's own text sits on: the stops of its gradient, or, when it has none, what is behind it */
const fillsOf = el => { const g = gradientStops(el); return g.length ? g : [bgOf(el)]; };
/* the worst (lowest) contrast between one colour and all of them */
const worstOn = (c, fills) => fills.reduce((w, f) => Math.min(w, ratio(c, f)), 99);
const textOn = el => { const c = rgb(getComputedStyle(el).color); return c ? over(c, bgOf(el)) : null; };
const varOf = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const hexOf = name => { const c = rgb(varOf(name)) || null; return c ? '#' + c.slice(0, 3).map(v => v.toString(16).toUpperCase().padStart(2, '0')).join('') : varOf(name).toUpperCase(); };
const R2 = v => Math.round(v * 100) / 100;

/* the reds that must never come back in an accent role */
const RIOT = ['#E63946', '#B02230', '#F35A67', '#C42A38', '#D4303E', '#C22B3A', '#FF6B78', '#FF8A93'];

/* What the browser really does to a colour under a CSS filter. Chromium runs the page's filters and a canvas's
   through the same pipeline, so this is a measurement, not a model of one - it is how the hover state of PLAY is
   checked below rather than taken on trust. */
const shot = document.createElement('canvas');
shot.width = shot.height = 4;
const shotCx = shot.getContext('2d', { willReadFrequently: true });
function underFilter(c, filter){
  if (!filter || filter === 'none') return c;
  shotCx.filter = 'none';
  shotCx.fillStyle = '#FFFFFF'; shotCx.fillRect(0, 0, 4, 4);   /* an opaque backdrop: the button has one too */
  shotCx.filter = filter;
  shotCx.fillStyle = 'rgb(' + c[0] + ',' + c[1] + ',' + c[2] + ')';
  shotCx.fillRect(0, 0, 4, 4);
  shotCx.filter = 'none';
  const d = shotCx.getImageData(2, 2, 1, 1).data;
  return [d[0], d[1], d[2], 1];
}
/* every declaration a rule makes, by selector (there may be more than one rule for it). A value written with var()
   comes back empty from the CSSOM, so ruleText gives the rule as it was written when the value itself matters. */
function ruleProps(selector){
  const out = [];
  for (const s of document.styleSheets) {
    let rules; try { rules = s.cssRules; } catch (e) { continue; }
    for (const r of rules) if (r.selectorText === selector) for (const p of r.style) out.push([p, r.style.getPropertyValue(p)]);
  }
  return out;
}
function ruleText(selector){
  const out = [];
  for (const s of document.styleSheets) {
    let rules; try { rules = s.cssRules; } catch (e) { continue; }
    for (const r of rules) if (r.selectorText === selector) out.push(r.cssText);
  }
  return out.join('\n');
}

/* the same table src\SelfTest\AccentSelfTests.cs measures the app's own copy against. If the two ever drift apart,
   the one that moved fails its own suite and says which value changed. */
const TABLE = [
  ['#F7C548', '#F7C548', '#F9D271', '#F6BC2D', '#FADB8F', '#1E2257', '#A67907', '#7F5C06', '#F7C548', '#F7C548'],
  ['#2EC4B6', '#2EC4B6', '#45D3C6', '#28A99D', '#5ED9CE', '#1E2257', '#218C82', '#1A7068', '#2EC4B6', '#2EC4B6'],
  ['#4D55C2', '#4D55C2', '#636ACA', '#4D55C2', '#333994', '#FFFFFF', '#4D55C2', '#4D55C2', '#6B72CC', '#8D92D8'],
  ['#8E4EC6', '#8E4EC6', '#9559CA', '#813DBD', '#69329A', '#FFFFFF', '#8E4EC6', '#8642C2', '#9A61CC', '#B387D8'],
  ['#F05A8C', '#F05A8C', '#F481A7', '#F0578A', '#F69DBB', '#1E2257', '#ED3673', '#C4124D', '#F05A8C', '#F16997'],
  ['#2FA56B', '#2FA56B', '#39C681', '#2EA36A', '#50CD90', '#1E2257', '#298F5D', '#206F48', '#2FA56B', '#31AB6F'],
  ['#F4822A', '#F4822A', '#F69950', '#F2710D', '#F7AA6E', '#1E2257', '#CC5F0B', '#A04A08', '#F4822A', '#F4822A'],
  ['#FFFFFF', '#FFFFFF', '#FFFFFF', '#F0F0F0', '#FFFFFF', '#1E2257', '#808080', '#636363', '#FFFFFF', '#FFFFFF'],
  ['#000000', '#616161', '#757575', '#616161', '#3D3D3D', '#FFFFFF', '#000000', '#000000', '#7A7A7A', '#999999'],
  ['#FFFF00', '#FFFF00', '#FFFF29', '#E0E000', '#FFFF47', '#1E2257', '#858500', '#666600', '#FFFF00', '#FFFF00'],
  ['#1E2257', '#4D55C2', '#636ACA', '#4D55C2', '#333994', '#FFFFFF', '#1E2257', '#1E2257', '#6B72CC', '#8D92D8'],
  /* the danger red's own band: never refused, always moved out of it, because that red already means "it failed" */
  ['#E63946', '#E21D6F', '#E21D6F', '#C71A62', '#A31550', '#FFFFFF', '#E63981', '#BE185D', '#E63981', '#ED6EA3'],
  ['#FF0000', '#E6005F', '#E6005F', '#C70052', '#9E0041', '#FFFFFF', '#FA0068', '#C70053', '#FF006A', '#FF5CA0'],
  ['#FF6B78', '#FF6BA9', '#FF94C1', '#FF4D97', '#FFB3D3', '#1E2257', '#FA0069', '#C70053', '#FF6BA9', '#FF6BA9'],
];
const TABLE_KEYS = ['--acc-fill', '--acc-hi', '--acc-deep', '--acc-sub', '--acc-ink',
  '--acc-l-line', '--acc-l-text', '--acc-d-line', '--acc-d-text'];

window.__spChecks = async function(group, mode){
  const out = [], all = group === 'all';
  /* 'hc' is Windows high contrast with "reduce motion" on: colours there are the system's, not ours, so the contrast
     ratios are not ours to measure - what matters is that nothing disappears and nothing moves */
  const hc = mode === 'hc';
  /* the app answers a setting over the message channel, so a change is not finished in the same breath */
  const tick = () => new Promise(r => setTimeout(r, 40));
  const pick = async v => { SR.accent(v); await tick(); };
  const dark = document.documentElement.getAttribute('data-theme') === 'dark'
    || (!document.documentElement.hasAttribute('data-theme') && matchMedia('(prefers-color-scheme: dark)').matches);
  const th = dark ? 'dark' : 'light';
  const add = (n, ok, why) => out.push({ n, ok: !!ok, why: ok ? '' : String(why === undefined ? 'false' : why) });
  const ratioAtLeast = (n, got, want) =>
    add(n + ' (' + R2(got) + ' >= ' + want + ')', typeof got === 'number' && !isNaN(got) && got >= want, R2(got));
  const $ = s => document.querySelector(s);
  /* one check that throws is one failure with a reason, never a run that stops half way through */
  const seen = (sel, fn, n) => {
    const el = $(sel);
    if (!el) { add(n, false, 'no ' + sel); return; }
    try { fn(el); } catch (err) { add(n, false, String(err)); }
  };

  /* ================= base: what was already true before the recolour ================= */
  add('no script error on the page', (window.__errors || []).length === 0, (window.__errors || []).join(' | '));
  add('the page has its own title', document.title.length > 0, document.title);
  add('<html lang> follows the language', document.documentElement.lang.length >= 2, document.documentElement.lang);
  add('the glue script ran (window.spHost)', !!H);
  add('the app is talking to the page (chrome.webview)', !!(window.chrome && window.chrome.webview));
  add('the prototype-only bar is hidden in the app', getComputedStyle($('#protoBar')).display === 'none');
  for (const sel of ['.rail', '.play', '.hero', '#community', '#setTree', '#setBody', '#toast', '#checks', '.launch-row'])
    add('the page still has ' + sel, !!$(sel));
  add('the rail has the title tile', !!$('.tile[data-title]'));
  add('PLAY is a real button with a label', $('.play').tagName === 'BUTTON' && $('.play').textContent.trim().length > 0);
  add('PLAY is still 300 x 72', Math.round($('.play').getBoundingClientRect().width) === 300
    && Math.round($('.play').getBoundingClientRect().height) === 72,
    $('.play').getBoundingClientRect().width + 'x' + $('.play').getBoundingClientRect().height);
  add('the layout is still rail | content | column', getComputedStyle($('.app')).gridTemplateColumns.split(' ').length === 3,
    getComputedStyle($('.app')).gridTemplateColumns);
  add('the settings overlay is closed to start with', $('#m-settings').hidden);
  add('nothing spills sideways', document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1,
    document.documentElement.scrollWidth + ' > ' + document.documentElement.clientWidth);
  add('body text is at least 12 px', parseFloat(getComputedStyle(document.body).fontSize) >= 12);
  /* the words are there in this language */
  for (const k of ['play', 'set.general', 'set.closeCap', 'set.advCap', 'tg.help', 'aegis.stop'])
    add('a word for ' + k, (H.t(k) || '').length > 0);
  add('every switch and button on the page has a name',
    [...document.querySelectorAll('button')].filter(b => b.offsetParent !== null
      && !b.textContent.trim() && !b.getAttribute('aria-label') && !b.getAttribute('title')).length === 0,
    [...document.querySelectorAll('button')].filter(b => b.offsetParent !== null && !b.textContent.trim()
      && !b.getAttribute('aria-label') && !b.getAttribute('title')).map(b => b.className).join(','));
  /* plain readability of the things that were never red */
  seen('.play-status', el => ratioAtLeast('the line under PLAY reads', ratio(textOn(el), bgOf(el)), 4.5), 'the line under PLAY reads');
  /* アプリの中では右の欄の一覧が空（見本の人は出さない）ので要素が無い: 無ければそれで良い */
  if ($('.cname small')) seen('.cname small', el => ratioAtLeast('the small text in the right column reads', ratio(textOn(el), bgOf(el)), 4.5), 'the small text in the right column reads');
  else add('the small text in the right column reads', true, 'the list is empty inside the app');
  /* 2026-10-03（持ち主の写真: 1.0.0 の画面で「まだ記録はありません」が 1 文字ずつ縦に並んだ。配った 1.0.0 の ui\index.html は .cempty が
     表の行（26 px の絵の列）のままだった）: 右の欄の空の文は 1 行の文として横に出る。測るのは**文字の並び**（Range）の箱で、li の箱ではない
     （崩す係 7: li は 236 px あるのに、文字は 26 px の列に入っていた。li の幅を測った点検は 1.0.0 の崩れを見逃した）。文字の並びの幅が
     文字 1 つ分の 3 倍より広く（26 px の列には日本語が 2 文字入るので、2 倍では ja の崩れを見逃した）、行は 2 行まで、表（grid）の行では
     ない。英語は単語が折れないので幅の点検は通ってしまうが、行数と grid の点検が見つける（1.0.0 の形の写しで確かめた）。
     欄は開いた状態で見る（1280 px では開いて始まるが、念のため開く）。 */
  if (SR.panel) SR.panel('open');
  await tick();
  seen('#clist .cempty', el => {
    const r = el.getBoundingClientRect(), cs = getComputedStyle(el);
    const range = document.createRange();
    range.selectNodeContents(el);
    const tr = range.getBoundingClientRect();   /* the box of the letters themselves, wherever the layout put them */
    const probe = document.createElement('span');
    probe.textContent = [...(el.textContent || 'x')][0];
    probe.style.cssText = 'position:absolute;left:-9999px;top:0;visibility:hidden;white-space:nowrap;font:' + cs.font;
    document.body.append(probe);
    const charW = probe.getBoundingClientRect().width;
    probe.remove();
    const lh = parseFloat(cs.lineHeight) || parseFloat(cs.fontSize) * 1.2;
    const lines = tr.height / lh;
    add('右の欄の空の文は横に並ぶ（文字の並びの幅が文字 1 つ分の 3 倍より広い）', tr.width > charW * 3 && charW > 0,
      Math.round(tr.width) + 'px の文字の並び vs 文字 1 つ ' + Math.round(charW) + 'px（li は ' + Math.round(r.width) + 'px）: ' + el.textContent);
    add('右の欄の空の文は 2 行まで（1 文字ずつ縦ではない）', lines > 0 && lines <= 2.5, 'lines ' + lines.toFixed(1) + ' (' + Math.round(tr.height) + 'px)');
    add('右の欄の空の文は表の行ではない（display が grid ではない）', cs.display !== 'grid', cs.display);
    add('右の欄の空の文は、欄の中に見えている', r.width > 0 && r.height > 0 && tr.width > 0 && el.offsetParent !== null, Math.round(r.width) + 'x' + Math.round(r.height));
  }, '右の欄の空の文（.cempty）');
  /* the three-language tables are all there */
  for (const l of ['ja', 'zh', 'en']) add('the ' + l + ' words are loaded', !!(H.T[l] && Object.keys(H.T[l]).length > 50));
  add('the state lamp still has three colours', !!$('.dot'));
  add('the Aegis panel has its rows', document.querySelectorAll('#checks li').length > 0);
  add('the tools list is built from one table', typeof H.cmdLabel === 'function' && H.cmdLabel('makeReport').length > 0);

  /* ================= 2026-09-24（持ち主）: タブの分け方と、動画なし ================= */
  /* 概要は概要、パッチノートはパッチノート。パッチノートのタブでは hero が「プレイの帯」にたたまれ、売り文句とカードは出ない。
     プレイの行だけはどちらにも残り、300 x 72 のまま。切り替えると一番上へ戻り、見出しが最初の画面に入る。
     動画のもの（再生・消音のボタン、「今すぐ見る」、「遊び方の動画」のカード、「動画の代わり」の札）はページに無い。 */
  {
    const view = $('#view-game'), hero = $('#hero');
    const disp = sel => { const el = $(sel); return el ? getComputedStyle(el).display : 'gone'; };
    const heroH = () => Math.round(hero.getBoundingClientRect().height);
    H.closeAll(); H.show('game');
    await tick();
    add('概要: hero は画面 1 枚分の高さ', heroH() >= 460, heroH());
    add('概要: 売り文句とカードが出ている', disp('.hero-copy') !== 'none' && disp('.media-col') !== 'none', disp('.hero-copy') + '/' + disp('.media-col'));
    add('概要: カードは 2 枚（「新しいこと」と「Aegis の仕組み」。動画のカードは無い）', document.querySelectorAll('.media .media-card').length === 2
      && !!$('.media-card[data-cmd="media.new"]') && !!$('.media-card[data-open="d-aegis"]') && !$('[data-cmd="video.howto"]'),
      document.querySelectorAll('.media .media-card').length);
    for (const gone of ['#videoBtn', '#muteBtn', '.vctl', '.scene-tag', '[data-cmd="video.intro"]', '[data-cmd="video.howto"]'])
      add('動画のものが無い: ' + gone, !$(gone));
    view.scrollTop = 400;
    document.getElementById('tab-pn').click();
    await tick();
    add('パッチノート: 概要の中身は出ない（panel-ov は hidden）', $('#panel-ov').hidden && !$('#panel-pn').hidden && view.dataset.tab === 'pn', view.dataset.tab);
    add('パッチノート: 売り文句とカードは消える', disp('.hero-copy') === 'none' && disp('.media-col') === 'none', disp('.hero-copy') + '/' + disp('.media-col'));
    add('パッチノート: hero は「プレイの帯」にたたまれる（300 px 未満）', heroH() > 0 && heroH() < 300, heroH());
    add('パッチノート: プレイの行は残り、300 x 72 のまま', $('.play').offsetParent !== null
      && Math.round($('.play').getBoundingClientRect().width) === 300 && Math.round($('.play').getBoundingClientRect().height) === 72,
      $('.play').getBoundingClientRect().width + 'x' + $('.play').getBoundingClientRect().height);
    add('パッチノート: 切り替えで一番上へ戻る', view.scrollTop === 0, view.scrollTop);
    seen('#panel-pn .notes-head h2', el => {
      const r = el.getBoundingClientRect();
      add('パッチノート: 見出しが最初の画面に入っている', r.top >= 0 && r.bottom <= view.clientHeight, Math.round(r.top) + '..' + Math.round(r.bottom) + ' of ' + view.clientHeight);
    }, 'パッチノート: 見出しが最初の画面に入っている');
    document.getElementById('tab-ov').click();
    await tick();
    add('概要に戻る: 売り文句とカードが戻り、hero も元の高さ', !$('#panel-ov').hidden && disp('.hero-copy') !== 'none' && heroH() >= 460, disp('.hero-copy') + '/' + heroH());
    /* パッチノートを開くと「読んだ」印（sp.seenNotes）が付いて左の NEW の札が消える。後の検査（the NEW badge reads）はその札を
       見るので、読む前の状態へ戻す（localStorage は次の言語の回にも残るため） */
    try { localStorage.removeItem('sp.seenNotes'); } catch (e) {}
    $('#notesDot').hidden = false;
    H.renderTitles();
    /* 3 言語の辞書: 動画の言葉は無く、音の説明は「1 つだけ」 */
    for (const l of ['ja', 'zh', 'en']) {
      const T = H.T[l] || {};
      add('the ' + l + ' words: no video keys', ['hero.watch', 'hero.standin', 'hero.mute', 'hero.motion', 'media.howto', 'dur.howto', 'set.video', 'toast.videoOff', 'toast.intro', 'toast.howto'].every(k => !(k in T)));
      add('the ' + l + ' words: the sound is one (set.sndD) and its switch and volume are still there', /1|one/i.test(T['set.sndD'] || '') && !!T['set.snd'] && !!T['set.sndVol'], T['set.sndD']);
    }
  }

  /* ================= 2026-10-01（持ち主「クライアント起動していきなり MOD のところ開く必要ある？ホームが標準じゃダメ？」） =================
     起動したらホーム。run.ps1 がスプラッシュの終わった瞬間の画面を、何かが動かす前に書き留めている（window.__start）。
     9 回のうち 1 回目は何も保存されていない起動（初めて・未インストール）、2 回目からは run.ps1 が前の回の終わりに入れた
     「最新」（sp.sample）での起動で、これが前は MOD の画面で開いていた場合（名前の後ろの「保存されていた状態」で分かる）。ホームの中身（版に縛られない紹介、本物の履歴、押せるのはパッチノートがある物だけ）も見る。 */
  {
    const s = window.__start || null;
    const visibleViews = () => [...document.querySelectorAll('.view')].filter(v => !v.hidden).map(v => v.id).join(',');
    add('起動時はホーム（保存されていた状態: ' + (s ? s.stored : '?') + '）', !!s && s.view === 'view-home', s ? s.view : 'run.ps1 did not write window.__start');
    add('起動時: レールで「今のページ」なのはホームのボタンだけ', !!s && s.rail === 'home', s ? s.rail : '-');
    add('起動時: 見本の状態を「最新」に変えても（spReview.sample も起動と同じ）ホームのまま', !!s && s.afterSample === 'view-home', s ? s.afterSample : '-');

    H.closeAll(); H.show('home');
    await tick();
    const home = $('#view-home'), card = $('#view-home .feature-copy'), st = $('#homeState');
    add('ホーム: 表示に切り替えられる', visibleViews() === 'view-home', visibleViews());
    /* the big card: what PocketRoles is, in this language, tied to no version and never 「準備中」 */
    const cardText = card ? [...card.childNodes].map(n => n.textContent).join(' ') : '';
    const cardOwn = card ? cardText.replace(st ? st.textContent : '', '') : '';
    add('ホームの大きな札: 題と説明はこの言語の紹介文', !!card && card.querySelector('h3').textContent === H.t('home.title')
      && card.querySelector('p').textContent === H.t('home.body') && H.t('home.body').length > 10, card ? card.querySelector('h3').textContent : 'no card');
    add('ホームの大きな札: 版の番号も「準備中」も書いていない（版に縛られない）', !!card && !/v?\d+\.\d+\.\d+/.test(cardOwn) && !cardOwn.includes(H.t('news.prep')), cardOwn);
    add('ホームの大きな札: ボタンは「PocketRoles へ」と「パッチノート」（タブと同じ語）',
      !!card && !!card.querySelector('[data-view="game"]') && card.querySelector('[data-view="game"]').textContent.trim() === H.t('home.open')
      && !!card.querySelector('[data-goto-notes]') && card.querySelector('[data-goto-notes]').textContent.trim() === H.t('tab.notes'),
      card ? [...card.querySelectorAll('button')].map(b => b.textContent.trim()).join(' / ') : '');
    /* in the app the line after the name is the "status" event's; none has come yet, so there is nothing there */
    add('ホームの大きな札: アプリから status が来る前は、状態の行は空で隠れている（見本を出さない）', !!st && st.hidden && st.textContent === '',
      st ? (st.hidden + ' / ' + st.textContent) : 'no #homeState');

    /* the news: the real history, newest first. The top row is v0.5.6 (MOD v0.5.6 and Client v1.1.2 go out the same day,
       the owner 2026-10-01). Its time is ONE line of the page (V056_AT in design\launcher-proto\index.html), written on the
       day the GitHub release is published; until then the row and the patch notes say 準備中. What is expected here is
       read from that row, so nothing in this file changes on the day - but a time that is there must be a real UTC time
       to the second, later than v0.5.5's, and the patch notes must show the very same one. */
    const rows = [...document.querySelectorAll('#news .news-row')];
    const verOf = r => (r.querySelector('.ver') || {}).textContent || '';
    const utcText = iso => { const d = new Date(iso), p = n => String(n).padStart(2, '0');
      return `${d.getUTCFullYear()}/${p(d.getUTCMonth() + 1)}/${p(d.getUTCDate())} ${p(d.getUTCHours())}:${p(d.getUTCMinutes())} UTC`; };
    add('お知らせ: 7 行（v0.5.6〜v0.5.0）', rows.length === 7 && rows.map(verOf).join(',') === 'v0.5.6,v0.5.5,v0.5.4,v0.5.3,v0.5.2,v0.5.1,v0.5.0',
      rows.map(verOf).join(','));
    const r056 = rows[0], t056 = r056 ? r056.querySelector('time') : null, out056 = !!t056;
    const prep = rows.filter(r => r.querySelector('.prep'));
    if (out056) {
      const at = t056.getAttribute('datetime');
      add('お知らせ: v0.5.6 は公開済み（「準備中」の行は無い）', prep.length === 0, prep.map(verOf).join(','));
      add('お知らせ: v0.5.6 の時刻は秒まである UTC（GitHub の published_at の形）で、v0.5.5 より後、UTC で出る',
        /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/.test(at) && Date.parse(at) > Date.parse('2026-09-23T10:00:09Z') && t056.textContent === utcText(at), at + ' / ' + t056.textContent);
    } else {
      add('お知らせ: v0.5.6 は準備中（公開の日に時刻の 1 行を入れる）。「準備中」はこの 1 行だけで、版の番号は出ている', prep.length === 1 && prep[0] === r056
        && verOf(r056) === 'v0.5.6' && r056.querySelector('.prep').textContent === H.t('news.prep'), prep.map(verOf).join(','));
    }
    const r055 = rows.find(r => verOf(r) === 'v0.5.5');
    add('お知らせ: v0.5.5 は公開済み（GitHub で公開した時刻 2026-09-23 10:00 UTC）', !!r055 && !r055.querySelector('.prep')
      && !!r055.querySelector('time') && r055.querySelector('time').getAttribute('datetime') === '2026-09-23T10:00:09Z'
      && r055.querySelector('time').textContent === '2026/09/23 10:00 UTC', r055 ? r055.textContent : 'no v0.5.5 row');
    const times = [...document.querySelectorAll('#news time')];
    add('お知らせ: 時刻は UTC で持って UTC で出す（触れると自分の時刻）', times.length === (out056 ? 7 : 6)
      && times.every(x => /Z$/.test(x.getAttribute('datetime')) && / UTC$/.test(x.textContent) && !!x.closest('.lt') && !!x.closest('.lt').querySelector('.lt-pop')),
      times.map(x => x.getAttribute('datetime')).join(','));
    add('お知らせ: 新しい順', times.every((x, i) => i === 0 || Date.parse(times[i - 1].getAttribute('datetime')) > Date.parse(x.getAttribute('datetime'))));
    add('お知らせ: どの行にも題がこの言語である', rows.every(r => (r.querySelector('.t') || {}).textContent));
    const buttons = [...document.querySelectorAll('#news button')];
    add('お知らせ: 押せるのはパッチノートがある v0.5.6 だけ（v0.5.5 もほかもボタンにしない）', buttons.length === 1 && verOf(buttons[0]) === 'v0.5.6'
      && rows.filter(r => r !== r056).every(r => !r.querySelector('button') && !r.classList.contains('link')),
      buttons.map(verOf).join(','));
    add('お知らせ: 「時刻は見本」の札はもう無い', !home.querySelector('h2 .sample-tag') && !Object.keys(H.T).some(l => 'home.newsSample' in (H.T[l] || {})));
    /* nothing of the prototype shows on home inside the app */
    const shown = el => el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
    const leftovers = [...home.querySelectorAll('.sample, .sample-tag, .proto-bar, .proto, .badge-smp')].filter(shown);
    add('ホーム: プロトタイプの品（見本の札・プロトタイプの帯）が見えていない', leftovers.length === 0, leftovers.map(x => x.className + ':' + x.textContent).join(' | '));
    add('ホーム: 「（プロトタイプ）」「見本」の文字が見えていない', !home.innerText.includes(H.t('proto')) && !home.innerText.includes(H.t('sample')),
      home.innerText.slice(0, 200));

    /* a row with nothing behind it does nothing at all: no page change, no notice (v0.5.5 now that v0.5.6 has the notes) */
    const plain = r055 ? r055.querySelector('.news-item') : null;
    $('#toast').textContent = '';
    if (plain) plain.click();
    await tick();
    add('お知らせ: パッチノートの無い行（v0.5.5）を押しても何も起きない（画面も知らせも）', !!plain && visibleViews() === 'view-home' && $('#toast').textContent === '',
      visibleViews() + ' / ' + $('#toast').textContent);
    /* the one with patch notes opens them */
    if (buttons[0]) buttons[0].click();
    await tick();
    add('お知らせ: v0.5.6 を押すとパッチノートのタブが開く', visibleViews() === 'view-game' && $('#tab-pn').getAttribute('aria-selected') === 'true'
      && !$('#panel-pn').hidden, visibleViews() + ' / ' + $('#tab-pn').getAttribute('aria-selected'));
    const pill = $('#panel-pn .notes-head .pill');
    add('パッチノート: 見出しの版は、押したお知らせの行と同じ v0.5.6', ($('#panel-pn .notes-head h2') || {}).textContent === 'v0.5.6',
      ($('#panel-pn .notes-head h2') || {}).textContent);
    add('パッチノート: v0.5.6 の札はお知らせの行と同じ（' + (out056 ? '公開済み' : '準備中') + '）', !!pill
      && pill.textContent === H.t(out056 ? 'notes.out' : 'news.prep') && pill.classList.contains('red') === out056
      && !Object.keys(H.T).some(l => 'notes.unreleased' in (H.T[l] || {})), pill ? pill.textContent + ' / ' + pill.className : 'no pill');
    /* 2026-10-01（公開前レビュー 2 回目）: 見出しの時刻は見本の status の「更新 2026/09/22 03:00 UTC」だった（押した元のお知らせの行より前）。
       今は、お知らせの行と同じ GitHub の公開時刻。準備中の間は時刻を出さない */
    const nt = $('#notesTime'), ntTime = nt && nt.querySelector('time');
    add('パッチノート: 見出しの時刻は、お知らせの v0.5.6 と同じ' + (out056 ? '公開時刻' : '（準備中なので出さない）') + '（見本の「更新」時刻ではない）', !!nt
      && (out056 ? !!ntTime && ntTime.getAttribute('datetime') === t056.getAttribute('datetime') && ntTime.textContent === t056.textContent : !ntTime && nt.textContent === '')
      && !nt.textContent.includes('2026/09/22') && !Object.keys(H.T).some(l => 'notes.updated' in (H.T[l] || {})), nt ? nt.textContent : 'no #notesTime');
    /* the body: three groups in this language (2026-10-01: until v0.5.5 it was Japanese only, with a note saying so) */
    /* how many: from the page's own data (spReview.notes), so the notes can lose or gain an item on the release day
       without this file changing - but every group and every item of it must be drawn, with a heading, in this language */
    const groups = [...document.querySelectorAll('#notesBody .ngroup')], items = [...document.querySelectorAll('#notesBody li')];
    const nd = SR && SR.notes, wantG = nd ? nd.groups.length : -1, wantI = nd ? nd.groups.reduce((n, g) => n + g.items.length, 0) : -1;
    add('パッチノート: 中身は NOTES のまとまりと項目が全部（どの項目にも太字の見出し）、どの文もこの言語で書いてある', !!nd && nd.v === 'v0.5.6'
      && wantG >= 2 && wantI >= 5 && groups.length === wantG && items.length === wantI
      && nd.groups.every(g => ['ja', 'zh', 'en'].every(l => typeof g.h[l] === 'string' && g.h[l].length > 0)
        && g.items.every(it => ['ja', 'zh', 'en'].every(l => typeof it.b[l] === 'string' && it.b[l].length > 0 && (!it.s || (typeof it.s[l] === 'string' && it.s[l].length > 0)))))
      && groups.every(g => !!g.querySelector('h3 svg.icon') && g.querySelector('h3').textContent.trim().length > 0)
      && items.every(li => ((li.querySelector('b') || {}).textContent || '').trim().length > 0), groups.length + '/' + wantG + ' groups, ' + items.length + '/' + wantI + ' items');
    const words = { ja:['インフルエンサー', 'BepInEx', '修復'], zh:['网红', '守护天使', '船员', '修复'], en:['Influencer', 'Guardian Angel', 'Crewmate', 'Repair'] }[H.lang] || [];
    const bodyText = ($('#notesBody') || {}).textContent || '';
    add('パッチノート: この言語の言葉で書いてある（中国語は本体の公式の言い方）', words.length > 0 && words.every(w => bodyText.includes(w))
      && !bodyText.includes('内鬼') && (H.lang === 'ja' || !/[ぁ-ん]/.test(bodyText)), words.filter(w => !bodyText.includes(w)).join(','));
    add('パッチノート: コマンドは code で出る（`` の印は残らない）', [...document.querySelectorAll('#notesBody code')].some(c => c.textContent === '/dummy') && !bodyText.includes('`'));
    add('パッチノート: 「日本語だけ」の断りはもう無い', !document.getElementById('notesLangNote') && !Object.keys(H.T).some(l => 'notes.jaOnly' in (H.T[l] || {})));
    /* 2026-10-01（仕上げ。公開前レビュー 5）: 「読んだ」印は今の版で書いて、今の版で比べる。印を書く所・比べる所の版を
       v0.5.5 に戻した写しが、前はどの検査にも落ちなかった（NEW の札が消えない、または次の版で出ない）。
       印を消してからタブを開き直し、書かれた印を見る。次に「最新」の状態で、今の版の印なら NEW が無く、前の版の印なら NEW が出るかを見る */
    {
      const seen0 = localStorage.getItem('sp.seenNotes'), st0 = localStorage.getItem('sp.sample');
      const prNew = () => !!document.querySelector('#railTitles .tile[data-title="pocketroles"] .badge-new');
      try { localStorage.removeItem('sp.seenNotes'); } catch (e) {}
      document.getElementById('tab-ov').click(); await tick();
      document.getElementById('tab-pn').click(); await tick();
      const wrote = localStorage.getItem('sp.seenNotes');
      add('パッチノート: 開くと「読んだ」印は今の版（NOTES の版 = お知らせの v0.5.6）になる', !!nd && wrote === nd.v && nd.v === 'v0.5.6' && $('#notesDot').hidden, wrote);
      H.setSampleState('latest');
      try { localStorage.setItem('sp.seenNotes', 'v0.5.6'); } catch (e) {}
      H.renderTitles(); const newIfRead = prNew();
      try { localStorage.setItem('sp.seenNotes', 'v0.5.5'); } catch (e) {}
      H.renderTitles(); const newIfOld = prNew();
      add('パッチノート: 「最新」の時、今の版の印なら NEW の札は無く、前の版（v0.5.5）の印なら NEW が出る', !newIfRead && newIfOld, newIfRead + ' / ' + newIfOld);
      try { if (seen0 === null) localStorage.removeItem('sp.seenNotes'); else localStorage.setItem('sp.seenNotes', seen0); } catch (e) {}
      H.setSampleState(['none', 'update', 'latest'].includes(st0) ? st0 : 'none');
      try { if (st0 === null) localStorage.removeItem('sp.sample'); } catch (e) {}
    }
    /* and back the way the tab block above leaves it: opening the notes marks them read (the NEW badge goes), and
       localStorage stays for the next language's run */
    document.getElementById('tab-ov').click();
    await tick();
    try { localStorage.removeItem('sp.seenNotes'); } catch (e) {}
    $('#notesDot').hidden = false;
    H.renderTitles();

    /* the words of home and the news are in all three languages, the old ones are gone from all three */
    for (const k of ['home.title', 'home.body', 'home.more', 'home.open', 'home.news', 'news.prep', 'news.next', 'mod.subNone0', 'notes.out', 'tab.notes'])
      add('3 言語の語: ' + k, ['ja', 'zh', 'en'].every(l => typeof (H.T[l] || {})[k] === 'string' && H.T[l][k].length > 0),
        ['ja', 'zh', 'en'].map(l => l + '=' + (H.T[l] || {})[k]).join(' | '));
    for (const k of ['home.eyebrow', 'home.newsSample', 'notes.unreleased'])
      add('3 言語とも古い語が無い: ' + k, ['ja', 'zh', 'en'].every(l => !(k in (H.T[l] || {}))));
    add('3 言語の語: home.more はタブの「パッチノート」と同じ語', ['ja', 'zh', 'en'].every(l => H.T[l]['home.more'] === H.T[l]['tab.notes']));
    add('3 言語の語: パッチノートの前書き（notes.lead）とメディアの札（media.new）は v0.5.6 のこと',
      ['ja', 'zh', 'en'].every(l => (H.T[l]['notes.lead'] || '').includes('v0.5.6') && (H.T[l]['media.new'] || '').includes('v0.5.6')
        && !(H.T[l]['notes.lead'] || '').includes('v0.5.5') && !(H.T[l]['media.new'] || '').includes('v0.5.5')),
      ['ja', 'zh', 'en'].map(l => l + '=' + H.T[l]['media.new']).join(' | '));
    for (const pre of ['home.', 'news.', 'notes.', 'mod.']) {
      const keys = l => Object.keys(H.T[l] || {}).filter(k => k.startsWith(pre)).sort().join(',');
      add('3 言語の鍵がそろっている: ' + pre + '*', keys('ja') === keys('zh') && keys('ja') === keys('en'),
        'ja[' + keys('ja') + '] zh[' + keys('zh') + '] en[' + keys('en') + ']');
    }
    H.closeAll(); H.show('game');
    await tick();
  }

  /* ================= high contrast and "reduce motion" ================= */
  if (all && hc) {
    add('Windows high contrast really is on for this run', matchMedia('(forced-colors: active)').matches);
    add('"reduce motion" really is on for this run', matchMedia('(prefers-reduced-motion: reduce)').matches);
    seen('.play', el => {
      add('PLAY takes the system colours rather than ours',
        getComputedStyle(el).backgroundColor !== 'rgba(0, 0, 0, 0)' && gradientStops(el).length === 0,
        getComputedStyle(el).backgroundImage);
      add('... and keeps an edge, so it is still a button', parseFloat(getComputedStyle(el).borderTopWidth) >= 1,
        getComputedStyle(el).borderTopWidth);
      add('... and nothing on it animates', getComputedStyle(el).transitionDuration === '0s',
        getComputedStyle(el).transitionDuration);
    }, 'PLAY in high contrast');
    for (const [sel, name] of [['.seg-ind', 'the tab underline'], ['.dot', 'the state lamp']])
      seen(sel, el => {
        const c = sel === '.seg-ind' ? getComputedStyle(el, '::after').backgroundColor : getComputedStyle(el).backgroundColor;
        add(name + ' is still drawn in high contrast', c !== 'rgba(0, 0, 0, 0)' && c !== 'transparent', c);
      }, name + ' in high contrast');
    /* the Aegis lamp on the rail says "something was found" by colour alone. In high contrast a background colour is
       the system's to choose, so without this the red and the green came out the same white and the host learnt
       nothing until they opened the panel. */
    seen('.rail-btn[data-open="d-aegis"] .aegis-dot', el => {
      add('the Aegis lamp keeps its own colour in high contrast',
        getComputedStyle(el).forcedColorAdjust === 'none', getComputedStyle(el).forcedColorAdjust);
      el.style.background = '#E63946';
      const red = getComputedStyle(el).backgroundColor;
      el.style.background = '#2EC4B6';
      const teal = getComputedStyle(el).backgroundColor;
      el.style.removeProperty('background');
      add('... so "found something" and "all clear" do not look the same', red !== teal, red + ' vs ' + teal);
      add('... and it has an edge, so it can be found at all',
        getComputedStyle(el).boxShadow !== 'none', getComputedStyle(el).boxShadow);
    }, 'the Aegis lamp in high contrast');
    add('... and it does not rely on the colour: there are words beside it', (() => {
      const words = $('#aegisDotText'), btn = $('.rail-btn[data-open="d-aegis"]');
      return !!words && words.textContent.trim().length > 0
        && btn.getAttribute('aria-describedby') === 'aegisDotText';
    })(), ($('#aegisDotText') || {}).textContent);
    await SR.setPage('general');
    await tick();
    seen('#set-accent-gold .sw-dot', el => {
      add('the colour swatches still show their real colours (a colour picker must)',
        getComputedStyle(el).forcedColorAdjust === 'none', getComputedStyle(el).forcedColorAdjust);
      add('... and the gold one really is gold', rgb(getComputedStyle(el).backgroundColor)[0] > 200,
        getComputedStyle(el).backgroundColor);
    }, 'the colour swatches in high contrast');
    /* the mark on the chosen one used to be one pixel of border width on a 40 px square, with the wash and the ring
       colour both taken away by the system: an outline in the system's own selection colour says it plainly */
    seen('#set-accent-gold', el => {
      const s = getComputedStyle(el), off = getComputedStyle($('#set-accent-teal'));
      add('the chosen colour is marked by more than a pixel of border in high contrast',
        s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= 2 && off.outlineStyle === 'none',
        s.outlineStyle + ' ' + s.outlineWidth + ' / not chosen: ' + off.outlineStyle);
      add('... in a colour the system chose, not one of ours', s.outlineColor !== off.borderTopColor
        || s.outlineColor !== s.borderTopColor, s.outlineColor + ' vs ' + s.borderTopColor);
    }, 'the chosen swatch in high contrast');
    /* 「すきな色をえらぶ」 is a button like the other seven: only the dot inside it keeps our colours */
    seen('.sw-custom', el => {
      add('「すきな色をえらぶ」 is drawn by the system like its seven neighbours',
        getComputedStyle(el).forcedColorAdjust !== 'none', getComputedStyle(el).forcedColorAdjust);
      add('... so its edge looks like theirs',
        getComputedStyle(el).borderTopColor === getComputedStyle($('#set-accent-teal')).borderTopColor,
        getComputedStyle(el).borderTopColor + ' vs ' + getComputedStyle($('#set-accent-teal')).borderTopColor);
    }, '「すきな色をえらぶ」 in high contrast');
    add('the row for one player\'s evidence is there in high contrast too', (() => { SR.setPage('pocketroles'); return !!$('#tool-exportOne'); })());
    H.closeAll();
  }

  /* ================= the recolour ================= */
  if (all && !hc) {
    /* every accent value the stylesheet asks for actually resolves */
    for (const n of ['--accent', '--accent-hi', '--accent-deep', '--accent-sub', '--accent-ink', '--accent-line',
      '--accent-text', '--accent-soft', '--danger', '--danger-text', '--danger-soft', '--danger-dot'])
      add(th + ': ' + n + ' resolves', /^(#|rgb)/.test(varOf(n)), varOf(n) || '(empty)');
    add(th + ': the colour with nothing chosen is the StarPocket gold', hexOf('--accent') === '#F7C548', hexOf('--accent'));
    add(th + ': the text on a filled surface is the brand navy', hexOf('--accent-ink') === '#1E2257', hexOf('--accent-ink'));
    /* the accent is no longer any of the reds that read as somebody else's */
    for (const n of ['--accent', '--accent-hi', '--accent-deep', '--accent-sub', '--accent-line', '--accent-text'])
      add(th + ': ' + n + ' is not one of the old reds', RIOT.indexOf(hexOf(n)) < 0, hexOf(n));
    add(th + ': the crewmate keeps its own red', hexOf('--crew') === '#E63946', hexOf('--crew'));
    add(th + ': ... and it is still in the title artwork, not in any rule',
      !!document.querySelector('#card-shield path[fill="#E63946"]')
      && !!document.querySelector('#lv-madmate [fill="#E63946"], #lv-madmate use[fill="#E63946"]'));
    /* no accent value is declared and then never used: a dead token is a colour nobody can see they changed */
    const sheetText = [...document.styleSheets].map(s => { try { return [...s.cssRules].map(r => r.cssText).join('\n'); } catch (e) { return ''; } }).join('\n');
    for (const n of ['--accent', '--accent-hi', '--accent-deep', '--accent-sub', '--accent-ink', '--accent-line',
      '--accent-text', '--accent-soft', '--danger', '--danger-text', '--danger-soft', '--danger-dot'])
      add(th + ': something on the page actually uses ' + n, sheetText.includes('var(' + n + ')'));
    add(th + ': danger is its own colour, not the accent', hexOf('--danger-text') !== hexOf('--accent-text'),
      hexOf('--danger-text'));
    add(th + ': the dead --crew-deep is gone', varOf('--crew-deep') === '', varOf('--crew-deep'));

    /* measured on what the browser actually paints */
    seen('.play', el => {
      const ink = rgb(getComputedStyle(el).color), stops = gradientStops(el);
      add('PLAY: its background really is a gradient', stops.length >= 2, stops.length);
      ratioAtLeast('PLAY: its label reads on every part of the button', worstOn(ink, stops), 4.5);
      ratioAtLeast('PLAY: the button shows against the hero behind it', worstOn([11, 12, 34], stops), 3);
      add('PLAY: no white label left on a pale button', ratio(ink, [255, 255, 255]) > 1.5, getComputedStyle(el).color);
      /* AND UNDER THE POINTER. The guard works every ratio above out from the RESTING colours, so a hover that
         repainted the button would carry all of them off with it: brightness(1.06) saturate(1.05) used to take the
         label on the navy and the violet below 4.5 the moment the pointer landed on the biggest button in the app.
         The hover rule is read here and measured, filter and all. */
      const hover = ruleProps('.play:hover');
      const repaint = hover.filter(([p]) => /^(filter|backdrop-filter|background|color|opacity|mix-blend-mode)/.test(p));
      add('PLAY: hovering it does not repaint it', repaint.length === 0, repaint.map(x => x.join(':')).join('; '));
      const f = (hover.find(([p]) => p === 'filter') || [])[1] || 'none';
      ratioAtLeast('PLAY: its label reads under the pointer too',
        worstOn(underFilter(ink, f), stops.map(s => underFilter(s, f))), 4.5);
      ratioAtLeast('PLAY: and the button still shows on the hero under the pointer',
        worstOn([11, 12, 34], stops.map(s => underFilter(s, f))), 3);
    }, 'PLAY');
    seen('.play-sub', el => ratioAtLeast('PLAY: the small pill inside it reads',
      ratio(textOn(el), bgOf(el)), 4.5), 'PLAY: the small pill inside it reads');
    seen('.btn-red', el => {
      ratioAtLeast('the primary button: its label reads',
        worstOn(rgb(getComputedStyle(el).color), fillsOf(el)), 4.5);
      const b = rgb(getComputedStyle(el).borderTopColor);
      add('the primary button has an edge, so a pale colour still shows', parseFloat(getComputedStyle(el).borderTopWidth) >= 1,
        getComputedStyle(el).borderTopWidth);
      ratioAtLeast('the primary button: its edge shows on the page behind it',
        ratio(over(b, bgOf(el.parentElement)), bgOf(el.parentElement)), 3);
      /* measured from the rule, not from the screen: this button lives on the home and library pages */
      add('the primary button is still 40 px tall', getComputedStyle(el).height === '40px', getComputedStyle(el).height);
    }, 'the primary button');
    seen('.badge-new', el => ratioAtLeast('the NEW badge reads',
      worstOn(rgb(getComputedStyle(el).color), fillsOf(el)), 4.5), 'the NEW badge reads');
    /* the 2 px marks: they carry meaning, so 3:1 against what is BEHIND them */
    for (const [sel, name] of [['.seg-ind', 'the tab underline'], ['.seg .reddot', 'the unread dot']])
      seen(sel, el => {
        const line = rgb(varOf('--accent-line'));
        ratioAtLeast(name + ' shows', ratio(line, bgOf(el.parentElement)), 3);
      }, name + ' shows');
    /* red stays red where red is the only thing that means "wrong" */
    for (const [sel, name] of [['.stop', 'Aegis 「止める」'], ['.streamer-chip', '「配信中」']])
      seen(sel, el => {
        ratioAtLeast(name + ' reads', ratio(textOn(el), bgOf(el)), 4.5);
        add(name + ' is still red', rgb(getComputedStyle(el).color)[0] > rgb(getComputedStyle(el).color)[1] + 30,
          getComputedStyle(el).color);
      }, name);
    add('a failed step is still red', rgb(varOf('--danger-text'))[0] > rgb(varOf('--danger-text'))[1] + 30,
      varOf('--danger-text'));
    add('the state lamp keeps the one red that shows everywhere', hexOf('--danger-dot') === '#E63946', hexOf('--danger-dot'));
    /* high contrast: the page now has something to say about it */
    add('the page steps aside in Windows high contrast',
      [...document.styleSheets].some(s => { try { return [...s.cssRules].some(r => (r.conditionText || '').includes('forced-colors')); } catch (e) { return false; } }));

    /* ================= the colour setting ================= */
    add('the colour setting is in settings.json, not in the browser',
      Object.prototype.hasOwnProperty.call(H.PREF_DEFAULTS, 'accent') && H.PREF_DEFAULTS.accent === 'default',
      JSON.stringify(H.PREF_DEFAULTS.accent));
    /* the page's own copy of the steps agrees with the app's, colour for colour */
    for (const row of TABLE) {
      const v = SR.acc.vars(row[0]);
      const bad = TABLE_KEYS.filter((k, i) => v[k] !== row[i + 1]).map((k, i) => k + '=' + v[k]);
      add('the page works out ' + row[0] + ' exactly as the app does', bad.length === 0, bad.join(' '));
    }
    add('a wash of the colour is the colour, see-through', SR.acc.vars('#2EC4B6')['--acc-l-soft'] === 'rgba(46,196,182,.22)',
      SR.acc.vars('#2EC4B6')['--acc-l-soft']);

    /* the row of colours. The sheet puts the focus on itself a frame after it opens, so the wait here is what lets
       the keyboard checks below start from a button rather than from the sheet. */
    H.openLayer('m-settings');
    await tick();
    const block = $('#set-accent');
    add('Settings → 全般 has a colour row', !!block);
    if (block) {
      const sw = block.querySelectorAll('.sw');
      add('there are seven ready-made colours and one of your own', sw.length === 8, sw.length);
      add('the colour row is a radio group', block.querySelector('[role="radiogroup"]') !== null);
      add('every colour button says which colour it is',
        [...sw].every(b => (b.getAttribute('aria-label') || b.getAttribute('title') || '').length > 0));
      const gold = $('#set-accent-gold');
      add('the StarPocket gold is one of them', !!gold);
      add('... and it is the one chosen to start with', gold && gold.getAttribute('aria-checked') === 'true');
      add('... and it says it is the first colour', gold && /—|-/.test(gold.getAttribute('aria-label')),
        gold && gold.getAttribute('aria-label'));
      add('anyone can pick a colour of their own', !!$('#set-accent-custom') && $('#set-accent-custom').type === 'color');
      add('and there is a way back', !!$('#set-accent-reset') && $('#set-accent-reset').textContent.trim().length > 0);
      add('the row explains itself in this language', (block.querySelector('.sw-note') || {}).textContent.length > 20);
      add('the chosen colour is not marked by colour alone',
        getComputedStyle(gold).borderTopWidth !== getComputedStyle($('#set-accent-teal')).borderTopWidth,
        getComputedStyle(gold).borderTopWidth);
      /* a radio group is worked the way every other radio group is: ONE stop on the way round with Tab, and the
         arrow keys move between the colours. Eight Tab stops and dead arrow keys is not a radio group. */
      const radios = [...block.querySelectorAll('[role="radio"]')];
      add('eight colours, and every one of them a radio', radios.length === 8,
        radios.map(b => b.tagName).join(','));
      add('only the chosen colour is a stop on the way round with Tab',
        radios.filter(b => b.tabIndex === 0).length === 1 && gold.tabIndex === 0,
        radios.map(b => b.tabIndex).join(','));
      add('no radio holds something else that can be focused (ARIA does not allow it)',
        radios.every(b => !b.querySelector('a[href],button,input,select,textarea,[tabindex]')));
      add('「すきな色をえらぶ」 can be focused itself', $('#set-accent-custom-btn')
        && $('#set-accent-custom-btn').tagName === 'BUTTON'
        && $('#set-accent-custom-btn').matches('.sw-custom'));
      add('... and the colour field is out of the way of the keyboard and the mouse',
        $('#set-accent-custom').tabIndex < 0 && $('#set-accent-custom').getAttribute('aria-hidden') === 'true'
        && getComputedStyle($('#set-accent-custom')).pointerEvents === 'none');
      /* it still lies on its button, so the browser opens the colour dialog where the swatch is */
      add('... while still lying on the button that opens it', (() => {
        const a = $('#set-accent-custom-btn').getBoundingClientRect(), b = $('#set-accent-custom').getBoundingClientRect();
        return Math.abs(a.left - b.left) <= 1 && Math.abs(a.top - b.top) <= 1
          && Math.abs(a.width - b.width) <= 1 && Math.abs(a.height - b.height) <= 1;
      })(), JSON.stringify($('#set-accent-custom').getBoundingClientRect()));
      /* and it takes no room in the row - its own width, its negative margin and the gap before it add up to nothing,
         so everything after it (「元にもどす」) sits exactly where it did before the field was moved out here */
      add('... and it takes no room of its own in the row', (() => {
        const f = $('#set-accent-custom'), row = f.parentElement;
        const gap = parseFloat(getComputedStyle(row).columnGap) || 0;
        return Math.round(f.getBoundingClientRect().width + parseFloat(getComputedStyle(f).marginLeft) + gap) === 0;
      })(), $('#set-accent-custom').getBoundingClientRect().width + ' + '
        + getComputedStyle($('#set-accent-custom')).marginLeft + ' + gap '
        + getComputedStyle($('#set-accent-custom').parentElement).columnGap);
      /* the arrow keys pick as they move (the radio pattern) */
      gold.focus();
      gold.dispatchEvent(new KeyboardEvent('keydown', { key:'ArrowRight', bubbles:true }));
      await tick();
      add('an arrow key moves to the next colour and picks it',
        document.activeElement === $('#set-accent-teal') && hexOf('--accent') === '#2EC4B6',
        (document.activeElement || {}).id + ' / ' + hexOf('--accent'));
      $('#set-accent-teal').dispatchEvent(new KeyboardEvent('keydown', { key:'ArrowLeft', bubbles:true }));
      await tick();
      add('... and back again', document.activeElement === gold && hexOf('--accent') === '#F7C548',
        (document.activeElement || {}).id + ' / ' + hexOf('--accent'));
      /* arrowing onto 「すきな色をえらぶ」 must not throw the browser's colour dialog in the way */
      const opened = [];
      const field = $('#set-accent-custom');
      const realClick = field.click, realPicker = field.showPicker;
      field.click = () => opened.push('click');
      field.showPicker = () => opened.push('showPicker');
      $('#set-accent-orange').focus();
      $('#set-accent-orange').dispatchEvent(new KeyboardEvent('keydown', { key:'ArrowRight', bubbles:true }));
      await tick();
      add('arrowing onto 「すきな色をえらぶ」 opens nothing by itself',
        document.activeElement === $('#set-accent-custom-btn') && opened.length === 0, opened.join(','));
      /* pressing it does open it - that is the only thing it is for */
      $('#set-accent-custom-btn').click();
      add('... and pressing it opens the browser\'s colour dialog', opened.length === 1, opened.join(','));
      field.click = realClick;
      if (realPicker) field.showPicker = realPicker; else delete field.showPicker;
      await pick('#F7C548');
    }
    /* changing it: at once, and told to the app rather than kept in the browser */
    const before = window.__sent.length;
    await pick('#2EC4B6');
    add('picking a colour changes the page at once', hexOf('--accent') === '#2EC4B6', hexOf('--accent'));
    const sent = window.__sent.slice(before).filter(m => m.cmd === 'settings.set' && m.args && m.args.key === 'accent');
    add('... and the app is told, so it goes into settings.json', sent.length === 1 && sent[0].args.value === '#2EC4B6',
      JSON.stringify(sent));
    add('... and the swatch shows which one is chosen now',
      $('#set-accent-teal') && $('#set-accent-teal').getAttribute('aria-checked') === 'true');
    /* the focus ring must never be tied to the accent, or picking the wrong colour would make it vanish */
    add('the focus ring does not follow the accent', hexOf('--focus') !== hexOf('--accent'),
      hexOf('--focus') + ' vs ' + hexOf('--accent'));
    /* the guard, on the three colours that are meant to be awkward */
    const paleSurface = dark ? [48, 48, 48] : [224, 226, 234];
    for (const bad of ['#FFFFFF', '#000000', '#FFFF00']) {
      await pick(bad);
      const fill = rgb(varOf('--accent')), ink = rgb(varOf('--accent-ink'));
      ratioAtLeast(bad + ': the text on it still reads', ratio(fill, ink), 4.5);
      ratioAtLeast(bad + ': the button still shows on the hero', ratio(fill, [11, 12, 34]), 3);
      ratioAtLeast(bad + ': its text on the hardest surface of this theme still reads',
        ratio(rgb(varOf('--accent-text')), paleSurface), 4.5);
      ratioAtLeast(bad + ': its lines still show on that surface',
        ratio(rgb(varOf('--accent-line')), paleSurface), 3);
      seen('.play', el => ratioAtLeast(bad + ': PLAY still reads with it',
        worstOn(rgb(getComputedStyle(el).color), gradientStops(el)), 4.5), bad + ': PLAY still reads with it');
    }
    await pick('#000000');
    add('a colour that had to be moved is said so, in words', !!$('#set-accent-fixed')
      && $('#set-accent-fixed').textContent.length > 8, ($('#set-accent-fixed') || {}).textContent);
    add('... and that notice is announced, not just drawn',
      $('#set-accent-fixed') && $('#set-accent-fixed').getAttribute('role') === 'status');
    /* the notice used to speak in hex codes the person had never seen, and never said which way the colour went */
    add('... and it says which way the colour went, in words',
      $('#set-accent-fixed') && ($('#set-accent-fixed').textContent.includes(H.t('set.accentUp'))
        || $('#set-accent-fixed').textContent.includes(H.t('set.accentDown'))),
      ($('#set-accent-fixed') || {}).textContent);
    add('... and shows the two colours as colours, not as codes', (() => {
      const chips = $('#set-accent-fixed') ? [...$('#set-accent-fixed').querySelectorAll('.sw-chip')] : [];
      if (chips.length !== 2) return false;
      const bg = chips.map(c => rgb(getComputedStyle(c).backgroundColor));
      /* the code is kept, but only where someone who wants it can ask for it */
      return bg.every(c => c && c[3] === 1) && chips.every(c => /#[0-9A-F]{6}/.test(c.getAttribute('aria-label')))
        && !/#[0-9A-F]{6}/.test($('#set-accent-fixed').childNodes[0].textContent);
    })(), ($('#set-accent-fixed') || {}).textContent);
    await pick('#F7C548');
    add('a colour that needs no moving says nothing', !$('#set-accent-fixed'));

    /* ---- red is never the accent (the page's own comment says so; now it is true) ---- */
    await pick('#E63946');
    add('the crewmate\'s red is not refused as the Client\'s colour', hexOf('--accent').length === 7);
    add('... but it is not the lamp\'s red either', hexOf('--accent-line') !== hexOf('--danger-dot'),
      hexOf('--accent-line') + ' vs ' + hexOf('--danger-dot'));
    add('... nor is anything else worked out from it',
      ['--accent', '--accent-hi', '--accent-deep', '--accent-sub', '--accent-text']
        .every(n => hexOf(n) !== hexOf('--danger-dot')));
    add('... and the person is told that red means something else here',
      $('#set-accent-fixed') && $('#set-accent-fixed').textContent.includes(H.t('set.accentRed')),
      ($('#set-accent-fixed') || {}).textContent);
    /* the two dots that used to come out the same colour: "not read yet" and "it stopped" */
    seen('.seg .reddot', el => {
      const unread = rgb(getComputedStyle(el).backgroundColor), lamp = rgb(varOf('--danger-dot'));
      add('the unread mark and the stopped lamp are not the same red',
        unread.slice(0, 3).join(',') !== lamp.slice(0, 3).join(','), getComputedStyle(el).backgroundColor);
    }, 'the unread mark against the stopped lamp');
    await pick('#F7C548');

    /* ---- a drag round the colour wheel: the colour follows every step, the app is told once ---- */
    const field = $('#set-accent-custom');
    if (field) {
      const n0 = window.__sent.length;
      const same = [];
      let last = '';
      for (let i = 0; i < 12; i++) {
        last = '#' + (0x20 + i * 3).toString(16).toUpperCase().padStart(2, '0') + 'A6FB';
        field.value = last;
        SR.accentLive(last);
        same.push($('#set-accent-custom') === field && document.contains(field) && field.isConnected);
      }
      add('a drag round the colour wheel leaves the colour field where it is', same.every(Boolean),
        same.map(Number).join(''));
      add('... and the colour follows it at once', hexOf('--accent') === last, hexOf('--accent') + ' vs ' + last);
      add('... and nothing is sent to the app while it is still moving',
        window.__sent.slice(n0).filter(m => m.cmd === 'settings.set').length === 0);
      await new Promise(r => setTimeout(r, 600));
      const sentNow = window.__sent.slice(n0).filter(m => m.cmd === 'settings.set' && m.args.key === 'accent');
      add('... and the app is told once, when it stops', sentNow.length === 1 && sentNow[0].args.value === last,
        JSON.stringify(sentNow.map(m => m.args.value)));
      await pick('#F7C548');
    }

    /* ---- the page can always put itself right: WebView2 reloads it by itself when its render process dies, and the
       page that comes back is painted by whatever colour the app had registered at start-up ---- */
    await pick('#2EC4B6');
    for (const n of SR.acc.names) document.documentElement.style.removeProperty(n);
    add('a page reloaded by WebView2 really does come back without the colour', hexOf('--accent') === '#F7C548',
      hexOf('--accent'));
    window.__emit({ type:'event', name:'shell', data:{ version:'0.3', accent:'#2EC4B6',
      vars:SR.acc.vars('#2EC4B6'), close:'tray', startGame:'pocketroles', autostart:false } });
    await tick();
    add('... and the app\'s "shell" puts it back, every time', hexOf('--accent') === '#2EC4B6', hexOf('--accent'));
    seen('.play', el => add('... PLAY with it', gradientStops(el).some(c => c[0] === 0x2E && c[1] === 0xC4),
      getComputedStyle(el).backgroundImage), '... PLAY with it');
    await pick('#F7C548');
    /* 元にもどす */
    await pick('#F05A8C');
    $('#set-accent-reset').click();
    await tick();
    add('「元にもどす」 brings the StarPocket gold back', hexOf('--accent') === '#F7C548', hexOf('--accent'));
    add('... and takes the chosen colour back off <html>',
      document.documentElement.style.getPropertyValue('--acc-fill') === '',
      document.documentElement.style.getPropertyValue('--acc-fill'));
    add('the words for the colour row are in this language',
      H.t('set.accentCap').length > 0 && H.t('set.accentReset').length > 0 && H.t('set.accentD').length > 20
      && H.t('set.accentCustom').length > 0 && H.t('set.accentDefault').length > 0);
    for (const k of ['gold', 'teal', 'navy', 'violet', 'pink', 'green', 'orange'])
      add('a name for the ' + k + ' colour in this language', H.t('set.accent.' + k).length > 0);
    for (const k of ['set.accentUp', 'set.accentDown', 'set.accentRed', 'set.accentPicked', 'set.accentUsed',
      'aegis.dotOk', 'aegis.dotFound'])
      add('words for ' + k + ' in this language', H.t(k).length > 0, H.t(k));
    /* the keyboard ring. The page draws it with one rule, :focus-visible{outline:2px solid var(--focus)}, so it can
       only be seen when the thing that TAKES the focus is the thing you can see. 「すきな色をえらぶ」 used to hand
       its focus to a colour field laid over it at opacity 0, and the ring went with it. (The window is not focused
       in a headless run, so :focus-visible never matches here; what is measured is where the focus lands and whether
       the ring would show on it.) */
    await SR.setPage('general');
    await tick();
    const ring = ruleText(':focus-visible');
    add('the page draws a keyboard ring at all', /outline:\s*[2-9]px\s+solid/.test(ring), ring);
    for (const [sel, name] of [['#set-accent-gold', 'a ready-made colour'], ['#set-accent-custom-btn', '「すきな色をえらぶ」'], ['#set-accent-reset', '「元にもどす」']])
      seen(sel, el => {
        el.focus();
        const s = getComputedStyle(el), box = el.getBoundingClientRect();
        add('the keyboard reaches ' + name + ' itself', document.activeElement === el,
          (document.activeElement || {}).id);
        add('... and what it reaches can be seen, so the ring can be too (' + name + ')',
          s.opacity === '1' && s.visibility === 'visible' && box.width >= 20 && box.height >= 20,
          s.opacity + ' ' + s.visibility + ' ' + Math.round(box.width) + 'x' + Math.round(box.height));
        ratioAtLeast('... and the ring\'s colour shows against the sheet (' + name + ')',
          ratio(rgb(s.getPropertyValue('--set-focus').trim()), bgOf(el.parentElement)), 3);
      }, 'the keyboard ring on ' + name);
    H.closeAll();

    /* ================= the one-person evidence row ================= */
    H.closeAll();
    SR.setPage('pocketroles');
    const row = $('#tool-exportOne');
    add('Settings → PocketRoles has a row for one player\'s evidence', !!row);
    if (row) {
      const box = $('#tool-exportOne-who');
      add('... with a box to say whose', !!box && box.tagName === 'INPUT');
      add('... and that box has a name a screen reader can read', box && (box.getAttribute('aria-label') || '').length > 0,
        box && box.getAttribute('aria-label'));
      add('... and a hint of what to type', box && (box.placeholder || '').length > 0, box && box.placeholder);
      add('... and the button says what it does', row.textContent.trim().length > 0, row.textContent);
      add('... and the row explains itself in this language',
        ($('#tool-exportOne-d') || {}).textContent.length > 30, ($('#tool-exportOne-d') || {}).textContent);
      add('... and it sits right after the report zip',
        $('#tool-makeReport').closest('.tool').nextElementSibling === row.closest('.tool'));
      /* the box goes UNDER the words, so the explanation keeps the width every other row has */
      const words = $('#tool-exportOne-d'), ask = box.parentElement;
      add('... and the box does not squeeze the words into a column',
        ask.getBoundingClientRect().top >= words.getBoundingClientRect().bottom - 2,
        'box top ' + Math.round(ask.getBoundingClientRect().top) + ' vs words bottom ' + Math.round(words.getBoundingClientRect().bottom));
      add('... and the words are as wide as the other rows\'',
        words.getBoundingClientRect().width > $('#tool-makeReport-d').getBoundingClientRect().width,
        Math.round(words.getBoundingClientRect().width) + ' vs ' + Math.round($('#tool-makeReport-d').getBoundingClientRect().width));
      /* the row must say the same as the dialog that comes AFTER the zip: the zip does hold a hash of the PUID and
         the player's erase code, and this PC's own details. "records only, no hash" read as the opposite. */
      const said = ($('#tool-exportOne-d') || {}).textContent || '';
      const pairs = { ja:['PUID のハッシュ', 'フレンドコードもそのハッシュも入りません', 'system.txt'],
        zh:['PUID 的哈希', '不包含好友编号及其哈希', 'system.txt'],
        en:['hash of the PUID', 'neither the friend code nor its hash', 'system.txt'] };
      for (const want of pairs[H.lang] || pairs.ja)
        add('... and it says, before the zip is made: ' + want, said.includes(want), said);
      add('... and it no longer says the zip holds nothing else',
        !/だけを入れた|只包含一名玩家证据记录|evidence records only/.test(said), said);

      /* the answer opens a window over the settings sheet, so every press below starts from a fresh one */
      const reopen = async () => {
        const close = $('#m-confirm').querySelector('[data-close]');
        if (!$('#m-confirm').hidden && close) close.click();
        H.closeAll();
        $('#toast').textContent = '';
        SR.setPage('pocketroles');
        await tick();
        return $('#tool-exportOne');
      };
      const n0 = window.__sent.length;
      box.value = 'abcdefghijklmnop';
      row.click();
      const inv = window.__sent.slice(n0).filter(m => m.cmd === 'exportOne');
      add('pressing it sends exportOne', inv.length === 1, JSON.stringify(window.__sent.slice(n0)));
      add('... and what was typed goes with it', inv.length === 1 && inv[0].args.who === 'abcdefghijklmnop',
        inv.length ? JSON.stringify(inv[0].args) : '');
      await tick();

      /* ---- while it runs: the app cannot answer until the whole job is done, and that can be minutes ---- */
      window.__reply.exportOne = { delayMs:400, answer:{ ok:true, data:{ title:'T', text:'B', open:'O' } } };
      let btn = await reopen();
      btn.click();
      add('pressing it holds the button while the zip is made', btn.disabled && btn.getAttribute('aria-busy') === 'true',
        btn.disabled + ' / ' + btn.getAttribute('aria-busy'));
      add('... and says so on the screen', $('#toast').textContent.length > 4, $('#toast').textContent);
      const n1 = window.__sent.length;
      btn.click();
      add('... and cannot be started twice over',
        window.__sent.slice(n1).filter(m => m.cmd === 'exportOne').length === 0);
      await new Promise(r => setTimeout(r, 700));
      add('... and can be pressed again when it is done', !btn.disabled && !btn.hasAttribute('aria-busy'));

      /* ---- a mistyped code: the commonest failure there is, and the app's answer for it is two lines long ---- */
      const bad = 'AEG-1234 は読めません — 記録を消すためのコード・フレンドコード・証拠 ID のどれかを入れてください';
      window.__reply.exportOne = { answer:{ ok:false, error:bad } };
      btn = await reopen();
      btn.click();
      await new Promise(r => setTimeout(r, 300));
      add('a mistyped code is shown where it can be read, not in a notice that fades',
        !$('#m-confirm').hidden && $('#cfBody').textContent === bad,
        $('#m-confirm').hidden + ' / ' + $('#cfBody').textContent);
      add('... in a window with nothing to cancel, only a way out',
        $('#m-confirm').querySelector('.dlg-acts [data-close]').hidden === true
        && $('#cfYes').textContent.trim().length > 0, $('#cfYes').textContent);

      /* ---- and an answer that comes after the page has given up waiting: the zip IS on the desktop ---- */
      const wasLong = SR.invokeMs.long;
      SR.invokeMs.long = 60;
      window.__reply.exportOne = { delayMs:400, answer:{ ok:true, data:{ title:'LATE', text:'the zip is there', open:'O' } } };
      btn = await reopen();
      btn.click();
      await new Promise(r => setTimeout(r, 200));
      add('a wait that runs out says so', $('#toast').textContent.length > 4, $('#toast').textContent);
      await new Promise(r => setTimeout(r, 500));
      add('... and the answer that comes late is still shown, not dropped',
        !$('#m-confirm').hidden && $('#cfTitle').textContent === 'LATE',
        $('#m-confirm').hidden + ' / ' + $('#cfTitle').textContent);
      SR.invokeMs.long = wasLong;
      delete window.__reply.exportOne;

      /* ---- 配信モード: the box holds somebody else's friend code ---- */
      await reopen();
      $('#tool-exportOne-who').value = 'Wobble#4821';
      $('#tool-exportOne-who').dispatchEvent(new Event('input', { bubbles:true }));
      SR.streamer(true);
      SR.setPage('pocketroles');
      await tick();
      const box2 = $('#tool-exportOne-who');
      add('配信モード empties what was typed about another player',
        box2.value === '' && !SR.asked.exportOne, box2.value + ' / ' + SR.asked.exportOne);
      box2.value = 'Wobble#4821';
      const sec = getComputedStyle(box2).webkitTextSecurity || getComputedStyle(box2).getPropertyValue('-webkit-text-security');
      add('... and covers up what is typed next, the way the search box is switched off',
        sec === 'disc' || box2.type === 'password', sec + ' / ' + box2.type);
      add('... while the box still works, so a zip can still be made', !box2.disabled);
      SR.streamer(false);
      box2.value = '';
      H.closeAll();
    }
    add('a long job is given half an hour, not fifteen seconds (exportOne)', SR.longCmds.has('exportOne'));
    add('... and so is the uninstall', SR.longCmds.has('uninstall'));
    add('a timeout is never dressed up as "still working" for the evidence zip',
      !window.__hostSource.includes("r.timeout && cmd === 'exportOne'"));

    /* ================= v0.4: the log page and the logs-folder size ================= */
    H.closeAll();
    SR.setPage('pocketroles');
    await tick();
    const logRow = $('#tool-showLog');
    add('Settings → PocketRoles still has the 進行ログ row', !!logRow);
    const logWords = ($('#tool-showLog-d') || {}).textContent || '';
    add('... and it no longer promises the launcher\'s own file', !/launcher\.log/.test(logWords), logWords);
    add('... and it says the record is opened inside the app', logWords.length > 25, logWords);
    /* the row about the zips must not still say "monthly" or "never deleted": the logs ARE deleted at 30 days */
    const folderWords = [...document.querySelectorAll('#tools-folders .auto')].map(e => e.textContent).join(' | ');
    add('the zip row says "per day", not "per month"',
      !/月ごと|按月|monthly|logs-年-月|logs-<year>/.test(folderWords), folderWords);
    add('... and no longer says they are never deleted',
      !/自動では消さない|不会自动删除|Never deleted automatically/.test(folderWords), folderWords);

    if (logRow) {
      const answer = {
        ok:true,
        data:{ title:'TASK LOG', sub:'what this app has done', none:'nothing yet', close:'CLOSE',
          path:'%USERPROFILE%\\AppData\\Local\\StarPocket\\Client\\client.log',
          lines:['[12:00:01] one', '[12:00:02] two'], truncated:true, more:'older lines are left out',
          logs:{ bytes:129 * 1024 * 1024, size:'129 MB', big:false, text:'Logs: 129 MB', tip:'past games', logs:4, zips:1 } },
      };
      window.__reply.showLog = { answer };
      const n0 = window.__sent.length;
      logRow.click();
      await new Promise(r2 => setTimeout(r2, 250));
      add('pressing it asks the app for the log', window.__sent.slice(n0).some(m => m.cmd === 'showLog'));
      const page = $('#sp-log');
      add('... and a page opens inside this window', !!page && !page.hidden, page ? page.hidden : 'no page');
      add('... with no second window anywhere', !window.__sent.some(m => m.cmd === 'openExternal'));
      add('... showing the app\'s own title', ($('#sp-log-title') || {}).textContent === 'TASK LOG', ($('#sp-log-title') || {}).textContent);
      const items = [...document.querySelectorAll('#sp-log-lines li')];
      add('... and every line the app sent', items.length === 2, items.length);
      add('... with the time on its own', items.length === 2 && items[0].querySelector('time').textContent === '12:00:01',
        items.length ? items[0].textContent : '');
      add('... newest at the bottom, as the launcher\'s log box had it',
        items.length === 2 && items[1].textContent.includes('two'), items.length ? items[1].textContent : '');
      add('... and it says when older lines were left out',
        !$('#sp-log-more').hidden && $('#sp-log-more').textContent === 'older lines are left out', $('#sp-log-more').textContent);
      const foot = ($('#sp-log-foot') || {}).textContent || '';
      add('... the file is named with no Windows account name in it',
        foot.includes('%USERPROFILE%') && !/C:\\Users\\[A-Za-z]/.test(foot), foot);
      add('... and the logs folder\'s size is on the page', foot.includes('Logs: 129 MB'), foot);
      /* the box of lines scrolls, so a keyboard must be able to reach it */
      add('... the box of lines can be reached by keyboard', $('#sp-log-lines').tabIndex === 0);
      if (!hc) {
        const lines = $('#sp-log-lines');
        ratioAtLeast('the lines can be read on their own background', ratio(textOn(lines), bgOf(lines)), 4.5);
      }
      /* it keeps asking while it is open: that is why it is the one row the app never holds while it is busy */
      const n1 = window.__sent.length;
      await new Promise(r2 => setTimeout(r2, 2300));
      add('while it is open it asks again, so a long task can be watched',
        window.__sent.slice(n1).some(m => m.cmd === 'showLog'));
      $('#sp-log-close').click();
      await new Promise(r2 => setTimeout(r2, 1400));
      add('「閉じる」 closes it', $('#sp-log').hidden, $('#sp-log').hidden);
      const n2 = window.__sent.length;
      await new Promise(r2 => setTimeout(r2, 2300));
      add('... and it stops asking once it is closed', !window.__sent.slice(n2).some(m => m.cmd === 'showLog'));

      /* an empty log says so rather than showing an empty box */
      window.__reply.showLog = { answer:{ ok:true, data:{ title:'T', none:'NOTHING YET', lines:[] } } };
      $('#tool-showLog').click();
      await new Promise(r2 => setTimeout(r2, 250));
      add('an empty log says so in words', ($('#sp-log-lines') || {}).textContent.includes('NOTHING YET'),
        ($('#sp-log-lines') || {}).textContent);
      /* v0.4 review: the app no longer counts the logs folder on the window's own thread for every one of these
         two-second asks - it hands over the number its background walk already found. The first ask of all can
         therefore arrive with no size line at all, and the page must still draw. */
      add('... and a reply with no size line still opens the page', !$('#sp-log').hidden);
      {
        const kept = $('#tool-openLogsFolder').closest('.tool').querySelector('.sp-logsize');
        add('... and the size already known is kept, not blanked',
          !!kept && kept.textContent === 'Logs: 129 MB', kept && kept.textContent);
      }
      document.dispatchEvent(new KeyboardEvent('keydown', { key:'Escape', bubbles:true }));
      await new Promise(r2 => setTimeout(r2, 1400));
      add('Escape closes it too', $('#sp-log').hidden);
      delete window.__reply.showLog;

      /* the size beside 「ログのフォルダを開く」, and the 2 GB notice in the danger colour rather than the accent */
      window.__emit({ type:'event', name:'logs', data:{ bytes:129 * 1024 * 1024, size:'129 MB', big:false, text:'Logs: 129 MB', tip:'past games', logs:4, zips:1 } });
      await tick();
      let size = $('#tool-openLogsFolder').closest('.tool').querySelector('.sp-logsize');
      add('the size appears beside 「ログのフォルダを開く」', !!size && size.textContent === 'Logs: 129 MB', size && size.textContent);
      add('... with the app\'s own words as its tooltip', !!size && size.title === 'past games', size && size.title);
      add('... and it is not shouting at 129 MB', !!size && size.dataset.big === '0');
      window.__emit({ type:'event', name:'logs', data:{ bytes:2200000000, size:'2.0 GB', big:true, text:'Logs: 2.0 GB', tip:'over 2 GB', logs:9, zips:3 } });
      await tick();
      size = $('#tool-openLogsFolder').closest('.tool').querySelector('.sp-logsize');
      add('past 2 GB the same line says the new size', !!size && size.textContent === 'Logs: 2.0 GB', size && size.textContent);
      add('... and the reason is in its tooltip, not only in a colour', !!size && size.title === 'over 2 GB', size && size.title);
      if (size && !hc) {
        const c = rgb(getComputedStyle(size).color), danger = rgb(varOf('--danger-text'));
        add('... and it is the danger red, never the accent',
          !!c && !!danger && c[0] === danger[0] && c[1] === danger[1] && c[2] === danger[2],
          getComputedStyle(size).color + ' vs ' + varOf('--danger-text'));
        ratioAtLeast('... and it can still be read', ratio(c, bgOf(size)), 4.5);
      }
      /* the settings sheet is rebuilt whenever it opens: the line has to come back with it */
      H.closeAll();
      await tick();
      SR.setPage('pocketroles');
      await tick();
      const again = $('#tool-openLogsFolder').closest('.tool').querySelector('.sp-logsize');
      add('the size is still there after the settings sheet is rebuilt', !!again && again.textContent === 'Logs: 2.0 GB',
        again && again.textContent);
      H.closeAll();
    }
    /* ================= 2026-10-03: 開発の置き場所を選んだ答えが画面に出る ================= */
    /* 持ち主 11:48「クライアントでフォルダ選んべない」: 選んでも画面に何も出なかった（ok の答えを report() が捨てていた）。開き直さない答え
       （同じフォルダ: data.message）はアプリの言葉が知らせに出る。開き直す答え（data.restart + data.text）は moveCopy と同じで、アプリが
       自分の箱で知らせるのでページは黙る（崩す係 8: 同じ文が 2 回出ていた）。開発の欄は、アプリが "shell" で devFolder を渡した時だけ
       描かれる（ui/host-v01.js）ので、ここで渡す。ページは場所を渡さない（命令の名前だけ）。やめた時（cancelled）は黙る。断り（error）は
       アプリの言葉で出る。フォルダ選択の窓は長く迷えるので、15 秒の待ちではなく長い待ち（LONG_CMDS）: 崩す係 1 は、15 秒を過ぎてから
       選ぶと「応答がありません」だけが出て、アプリの答えが捨てられるのを見つけた。 */
    {
      const wait = ms => new Promise(r => setTimeout(r, ms));
      window.__emit({ type:'event', name:'shell', data:{ version:'1.1', devFolder:'%USERPROFILE%\\Desktop\\HostRoles', devOn:false, mode:'friend' } });
      await tick();
      H.closeAll(); SR.setPage('pocketroles'); await tick();
      const pickBtn = $('#set-dev [data-cmd="pickModSource"]');
      add('設定 → PocketRoles → 開発 に「別のフォルダを選ぶ…」がある（アプリが置き場所を渡した時）', !!pickBtn && pickBtn.textContent.trim().length > 0, pickBtn ? pickBtn.textContent : 'no #set-dev button');
      add('... その上に「置き場所:」とフォルダが出ている', !!$('#set-dev-path') && $('#set-dev-path').textContent.includes('HostRoles'), $('#set-dev-path') && $('#set-dev-path').textContent);
      add('pickModSource は長く待つ命令（LONG_CMDS。フォルダ選択の窓を開く pickSteam・moveCopy と同じ）',
        SR.longCmds && SR.longCmds.has('pickModSource') && SR.longCmds.has('pickSteam') && SR.longCmds.has('moveCopy'), SR.longCmds ? [...SR.longCmds].join(',') : 'no longCmds');
      if (pickBtn) {
        $('#toast').textContent = '';
        window.__reply.pickModSource = { answer:{ ok:true, data:{ path:'%USERPROFILE%\\work\\HostRoles', restart:false, message:'MSG-SAME' } } };
        const n0 = window.__sent.length;
        pickBtn.click();
        await wait(150);
        const sent = window.__sent.slice(n0).filter(m => m.cmd === 'pickModSource');
        add('押すと pickModSource を 1 回だけ送る（場所は送らない）', sent.length === 1 && JSON.stringify(sent[0].args || {}) === '{}', JSON.stringify(window.__sent.slice(n0)));
        add('開き直さない答え（同じフォルダ）は、アプリの言葉がそのまま知らせに出る', $('#toast').textContent === 'MSG-SAME', $('#toast').textContent);
        $('#toast').textContent = '';
        window.__reply.pickModSource = { answer:{ ok:true, data:{ path:'%USERPROFILE%\\work\\HostRoles', restart:true, text:'MSG-PICKED' } } };
        pickBtn.click(); await wait(150);
        add('開き直す答えは、ページは黙る（アプリが自分の箱で知らせる。同じ文を 2 回出さない）', $('#toast').textContent === '', $('#toast').textContent);
        window.__reply.pickModSource = { answer:{ ok:true, data:{ cancelled:true } } };
        pickBtn.click(); await wait(150);
        add('選ぶのをやめた時は何も言わない（「失敗しました」と言わない）', $('#toast').textContent === '', $('#toast').textContent);
        window.__reply.pickModSource = { answer:{ ok:false, error:'BAD-FOLDER' } };
        pickBtn.click(); await wait(150);
        add('ソースでないフォルダを選んだ時は、アプリの断りの言葉が出る', $('#toast').textContent === 'BAD-FOLDER', $('#toast').textContent);
        $('#toast').textContent = '';
        window.__reply.pickModSource = { answer:{ ok:false, busy:true } };
        pickBtn.click(); await wait(150);
        add('作業中は「ほかの処理の途中です」の知らせ（toast.busy の文そのもの）', $('#toast').textContent === H.t('toast.busy') && $('#toast').textContent.length > 0, $('#toast').textContent);
        /* 15 秒の待ち（INVOKE_MS.short）を 60 ms に縮め、答えを 300 ms 遅らせる: 長い待ちの命令なら答えが出る。短い待ちだと
           「応答がありません」が出て、遅れて来た答えは捨てられる（崩す係 1 が本物の 15 秒で見た事と同じ形） */
        const wasShort = SR.invokeMs.short;
        SR.invokeMs.short = 60;
        $('#toast').textContent = '';
        window.__reply.pickModSource = { delayMs:300, answer:{ ok:true, data:{ path:'%USERPROFILE%\\work\\HostRoles', restart:false, message:'MSG-LATE' } } };
        pickBtn.click(); await wait(600);
        add('短い待ちを過ぎてから選んでも、アプリの答えが出る（15 秒で「応答がありません」と言わない）', $('#toast').textContent === 'MSG-LATE', $('#toast').textContent);
        SR.invokeMs.short = wasShort;
        /* 本当に答えが無い時（長い待ちも切れた）: 「応答がありません（開発の置き場所）」とボタンの名前付き（EXTRA_LABEL） */
        const wasLong = SR.invokeMs.long;
        SR.invokeMs.long = 60;
        $('#toast').textContent = '';
        window.__reply.pickModSource = 'never';
        pickBtn.click(); await wait(300);
        const label = H.cmdLabel('pickModSource');
        add('答えが無いままなら「応答がありません」にボタンの名前が付く（EXTRA_LABEL）',
          $('#toast').textContent === H.t('toast.timeout', { x:label }) && label !== 'pickModSource' && $('#toast').textContent.includes(label),
          $('#toast').textContent + ' / ' + label);
        SR.invokeMs.long = wasLong;
        delete window.__reply.pickModSource;
      }
      /* ================= 2026-10-03（崩す係 4）: 置き場所が消えた時も、欄と「別のフォルダを選ぶ…」が出る ================= */
      /* 置き場所のフォルダを移した・消した（デスクトップの HostRoles もショートカットも無い）と、アプリは devFolder:null を渡す。前はそれで欄ごと
         消え、「別のフォルダを選ぶ…」も押せなかった＝アプリの中から直せなかった（開発のスイッチが ON でも、説明なしに友達モードで起動）。
         今は settings.json に devBuild か devSource がある人にだけ devMissing:true が付き（ClientContext.DevBlockShape）、ページは
         「置き場所: 見つかりません」と選ぶボタンを描く。スイッチが ON なら、いまは友達モードで動いている事の 1 行。ON にする操作はアプリが
         dev_nofolder で断る（ここではその答えを作って、言葉が出てスイッチが戻る事を見る）。 */
      {
        const lang = document.documentElement.lang || '';
        const jaText = (H.T && H.T.ja && H.T.ja['set.devMissingOn']) || '';
        window.__emit({ type:'event', name:'shell', data:{ version:'1.1', devFolder:null, devMissing:true, devOn:true, mode:'friend' } });
        await tick();
        H.closeAll(); SR.setPage('pocketroles'); await tick();
        const missBtn = $('#set-dev [data-cmd="pickModSource"]');
        add('置き場所が見つからなくても（devMissing）、開発の欄と「別のフォルダを選ぶ…」がある',
          !!$('#set-dev') && !!missBtn && missBtn.textContent.trim() === H.t('set.devPick') && missBtn.offsetParent !== null,
          ($('#set-dev') ? 'set-dev ok' : 'no #set-dev') + ' / ' + (missBtn ? missBtn.textContent : 'no button'));
        const pathEl = $('#set-dev-path');
        add('「置き場所: 見つかりません」と出る（フォルダの名前は出ない）',
          !!pathEl && pathEl.textContent.includes(H.t('set.devPath')) && !!$('#set-dev-notfound') && $('#set-dev-notfound').textContent === H.t('set.devNotFound')
          && H.t('set.devNotFound') !== 'set.devNotFound' && !pathEl.textContent.includes('HostRoles') && !pathEl.textContent.includes('%USERPROFILE%'),
          pathEl ? pathEl.textContent : 'no #set-dev-path');
        const line = $('#set-dev-missing');
        add('スイッチが ON なのに置き場所が無い: 友達モードで動いている事の 1 行が出る（この言語の言葉）',
          !!line && line.textContent === H.t('set.devMissingOn') && line.textContent.length > 0 && H.t('set.devMissingOn') !== 'set.devMissingOn'
          && (lang.startsWith('ja') || line.textContent !== jaText),
          (line ? line.textContent : 'no #set-dev-missing') + ' / lang=' + lang);
        if (line && !hc) ratioAtLeast('... その 1 行は読める', ratio(textOn(line), bgOf(line)), 4.5);
        if (line && !hc) ratioAtLeast('... 「見つかりません」も読める', ratio(textOn($('#set-dev-notfound')), bgOf($('#set-dev-notfound'))), 4.5);
        add('... その 1 行は横にはみ出さない（折り返す）', !!line && line.scrollWidth <= line.clientWidth + 1 && line.getBoundingClientRect().height > 0,
          line ? line.scrollWidth + ' > ' + line.clientWidth : 'no line');
        add('スイッチは設定どおり ON で描かれる', !!$('#set-devBuild') && $('#set-devBuild').checked === true);
        if (missBtn) {
          window.__reply.pickModSource = { answer:{ ok:true, data:{ cancelled:true } } };
          const n1 = window.__sent.length;
          missBtn.click(); await wait(150);
          const sent1 = window.__sent.slice(n1).filter(m => m.cmd === 'pickModSource');
          add('「見つかりません」の時も、押すと pickModSource を 1 回だけ送る（場所は送らない）', sent1.length === 1 && JSON.stringify(sent1[0].args || {}) === '{}', JSON.stringify(window.__sent.slice(n1)));
          delete window.__reply.pickModSource;
        }
        /* OFF にするのは通る（消えたフォルダに開発モードで閉じ込めない）: 本物のアプリは保存して開き直す。ここでは settings.set が 1 回送られる事を見る */
        if ($('#set-devBuild')) {
          const n2 = window.__sent.length;
          $('#set-devBuild').click(); await wait(150);
          const off = window.__sent.slice(n2).filter(m => m.cmd === 'settings.set' && m.args && m.args.key === 'devBuild');
          add('ON → OFF は送られる（settings.set devBuild=false を 1 回）', off.length === 1 && off[0].args.value === false && $('#set-devBuild').checked === false, JSON.stringify(off));
          SR.setPage('pocketroles'); await tick();
          add('OFF にすると友達モードの 1 行は消える（欄・「見つかりません」・選ぶボタンは残る）',
            !$('#set-dev-missing') && !!$('#set-dev-notfound') && !!$('#set-dev [data-cmd="pickModSource"]'),
            ($('#set-dev-missing') ? 'line still there' : 'no line') + ' / ' + ($('#set-dev-notfound') ? 'notfound ok' : 'no notfound'));
        }
        /* スイッチ OFF で置き場所が無い（設定に devSource だけ残っている人）: 欄と「見つかりません」と選ぶボタン。1 行は出ない */
        window.__emit({ type:'event', name:'shell', data:{ version:'1.1', devFolder:null, devMissing:true, devOn:false, mode:'friend' } });
        await tick(); SR.setPage('pocketroles'); await tick();
        add('スイッチが OFF なら友達モードの 1 行は出ない（欄と「見つかりません」と選ぶボタンは出る）',
          !$('#set-dev-missing') && !!$('#set-dev-notfound') && !!$('#set-dev [data-cmd="pickModSource"]') && !!$('#set-devBuild') && !$('#set-devBuild').checked,
          ($('#set-dev-missing') ? 'line there' : 'no line') + ' / ' + ($('#set-devBuild') ? 'checked=' + $('#set-devBuild').checked : 'no switch'));
        /* OFF → ON は、置き場所が無いのでアプリが断る（dev_nofolder の言葉 + 設定どおり devBuild:false）: 言葉が出て、スイッチが戻る */
        if ($('#set-devBuild')) {
          $('#toast').textContent = '';
          window.__reply['settings.set'] = { answer:{ ok:false, error:'NO-FOLDER', data:{ key:'devBuild', devBuild:false } } };
          const n3 = window.__sent.length;
          $('#set-devBuild').click(); await wait(200);
          const on = window.__sent.slice(n3).filter(m => m.cmd === 'settings.set' && m.args && m.args.key === 'devBuild');
          add('OFF → ON を押すと settings.set devBuild=true が 1 回送られる', on.length === 1 && on[0].args.value === true, JSON.stringify(on));
          add('置き場所が無い時の ON は断られ、アプリの言葉（dev_nofolder）が知らせに出る', $('#toast').textContent === 'NO-FOLDER', $('#toast').textContent);
          add('... スイッチは OFF に戻る（答えの devBuild:false どおり）', !!$('#set-devBuild') && $('#set-devBuild').checked === false && H.prefs.devBuild === false,
            ($('#set-devBuild') ? 'checked=' + $('#set-devBuild').checked : 'no switch') + ' prefs=' + H.prefs.devBuild);
          delete window.__reply['settings.set'];
          $('#toast').textContent = '';
        }
      }
      H.closeAll();
      window.__emit({ type:'event', name:'shell', data:{ version:'1.1', devFolder:null, mode:'friend' } });
      await tick();
      /* devMissing が無い devFolder:null は、今までどおり欄なし（普通の PC） */
      SR.setPage('pocketroles'); await tick();
      add('devMissing が無く devFolder も null なら、開発の欄は描かない（普通の PC。今までどおり）', !$('#set-dev'), $('#set-dev') ? 'set-dev drawn' : '');
      H.closeAll();
    }
    /* ================= v1.3: プロフィールの絵（自分の画像） ================= */
    /* アプリが "shell" で渡す写し（本物は 256×256 の PNG。ここは 1×1 で足りる。tools/ なので data: の綴りを書いてよい）。ページは
       4 か所の <use class="av-use"> を #av-custom に向け、<html> に av-photo を付けて丸いっぱいに描く。null で組み込みの絵へ戻る。
       グルーの on() は 'host:' + name を e.detail で受けるので、CustomEvent で直接届ける */
    {
      const PNG1 = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';
      const hrefs = () => [...document.querySelectorAll('use.av-use')].map(u => u.getAttribute('href'));
      const radios = () => [...document.querySelectorAll('#avGrid [role="radio"]')];
      /* the round buttons that are really on the screen (the title-bar one is only drawn in a narrow window) */
      const roundWidths = () => [...document.querySelectorAll('.avatar-btn svg')].map(s => Math.round(s.getBoundingClientRect().width)).filter(w => w > 0);
      H.closeAll();
      $('#avBtn').click();
      await tick();
      add('the avatar grid has 7 tiles: 6 built-in and one for your own picture',
        radios().length === 7 && radios()[6].matches('[data-av="custom"]'), radios().length + ' / ' + (radios()[6] && radios()[6].dataset.av));
      add('... the 7th has a name of its own', !!radios()[6] && (radios()[6].getAttribute('aria-label') || '').length > 0);
      add('... 「画像をやめて元の絵に戻す」 is hidden while there is no picture', $('#avReset').hidden === true);
      add('... and the note under the grid is there', !$('#avMore').hidden && $('#avMore small').textContent.length > 0, $('#avMore').hidden);
      document.dispatchEvent(new CustomEvent('host:shell', { detail:{ version:'1.0', avatar:PNG1 } }));
      await tick();
      add('a picture from the app is drawn in all 4 places (#tbAvatar, #acctBtn, #acctBtn2, .pop-av)',
        hrefs().length === 4 && hrefs().every(x => x === '#av-custom'), hrefs().join(','));
      add('... <html> says av-photo', document.documentElement.classList.contains('av-photo'));
      add('... the sprite holds the picture', $('#avCustomImg').getAttribute('href') === PNG1, ($('#avCustomImg').getAttribute('href') || '').slice(0, 30));
      add('... the reset button shows', $('#avReset').hidden === false);
      add('... the 7th tile is checked and the 6 built-in ones are not',
        radios().length === 7 && radios()[6].getAttribute('aria-checked') === 'true' && radios().slice(0, 6).every(b => b.getAttribute('aria-checked') === 'false'),
        radios().map(b => b.getAttribute('aria-checked')).join(','));
      add('... and the picture fills the round button (34 px, not the icon\'s 28)',
        roundWidths().length > 0 && roundWidths().every(w => w === 34), roundWidths().join(','));
      document.dispatchEvent(new CustomEvent('host:shell', { detail:{ version:'1.0', avatar:null } }));
      await tick();
      add('avatar:null goes back to the built-in icon',
        hrefs().length === 4 && hrefs().every(x => /^#av-\d$/.test(x)) && !document.documentElement.classList.contains('av-photo'), hrefs().join(','));
      add('... and the reset button hides again', $('#avReset').hidden === true);
      $('#toast').textContent = '';
      document.dispatchEvent(new CustomEvent('host:shell', { detail:{ version:'1.0', avatar:null, avatarError:'x' } }));
      await tick();
      add('avatarError is said on the screen with the app\'s own words', $('#toast').textContent === 'x', $('#toast').textContent);
      H.profile.applyRemote({ name:'テスト', avatar:2 });
      add('the profile from settings.json wins: the name', $('#meName').textContent === 'テスト', $('#meName').textContent);
      add('... and the icon', hrefs().length === 4 && hrefs().every(x => x === '#av-2'), hrefs().join(','));
      /* back to where the page started, so nothing below (and no other language run) sees this */
      H.profile.applyRemote({ name:'', avatar:0 });
      H.profile.setCustom(null);
      H.closeAll();
    }
    /* the two developer buttons: the app really does them now, and they are long jobs */
    add('the rebuild is given half an hour, not fifteen seconds', SR.longCmds.has('rebuild'));
    add('... and so is the developer update', SR.longCmds.has('devUpdate'));
    add('the log page is never opened as a second window',
      !window.__hostSource.includes('window.open('));

    /* ---- 最初の同意の画面（first-run） ----
       2026-09-24: この画面の「ブラウザで開く ›」は、押せる見た目のまま何も起きませんでした。
       行き先の 3 本（terms / privacy / rules）はどこにも公開しておらず、そのうえ openExternal は
       Bridge.Later にあって Supported にも BeforeConsent にも無いので、押しても C# 側で断られ、
       返事も捨てていたのでトーストすら出なかったためです。
       同意という一番信用が要る画面で、文書が本物だと示すためのボタンが黙って無反応、という形でした。
       ここを誰も見ていなかったのが、release 候補まで残った理由なので、見張りを置きました。

       2026-09-26（595ea20「法務文書を確定版（1.0）にした」）: ボタンそのものを ui/index.html から外しました。
       押せない物を置かない、という判断です。理由と、開けるようにする日の手順は ui/index.html の
       .fr-docfoot のすぐ上のコメントに書いてあります。
       **そのときこの見張りを直さなかったので、2 項目 × 3 言語 × 明暗 = 12 件が落ちたままになり、
       v1.0.2 から v1.1.1 まで 6 回、赤いまま出していました**（持ち主 2026-09-27 に発覚）。
       自己診断が鳴りっぱなしだと誰も見なくなり、その裏で別のバグ（設定の版が「V1.0.0（見本）」のまま）が
       release まで残りました。なので見張りは**消さずに、向きを変えて**残します:
       いまは「ボタンが**無い**こと」を見ます。2026-10-01 に design/launcher-proto/index.html からも frOpen を外したので、
       ui を作り直しても戻りません。もし誰かが design に戻したら、この項目が落ちて気づけます。 */
    if (typeof window.spFirstRun === 'function') {
      window.spFirstRun();
      await tick();
      /* 2026-10-01（公開前レビュー 2 回目）: 文書 3 本（ui\legal\）の読み込みは非同期。40 ms 1 回だけ待っていた頃は、自己テストと
         同時に走らせる（PC が忙しい）と、読み終わる前に見て「警告は出ていない／チェックは押せる」が落ちた。読み終わるまで（最大 5 秒）待つ */
      for (let i = 0; i < 100; i++) {
        const w = $('#frDocWarn'), a = $('#frAgree');
        if (w && w.hidden === true && a && a.disabled === false) break;
        await new Promise(r => setTimeout(r, 50));
      }

      const fr = $('#firstrun'), open = $('#frOpen'), warn = $('#frDocWarn'), agree = $('#frAgree');
      add('同意の画面: 出る', fr && fr.hidden === false);
      add('同意の画面: 「ブラウザで開く」のボタンは無い（2026-09-26 に外した。押せない物を置かないため）',
        open === null, open ? open.outerHTML.slice(0, 90) : 'no button');
      /* 文書が 3 本とも読めた時だけ同意できる。読めていない物に同意させない */
      add('同意の画面: 3 本読めていれば、警告は出ていない', !!warn && warn.hidden === true);
      add('同意の画面: 3 本読めていれば、チェックは押せる', !!agree && agree.disabled === false);

      if (agree) {
        agree.checked = true;
        agree.dispatchEvent(new Event('change'));
        await tick();
        add('同意の画面: チェックを入れると「同意する」が押せる',
          $('#frAccept').getAttribute('aria-disabled') === 'false');
        agree.checked = false;
        agree.dispatchEvent(new Event('change'));
        await tick();
      }

      /* 出したままにすると、後ろのページが inert のまま次の言語の回に入ってしまいます */
      if ($('#frDecline')) { fr.hidden = true; fr.removeAttribute('data-open'); }
      document.body.removeAttribute('inert');
      for (const n of document.body.children) if (n.id !== 'firstrun' && n.id !== 'frAsk') n.removeAttribute('inert');
    }

    /* ---- 2026-10-01（公開前レビュー）: 設定 → Among Us の場所 ----
       (11) インストール前（コピーがまだ無い）の行にも、どこに作るか（set.copyHere と #set-copy-path）を出す。アプリでは
            "status" の modDir がそこに入る（host-v01.js applyPaths）。押す前に場所が分からないボタンにしない。
       (9)  「場所を変える」(moveCopy) の断りの文や、設定を保存できず元にも戻せなかった時の復旧の手順（アプリの r.error）は、
            3.6 秒で消えるトーストではなく、読み返せるダイアログで（reportFlow と同じ形）。busy は今までどおりトースト。
       (10) 動作環境の表: BepInEx は 3 言語とも win-x64（en だけ win-x86 のままだった）。
       ページの状態を "status" で変えるので、この回の一番最後に置く（言語・明暗ごとにページは読み直される）。 */
    {
      const dlgOpen = () => !$('#m-confirm').hidden;
      /* the layer closes with its own animation (hidden only at the end; at once under "reduce motion"): wait for it */
      const closeDlg = async () => {
        const c = $('#m-confirm').querySelector('.dlg-acts [data-close]'); if (dlgOpen() && c) c.click(); H.closeAll();
        for (let i = 0; i < 40 && dlgOpen(); i++) await new Promise(r => setTimeout(r, 50));
      };
      /* settings → PocketRoles, really open. closeDlg's closeAll starts closing Settings too, and a layer that is still
         fading out is not "hidden" yet: spReview.setPage would then only switch its page and leave it closing. Found on
         2026-10-01 when the check 「閉じる」で設定に戻る below was added: it failed for this test's reason, not the page's.
         So wait for it to be gone, then open it. */
      const openSettings = async () => {
        for (let i = 0; i < 40 && !$('#m-settings').hidden; i++) await new Promise(r => setTimeout(r, 50));
        SR.setPage('pocketroles'); await tick();
      };
      const DIR = 'D:\\Games\\Among Us PocketRoles';
      window.__emit({ type:'event', name:'status', data:{ mode:'friend', installed:false, steamFound:true, modDir:DIR, copySource:'default' } });
      await tick();
      await closeDlg(); await openSettings();
      const word = H.t('set.copyHere') || '';
      const cp = $('#set-copy-path'), row = cp && cp.closest('.sub-path');
      add('場所: インストール前の「' + word + '」の語がこの言語にある', word.length > 0 && word !== 'set.copyHere', word);
      add('場所: インストール前の行にも、どこに作るか（アプリの modDir）', !!cp && cp.tagName === 'CODE' && cp.textContent === DIR,
        cp ? cp.textContent : 'no #set-copy-path');
      add('場所: ... その語と「場所を変える」が同じ行に', !!row && row.textContent.includes(word) && !!row.querySelector('#set-movecopy'),
        row ? row.textContent : 'no row');

      const refusal = 'MOVE-REFUSED: OneDrive の中には置けません（約 1 GB を同期し続けます）。別のフォルダを選んでください。';
      window.__reply.moveCopy = { answer:{ ok:false, error:refusal } };
      $('#toast').textContent = '';
      const n0 = window.__sent.length;
      $('#set-movecopy').click();
      await new Promise(r => setTimeout(r, 300));
      add('場所を変える: 押すと moveCopy を 1 回だけ送る', window.__sent.slice(n0).filter(m => m.cmd === 'moveCopy').length === 1,
        JSON.stringify(window.__sent.slice(n0).map(m => m.cmd)));
      add('場所を変える: 断りの文は読み返せるダイアログに（消えるトーストではない）', dlgOpen() && $('#cfBody').textContent === refusal,
        dlgOpen() + ' / ' + $('#cfBody').textContent + ' / toast: ' + $('#toast').textContent);
      add('場所を変える: ... 題はこの操作の名前、やめるボタンは無く閉じるだけ',
        $('#cfTitle').textContent === H.cmdLabel('moveCopy') && $('#m-confirm').querySelector('.dlg-acts [data-close]').hidden === true
        && $('#cfYes').textContent.trim().length > 0, $('#cfTitle').textContent + ' / ' + $('#cfYes').textContent);
      /* 2026-10-01（公開前レビュー 2 回目）: 「別のフォルダを選んでください」と言うのに、閉じると設定ごと閉じていた。今は設定の上に出て、
         「閉じる」で設定の同じ所（場所を変える）へ戻る */
      add('場所を変える: 断りのダイアログは設定の上に出る（設定は開いたまま）', dlgOpen() && !$('#m-settings').hidden,
        'settings hidden: ' + $('#m-settings').hidden);
      const nY = window.__sent.length;
      $('#cfYes').click();
      for (let i = 0; i < 40 && dlgOpen(); i++) await new Promise(r => setTimeout(r, 50));
      await new Promise(r => setTimeout(r, 600));   /* long enough for Settings to have faded out, had anything closed it */
      add('場所を変える: 「閉じる」で、設定の同じ所（場所を変える）に戻る（選び直せる）',
        !dlgOpen() && !$('#m-settings').hidden && !$('#m-settings').inert && !!$('#set-movecopy') && $('#set-movecopy').getClientRects().length > 0
        && window.__sent.length === nY,
        dlgOpen() + ' / settings hidden ' + $('#m-settings').hidden + ' / inert ' + $('#m-settings').inert + ' / sent ' + JSON.stringify(window.__sent.slice(nY).map(m => m.cmd)));

      await closeDlg(); await openSettings();
      window.__reply.moveCopy = { answer:{ ok:false, busy:true } };
      $('#toast').textContent = '';
      $('#set-movecopy').click();
      await new Promise(r => setTimeout(r, 300));
      add('場所を変える: busy は今までどおりトースト（ダイアログは出ない）', !dlgOpen() && $('#toast').textContent.length > 2,
        dlgOpen() + ' / ' + $('#toast').textContent);

      await closeDlg(); await openSettings();
      window.__reply.moveCopy = { answer:{ ok:true, data:{ cancelled:true } } };
      $('#toast').textContent = '';
      $('#set-movecopy').click();
      await new Promise(r => setTimeout(r, 300));
      add('場所を変える: やめた時（cancelled）は何も出さない', !dlgOpen() && $('#toast').textContent === '',
        dlgOpen() + ' / ' + $('#toast').textContent);
      delete window.__reply.moveCopy;
      await closeDlg();

      const page = document.documentElement.outerHTML;
      add('動作環境の表: BepInEx は win-x64（win-x86 はどの言語にも無い）', !page.includes('win-x86') && page.includes('Unity.IL2CPP / win-x64). StarPocket Client downloads it'));
    }
  }

  /* ================= 2026-10-01: ホームの状態の行（アプリの "status"）と、初めてのインストールは MOD の画面 =================
     "status" でページの状態を変えるので、この回の一番最後に置く（言語・明暗ごとにページは読み直される）。 */
  {
    const visibleViews = () => [...document.querySelectorAll('.view')].filter(v => !v.hidden).map(v => v.id).join(',');
    const st = $('#homeState');
    const stText = () => st ? (st.hidden ? '(hidden)' : st.textContent) : 'no #homeState';
    H.closeAll(); H.show('home');
    window.__emit({ type:'event', name:'status', data:{ mode:'friend', installed:true, pstate:'ready', dll:'0.5.5', steamVersion:'2026.9.29', copyVersion:'2026.9.29', steamFound:true } });
    await tick();
    add('ホームの状態の行: 入っている版（アプリの status の dll）', !!st && !st.hidden && st.textContent === H.t('mod.sub', { v:'v0.5.5' }), stText());
    window.spHostUpdateAnswer({ ok:true, data:{ available:true, version:'0.5.6' } });
    await tick();
    add('ホームの状態の行: アップデートあり（「更新を確認」が見つけた時）', !!st && !st.hidden && st.textContent === H.t('mod.subUpdate0', { v:'v0.5.5' }), stText());
    window.__emit({ type:'event', name:'status', data:{ mode:'friend', installed:false, pstate:'install', steamFound:true } });
    await tick();
    add('ホームの状態の行: 未インストール（見本の「約 1.0 GB」は付けない）', !!st && !st.hidden && st.textContent === H.t('mod.subNone0') && !/GB|MB/.test(st.textContent), stText());
    const modCard = $('#homeMods .gcard:not(.ghost) .gsub');
    add('ホームの MOD のカード: 未インストールに見本の大きさを付けない', !!modCard && modCard.textContent === H.t('mod.subNone0'), modCard ? modCard.textContent : 'no card');
    add('ホーム: 状態が変わってもホームのまま（status で MOD の画面へ飛ばない）', visibleViews() === 'view-home', visibleViews());
    /* 2026-10-01（公開前レビュー 2 回目）: 起動がホームになったので、プレイボタンの赤・橙の札は MOD の画面を開くまで見えない。
       修復が要る時は「未インストール」、Aegis が止めた時・Steam 版が更新された時は「インストール済み」と出ていた。
       今はプレイボタンの見出し（ps.<状態>）を、注意の色で */
    const homeWith = async (installed, pstate) => {
      window.__emit({ type:'event', name:'status', data:{ mode:'friend', installed, pstate, dll:'0.5.5', steamVersion:'2026.9.29', copyVersion:'2026.9.29', steamFound:true } });
      await tick();
    };
    /* the colour is the .attn rule's (in high contrast the system's own colours win, so the rule is looked at, not the pixels) */
    const attnOk = key => !!st && !st.hidden && H.t(key) !== key && st.textContent === H.t(key) && st.classList.contains('attn')
      && /color/.test(ruleText('.eyebrow .home-state.attn'));
    await homeWith(false, 'repair');
    add('ホームの状態の行: 修復が要る時は「' + H.t('ps.repair') + '」（未インストールではない）、注意の色', attnOk('ps.repair'), stText());
    await homeWith(true, 'blocked');
    add('ホームの状態の行: Aegis が止めた時は「' + H.t('ps.blocked') + '」（インストール済みではない）', attnOk('ps.blocked'), stText());
    await homeWith(true, 'sync');
    add('ホームの状態の行: Steam 版が更新された時は「' + H.t('ps.sync') + '」', attnOk('ps.sync'), stText());
    await homeWith(true, 'ready');
    add('ホームの状態の行: 直ったら「インストール済み」に戻り、注意の色も外れる', !!st && st.textContent === H.t('mod.sub', { v:'v0.5.5' }) && !st.classList.contains('attn'), stText());
    add('ホーム: ... その間もホームのまま', visibleViews() === 'view-home', visibleViews());
    window.__emit({ type:'event', name:'status', data:{ mode:'friend', installed:false, pstate:'install', steamFound:true } });
    await tick();

    /* the first install: from home, the library's インストール (the same FLOW as the play button) opens the MOD page,
       where the steps are shown. The translation question comes first (chatTranslate is not chosen yet); closing it ends
       the flow before anything is sent. */
    H.closeAll(); H.show('library');
    await tick();
    const lib = $('#libPrimary');
    add('初めてのインストール: ライブラリのボタンは「インストール」', !!lib && lib.dataset.cmd === 'install', lib ? (lib.dataset.cmd || lib.dataset.view) : 'no #libPrimary');
    const n0 = window.__sent.length;
    if (lib) lib.click();
    await tick();
    add('初めてのインストール: 押すと MOD の画面へ', visibleViews() === 'view-game', visibleViews());
    add('初めてのインストール: レールの「今のページ」は PocketRoles（ホームではない）',
      !!$('.rail .tile[data-view="game"][aria-current="page"]') && !$('.rail .rail-btn[data-view="home"][aria-current]'),
      [...document.querySelectorAll('.rail [aria-current="page"]')].map(b => b.dataset.view).join(','));
    H.closeAll();
    await tick();
    add('初めてのインストール: 翻訳の質問を閉じると、何も送らずに終わる', window.__sent.slice(n0).filter(m => m.cmd === 'install').length === 0,
      JSON.stringify(window.__sent.slice(n0).map(m => m.cmd)));
    /* the review hook for the same first install (spReview.setup) lands on the MOD page too */
    H.show('home');
    await tick();
    SR.setup();
    await tick();
    add('初めてのインストール（見本の流れ spReview.setup）も MOD の画面', visibleViews() === 'view-game', visibleViews());
    H.closeAll();
    await tick();
  }
  return out;
};
})();
