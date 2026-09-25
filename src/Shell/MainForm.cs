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

        protected override void WndProc(ref Message m)
        {
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
