using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Screen1
{
    public class QuickBarForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        const int WS_EX_TOOLWINDOW = 0x80;

        private CaptureEngine _engine;
        private ControlForm _controlForm;
        private bool _expanded;
        private bool _dragging;
        private Point _dragOffset;

        private Panel _statusDot;
        private Label _statusLbl;
        private Button _expandBtn, _freezeBtn, _settingsBtn;

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= WS_EX_TOOLWINDOW; return cp; }
        }

        public QuickBarForm(CaptureEngine engine, ControlForm controlForm)
        {
            _engine = engine;
            _controlForm = controlForm;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(180, 36);
            Location = new Point((Screen.PrimaryScreen.WorkingArea.Width - 180) / 2, 10);
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(40, 40, 44);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            BuildUI();

            Load += (s, e) => SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
            Paint += OnPaint;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
        }

        private void BuildUI()
        {
            // Status dot
            _statusDot = new Panel
            {
                Size = new Size(10, 10),
                Location = new Point(12, 13),
                BackColor = Color.Gray
            };
            _statusDot.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(_statusDot.BackColor))
                    e.Graphics.FillEllipse(brush, 0, 0, 9, 9);
            };
            _statusDot.MouseDown += OnMouseDown;
            _statusDot.MouseMove += OnMouseMove;
            _statusDot.MouseUp += OnMouseUp;
            Controls.Add(_statusDot);

            // Status label
            _statusLbl = new Label
            {
                Text = "IDLE",
                Location = new Point(28, 10),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            _statusLbl.MouseDown += OnMouseDown;
            _statusLbl.MouseMove += OnMouseMove;
            _statusLbl.MouseUp += OnMouseUp;
            Controls.Add(_statusLbl);

            // Expand button
            _expandBtn = new Button
            {
                Text = "▼",
                Location = new Point(150, 6),
                Size = new Size(24, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _expandBtn.FlatAppearance.BorderSize = 0;
            _expandBtn.Click += (s, e) => ToggleExpand();
            Controls.Add(_expandBtn);

            // Freeze button (hidden when collapsed)
            _freezeBtn = new Button
            {
                Text = "⏸ Freeze",
                Location = new Point(10, 44),
                Size = new Size(76, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 64),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _freezeBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 84);
            _freezeBtn.Click += (s, e) => { _engine.ToggleFreeze(); UpdateUI(); _controlForm.UpdateUI(); };
            Controls.Add(_freezeBtn);

            // Settings button (hidden when collapsed)
            _settingsBtn = new Button
            {
                Text = "⚙ Settings",
                Location = new Point(92, 44),
                Size = new Size(82, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 64),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _settingsBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 84);
            _settingsBtn.Click += (s, e) => { if (_controlForm.Visible) _controlForm.Hide(); else _controlForm.Show(); };
            Controls.Add(_settingsBtn);

            UpdateUI();
        }

        private void ToggleExpand()
        {
            _expanded = !_expanded;
            if (_expanded)
            {
                Size = new Size(180, 82);
                _expandBtn.Text = "▲";
                _freezeBtn.Visible = true;
                _settingsBtn.Visible = true;
            }
            else
            {
                Size = new Size(180, 36);
                _expandBtn.Text = "▼";
                _freezeBtn.Visible = false;
                _settingsBtn.Visible = false;
            }
            Invalidate();
        }

        public void UpdateUI()
        {
            bool frozen = _engine.ManualFreeze;
            bool game = _engine.Settings.GameMode;

            _statusLbl.Text = frozen ? "FROZEN" : game ? "GAME" : "LIVE";
            _statusDot.BackColor = frozen ? Color.Orange : game ? Color.MediumPurple : Color.LimeGreen;
            _statusDot.Invalidate();

            _freezeBtn.Text = frozen ? "▶ Resume" : "⏸ Freeze";
            _freezeBtn.BackColor = frozen ? Color.FromArgb(255, 159, 10) : Color.FromArgb(60, 60, 64);
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Rounded rectangle background
            using (var path = RoundedRect(ClientRectangle, 10))
            using (var brush = new SolidBrush(Color.FromArgb(40, 40, 44)))
            {
                g.FillPath(brush, path);
            }
            using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (var pen = new Pen(Color.FromArgb(70, 70, 74), 1))
            {
                g.DrawPath(pen, path);
            }
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragOffset = e.Location;
                if (sender != this)
                {
                    var ctrl = (Control)sender;
                    _dragOffset = new Point(e.X + ctrl.Left, e.Y + ctrl.Top);
                }
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                var screenPos = PointToScreen(e.Location);
                if (sender != this)
                {
                    var ctrl = (Control)sender;
                    screenPos = ctrl.PointToScreen(e.Location);
                }
                Location = new Point(screenPos.X - _dragOffset.X, screenPos.Y - _dragOffset.Y);
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }
    }
}
