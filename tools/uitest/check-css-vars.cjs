/* tools/uitest/check-css-vars.cjs - var(--x) で使う名前が、どこかで決めてあるかを見る（画面は開かない・何も書かない）。
   2026-11-03: 最初の同意の画面と引き継ぎの窓の文字は var(--set-fg,#EDEEF5) だったが、--set-fg はどこにも決めておらず、
   明るいテーマでは予備の #EDEEF5 が地 #F4F7FC の上に出て読めなかった（1.08）。予備（var() の 2 つ目）があっても、
   決めていない名前は NG にする。
   見る組（組ごとに、使う名前が同じ組の中で決めてあるか）:
     design: design/launcher-proto/index.html
     ui:     ui/index.html と ui/*.js（ページが読む物。ui/legal などの下のフォルダーは見ない）
   「決めてある」: (1) CSS の宣言 --x: がある（JS の文字の中の CSS も）、
                   (2) JS が setProperty で入れる（その組に setProperty( があり、'--x' の文字がある。getPropertyValue などの読みは除く）。
   名前だけを見る。どの要素で決めたか（継承）までは見ない。
   もう 1 つ: 暗いテーマの 2 つの区画（@media (prefers-color-scheme: dark) の :root:not([data-theme="light"]) と
   :root[data-theme="dark"]）で、--set-* の名前の組が同じかを見る（片方にだけ決める抜けを捕まえる。
   tools/uitest/run.ps1 は prefers-color-scheme を切り替えるだけで、[data-theme="dark"] の区画を通らないため）。
   使い方: node tools/uitest/check-css-vars.cjs [--root <repo>]
   出力: ok / NG の行と「CSS vars: ok=n NG=m」。NG が 1 つでもあれば終了 1。 */
'use strict';
const fs = require('node:fs'), path = require('node:path');
const argRoot = process.argv.indexOf('--root');
const root = argRoot > 0 ? path.resolve(process.argv[argRoot + 1]) : path.resolve(__dirname, '../..');
const read = f => fs.readFileSync(path.join(root, f), 'utf8');
const lineOf = (t, i) => t.slice(0, i).split('\n').length;

function scan(sources) {          /* sources: [[名前, 文字]] */
  const used = new Map(), defined = new Map();
  const hasSetter = sources.some(([, t]) => /\.setProperty\(/.test(t));
  for (const [f, t] of sources) {
    for (const m of t.matchAll(/var\(\s*(--[\w-]+)/g)) {
      if (!used.has(m[1])) used.set(m[1], []);
      used.get(m[1]).push(f + ':' + lineOf(t, m.index));
    }
    for (const m of t.matchAll(/(?<![\w-])(--[\w-]+)\s*:/g))
      if (!defined.has(m[1])) defined.set(m[1], f + ':' + lineOf(t, m.index) + ' CSS');
    if (hasSetter)
      for (const m of t.matchAll(/(['"`])(--[\w-]+)\1/g)) {
        if (/(getPropertyValue|removeProperty)\(\s*$/.test(t.slice(Math.max(0, m.index - 24), m.index))) continue;
        if (!defined.has(m[2])) defined.set(m[2], f + ':' + lineOf(t, m.index) + ' JS');
      }
  }
  return { used, defined };
}

/* 暗いの 2 区画それぞれで決める --set-* の名前（同じ形の区画が 2 つ以上あれば合わせる。区画が無ければ null） */
function darkSets(t) {
  const s = t.replace(/\/\*[\s\S]*?\*\//g, '');
  const names = re => {
    const out = new Set(); let blocks = 0;
    for (const m of s.matchAll(re)) {
      const from = m.index + m[0].length, to = s.indexOf('}', from);
      if (to < 0) continue;
      blocks++;
      for (const d of s.slice(from, to).matchAll(/(?<![\w-])(--set-[\w-]+)\s*:/g)) out.add(d[1]);
    }
    return blocks ? out : null;
  };
  return [names(/:root:not\(\[data-theme="light"\]\)\s*\{/g), names(/:root\[data-theme="dark"\]\s*\{/g)];
}
const darkDiff = ([a, b]) => (a && b) ? [[...a].filter(k => !b.has(k)), [...b].filter(k => !a.has(k))] : null;

let ok = 0, ng = 0;
const line = (good, s) => { if (good) { ok++; console.log('ok ' + s); } else { ng++; console.error('NG ' + s); } };

/* 0. 点検そのものの確かめ: 予備のある決めていない名前を、必ず NG に数える（崩しで捕まえる） */
{
  const s = scan([['sample', ':root{--a:#fff} .x{color:var(--a)} .y{color:var(--b,#000)} .z{color:var(--c)}\nel.style.setProperty(\'--c\', 1)']]);
  const missing = [...s.used.keys()].filter(k => !s.defined.has(k));
  line(missing.join(',') === '--b', '[self] 予備があっても決めていない --b だけを NG に数える（' + (missing.join(',') || 'なし') + '）');
  const d = darkDiff(darkSets('@media (prefers-color-scheme: dark){\n  :root:not([data-theme="light"]){ --set-a:#000; --set-b:#111; }\n}\n' +
    ':root[data-theme="dark"]{ /* --set-b:#111; */ --set-a:#000; }'));
  line(!!d && d[0].join(',') === '--set-b' && d[1].length === 0, '[self] 暗いの片方の区画にしか無い --set-b を見つける（' + (d ? d[0].join(',') || 'なし' : '区画なし') + '）');
}

const uiDir = path.join(root, 'ui');
const groups = {
  design: ['design/launcher-proto/index.html'],
  ui: ['ui/index.html'].concat(fs.readdirSync(uiDir).filter(n => n.endsWith('.js')).sort().map(n => 'ui/' + n))
};
for (const [g, files] of Object.entries(groups)) {
  const { used, defined } = scan(files.map(f => [f, read(f)]));
  line(used.size > 0, '[' + g + '] var() を ' + used.size + ' 種類見つけた（' + files.length + ' ファイル）');
  const sets = darkSets(read(files[0])), dk = darkDiff(sets);
  line(!!dk && !dk[0].length && !dk[1].length && sets[0].size > 0, '[' + g + '] 暗いの 2 区画で --set-* の名前の組が同じ（' + files[0] + '・' +
    (dk ? sets[0].size + '／' + sets[1].size + ' 個' + (dk[0].length || dk[1].length ? '。@media の区画だけ: ' + (dk[0].join(', ') || 'なし') +
    '。[data-theme="dark"] の区画だけ: ' + (dk[1].join(', ') || 'なし') : '') : '区画が見つからない') + '）');
  for (const [k, where] of [...used].sort((a, b) => a[0] < b[0] ? -1 : 1)) {
    const d = defined.get(k);
    if (d) ok++;
    else line(false, '[' + g + '] ' + k + ' は決めていない。使う所: ' + where.join(', '));
  }
}
console.log('CSS vars: ok=' + ok + ' NG=' + ng);
process.exitCode = ng ? 1 : 0;
