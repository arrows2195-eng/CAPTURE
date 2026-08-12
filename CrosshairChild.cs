using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Screen1
{
    public class CrosshairChild : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint aff);
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point pt);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private Thread _thread;
        private volatile bool _run;
        private int _lastX = int.MinValue, _lastY = int.MinValue;

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED; return cp; }
        }

        public CrosshairChild()
        {
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(24, 24);
            TopMost = true;
            ShowInTaskbar = false;
            TransparencyKey = Color.Magenta;
            BackColor = Color.Magenta;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Paint += OnPaint;
            Load += (s, e) => { SetWindowDisplayAffinity(Handle, 0x11); StartTrack(); };
            FormClosing += (s, e) => _run = false;
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var dark = new Pen(Color.FromArgb(200, 0, 0, 0), 3))
            using (var white = new Pen(Color.White, 1.5f))
            {
                g.DrawLine(dark, 12, 2, 12, 10); g.DrawLine(dark, 12, 14, 12, 22);
                g.DrawLine(dark, 2, 12, 10, 12); g.DrawLine(dark, 14, 12, 22, 12);
                g.DrawLine(white, 12, 2, 12, 10); g.DrawLine(white, 12, 14, 12, 22);
                g.DrawLine(white, 2, 12, 10, 12); g.DrawLine(white, 14, 12, 22, 12);
            }
            g.FillEllipse(Brushes.White, 10, 10, 4, 4);
            g.DrawEllipse(Pens.Black, 10, 10, 4, 4);
        }

        private void StartTrack()
        {
            _run = true;
            _thread = new Thread(() => {
                while (_run)
                {
                    Point p; GetCursorPos(out p);
                    if (p.X != _lastX || p.Y != _lastY)
                    {
                        _lastX = p.X; _lastY = p.Y;
                        SetWindowPos(Handle, HWND_TOPMOST, p.X - 12, p.Y - 12, 0, 0, 0x11);
                    }
                    Thread.Sleep(11);
                }
            }) { IsBackground = true };
            _thread.Start();
        }
    }
}
