#!/usr/bin/env node
'use strict';
// 公開 HTML の本文だけを Markdown にする。Node.js 標準機能のみ、通信なし。
// node tools/import-legal.cjs --source <公開HTMLのフォルダー> [--check]
// 次の版でも同じコマンドで9文書を作り直す。文言・版・日付の置換は行わない。
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const args = process.argv.slice(2);
const sourceAt = args.indexOf('--source');
if (sourceAt < 0 || !args[sourceAt + 1] ||
    args.some((arg, i) => i !== sourceAt + 1 && !['--source', '--check'].includes(arg))) {
  console.error('使い方: node tools/import-legal.cjs --source <公開HTMLのフォルダー> [--check]');
  process.exit(2);
}
const source = path.resolve(args[sourceAt + 1]);
const checkOnly = args.includes('--check');
const inlineTags = new Set(['strong', 'b', 'em', 'i', 'code', 'a', 'span', 'br']);
const containerTags = new Set(['main', 'section', 'div', 'thead', 'tbody', 'tfoot']);

function decode(text) {
  const named = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: '\u00a0' };
  return text.replace(/&(#x[\da-f]+|#\d+|[a-z][\da-z]+);/gi, (whole, key) => {
    if (key[0] === '#') return String.fromCodePoint(key[1].toLowerCase() === 'x'
      ? parseInt(key.slice(2), 16) : parseInt(key.slice(1), 10));
    assert(Object.hasOwn(named, key), `未対応の文字参照: ${whole}`);
    return named[key];
  });
}

