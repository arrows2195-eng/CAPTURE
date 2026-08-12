using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Screen1
{
    public class ControlForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        private CaptureEngine _engine;
        private Button _startBtn, _stopBtn, _freezeBtn, _cursorBtn, _gameModeBtn;
        private NumericUpDown _fpsMinNum, _fpsMaxNum, _qualityNum, _pixelNum;
        private NumericUpDown _regionX, _regionY, _regionW, _regionH;
        private Label _statusLbl;
        private RegionSelectorForm _regionSelector;

        public ControlForm(CaptureEngine engine)
        {
            _engine = engine;
            Text = "Screen 1 - Settings";
            Size = new Size(320, 520);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 34);
            ForeColor = Color.White;
            TopMost = true;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9);

            _regionSelector = new RegionSelectorForm();
            _regionSelector.RegionChanged += r => {
                _engine.Settings.CaptureRegion = r;
                _regionX.Value = r.X;
                _regionY.Value = r.Y;
                _regionW.Value = r.Width;
                _regionH.Value = r.Height;
            };

            BuildUI();
            Load += (s, e) => SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
            FormClosing += (s, e) => { e.Cancel = true; Hide(); };
        }

        private void BuildUI()
        {
            int y = 10;

            // Capture controls
            AddLabel("CAPTURE", 10, y); y += 22;
            _startBtn = AddButton("▶ Start", 10, y, 90, () => {
                if (_engine.Settings.FakeCursor) CursorHider.HideCursors();
                _engine.Start();
                UpdateUI();
            });
            _stopBtn = AddButton("■ Stop", 105, y, 90, () => {
                _engine.Stop();
                CursorHider.RestoreCursors();
                UpdateUI();
            });
            y += 35;

            _freezeBtn = AddButton("⏸ Freeze", 10, y, 90, () => { _engine.ToggleFreeze(); UpdateUI(); });
            _cursorBtn = AddButton("🖱 Cursor", 105, y, 90, () => {
                _engine.Settings.FakeCursor = !_engine.Settings.FakeCursor;
                if (_engine.Settings.FakeCursor) CursorHider.HideCursors();
                else CursorHider.RestoreCursors();
                UpdateUI();
            });
            _gameModeBtn = AddButton("🎮 Game", 200, y, 90, () => {
                _engine.Settings.GameMode = !_engine.Settings.GameMode;
                UpdateUI();
            });
            y += 45;

            // FPS
            AddLabel("FPS RANGE", 10, y); y += 22;
            AddLabel("Min:", 10, y + 3);
            _fpsMinNum = AddNumeric(45, y, 60, 1, 60, _engine.Settings.FpsMin);
            _fpsMinNum.ValueChanged += (s, e) => _engine.Settings.FpsMin = (int)_fpsMinNum.Value;
            AddLabel("Max:", 115, y + 3);
            _fpsMaxNum = AddNumeric(150, y, 60, 1, 60, _engine.Settings.FpsMax);
            _fpsMaxNum.ValueChanged += (s, e) => _engine.Settings.FpsMax = (int)_fpsMaxNum.Value;
            y += 35;

            // Quality
            AddLabel("QUALITY", 10, y); y += 22;
            _qualityNum = AddNumeric(10, y, 80, 1, 100, _engine.Settings.Quality);
            _qualityNum.ValueChanged += (s, e) => _engine.Settings.Quality = (int)_qualityNum.Value;
            AddButton("Low", 100, y, 50, () => { _qualityNum.Value = 30; });
            AddButton("Med", 155, y, 50, () => { _qualityNum.Value = 60; });
            AddButton("High", 210, y, 50, () => { _qualityNum.Value = 90; });
            y += 35;

            // Pixelation
            AddLabel("PIXELATION", 10, y); y += 22;
            _pixelNum = AddNumeric(10, y, 80, 1, 64, _engine.Settings.Pixelation);
            _pixelNum.ValueChanged += (s, e) => _engine.Settings.Pixelation = (int)_pixelNum.Value;
            AddButton("1x", 100, y, 40, () => { _pixelNum.Value = 1; });
            AddButton("4x", 145, y, 40, () => { _pixelNum.Value = 4; });
            AddButton("8x", 190, y, 40, () => { _pixelNum.Value = 8; });
            AddButton("16x", 235, y, 45, () => { _pixelNum.Value = 16; });
            y += 45;

            // Region
            AddLabel("CAPTURE REGION", 10, y); y += 22;
            AddLabel("X:", 10, y + 3); _regionX = AddNumeric(25, y, 55, 0, 9999, _engine.Settings.CaptureRegion.X);
            AddLabel("Y:", 85, y + 3); _regionY = AddNumeric(100, y, 55, 0, 9999, _engine.Settings.CaptureRegion.Y);
            AddLabel("W:", 160, y + 3); _regionW = AddNumeric(178, y, 55, 64, 9999, _engine.Settings.CaptureRegion.Width);
            y += 28;
            AddLabel("H:", 10, y + 3); _regionH = AddNumeric(25, y, 55, 64, 9999, _engine.Settings.CaptureRegion.Height);
            AddButton("Show", 90, y, 55, () => { _regionSelector.SetRegion(_engine.Settings.CaptureRegion); _regionSelector.Show(); });
            AddButton("Hide", 150, y, 55, () => { _regionSelector.Hide(); });

            _regionX.ValueChanged += (s, e) => UpdateRegion();
            _regionY.ValueChanged += (s, e) => UpdateRegion();
            _regionW.ValueChanged += (s, e) => UpdateRegion();
            _regionH.ValueChanged += (s, e) => UpdateRegion();
            y += 28;

            AddButton("1080p", 10, y, 55, () => SetRegionPreset(0, 0, 1920, 1080));
            AddButton("720p", 70, y, 55, () => SetRegionPreset(0, 0, 1280, 720));
            AddButton("540p", 130, y, 55, () => SetRegionPreset(0, 0, 960, 540));
            AddButton("VGA", 190, y, 55, () => SetRegionPreset(0, 0, 640, 480));
            y += 40;

            // Status
            _statusLbl = new Label { Text = "IDLE", Location = new Point(10, y), AutoSize = true, ForeColor = Color.Gray };
            Controls.Add(_statusLbl);

            UpdateUI();
        }

        private void UpdateRegion()
        {
            _engine.Settings.CaptureRegion = new Rectangle(
                (int)_regionX.Value, (int)_regionY.Value,
                (int)_regionW.Value, (int)_regionH.Value);
        }

        private void SetRegionPreset(int x, int y, int w, int h)
        {
            _regionX.Value = x; _regionY.Value = y;
            _regionW.Value = w; _regionH.Value = h;
            _regionSelector.SetRegion(new Rectangle(x, y, w, h));
        }

        public void UpdateUI()
        {
            _freezeBtn.Text = _engine.ManualFreeze ? "▶ Unfreeze" : "⏸ Freeze";
            _freezeBtn.BackColor = _engine.ManualFreeze ? Color.FromArgb(255, 159, 10) : Color.FromArgb(60, 60, 64);

            _cursorBtn.BackColor = _engine.Settings.FakeCursor ? Color.FromArgb(79, 172, 254) : Color.FromArgb(60, 60, 64);
            _gameModeBtn.BackColor = _engine.Settings.GameMode ? Color.FromArgb(191, 90, 242) : Color.FromArgb(60, 60, 64);

            string status = _engine.ManualFreeze ? "FROZEN" : _engine.Settings.GameMode ? "GAME MODE" : "LIVE";
            _statusLbl.Text = status;
            _statusLbl.ForeColor = _engine.ManualFreeze ? Color.Orange : _engine.Settings.GameMode ? Color.MediumPurple : Color.LimeGreen;
        }

        private Label AddLabel(string text, int x, int y)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8) };
            Controls.Add(lbl);
            return lbl;
        }

        private Button AddButton(string text, int x, int y, int w, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 64),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 84);
            btn.Click += (s, e) => onClick();
            Controls.Add(btn);
            return btn;
        }

        private NumericUpDown AddNumeric(int x, int y, int w, int min, int max, int val)
        {
            var num = new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(w, 24),
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, val)),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(num);
            return num;
        }
    }
}
