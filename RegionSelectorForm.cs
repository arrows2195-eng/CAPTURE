using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Screen1
{
    public class RegionSelectorForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint aff);

        public event Action<Rectangle> RegionChanged;
        private Rectangle _region = new Rectangle(100, 100, 640, 480);
        private string _handle;
        private Point _start;
        private Rectangle _startRegion;
        private Label _lbl;

        public RegionSelectorForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            TransparencyKey = Color.Magenta;
            BackColor = Color.Magenta;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _lbl = new Label { AutoSize = true, ForeColor = Color.Cyan, BackColor = Color.FromArgb(180, 0, 0, 0), Font = new Font("Consolas", 10), TextAlign = ContentAlignment.MiddleCenter };
            Controls.Add(_lbl);
            UpdateBounds();

            Paint += OnPaint;
            MouseDown += OnDown;
            MouseMove += OnMove;
            MouseUp += OnUp;
            Load += (s, e) => SetWindowDisplayAffinity(Handle, 0x11);
        }

        public void SetRegion(Rectangle r) { _region = r; UpdateBounds(); Invalidate(); }

        private void UpdateBounds()
        {
            Bounds = new Rectangle(_region.X - 14, _region.Y - 14, _region.Width + 28, _region.Height + 28);
            _lbl.Text = _region.Width + " x " + _region.Height;
            _lbl.Location = new Point((Width - _lbl.PreferredWidth) / 2, (Height - _lbl.PreferredHeight) / 2);
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var pen = new Pen(Color.Cyan, 6))
                g.DrawRectangle(pen, 14, 14, _region.Width, _region.Height);
            foreach (var r in GetHandles().Values)
            {
                g.FillRectangle(Brushes.Cyan, r);
                g.DrawRectangle(Pens.Black, r);
            }
        }

        private Dictionary<string, Rectangle> GetHandles()
        {
            int s = 14, w = _region.Width, h = _region.Height;
            int cx = s + w / 2 - 7, cy = s + h / 2 - 7;
            return new Dictionary<string, Rectangle> {
                {"nw", new Rectangle(s-7, s-7, 14, 14)}, {"n", new Rectangle(cx, s-7, 14, 14)}, {"ne", new Rectangle(s+w-7, s-7, 14, 14)},
                {"w", new Rectangle(s-7, cy, 14, 14)}, {"e", new Rectangle(s+w-7, cy, 14, 14)},
                {"sw", new Rectangle(s-7, s+h-7, 14, 14)}, {"s", new Rectangle(cx, s+h-7, 14, 14)}, {"se", new Rectangle(s+w-7, s+h-7, 14, 14)}
            };
        }

        private void OnDown(object sender, MouseEventArgs e)
        {
            _start = e.Location;
            _startRegion = _region;
            _handle = null;
            foreach (var kv in GetHandles()) if (kv.Value.Contains(e.Location)) { _handle = kv.Key; Capture = true; return; }
            if (new Rectangle(14, 14, _region.Width, _region.Height).Contains(e.Location)) { _handle = "move"; Capture = true; }
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (_handle == null || e.Button != MouseButtons.Left) return;
            int dx = e.X - _start.X, dy = e.Y - _start.Y;
            var sr = _startRegion;

            if (_handle == "move") { _region = new Rectangle(Math.Max(0, sr.X + dx), Math.Max(0, sr.Y + dy), sr.Width, sr.Height); }
            else
            {
                int nx = sr.X, ny = sr.Y, nw = sr.Width, nh = sr.Height;
                if (_handle.Contains("w")) { nx = sr.X + dx; nw = sr.Width - dx; }
                if (_handle.Contains("e")) { nw = sr.Width + dx; }
                if (_handle.Contains("n")) { ny = sr.Y + dy; nh = sr.Height - dy; }
                if (_handle.Contains("s")) { nh = sr.Height + dy; }
                if (nw < 64) { if (_handle.Contains("w")) nx = sr.X + sr.Width - 64; nw = 64; }
                if (nh < 64) { if (_handle.Contains("n")) ny = sr.Y + sr.Height - 64; nh = 64; }
                _region = new Rectangle(Math.Max(0, nx), Math.Max(0, ny), nw, nh);
            }
            UpdateBounds();
            RegionChanged?.Invoke(_region);
            Invalidate();
        }

        private void OnUp(object sender, MouseEventArgs e) { _handle = null; Capture = false; }
    }
}
