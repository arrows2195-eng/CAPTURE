using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Screen1
{
    public class CaptureForm : Form
    {
        private WebView2 _webView;
        private CaptureEngine _engine;
        private GlobalKeyboardHook _keyHook;
        private MemoryStream _imgStream;
        private bool _webViewReady;
        private string _webRoot;

        public CaptureForm()
        {
            Text = "CAPTURE";
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.Black;

            _engine = new CaptureEngine();
            _engine.FrameReady += OnFrame;

            _keyHook = new GlobalKeyboardHook();
            _keyHook.KeyPressed += OnKey;

            Load += OnLoad;
            FormClosed += OnClosed;
            Resize += OnResize;

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                _webRoot = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "web"
                );

                if (!Directory.Exists(_webRoot))
                {
                    _webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "web");
                }

                var env = await CoreWebView2Environment.CreateAsync(null, null,
                    new CoreWebView2EnvironmentOptions("--disable-web-security --allow-file-access-from-files"));

                _webView = new WebView2
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent
                };

                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webView.CoreWebView2.AddHostObjectToScript("captureHost", new CaptureHost(this));

                // Enable file access for local HTML
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "capture.app",
                    _webRoot,
                    CoreWebView2HostResourceAccessKind.Allow
                );

                string htmlPath = Path.Combine(_webRoot, "index.html");
                if (File.Exists(htmlPath))
                {
                    _webView.CoreWebView2.Navigate($"https://capture.app/index.html");
                }
                else
                {
                    // Fallback: load HTML directly
                    string html = File.ReadAllText(htmlPath);
                    _webView.NavigateToString(html);
                }

                Controls.Add(_webView);
                _webView.BringToFront();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                HandleWebMessage(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private void HandleWebMessage(string json)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(HandleWebMessage), json);
                return;
            }

            try
            {
                var msg = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                string type = msg.type?.ToString();

                switch (type)
                {
                    case "startCapture":
                        _engine.Start();
                        SendStateUpdate();
                        break;

                    case "stopCapture":
                        _engine.Stop();
                        CursorHider.RestoreCursors();
                        SendStateUpdate();
                        break;

                    case "toggleFreeze":
                        _engine.ToggleFreeze();
                        SendStateUpdate();
                        break;

                    case "toggleCursor":
                        _engine.Settings.FakeCursor = !_engine.Settings.FakeCursor;
                        if (_engine.Settings.FakeCursor) CursorHider.HideCursors();
                        else CursorHider.RestoreCursors();
                        SendStateUpdate();
                        break;

                    case "toggleGameMode":
                        _engine.Settings.GameMode = !_engine.Settings.GameMode;
                        SendStateUpdate();
                        break;

                    case "updateSetting":
                        string key = msg.payload.key?.ToString();
                        var value = msg.payload.value;
                        UpdateSetting(key, value);
                        break;

                    case "updateRegion":
                        var region = msg.payload;
                        _engine.Settings.CaptureRegion = new Rectangle(
                            (int)region.x, (int)region.y,
                            (int)region.width, (int)region.height
                        );
                        break;

                    case "showRegionSelector":
                        // Region selector is now handled in WebView
                        break;

                    case "hideRegionSelector":
                        // Region selector is now handled in WebView
                        break;

                    case "getInitialState":
                        SendStateUpdate();
                        SendSettingsUpdate();
                        break;

                    case "webviewReady":
                        _webViewReady = true;
                        SendStateUpdate();
                        SendSettingsUpdate();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleWebMessage error: {ex.Message}");
            }
        }

        private void UpdateSetting(string key, object value)
        {
            switch (key)
            {
                case "fpsMin": _engine.Settings.FpsMin = Convert.ToInt32(value); break;
                case "fpsMax": _engine.Settings.FpsMax = Convert.ToInt32(value); break;
                case "quality": _engine.Settings.Quality = Convert.ToInt32(value); break;
                case "pixelation": _engine.Settings.Pixelation = Convert.ToInt32(value); break;
            }
            SendSettingsUpdate();
        }

        private void SendStateUpdate()
        {
            if (!_webViewReady) return;

            var state = new
            {
                running = _engine.IsRunning(),
                frozen = _engine.ManualFreeze,
                gameMode = _engine.Settings.GameMode,
                fakeCursor = _engine.Settings.FakeCursor
            };

            SendToWebView("stateUpdate", state);
        }

        private void SendSettingsUpdate()
        {
            if (!_webViewReady) return;

            var settings = new
            {
                fpsMin = _engine.Settings.FpsMin,
                fpsMax = _engine.Settings.FpsMax,
                quality = _engine.Settings.Quality,
                pixelation = _engine.Settings.Pixelation,
                region = new
                {
                    x = _engine.Settings.CaptureRegion.X,
                    y = _engine.Settings.CaptureRegion.Y,
                    width = _engine.Settings.CaptureRegion.Width,
                    height = _engine.Settings.CaptureRegion.Height
                }
            };

            SendToWebView("settingsUpdate", settings);
        }

        private void SendToWebView(string type, object payload)
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(new { type, payload });
                _webView?.CoreWebView2?.PostWebMessageAsString(json);
            }
            catch { }
        }

        private void OnFrame(byte[] data)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<byte[]>(OnFrame), new object[] { data }); return; }

            var newStream = new MemoryStream(data);
            var newImg = Image.FromStream(newStream, false, false);
            var oldImg = _webView?.BackgroundImage; // We don't use background image anymore
            var oldStream = _imgStream;

            // The WebView2 handles display via CSS/HTML, we just need to keep the engine running
            // The actual frame display is handled by the WebView2 overlay (transparent)
            // We keep this for compatibility but don't render to a PictureBox

            _imgStream = newStream;
            if (oldImg != null) oldImg.Dispose();
            if (oldStream != null) oldStream.Dispose();
        }

        private void OnKey(Keys k)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<Keys>(OnKey), new object[] { k }); return; }

            if (k == _engine.Settings.FreezeKey)
            {
                _engine.ToggleFreeze();
                SendStateUpdate();
            }
            if (_engine.Settings.GameMode && (k == Keys.Z || k == Keys.X || k == Keys.C || k == Keys.V))
            {
                _engine.GameModeKeyPause();
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // Window is already maximized and borderless
        }

        private void OnResize(object sender, EventArgs e)
        {
            // WebView2 handles its own resize via Dock=Fill
        }

        private void OnClosed(object sender, FormClosedEventArgs e)
        {
            _keyHook?.Dispose();
            _engine?.Stop();
            _engine?.Dispose();
            _webView?.Dispose();
            CursorHider.RestoreCursors();
        }

        // Called by CaptureHost from JavaScript
        public void RequestStateUpdate() => SendStateUpdate();
        public void RequestSettingsUpdate() => SendSettingsUpdate();
    }

    // Host object exposed to JavaScript
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class CaptureHost
    {
        private CaptureForm _form;

        public CaptureHost(CaptureForm form)
        {
            _form = form;
        }

        public void RequestStateUpdate() => _form.RequestStateUpdate();
        public void RequestSettingsUpdate() => _form.RequestSettingsUpdate();
    }
}