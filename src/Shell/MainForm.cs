// The main window: borderless, 1280x720 logical pixels in the middle of the screen (SPEC 5.4), hosting the UI in
// WebView2 from the files next to the exe (SetVirtualHostNameToFolderMapping). Nothing leaves the PC: navigations
// elsewhere are cancelled, new windows and downloads refused, and every request not for app.starpocket.local answers 403
// (PORT-MAP 4.2). Messages are taken only from our own origin. The close button / Alt+F4 is decided by ClientApp
// (tray or quit, PORT-MAP 4.3).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Starpocket.Client.Shell
{
    internal sealed class MainForm : Form
    {
        static readonly int WM_TaskbarButtonCreated = Native.RegisterWindowMessage("TaskbarButtonCreated");
        static readonly Color Night = Color.FromArgb(0x0B, 0x0C, 0x22);

        public readonly WebView2 Web;
        CoreWebView2Environment environment;
        readonly Action<string> log;

        /// <summary>The close button, Alt+F4 or the system menu's Close (the app decides: hide to the tray or quit).</summary>
        public event EventHandler CloseRequested;
        public event EventHandler TaskbarButtonCreated;
        /// <summary>A message from our own page (WebMessageAsJson).</summary>
        public event Action<string> WebMessage;
        /// <summary>The page finished loading (again after a reload).</summary>
        public event EventHandler UiReady;
        /// <summary>v1.2: 薄くなっている最中に窓がもう一度求められて、演出を途中でやめた（EnsureOpaque）。
        /// 隠すことも終わることも起きないので、ClientApp は「閉じかけ」を取り消してページにも知らせる。</summary>
        public event EventHandler FadeCancelled;

        /// <summary>Set by ClientApp before it closes the window for good.</summary>
        public bool AllowClose;
        /// <summary>WebView2 handles the page's app-region: drag strips itself (else the UI asks for window.drag).</summary>
        public bool DragRegionSupported { get; private set; }

        /// <summary>2026-10-01（公開前レビュー）: true の間だけ、WinForms の Show() が窓をアクティブにしない
        /// （<see cref="ShowWithoutActivation"/>。Show() は SW_SHOWNOACTIVATE で出す）。<see cref="ShowNoActivate"/> だけが立てる。</summary>
        public bool NoActivate;
        protected override bool ShowWithoutActivation => NoActivate;

        /// <summary>
        /// 2026-10-01（公開前レビューの指摘・見えない窓で実測）: ゲームが終わった時に、**フォーカスを取らずに**窓を戻す（SPEC 5.4）。
        /// トレイへしまう時は「最小化してから Hide()」（<see cref="LeaveToTray"/>）。その窓を SW_SHOWNOACTIVATE だけで戻すと、
        /// 窓は画面に出るのに **WinForms の Visible は false のまま**残る（Hide() で消した印を、WinForms は自分の Show() でしか戻さない）。
        /// すると次の ✕ が「見えていない窓」として扱われ（CloseFade.PlannedLeave）、ShowWindow は Show() をもう一度呼ぶ。
        /// だから SW_SHOWNOACTIVATE で最小化を解いた**後で**、まだ Visible が false なら、アクティブにしない Show() で印を戻す。
        /// 順番が大事: 先に Show() を呼ぶと、WindowState が Minimized のままなので WinForms は SW_SHOWMINIMIZED で出してしまう。
        /// ShowWindow が投げた時は、そのまま呼んだ側へ（呼んだ側が普通の Show() に切り替える）。
        /// </summary>
        public void ShowNoActivate()
        {
            Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
            if (Visible) return;
            NoActivate = true;
            try { Show(); }
            finally { NoActivate = false; }
        }

        /// <summary>v1.2: 優しく閉じる（CloseFade.cs）。null か Running でない間は演出中ではない。</summary>
        CloseFade fade;
        System.Windows.Forms.Timer fadeTimer;   // ほかの Timer（System.Threading / System.Timers）と取り違えないよう名前ごと書く

        public MainForm(Action<string> log)
        {
            this.log = log ?? (_ => { });
            Text = AppInfo.Name;
            Icon = Icons.Brand();
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Night;
            Bounds = InitialBounds();
            Web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Night };
            Controls.Add(Web);
        }

        /// <summary>1280x720 logical pixels (times the monitor's scale), never larger than the work area, centred on the
        /// monitor under the pointer.</summary>
        static Rectangle InitialBounds()
        {
            var screen = Screen.FromPoint(Cursor.Position);
            var wa = screen.WorkingArea;
            float k = 1f;
            try
            {
                var c = new Native.POINT { X = wa.Left + wa.Width / 2, Y = wa.Top + wa.Height / 2 };
                IntPtr mon = Native.MonitorFromPoint(c, Native.MONITOR_DEFAULTTONEAREST);
                uint dx, dy;
                if (Native.GetDpiForMonitor(mon, 0, out dx, out dy) == 0 && dx >= 96) k = dx / 96f;
            }
            catch (Exception) { }
            int w = Math.Min((int)Math.Round(1280 * k), wa.Width);
            int h = Math.Min((int)Math.Round(720 * k), wa.Height);
            return new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // a borderless window that still minimises from its taskbar button and has a system menu (Alt+Space)
                cp.Style |= Native.WS_MINIMIZEBOX | Native.WS_SYSMENU;
                // 2026-10-01: WS_CAPTION は**ここには書かない**（EnsureCaptionStyle の説明）。ここに書くと、閉じて開くたびに
                // 窓がタイトルバーの分ずつ大きくなる（公開前レビューの指摘・見えない窓で 1280x720 → 1296x759 → 1312x798 と実測）。
                if (!Native.IsWindows11OrLater) cp.ClassStyle |= Native.CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = Native.DWMWCP_ROUND;   // Windows 11: the system's rounded corners and outline
                Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            }
            catch (Exception) { }
            try { Native.ChangeWindowMessageFilterEx(Handle, WM_TaskbarButtonCreated, Native.MSGFLT_ALLOW, IntPtr.Zero); } catch (Exception) { }
        }

        /// <summary>
        /// 2026-10-01: 出来上がった窓にだけ WS_CAPTION を付ける。**Windows は、タイトルバーを持たない窓には最小化・元に戻す時の
        /// 動き（タスクバーへ縮む／タスクバーから伸びる）を付けない**ので、縮める直前と戻す直前に呼ぶ。タイトルバーそのものは
        /// WndProc の WM_NCCALCSIZE で幅 0 にするので、見た目は枠なしのまま。
        ///
        /// なぜ CreateParams に書かないのか（公開前レビューで見つかった重大な不具合）: .NET Framework 4.8 の WinForms は、
        /// 最小化から戻す時に「覚えておいた中身の大きさ＋ CreateParams の枠（AdjustWindowRectEx）」で窓の大きさを付け直す。
        /// CreateParams に WS_CAPTION があると、本当は 0 の枠を足してしまい、**閉じて開くたびに 16x39 ずつ大きくなった**。
        /// 実物の窓にだけ付ければ、WinForms の計算は「枠なし」のまま（見えない窓で 3 回繰り返して 1280x720 のまま、と実測）。
        /// 公開前レビュー（2 回目）: その実測は 64 bit で行っていて、x86 の公開版では GetWindowLongPtrW が無いため、ここは毎回失敗して
        /// いた（Native.GetWindowStyle）。直した後で、ビルドした x86 の exe の MainForm そのものを 32 bit の PowerShell で見えない窓に
        /// して測り直した: トレイへしまう（LeaveToTray）→ 戻す・ゲームの後（ShowNoActivate）・ページの「－」→ 戻す を各 2〜3 回、
        /// どれも 1280x720 のまま、WS_CAPTION は付いたまま、ログに「caption style:」は 0 行。
        /// WinForms が自分のスタイルを書き直すと（UpdateStyles）この印は消えるが、その時は動きが付かないだけで、大きさは狂わない。
        /// 最大化ボタン（WS_MAXIMIZEBOX）と枠（WS_THICKFRAME）は付けないので、ダブルクリックでの最大化や端へのスナップも起きない。
        /// </summary>
        public void EnsureCaptionStyle()
        {
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                // 公開前レビュー: 32 bit（公開する exe）でも呼べる形で（Native.GetWindowStyle の説明）
                int style = Native.GetWindowStyle(Handle);
                if ((style & Native.WS_CAPTION) == Native.WS_CAPTION) return;
                Native.SetWindowStyle(Handle, style | Native.WS_CAPTION);
                Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
            }
            catch (Exception ex) { log("caption style: " + ex.Message); }   // 付かなければ動きが無いだけ
        }

        protected override void WndProc(ref Message m)
        {
            // 2026-10-01: 窓の全面をクライアント領域（ページ）にする。EnsureCaptionStyle で WS_CAPTION を付けるので、何もしないと
            // Windows が上端にタイトルバーの分を取ってしまう。何も変えずに 0 を返すと「窓の四角＝クライアントの四角」になる
            // （wParam が TRUE でも FALSE でも、渡された四角をそのまま残すのが答え）。枠もタイトルバーも描かれない。
            // WS_CAPTION が無い時（今までの枠なし）も、答えは同じ「窓の四角＝クライアント」なので、いつも 0 を返してよい。
            if (m.Msg == Native.WM_NCCALCSIZE) { m.Result = IntPtr.Zero; return; }
            if (m.Msg == WM_TaskbarButtonCreated && WM_TaskbarButtonCreated != 0) TaskbarButtonCreated?.Invoke(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            base.OnFormClosing(e);
        }

        /// <summary>v1.2: 優しく閉じる。窓を 140 ms（CloseFade.DurationMs）かけて薄くしてから <paramref name="then"/>（Hide か
        /// Shutdown）を呼ぶ。返すのは掛かるミリ秒で、0 なら then はもう同期に呼び済み（窓が見えていない・Windows の「アニメーション
        /// 効果」が OFF か読めない・タイマーが作れない）。演出の最中にもう一度呼ばれたら then を差し替えるだけ（後から来た方が勝つ）。
        ///
        /// 落とし穴（コードを読む人へ）:
        ///   - Opacity を 1 未満にした瞬間、WinForms は窓に WS_EX_LAYERED を付けて SetLayeredWindowAttributes(LWA_ALPHA) を
        ///     使う。最初の tick は α=1.0 なのでまだレイヤ化されず、2 tick 目（α≈0.97）で切り替わる。Win8 以降は DWM が子 HWND
        ///     （WebView2）ごと α を掛けるので、ページも一緒に薄くなる。
        ///   - 窓の大きさは動かさない（CloseFade.cs の頭）。
        ///   - 終わったら reset が Opacity を 1.0 に戻す（Hide された窓が次に Show される時に薄いままにならないように。
        ///     EnsureOpaque も同じことを Show の直前にする）。</summary>
        public int FadeOut(Action then)
        {
            if (fade != null && fade.Running) { fade.Then = then; return CloseFade.DurationMs; }
            int ms = CloseFade.PlannedMs(Visible && IsHandleCreated && !IsDisposed, Native.TryClientAreaAnimation());
            var clock = Stopwatch.StartNew();
            fade = new CloseFade(ms, () => clock.Elapsed.TotalMilliseconds, a => { Opacity = a; }, then,
                () => { if (!IsDisposed && IsHandleCreated) Opacity = 1.0; }, log);
            if (ms > 0)
            {
                try
                {
                    fadeTimer = new System.Windows.Forms.Timer { Interval = CloseFade.TickMs };
                    fadeTimer.Tick += (s, e) => { fade.Tick(); if (!fade.Running) StopFadeTimer(); };
                    fadeTimer.Start();
                }
                catch (Exception ex)
                {
                    log("close fade timer: " + ex.Message);
                    fade.Finish();   // 演出は諦めて、then はここで同期に
                    return 0;
                }
            }
            fade.Start();
            return ms;
        }

        /// <summary>
        /// 2026-10-01（持ち主の画面録画）: トレイへしまう。Windows の**最小化の動き（タスクバーへ縮んでいく）**を見せてから
        /// <paramref name="then"/>（Hide）を呼ぶ。<paramref name="how"/> はどの去り方になったか（<see cref="CloseFade.PlannedLeave"/>:
        /// "minimize" / "fade" / "none"）。返すのは掛かるミリ秒で、0 なら then はもう同期に呼び済み。
        ///
        ///   - 最小化の動きが使えない時（設定で OFF・読めない・既に最小化済み）は <see cref="FadeOut"/> と同じ（薄くなる／すぐ）。
        ///   - 待つ間は <see cref="CloseFade"/> を「時計だけ」で回す（α は触らない）。だから決めごとは FadeOut と同じ:
        ///     演出の最中にもう一度呼ばれたら then を差し替える（✕ のあとのトレイの「終了」は、縮み終わったら終了）、
        ///     途中で窓がもう一度求められたら <see cref="EnsureOpaque"/> がやめる（隠さない）。
        ///   - 窓は**最小化されたまま**隠れる。次に出す側（ClientApp.ShowWindow の Minimized → Normal、ShowAfterGame の
        ///     SW_SHOWNOACTIVATE）が元に戻すので、その時 Windows が「タスクバーから伸びる」動きを付ける。
        ///   - 待つ時間（<see cref="CloseFade.MinimizeHoldMs"/>）は Windows の縮む動き（約 0.2〜0.25 秒）より少し長め。
        ///     短いと縮み切る前に隠れて、最後が途切れて見える。その間、窓はもう最小化されているので待たされた感じは無い。
        /// </summary>
        public int LeaveToTray(Action then, out string how)
        {
            if (fade != null && fade.Running) { fade.Then = then; how = leaveHow ?? "fade"; return CloseFade.DurationMs; }
            how = CloseFade.PlannedLeave(Visible && IsHandleCreated && !IsDisposed, WindowState == FormWindowState.Minimized,
                Native.TryClientAreaAnimation(), Native.TryMinimizeAnimation());
            if (how != "minimize")
            {
                int ms0 = FadeOut(then);
                if (ms0 == 0) how = "none";
                leaveHow = how;
                return ms0;
            }
            var clock = Stopwatch.StartNew();
            fade = new CloseFade(CloseFade.MinimizeHoldMs, () => clock.Elapsed.TotalMilliseconds, null, then, null, log);
            leaveHow = how;
            try
            {
                fadeTimer = new System.Windows.Forms.Timer { Interval = CloseFade.TickMs };
                bool sawMinimized = false;
                fadeTimer.Tick += (s, e) =>
                {
                    // 縮んでいる間に、タスクバーのボタンや Alt+Tab で人が戻した（Windows が元に戻した）: 隠さずにやめる
                    // （公開前レビューの指摘: そのままだと 0.3 秒後に、戻したばかりの窓が消えた）。後始末は EnsureOpaque と同じ。
                    // 「一度は最小化になった」のを見てからだけ。縮まなかった時（万一）にまでやめると、✕ が効かなくなる
                    bool minimized = WindowState == FormWindowState.Minimized;
                    if (minimized) sawMinimized = true;
                    else if (sawMinimized && fade != null && fade.Running) { EnsureOpaque(); return; }
                    fade.Tick();
                    if (!fade.Running) StopFadeTimer();
                };
            }
            catch (Exception ex) { log("leave timer: " + ex.Message); fadeTimer = null; }
            if (fadeTimer == null) { fade.Finish(); how = "none"; return 0; }   // 待てないなら、縮める前にすぐ隠す
            EnsureCaptionStyle();   // 縮む動きを付けるため（WinForms が消していることがある）
            try { Native.ShowWindow(Handle, Native.SW_MINIMIZE); }
            catch (Exception ex) { log("minimize: " + ex.Message); }   // 縮まなくても、時間が来れば隠れる
            fadeTimer.Start();
            fade.Start();
            return CloseFade.MinimizeHoldMs;
        }

        /// <summary>最後に始めた去り方（LeaveToTray の how）。演出中に差し替えが来た時に同じ答えを返すため。</summary>
        string leaveHow;

        /// <summary>窓を出す直前に: 薄いまま残っていたら 1.0 に戻す（Show / ShowWindow / SW_SHOWNOACTIVATE の前）。
        ///
        /// v1.2: **まだ薄くなっている最中なら、演出ごとやめる。** 濃さを戻すだけでは駄目で、放っておくと 140 ms 後に
        /// 予定どおり then（Hide）が走り、出したばかりの窓がすぐ消える（持ち主から見れば「開かない」）。
        /// やめた事は FadeCancelled で ClientApp に伝える（あちらの「閉じかけ」の印を戻し、ページの縮みも外すため）。
        /// 終了の最中はここへ来ない（ClientApp.ShowWindow が quitFading で先に返す）。</summary>
        public void EnsureOpaque()
        {
            bool cancelled = false;
            var f = fade;
            if (f != null && f.Running)
            {
                try { f.Cancel(); cancelled = true; }
                catch (Exception ex) { log("fade cancel: " + ex.Message); }
                StopFadeTimer();
            }
            try { if (Opacity < 1.0) Opacity = 1.0; }
            catch (Exception ex) { log("opaque: " + ex.Message); }
            if (cancelled) { try { FadeCancelled?.Invoke(this, EventArgs.Empty); } catch (Exception ex) { log("fade cancelled: " + ex.Message); } }
        }

        void StopFadeTimer()
        {
            var t = fadeTimer;
            fadeTimer = null;
            if (t == null) return;
            try { t.Stop(); t.Dispose(); } catch (Exception) { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopFadeTimer();
            base.OnFormClosed(e);
        }

        /// <summary>window.drag (when WebView2 cannot drag by itself): move the window like its title bar.</summary>
        public void BeginDrag()
        {
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
        }

        public static bool IsOurUri(string uri)
        {
            Uri u;
            return Uri.TryCreate(uri, UriKind.Absolute, out u)
                && u.Scheme == Uri.UriSchemeHttps
                && string.Equals(u.Host, AppInfo.UiHostName, StringComparison.OrdinalIgnoreCase)
                && u.IsDefaultPort;
        }

        /// <summary>v1.2: ページは音（Aegis が見つけた時の 1 つ）を WebAudio で作る（ui\index.html, window.spSound）。Chromium は
        /// 何も言わなければクリックの後にしか音を出させないが、その音は起動時のスキャンが赤で終わった時にも鳴り、それは誰も
        /// クリックしないうちに来る。この引数 1 つで、このアプリ自身のページ（出せる唯一のページ）に限ってその規則を外す。
        /// ページの側は AudioContext がすぐ動かない音を捨てるままなので、遅れて鳴ることはない。</summary>
        public const string AutoplayArgument = "--autoplay-policy=no-user-gesture-required";

        /// <summary>Creates WebView2 (user data under %LOCALAPPDATA%\StarPocket\Client\WebView2, no WEBVIEW2_* environment
        /// variables: those would pass on to the game) and opens the UI.
        /// <paramref name="bootScript"/> runs as soon as the document exists - before the page's own script and before
        /// the first paint - so the Client's chosen colour is already on &lt;html&gt; and the default never flashes past.</summary>
        public async Task InitializeWebViewAsync(string userDataFolder, string uiFolder, string browserLanguage, bool devTools, string bootScript = null)
        {
            var options = new CoreWebView2EnvironmentOptions { Language = browserLanguage, AdditionalBrowserArguments = AutoplayArgument };
            environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            await Web.EnsureCoreWebView2Async(environment);
            var core = Web.CoreWebView2;
            var s = core.Settings;
            s.AreDevToolsEnabled = devTools;
            s.AreDefaultContextMenusEnabled = false;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;
            s.AreBrowserAcceleratorKeysEnabled = false;
            s.AreHostObjectsAllowed = false;
            s.IsWebMessageEnabled = true;
            TrySet(() => s.IsGeneralAutofillEnabled = false);
            TrySet(() => s.IsPasswordAutosaveEnabled = false);
            TrySet(() => s.IsSwipeNavigationEnabled = false);
            TrySet(() => s.IsPinchZoomEnabled = false);
            // local pages only: no SmartScreen look-ups
            TrySet(() => s.IsReputationCheckingRequired = false);
            DragRegionSupported = TrySet(() => s.IsNonClientRegionSupportEnabled = true);

            core.SetVirtualHostNameToFolderMapping(AppInfo.UiHostName, uiFolder, CoreWebView2HostResourceAccessKind.Deny);
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (o, e) =>
            {
                if (!IsOurUri(e.Request.Uri)) e.Response = environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
            };
            core.NavigationStarting += (o, e) => { if (!IsOurUri(e.Uri)) { e.Cancel = true; log("navigation refused: " + Short(e.Uri)); } };
            core.FrameNavigationStarting += (o, e) => { if (!IsOurUri(e.Uri)) e.Cancel = true; };
            core.NewWindowRequested += (o, e) => { e.Handled = true; };
            core.DownloadStarting += (o, e) => { e.Cancel = true; };
            core.PermissionRequested += (o, e) => { e.State = CoreWebView2PermissionState.Deny; };
            core.WebMessageReceived += (o, e) =>
            {
                if (e.Source == null || !e.Source.StartsWith(AppInfo.UiOrigin, StringComparison.OrdinalIgnoreCase)) return;
                string json;
                try { json = e.WebMessageAsJson; } catch (Exception) { return; }
                WebMessage?.Invoke(json);
            };
            core.NavigationCompleted += (o, e) =>
            {
                if (e.IsSuccess) UiReady?.Invoke(this, EventArgs.Empty);
                else log("the UI did not load: " + e.WebErrorStatus);
            };
            core.ProcessFailed += (o, e) =>
            {
                log("WebView2 process failed: " + e.ProcessFailedKind);
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited || e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                {
                    try { core.Reload(); } catch (Exception) { }
                }
            };
            // the Client's colour, before anything is drawn (an older runtime without this call simply paints the default)
            await SetBootScriptAsync(bootScript);
            core.Navigate(AppInfo.UiStartPage);
        }

        string bootScriptId;

        /// <summary>The script that writes the Client's colour onto &lt;html&gt; as each document is created. It is
        /// replaced whenever the colour changes: WebView2 reloads the page by itself when its render process dies
        /// (ProcessFailed above), and a reload that brought back the colour of start-up would leave the window painted
        /// in one colour while settings.json said another. An empty script takes it off again.</summary>
        public async Task SetBootScriptAsync(string bootScript)
        {
            var core = Web != null ? Web.CoreWebView2 : null;
            if (core == null) return;
            if (bootScriptId != null)
            {
                try { core.RemoveScriptToExecuteOnDocumentCreated(bootScriptId); }
                catch (Exception ex) { log("accent: " + ex.Message); }
                bootScriptId = null;
            }
            if (string.IsNullOrEmpty(bootScript)) return;
            try { bootScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(bootScript); }
            catch (Exception ex) { log("accent: " + ex.Message); }
        }

        static string Short(string s) => s == null ? "" : (s.Length > 120 ? s.Substring(0, 120) + "..." : s);

        static bool TrySet(Action set)
        {
            try { set(); return true; }
            catch (Exception) { return false; }   // an older WebView2 runtime without that setting
        }

        /// <summary>Sends a message to the page (UI thread).</summary>
        public void PostJson(string json)
        {
            try { Web.CoreWebView2?.PostWebMessageAsJson(json); }
            catch (Exception ex) { log("post: " + ex.Message); }
        }
    }
}
