
/* ===== 最初の同意の画面（first-run.md ①） =====================================
   出すかどうかを決めるのはアプリです（"shell" イベントの firstRun が null でない時だけ）。
   この画面が閉じるまで、アプリはネットに一切つながりません（利用規約 第12条3項）。
   ページ側でこの画面を消しても意味はありません。アプリは Bridge.BeforeConsent の外の命令を
   needConsent で断り続けます。関所はアプリ側にあります。

   文書は ui\legal\ に同梱した物を読みます（同意の前にネットへ取りに行けないため）。
   読み込みは textContent でそのまま入れます（Markdown を HTML にしません）。
   法律の文に、書式の解釈ちがいや差し込みが起きる余地を作らないためです。      */
(function(){
  const FR_T = {
    ja: {
      step:'はじめに', title:'利用規約とプライバシーポリシー',
      lead:'StarPocket Client を使う前に、読んで同意してください。',
      again:'大事なところが変わりました。もう一度読んで、同意をお願いします。',
      sum:[
        '全部無料です（有料の機能はありません）。StarPocket Games は個人の開発者の活動名で、会社ではありません。',
        'PocketRoles と Client のソースは GPL-3.0 で、だれでもビルド・改造できます。この規約は、公式の Client と私たちのサービス（お知らせ・アップデート・定義ファイル・問い合わせ）の約束です。公式の Client を使うには、同意が必要です。',
        'Aegis は、この PC の中だけで調べます。PC のスキャンの結果は送りません。部屋で「確実」のチートを見つけて出した人は、Innersloth に通報します（最初はオフ。下のチェックで始められます）。',
        'チャット翻訳は最初はオフです。オンにすると、部屋のチャットの文を Google か DeepL に送ります。',
        'ルールに合わないと、サービスの一部を止めることがあります。その時は、理由とコードを出します。',
        '規約を大事なところで変える時は、もう一度この画面を出します。'
      ],
      tabTerms:'利用規約', tabPrivacy:'プライバシーポリシー', tabRules:'遊び方のルール',
      version:'版 {0}', openBrowser:'ブラウザで開く（準備中）', loading:'読み込んでいます…',
      openSoon:'この 3 つの文書は、まだウェブに出していません。上の箱でお読みください。',
      docBlocked:'文書を読み込めませんでした。読めていない物に同意はできません。Client を入れ直してください。',
      loadFail:'文書を開けませんでした。Client の入っているフォルダの ui\\legal\\{0} を見てください。',
      trLabel:'チャット翻訳を使う',
      trLine:'部屋のチャットの文（ほかの人の文も）を、翻訳のために Google（アメリカ）か DeepL（ドイツ）に送ります。名前と部屋コードは送りません。',
      trWhen:'使う時は、部屋のチャットの文（ほかの人の文も）が Google か DeepL に送られます。',
      trLater:'あとから「設定 → PocketRoles → チャット翻訳」で変えられます。',
      arLabel:'チートで部屋から出した人を、Innersloth に自動で通報する',
      arLine:'部屋で「確実」のチート（キルできない役職のキルなど）を見つけて出した人を、Among Us の通報の機能で、ゲームを作った会社 Innersloth（アメリカ）に知らせます。送るのは、通報の理由と、その人がだれかだけです。同じ人へは 30 日に 1 回までです。',
      arWhen:'使う時は、チートで部屋から出した人を Innersloth に通報します。部屋から出すことは、入れても入れなくても今までどおりです。',
      arLater:'あとから「設定 → PocketRoles → Aegis」で変えられます。',
      agree:'利用規約とプライバシーポリシーを読んで、同意します',
      minor:'18 歳未満の人は、おうちの人と一緒に読んで、一緒にチェックしてください。',
      need:'チェックを入れてください', accept:'同意する', decline:'同意しない', uninstall:'アンインストール',
      askTitle:'同意しないで、Client を閉じますか？',
      askBody:'公式の StarPocket Client は、利用規約とプライバシーポリシーに同意しないと使えません。閉じても、何も送らず、何も残しません。PocketRoles のソースは GPL-3.0 のままなので、GitHub の公開のソースから、だれでもビルドしたり改造したりできます。Client を消したい時は「アンインストール」を押してください。',
      askBodyAgain:'公式の StarPocket Client は、新しい利用規約とプライバシーポリシーに同意しないと使えません。閉じても、何も送りません。今入っているもの（MOD 用のコピー、設定、前の同意の記録、ログ）は消えません。消したい時は「アンインストール」を押してください。',
      back:'戻る', quit:'Client を閉じる',
      saveFail:'答えを保存できませんでした: {0}'
    },
    zh: {
      step:'开始之前', title:'使用条款和隐私政策',
      lead:'使用 StarPocket Client 之前，请阅读并同意。',
      again:'重要的部分有了变化。请再读一遍并同意。',
      sum:[
        '全部免费（没有付费功能）。StarPocket Games 是一名个人开发者的活动名称，不是公司。',
        'PocketRoles 和 Client 的源代码采用 GPL-3.0，任何人都可以编译和修改。本条款是关于官方 Client 和我们服务（通知、更新、定义文件、咨询）的约定。使用官方 Client 需要同意。',
        'Aegis 只在这台电脑里检查，电脑扫描的结果不会发送。在房间里因"确定"等级的作弊被移出的人，会被举报给 Innersloth（默认关闭，可以用下方的勾选框开启）。',
        '聊天翻译默认关闭。开启后，会把房间的聊天内容发送给 Google 或 DeepL。',
        '不符合规则时，可能会停止部分服务。那时会显示理由和代码。',
        '条款的重要部分有变化时，会再次显示这个画面。'
      ],
      tabTerms:'使用条款', tabPrivacy:'隐私政策', tabRules:'游戏规则',
      version:'版本 {0}', openBrowser:'在浏览器中打开（准备中）', loading:'正在载入…',
      openSoon:'这三份文件尚未发布到网上。请在上面的框中阅读。',
      docBlocked:'无法载入文件。无法同意没有显示出来的内容。请重新安装 Client。',
      loadFail:'无法打开文档。请查看 Client 所在文件夹的 ui\\legal\\{0}。',
      trLabel:'使用聊天翻译',
      trLine:'会把房间的聊天内容（包括其他人的）发送给 Google（美国）或 DeepL（德国）进行翻译。不发送名字和房间代码。',
      trWhen:'使用时，房间的聊天内容（包括其他人的）会发送给 Google 或 DeepL。',
      trLater:'以后可以在"设置 → PocketRoles → 聊天翻译"中更改。',
      arLabel:'自动向 Innersloth 举报因作弊被移出房间的人',
      arLine:'在房间里发现"确定"等级的作弊（没有击杀能力的职业却击杀等）并移出的人，会用 Among Us 的举报功能告知游戏开发公司 Innersloth（美国）。只发送举报理由和是谁。同一个人 30 天最多 1 次。',
      arWhen:'使用时，会把因作弊被移出房间的人举报给 Innersloth。无论勾选与否，移出房间都照常进行。',
      arLater:'以后可以在"设置 → PocketRoles → Aegis"中更改。',
      agree:'我已阅读并同意使用条款和隐私政策',
      minor:'未满 18 岁的人，请和家长一起阅读，一起勾选。',
      need:'请勾选', accept:'同意', decline:'不同意', uninstall:'卸载',
      askTitle:'不同意并关闭 Client 吗？',
      askBody:'不同意使用条款和隐私政策，就不能使用官方 StarPocket Client。关闭时不会发送任何东西，也不会保存任何东西。PocketRoles 的源代码仍采用 GPL-3.0，任何人都可以从 GitHub 上公开的源代码编译和修改。想删除 Client 时，请按"卸载"。',
      askBodyAgain:'不同意新的使用条款和隐私政策，就不能使用官方 StarPocket Client。关闭时不会发送任何东西。已经安装的东西（MOD 用的副本、设置、以前的同意记录、日志）不会被删除。想删除时，请按"卸载"。',
      back:'返回', quit:'关闭 Client',
      saveFail:'无法保存你的回答：{0}'
    },
    en: {
      step:'Getting started', title:'Terms of Use and Privacy Policy',
      lead:'Please read and agree before using StarPocket Client.',
      again:'Something important has changed. Please read it again and agree.',
      sum:[
        'Everything is free (no paid features). StarPocket Games is the activity name of one individual developer, not a company.',
        'The source of PocketRoles and the Client is GPL-3.0, and anyone can build and modify it. These terms are about the official Client and our services (news, updates, the definitions file, support). You need to agree to use the official Client.',
        'Aegis checks only inside this PC. The results of the PC scan are not sent. Players removed for a "Certain" cheat in a room are reported to Innersloth (off at first; you can turn it on with the checkbox below).',
        'Chat translation is off at first. If you turn it on, the room’s chat text is sent to Google or DeepL.',
        'If something doesn’t follow the rules, part of the services may be stopped. We’ll show the reason and a code.',
        'If the terms change in an important way, this screen appears again.'
      ],
      tabTerms:'Terms of Use', tabPrivacy:'Privacy Policy', tabRules:'Play Rules',
      version:'Version {0}', openBrowser:'Open in browser (coming soon)', loading:'Loading…',
      openSoon:'These three documents are not on the web yet. Please read them in the box above.',
      docBlocked:'The documents could not be loaded. You cannot agree to text that was not shown. Please reinstall the Client.',
      loadFail:'The document could not be opened. Look in ui\\legal\\{0} next to the Client.',
      trLabel:'Use chat translation',
      trLine:'The room’s chat text (including other people’s) is sent to Google (US) or DeepL (Germany) for translation. Names and room codes are not sent.',
      trWhen:'When it is on, the room’s chat text (including other people’s) is sent to Google or DeepL.',
      trLater:'You can change this later in Settings → PocketRoles → Chat translation.',
      arLabel:'Automatically report players removed for cheating to Innersloth',
      arLine:'Players removed for a "Certain" cheat in a room (such as killing with a role that cannot kill) are reported with Among Us’s own report feature to Innersloth (US), the company that makes the game. Only the reason and who the player is are sent. At most once every 30 days for the same player.',
      arWhen:'When it is on, players removed for cheating are reported to Innersloth. They are still removed from the room either way, as before.',
      arLater:'You can change this later in Settings → PocketRoles → Aegis.',
      agree:'I have read and agree to the Terms of Use and the Privacy Policy',
      minor:'If you are under 18, read this together with a parent or guardian and tick the box together.',
      need:'Please tick the box', accept:'Agree', decline:'Decline', uninstall:'Uninstall',
      askTitle:'Close the Client without agreeing?',
      askBody:'You can’t use the official StarPocket Client without agreeing to the Terms of Use and the Privacy Policy. Closing it sends nothing and keeps nothing. The PocketRoles source stays GPL-3.0, so anyone can build and modify it from the public source on GitHub. To remove the Client, press "Uninstall".',
      askBodyAgain:'You can’t use the official StarPocket Client without agreeing to the new Terms of Use and Privacy Policy. Closing it sends nothing. What is already installed (the mod copy, settings, the earlier consent record, logs) is not removed. To remove it, press "Uninstall".',
      back:'Back', quit:'Close the Client',
      saveFail:'Your answer could not be saved: {0}'
    }
  };

  /* 文書のファイル名は言語ごとに決め打ちです。ページから来た文字列を混ぜません
     （混ぜると ..\ を書かれた時に、同梱した物以外を開けてしまいます）。 */
  const FILES = {
    terms:   { ja:'terms.ja.md',   zh:'terms.zh-CN.md',   en:'terms.en.md' },
    privacy: { ja:'privacy.ja.md', zh:'privacy.zh-CN.md', en:'privacy.en.md' },
    rules:   { ja:'rules.ja.md',   zh:'rules.zh-CN.md',   en:'rules.en.md' }
  };
  /* 同じ文書のウェブ版。2026-09-24 現在、この 3 本はどれも公開していません
     （terms と rules はページ自体が無く、privacy は会社のページに別の文で在ります）。
     そのうえ openExternal は Bridge.Later にあって Supported にも BeforeConsent にも無く、
     ShellOpen.AllowedWebPages も WebView2 の配布ページ 1 本だけです。
     つまり住所を書き換えても開きません。開けるようにするには C# 側で 3 か所
     （Supported へ移す・BeforeConsent に足す・AllowedWebPages に足す）が要ります。
     それまでは下のボタンを押せなくしてあります。この表は、その時の下書きとして残します。 */
  const SITE = {
    terms:'https://starpocketgames.com/{lang}/terms/',
    privacy:'https://starpocketgames.com/{lang}/privacy/',
    rules:'https://starpocketgames.com/{lang}/rules/'
  };

  let info = null;          /* アプリが送ってきた firstRun（版の番号など） */
  let doc = 'terms';        /* 今どのタブか */
  let sending = false;
  const cache = new Map();  /* 一度読んだ文書は覚えます（タブを戻した時に待たせない） */

  const el = id => document.getElementById(id);
  const L = () => (FR_T[typeof lang !== 'undefined' && FR_T[lang] ? lang : 'ja']);
  const fmt = (s, a) => String(s).replace('{0}', a);

  function verOf(which){
    if (!info) return '';
    return which === 'terms' ? info.terms : which === 'privacy' ? info.privacy : info.rules;
  }

  function paint(){
    const d = L();
    el('frStep').textContent = d.step;
    el('frTitle').textContent = d.title;
    el('frLead').textContent = d.lead;
    el('frAgain').textContent = d.again;
    el('frAgain').hidden = !(info && info.again);

    const sum = el('frSum'); sum.textContent = '';
    for (const line of d.sum) { const li = document.createElement('li'); li.textContent = line; sum.appendChild(li); }

    el('frTab-terms').textContent = d.tabTerms;
    el('frTab-privacy').textContent = d.tabPrivacy;
    el('frTab-rules').textContent = d.tabRules;
    el('frVer').textContent = fmt(d.version, verOf(doc));
    /* 行き先がまだ無いので押せなくします。設定の footer の「利用規約（準備中）」と同じ扱いです。
       押せる見た目で何も起きないのが、同意の画面では一番よくない形でした。 */
    const openBtn = el('frOpen');
    openBtn.textContent = d.openBrowser;
    openBtn.disabled = true;
    openBtn.title = d.openSoon;

    el('frTrLabel').textContent = d.trLabel;
    el('frTrLine').textContent = d.trLine;
    el('frTrWhen').textContent = d.trWhen;
    el('frTrLater').textContent = d.trLater;
    el('frArLabel').textContent = d.arLabel;
    el('frArLine').textContent = d.arLine;
    el('frArWhen').textContent = d.arWhen;
    el('frArLater').textContent = d.arLater;

    el('frAgreeLabel').textContent = d.agree;
    el('frMinor').textContent = d.minor;
    el('frNeed').textContent = d.need;
    el('frAccept').textContent = d.accept;
    el('frDecline').textContent = d.decline;
    el('frUninstall').textContent = d.uninstall;

    el('frAskTitle').textContent = d.askTitle;
    el('frAskBody').textContent = (info && info.again) ? d.askBodyAgain : d.askBody;
    el('frAskBack').textContent = d.back;
    el('frAskQuit').textContent = d.quit;
    el('frAskUninstall').textContent = d.uninstall;

    for (const b of el('frLangs').querySelectorAll('[data-frlang]'))
      b.setAttribute('aria-pressed', String(b.dataset.frlang === (typeof lang !== 'undefined' ? lang : 'ja')));

    loadDoc();
  }

  async function loadDoc(){
    const d = L();
    const lg = (typeof lang !== 'undefined' && FILES.terms[lang]) ? lang : 'ja';
    const name = FILES[doc][lg];
    const box = el('frDocText');
    el('frVer').textContent = fmt(d.version, verOf(doc));
    if (cache.has(name)) { box.textContent = cache.get(name); box.scrollTop = 0; docsOk.add(doc); setAccept(); return; }
    box.textContent = d.loading;
    try {
      const res = await fetch('legal/' + name, { cache:'no-store' });
      if (!res.ok) throw new Error(String(res.status));
      const text = await res.text();
      cache.set(name, text);
      docsOk.add(doc);
      if (FILES[doc][lg] === name) { box.textContent = text; box.scrollTop = 0; }
    } catch (err) {
      docsOk.delete(doc);
      box.textContent = fmt(d.loadFail, name);
    }
    setAccept();
  }

  function pickTab(which){
    doc = which;
    for (const b of el('frTabs').querySelectorAll('[data-frdoc]')) {
      const on = b.dataset.frdoc === which;
      b.setAttribute('aria-selected', String(on));
      b.tabIndex = on ? 0 : -1;
    }
    el('frDocText').setAttribute('aria-labelledby', 'frTab-' + which);
    loadDoc();
  }

  /* 3 本とも読み込めたか。読めていない物には同意させません。
     ここが無いと、ui\legal\ が欠けた配布物でも「版 0.9 に同意した」と記録だけが残ります。
     AppInfo.cs が嫌っている「記録が、見せていない物を指す」状態そのものです。 */
  const docsOk = new Set();

  function docsReady(){ return docsOk.size === 3; }

  function setAccept(){
    const on = el('frAgree').checked && docsReady();
    el('frAccept').setAttribute('aria-disabled', String(!on));
    el('frAgree').disabled = !docsReady();
    el('frDocWarn').hidden = docsReady();
    if (!docsReady()) el('frDocWarn').textContent = L().docBlocked;
    if (on) el('frAgreeBox').removeAttribute('data-need');
  }

  /* 画面を出す時に 3 本とも先に読んでおきます（人がタブを押さなくても数えられるように）。 */
  async function preloadDocs(){
    const lg = (typeof lang !== 'undefined' && FILES.terms[lang]) ? lang : 'ja';
    await Promise.all(['terms', 'privacy', 'rules'].map(async which => {
      const name = FILES[which][lg];
      if (cache.has(name)) { docsOk.add(which); return; }
      try {
        const res = await fetch('legal/' + name, { cache:'no-store' });
        if (!res.ok) throw new Error(String(res.status));
        cache.set(name, await res.text());
        docsOk.add(which);
      } catch (err) {
        docsOk.delete(which);
      }
    }));
    setAccept();
  }

  async function accept(){
    if (sending) return;
    /* 画面の作りを直に触られても、読めていない物には同意させません */
    if (!docsReady()) { setAccept(); el('frDocWarn').focus(); return; }
    if (!el('frAgree').checked) {
      el('frAgreeBox').setAttribute('data-need', '');
      el('frAgree').focus();
      return;
    }
    sending = true;
    const r = await bridge.invoke('consent.set', {
      agreed: true,
      terms: info ? info.terms : '', privacy: info ? info.privacy : '', rules: info ? info.rules : '',
      chatTranslate: el('frTr').checked ? 'on' : 'off',
      autoReport: el('frAr').checked ? 'on' : 'off'
    });
    sending = false;
    if (r && r.ok) { close(); return; }
    /* 保存できなかった時は閉じません。記録が無いまま先へ進ませない、というのがこの画面の役目です */
    el('frNeed').textContent = fmt(L().saveFail, (r && (r.error || r.reason)) || '?');
    el('frAgreeBox').setAttribute('data-need', '');
  }

  function close(){
    el('firstrun').removeAttribute('data-open');
    el('firstrun').hidden = true;
    document.body.removeAttribute('inert');
    for (const n of document.body.children) if (n.id !== 'firstrun' && n.id !== 'frAsk') n.removeAttribute('inert');
  }

  function show(i){
    info = i || { terms:'?', privacy:'?', rules:'?' };
    el('frTr').checked = !!info.chatTranslate;
    el('frAr').checked = false;                /* 自動の通報は、移ってきた人でも必ずオフから（D-40） */
    el('frTrBox').toggleAttribute('data-on', el('frTr').checked);
    el('frArBox').removeAttribute('data-on');
    el('frAgree').checked = false;
    docsOk.clear();
    setAccept();
    preloadDocs();          /* 待ちません。読めた物から数えて、3 本そろったら同意できるようになります */
    pickTab('terms');
    paint();
    /* 後ろを触れなくします。Tab で本体のボタンへ抜けられると、押せてしまう物が出ます */
    for (const n of document.body.children) if (n.id !== 'firstrun' && n.id !== 'frAsk') n.setAttribute('inert', '');
    el('firstrun').hidden = false;
    el('firstrun').setAttribute('data-open', '');
    requestAnimationFrame(() => el('frDocText').focus({ preventScroll:true }));
  }

  function ask(open){
    el('frAsk').hidden = !open;
    el('frAsk').toggleAttribute('data-open', open);
    if (open) requestAnimationFrame(() => el('frAskBack').focus());
    else el('frDecline').focus();
  }

  /* ---- 結線 ---- */
  el('frTabs').addEventListener('click', e => { const b = e.target.closest('[data-frdoc]'); if (b) pickTab(b.dataset.frdoc); });
  el('frTabs').addEventListener('keydown', e => {
    const order = ['terms', 'privacy', 'rules'];
    const i = order.indexOf(doc);
    if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
      e.preventDefault();
      const n = order[(i + (e.key === 'ArrowRight' ? 1 : order.length - 1)) % order.length];
      pickTab(n); el('frTab-' + n).focus();
    }
  });
  el('frLangs').addEventListener('click', e => {
    const b = e.target.closest('[data-frlang]');
    if (!b) return;
    setLangPref(b.dataset.frlang);          /* 本体の言語も一緒に変えます（この画面で選んだ言語が Client の言語） */
    bridge.invoke('setLang', { lang: b.dataset.frlang });
  });
  el('frTr').addEventListener('change', () => el('frTrBox').toggleAttribute('data-on', el('frTr').checked));
  el('frAr').addEventListener('change', () => el('frArBox').toggleAttribute('data-on', el('frAr').checked));
  el('frAgree').addEventListener('change', setAccept);
  el('frAccept').addEventListener('click', accept);
  el('frDecline').addEventListener('click', () => ask(true));
  el('frAskBack').addEventListener('click', () => ask(false));
  el('frAskQuit').addEventListener('click', () => bridge.invoke('consent.decline'));
  el('frUninstall').addEventListener('click', () => bridge.invoke('uninstall', {}));
  el('frAskUninstall').addEventListener('click', () => bridge.invoke('uninstall', {}));
  /* frOpen は押せません（上の paint を参照）。開けるようにする日が来たら、ここで
     bridge.invoke('openExternal', { url: SITE[doc].replace('{lang}', lg === 'zh' ? 'zh-CN' : lg) })
     を返してください。lang が 'zh' の時にファイル名が 'zh-CN' になる点に注意（公開ページ側は zh です）。 */
  /* Esc は「同意しない」と同じ確認へ。何もせず消える道は作りません */
  document.addEventListener('keydown', e => {
    if (e.key !== 'Escape' || el('firstrun').hidden) return;
    e.preventDefault(); e.stopPropagation();
    if (el('frAsk').hidden) ask(true); else ask(false);
  }, true);

  document.addEventListener('host:shell', e => { const d = e.detail || {}; if (d.firstRun) show(d.firstRun); });

  window.frApply = () => { if (!el('firstrun').hidden) paint(); };
  /* 見本を見る時だけ（本物はアプリが出します） */
  window.spFirstRun = i => show(i || { terms:'0.9', privacy:'0.9', rules:'0.8', chatTranslate:false, again:false });
})();
