#!/usr/bin/env node
'use strict';

// 公開 HTML と同梱 Markdown を、変換台本を使わずに照合する。Node 標準機能のみ。
// node tools/check-legal.cjs --source-dir "公開 HTML の場所" [--legal-dir ui/legal] [--self-test]
// 読むのは指定場所の terms/privacy/rules × ja/en/zh の HTML と対応する 9 md だけ。
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

function decode(text) {
  const named = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: '\u00a0' };
  return text.replace(/&(#x[0-9a-f]+|#[0-9]+|[a-z][a-z0-9]+);/gi, (all, key) => {
    if (key[0] === '#') return String.fromCodePoint(key[1].toLowerCase() === 'x' ? parseInt(key.slice(2), 16) : Number(key.slice(1)));
    if (!(key in named)) throw new Error('未対応の HTML 文字参照: ' + all);
    return named[key];
  });
}

function attr(tag, name) {
  const m = tag.match(new RegExp('\\b' + name + '\\s*=\\s*(?:"([^"]*)"|\'([^\']*)\'|([^\\s>]+))', 'i'));
  return m ? decode(m[1] ?? m[2] ?? m[3]) : null;
}

function htmlBody(source) {
  const mains = [...source.matchAll(/<main\b[^>]*>([\s\S]*?)<\/main>/gi)];
  if (mains.length !== 1) throw new Error('main が 1 個ではありません');
  let text = mains[0][1].replace(/<!--[\s\S]*?-->/g, '');
  text = text.replace(/<details\b[^>]*>[\s\S]*?<\/details>/gi, all => {
    if (!/\bui-acc--toc\b/.test(attr(all.slice(0, all.indexOf('>') + 1), 'class') || '')) throw new Error('目次以外の details は自動除外できません');
    return '';
  });
  text = text.replace(/<nav\b[^>]*>[\s\S]*?<\/nav>/gi, '');
  return text.replace(/<p\b[^>]*>[\s\S]*?<\/p>/gi, all => {
    const cls = attr(all.slice(0, all.indexOf('>') + 1), 'class') || '';
    return cls.split(/\s+/).includes('kicker') ? '' : all;
  });
}

// HTML のブロック境界を区切りにし、改行と字下げだけを正規化する。
// 本文内の連続した空白・全角空白・NBSP は保存して照合する。
function fromHtml(source) {
  const clean = htmlBody(source);
  const out = { segments: [], structure: [], links: [], strong: 0, code: 0 };
  const lists = [];
  const known = new Set('section div h1 h2 h3 h4 h5 h6 p ul ol li table thead tbody tr th td blockquote strong b code a em br hr'.split(' '));
  const blocks = new Set('section div h1 h2 h3 h4 h5 h6 p ul ol li table thead tbody tr th td blockquote br hr'.split(' '));
  let pending = '', quote = 0, row = null, link = null;
  function flush() {
    const value = pending.replace(/[\t ]*[\r\n]+[\t ]*/g, ' ').replace(/^[\t ]+|[\t ]+$/g, '');
    if (value) out.segments.push(value);
    pending = '';
  }
  for (const match of clean.matchAll(/<[^>]*>|[^<]+/g)) {
    const token = match[0];
    if (token[0] !== '<') {
      const value = decode(token);
      pending += value;
      if (link) link.text += value;
      continue;
    }
    const tag = token.match(/^<\s*(\/?)\s*([a-z0-9]+)/i);
    if (!tag) throw new Error('未対応の HTML 記法: ' + token.slice(0, 60));
    const name = tag[2].toLowerCase(), close = !!tag[1];
    if (!known.has(name)) throw new Error('未対応の HTML タグ: ' + name);
    if (blocks.has(name)) flush();
    if (!close) {
      if (/^h[1-6]$/.test(name)) out.structure.push('見出し:' + name.slice(1));
      if (name === 'ul' || name === 'ol') lists.push({ name, next: Number(attr(token, 'start') || '1') });
      if (name === 'li') {
        const list = lists[lists.length - 1];
        if (!list) throw new Error('リスト外の li');
        out.structure.push('項目:' + lists.length + ':' + (list.name === 'ul' ? '-' : list.next++) + ':引用' + quote);
      }
      if (name === 'blockquote') quote++;
      if (name === 'p' && quote) out.structure.push('引用段落:' + quote);
      if (name === 'tr') row = { head: false, cells: 0 };
      if (name === 'td' || name === 'th') {
        if (!row) throw new Error('行の外の表セル');
        row.cells++;
        if (name === 'th') row.head = true;
      }
      if (name === 'strong' || name === 'b') out.strong++;
      if (name === 'code') out.code++;
      if (name === 'a') {
        if (link) throw new Error('入れ子のリンク');
        link = { href: attr(token, 'href'), text: '' };
      }
    } else {
      if (name === 'ul' || name === 'ol') {
        if (lists.pop()?.name !== name) throw new Error('閉じていないリスト');
      }
      if (name === 'blockquote') quote--;
      if (name === 'tr') { out.structure.push('表行:' + (row.head ? '見出し' : '本文') + ':' + row.cells); row = null; }
      if (name === 'a') {
        if (!link || link.href === null) throw new Error('href の無いリンク');
        link.text = link.text.replace(/[\t ]*[\r\n]+[\t ]*/g, ' ').trim();
        out.links.push(link);
        link = null;
      }
    }
  }
  flush();
  if (lists.length || quote || link || row) throw new Error('HTML の構造が閉じていません');
  return out;
}