// 対象の公開ページは閉じタグを明記した HTML。曖昧な構造や未対応タグは黙って捨てず停止。
function parse(html) {
  const mains = [...html.matchAll(/<main\b[^>]*>([\s\S]*?)<\/main\s*>/gi)];
  assert.equal(mains.length, 1, 'main は1つ必要');
  const tree = { tag: 'main', attrs: {}, children: [] };
  const stack = [tree];
  const tokens = mains[0][1].match(/<!--[\s\S]*?-->|<[^>]*>|[^<]+/g) || [];
  for (const token of tokens) {
    if (token.startsWith('<!--')) continue;
    if (!token.startsWith('<')) { stack.at(-1).children.push(decode(token)); continue; }
    const close = token.match(/^<\/([\w-]+)\s*>$/);
    if (close) {
      assert.equal(stack.at(-1).tag, close[1].toLowerCase(), `閉じタグ不一致: ${token}`);
      stack.pop();
      continue;
    }
    const open = token.match(/^<([\w-]+)\b([\s\S]*?)\/?\s*>$/);
    assert(open, `未対応のタグ: ${token}`);
    const attrs = {};
    for (const m of open[2].matchAll(/([\w:-]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'=<>`]+)))?/g)) {
      const name = m[1].toLowerCase();
      assert(!Object.hasOwn(attrs, name), `属性が重複しています: ${name}`);
      attrs[name] = decode(m[2] ?? m[3] ?? m[4] ?? '');
    }
    const node = { tag: open[1].toLowerCase(), attrs, children: [] };
    stack.at(-1).children.push(node);
    if (node.tag !== 'br') stack.push(node);
  }
  assert.equal(stack.length, 1, '閉じていないタグ');
  return tree;
}

function skipped(node) {
  // パンくず・目次・分類ラベルはサイトの操作部分。h1/導入/更新日/本文は全部残す。
  return node.tag === 'nav' || node.tag === 'details' ||
    (node.tag === 'p' && (node.attrs.class || '').split(/\s+/).includes('kicker'));
}

function inline(node) {
  if (typeof node === 'string') return node.replace(/[\t ]*\r?\n[\t ]*/g, ' ');
  assert(inlineTags.has(node.tag), `インライン内の未対応タグ: ${node.tag}`);
  if (node.tag === 'br') return '\n';
  const content = node.children.map(inline).join('');
  if (node.tag === 'strong' || node.tag === 'b') return `**${content}**`;
  if (node.tag === 'em' || node.tag === 'i') return `*${content}*`;
  if (node.tag === 'code') {
    assert(!content.includes('`'), 'コード内のバッククォートには対応していません');
    return '`' + content + '`';
  }
  if (node.tag === 'a') {
    assert(node.attrs.href && !/[\s()]/.test(node.attrs.href), '未対応のリンク先');
    return `[${content}](${node.attrs.href})`;
  }
  return content;
}

function flow(children) {
  const blocks = [];
  let pending = [];
  const flush = () => {
    const value = pending.map(inline).join('').trim();
    if (value) blocks.push(value);
    pending = [];
  };
  for (const child of children) {
    if (typeof child === 'string' || inlineTags.has(child.tag)) pending.push(child);
    else { flush(); const value = block(child); if (value) blocks.push(value); }
  }
  flush();
  return blocks.join('\n\n');
}

function block(node) {
  if (skipped(node)) return '';
  if (containerTags.has(node.tag)) return flow(node.children);
  if (/^h[1-6]$/.test(node.tag)) return '#'.repeat(Number(node.tag[1])) + ' ' + node.children.map(inline).join('').trim();
  if (node.tag === 'p') return node.children.map(inline).join('').trim();
  if (node.tag === 'blockquote') return flow(node.children).split('\n').map(line => line ? '> ' + line : '>').join('\n');
  if (node.tag === 'ul' || node.tag === 'ol') {
    assert(!Object.hasOwn(node.attrs, 'reversed') && !Object.hasOwn(node.attrs, 'type'), 'リストの reversed/type は未対応');
    let index = Number(node.attrs.start || 1);
    assert(Number.isInteger(index) && index > 0, '不正なリスト開始番号');
    return node.children.filter(child => typeof child !== 'string' || child.trim()).map(child => {
      assert.equal(child.tag, 'li', 'リスト内に li 以外があります');
      assert(!Object.hasOwn(child.attrs, 'value'), 'li value は未対応');
      const prefix = node.tag === 'ol' ? `${index++}. ` : '- ';
      const lines = flow(child.children).split('\n');
      // 元の同梱文書と同じ3空白。10項以上はCommonMarkでも続きになる幅を確保する。
      const indent = ' '.repeat(Math.max(3, prefix.length));
      return prefix + lines[0] + lines.slice(1).map(line => '\n' + (line ? indent + line : '')).join('');
    }).join('\n');
  }
  if (node.tag === 'table') {
    const rows = [];
    function visit(part) {
      if (typeof part === 'string') { assert(!part.trim(), '表内の予期しない文字'); return; }
      if (part.tag === 'tr') { rows.push(part); return; }
      assert(['table', 'thead', 'tbody', 'tfoot'].includes(part.tag), `表内の未対応タグ: ${part.tag}`);
      part.children.forEach(visit);
    }
    visit(node);
    assert(rows.length, '空の表');
    const cells = rows.map(row => row.children.filter(child => typeof child !== 'string' || child.trim()));
    assert(cells[0].every(cell => cell.tag === 'th'), '表の先頭は見出し行が必要');
    const lines = cells.map(row => {
      assert.equal(row.length, cells[0].length, '表の列数不一致');
      return '| ' + row.map(cell => {
        assert(['th', 'td'].includes(cell.tag) && !Object.hasOwn(cell.attrs, 'colspan') && !Object.hasOwn(cell.attrs, 'rowspan'), '未対応の表セル');
        const text = cell.children.map(inline).join('').trim();
        assert(!text.includes('\n'), 'セル内改行は未対応');
        return text.replace(/\|/g, '\\|');
      }).join(' | ') + ' |';
    });
    lines.splice(1, 0, '|' + cells[0].map(() => '---').join('|') + '|');
    return lines.join('\n');
  }
  throw new Error(`本文に未対応のタグ: ${node.tag}`);
}

function format(bytes) {
  const text = bytes.toString('utf8');
  const cr = (text.match(/\r/g) || []).length;
  const lf = (text.match(/\n/g) || []).length;
  assert(cr === 0 || (cr === lf && !/(?<!\r)\n|\r(?!\n)/.test(text)), '混在改行は手動確認が必要');
  return { cr, lf, bom: text.startsWith('\ufeff'), newline: cr ? '\r\n' : '\n' };
}

try {
  // 全9文書を先に変換してから書き込む。未対応構造があれば、1件も上書きしない。
  const files = [];
  for (const kind of ['terms', 'privacy', 'rules']) for (const [htmlLang, mdLang] of [['ja', 'ja'], ['en', 'en'], ['zh', 'zh-CN']]) {
    const name = `${kind}.${mdLang}.md`;
    const destination = path.join(root, 'ui', 'legal', name);
    const before = fs.readFileSync(destination);
    const style = format(before);
    const html = fs.readFileSync(path.join(source, `${kind}.${htmlLang}.html`), 'utf8');
    const markdown = block(parse(html)).trim() + '\n';
    assert(markdown.startsWith('# '), `${name}: 表題がありません`);
    const after = Buffer.from((style.bom ? '\ufeff' : '') + markdown.replace(/\n/g, style.newline), 'utf8');
    files.push({ name, destination, before, after, style });
  }
  let changed = 0;
  for (const file of files) {
    const differs = !file.before.equals(file.after);
    if (differs) { changed++; if (!checkOnly) fs.writeFileSync(file.destination, file.after); }
    const end = format(file.after);
    console.log(`${file.name}: ${differs ? (checkOnly ? '不一致' : '更新') : '一致'}; CR ${file.style.cr}→${end.cr}; LF ${file.style.lf}→${end.lf}; BOM ${file.style.bom ? '有' : '無'}→${end.bom ? '有' : '無'}`);
  }
  console.log(`文書 ${files.length} 件、${checkOnly ? '不一致' : '更新'} ${changed} 件、手作業の文言補正 0 件`);
  if (checkOnly && changed) process.exitCode = 1;
} catch (error) {
  console.error('変換を停止しました: ' + error.message);
  process.exitCode = 1;
}