function inlineMd(text, out) {
  let plain = '', i = 0;
  while (i < text.length) {
    if (text[i] === '\\' && /[\\`*_[\]{}()#+.!>|-]/.test(text[i + 1] || '')) { plain += text[i + 1]; i += 2; continue; }
    if (text[i] === '`') {
      const end = text.indexOf('`', i + 1);
      if (end < 0) throw new Error('閉じていない code');
      out.code++;
      plain += text.slice(i + 1, end);
      i = end + 1;
      continue;
    }
    if (text.startsWith('**', i)) {
      const end = text.indexOf('**', i + 2);
      if (end < 0) throw new Error('閉じていない強調');
      out.strong++;
      plain += inlineMd(text.slice(i + 2, end), out);
      i = end + 2;
      continue;
    }
    if (text[i] === '[') {
      const link = text.slice(i).match(/^\[([^\]]*)\]\(([^\s)]*)\)/);
      if (link) {
        const label = inlineMd(link[1], out);
        out.links.push({ href: link[2], text: label });
        plain += label;
        i += link[0].length;
        continue;
      }
    }
    plain += text[i++];
  }
  return plain;
}

function tableCells(line) {
  const cells = [];
  let cell = '', code = false;
  for (let i = 1; i < line.length - 1; i++) {
    if (line[i] === '\\' && !code && i + 1 < line.length - 1) { cell += line[i] + line[++i]; continue; }
    if (line[i] === '`') code = !code;
    if (line[i] === '|' && !code) { cells.push(cell.trim()); cell = ''; } else cell += line[i];
  }
  cells.push(cell.trim());
  return cells;
}

function fromMarkdown(source) {
  const out = { segments: [], structure: [], links: [], strong: 0, code: 0 };
  const lines = source.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').split('\n');
  const listLevels = [];
  let previousQuote = false;
  for (let i = 0; i < lines.length; i++) {
    const original = lines[i];
    if (!original.trim()) { previousQuote = false; continue; }
    let line = original, quote = 0;
    while (/^\s*> ?/.test(line)) { line = line.replace(/^\s*> ?/, ''); quote++; }
    if (!line.trim()) { previousQuote = false; continue; }
    if (/^\s*---+\s*$/.test(line)) continue;
    const heading = line.match(/^(#{1,6}) +/);
    const list = line.match(/^( *)(-|[0-9]+\.) +/);
    if (heading) { out.structure.push('見出し:' + heading[1].length); line = line.slice(heading[0].length); }
    else if (list) {
      const indent = list[1].length;
      while (listLevels.length && listLevels[listLevels.length - 1].indent > indent) listLevels.pop();
      const parent = listLevels[listLevels.length - 1];
      if (!parent || parent.indent < indent) {
        const wanted = parent ? parent.indent + parent.width : 0;
        if (indent !== wanted) throw new Error('親項目の幅と字下げが合いません: ' + (i + 1) + ' 行');
        listLevels.push({ indent, width: 0 });
      }
      listLevels[listLevels.length - 1].width = Math.max(3, list[2].length + 1);
      out.structure.push('項目:' + listLevels.length + ':' + (list[2] === '-' ? '-' : Number(list[2].slice(0, -1))) + ':引用' + quote);
      line = line.slice(list[0].length);
    } else if (quote && !previousQuote) out.structure.push('引用段落:' + quote);
    previousQuote = !!quote;
    if (line.trim().startsWith('|') && line.trim().endsWith('|')) {
      const cells = tableCells(line.trim());
      if (cells.every(x => /^:?-{3,}:?$/.test(x))) continue;
      const next = (lines[i + 1] || '').trim();
      const head = next.startsWith('|') && tableCells(next).every(x => /^:?-{3,}:?$/.test(x));
      out.structure.push('表行:' + (head ? '見出し' : '本文') + ':' + cells.length);
      cells.forEach(cell => { const text = inlineMd(cell, out); if (text) out.segments.push(text); });
    } else {
      const text = inlineMd(line.trim(), out);
      if (text) out.segments.push(text);
    }
  }
  return out;
}

function difference(expected, actual) {
  const count = Math.max(expected.length, actual.length);
  for (let i = 0; i < count; i++) if (JSON.stringify(expected[i]) !== JSON.stringify(actual[i])) {
    const a = JSON.stringify(expected[i]), b = JSON.stringify(actual[i]);
    let at = 0;
    while (a && b && at < Math.min(a.length, b.length) && a[at] === b[at]) at++;
    const show = s => s === undefined ? '（無し）' : s.slice(Math.max(0, at - 24), at + 90);
    return (i + 1) + ' 番目: 公開=' + show(a) + ' / 同梱=' + show(b);
  }
  return null;
}

function checkOne(html, md) {
  const a = fromHtml(html), b = fromMarkdown(md);
  const version = lines => lines.slice(0, 12).some(s => /^(?:版|版本|Version)[:：] ?1\.3$/.test(s));
  const effective = lines => lines.slice(0, 12).some(s => /^(?:効力が生じる日時|このルールが始まる日（効力が生じる日）|Takes effect|Starts on \(takes effect\)|生效时间|本规则开始适用的日期（生效日）)[:：] ?2026-11-01 00:00(?:（日本時間）| \(Japan time\)|（日本时间）)$/.test(s));
  return {
    checks: [
      ['本文', difference(a.segments, b.segments)],
      ['構造', difference(a.structure, b.structure) || (a.strong !== b.strong || a.code !== b.code ? '強調/code の個数が違います' : null)],
      ['リンク', difference(a.links, b.links)],
      ['版', version(a.segments) && version(b.segments) ? null : '先頭の版が 1.3 と一致しません'],
      ['効力日', effective(a.segments) && effective(b.segments) ? null : '効力日の行が 2026-11-01 00:00 と一致しません'],
    ],
    counts: { blocks: a.segments.length, structure: a.structure.length, links: a.links.length },
  };
}

function selfTest() {
  const html = '<main><nav>除外</nav><p class="kicker">除外</p><h1>見出し</h1><ul><li>版: 1.3</li><li>効力が生じる日時: 2026-11-01 00:00（日本時間）</li></ul><p>A <strong>B</strong> C &amp; D <code>x</code></p><ol start="3"><li>項目<ul><li>子</li></ul></li></ol><blockquote><p>引用</p></blockquote><table><thead><tr><th>列</th></tr></thead><tbody><tr><td>値</td></tr></tbody></table><p><a href="https://example.invalid/">リンク</a></p></main>';
  const md = '# 見出し\n\n- 版: 1.3\n- 効力が生じる日時: 2026-11-01 00:00（日本時間）\n\nA **B** C & D `x`\n\n3. 項目\n   - 子\n\n> 引用\n\n| 列 |\n|---|\n| 値 |\n\n[リンク](https://example.invalid/)\n';
  assert(checkOne(html, md).checks.every(([, error]) => !error));
  assert(checkOne(html.replace('A <strong>', 'A  <strong>'), md.replace('A **', 'A  **')).checks.every(([, error]) => !error));
  assert(checkOne(html.replace('<td>値</td>', '<td><code>C:\\avatars\\</code></td>'), md.replace('| 値 |', '| `C:\\avatars\\` |')).checks.every(([, error]) => !error));
  const mutations = [
    ['文字', md.replace(' C &', ' X &'), '本文'],
    ['内部空白', md.replace(' C &', '  C &'), '本文'],
    ['削除', md.replace('   - 子\n', ''), '本文'],
    ['見出し', md.replace('# 見出し', '## 見出し'), '構造'],
    ['リスト番号', md.replace('3. 項目', '1. 項目'), '構造'],
    ['入れ子', md.replace('   - 子', '- 子'), '構造'],
    ['表', md.replace('| 値 |', '| 値 | 追加 |'), '構造'],
    ['リンク先', md.replace('https://example.invalid/', 'https://other.invalid/'), 'リンク'],
    ['版', md.replace('版: 1.3', '版: 1.2'), '版'],
    ['効力日', md.replace('2026-11-01', '2026-11-02'), '効力日'],
  ];
  for (const [name, changed, category] of mutations) assert(checkOne(html, changed).checks.find(([c]) => c === category)[1], name + 'の変更を検出できません');
  console.log('点検台本の自己点検: OK ' + (mutations.length + 3) + ' / NG 0');
}

function main(args) {
  let sourceDir, legalDir = path.resolve(__dirname, '../ui/legal'), runSelfTest = false;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--source-dir') sourceDir = args[++i];
    else if (args[i] === '--legal-dir') legalDir = args[++i];
    else if (args[i] === '--self-test') runSelfTest = true;
    else throw new Error('不明な引数: ' + args[i]);
  }
  if (runSelfTest) selfTest();
  if (!sourceDir) {
    if (runSelfTest) return;
    throw new Error('使い方: node tools/check-legal.cjs --source-dir "公開 HTML の場所" [--legal-dir ui/legal] [--self-test]');
  }
  let ok = 0, ng = 0, blocks = 0, structures = 0, links = 0;
  const totals = {};
  for (const doc of ['terms', 'privacy', 'rules']) for (const [htmlLang, mdLang] of [['ja', 'ja'], ['en', 'en'], ['zh', 'zh-CN']]) {
    const name = doc + '.' + mdLang + '.md';
    const html = fs.readFileSync(path.join(sourceDir, doc + '.' + htmlLang + '.html'), 'utf8');
    const md = fs.readFileSync(path.join(legalDir, name), 'utf8');
    const result = checkOne(html, md);
    blocks += result.counts.blocks; structures += result.counts.structure; links += result.counts.links;
    for (const [category, error] of result.checks) {
      totals[category] ||= { ok: 0, ng: 0 };
      if (error) { ng++; totals[category].ng++; console.log('NG ' + name + ' ' + category + ': ' + error); }
      else { ok++; totals[category].ok++; }
    }
    if (result.checks.every(([, error]) => !error)) console.log('OK ' + name + ': 本文・構造・リンク・版・効力日');
  }
  for (const [name, count] of Object.entries(totals)) console.log(name + ': OK ' + count.ok + ' / NG ' + count.ng);
  console.log('照合した本文単位 ' + blocks + ' / 構造単位 ' + structures + ' / リンク ' + links);
  console.log('結果: OK ' + ok + ' / NG ' + ng);
  if (ng) process.exitCode = 1;
}

try { main(process.argv.slice(2)); }
catch (error) { console.error('点検を完了できません: ' + error.message); process.exitCode = 1; }
