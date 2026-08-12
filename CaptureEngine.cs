using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Screen1
{
    public class CaptureEngine : IDisposable
    {
        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int index);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point pt);
        [DllImport("user32.dll")]
        static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
        [DllImport("user32.dll")]
        static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int w, int h, uint step, IntPtr hbr, uint flags);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObj);

        [StructLayout(LayoutKind.Sequential)]
        struct ICONINFO { public bool fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

        public event Action<byte[]> FrameReady;
        public CaptureSettings Settings = new CaptureSettings();
        public bool ManualFreeze;

        private Timer _timer;
        private Bitmap _bmp;
        private Graphics _gfx;
        private MemoryStream _ms = new MemoryStream();
        private ImageCodecInfo _jpegCodec;
        private EncoderParameters _encParams;
        private int _lastW, _lastH, _screenW, _screenH;
        private Random _rnd = new Random();
        private Queue<KeyValuePair<DateTime, byte[]>> _buffer = new Queue<KeyValuePair<DateTime, byte[]>>();
        private DateTime _gameStart, _keyPauseUntil, _randFreezeUntil, _nextRandFreeze;
        private byte[] _frozenFrame;
        private bool _running;

        public CaptureEngine()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.MimeType == "image/jpeg") { _jpegCodec = c; break; }
            _encParams = new EncoderParameters(1);
            _encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
            _timer = new Timer();
            _timer.Tick += Tick;

            IntPtr hdc = GetDC(IntPtr.Zero);
            _screenW = GetDeviceCaps(hdc, 118);
            _screenH = GetDeviceCaps(hdc, 117);
            ReleaseDC(IntPtr.Zero, hdc);
            if (_screenW <= 0) _screenW = Screen.PrimaryScreen.Bounds.Width;
            if (_screenH <= 0) _screenH = Screen.PrimaryScreen.Bounds.Height;
        }

        public void Start()
        {
            _running = true;
            ManualFreeze = false;
            _buffer.Clear();
            _frozenFrame = null;
            _gameStart = DateTime.UtcNow;
            _nextRandFreeze = DateTime.UtcNow.AddSeconds(12 + _rnd.NextDouble() * 20);
            _keyPauseUntil = DateTime.MinValue;
            _randFreezeUntil = DateTime.MinValue;
            SetInterval();
            _timer.Start();
        }

        public void Stop() { _running = false; _timer.Stop(); _buffer.Clear(); }
        public void ToggleFreeze()
        {
            ManualFreeze = !ManualFreeze;
            if (Settings.GameMode) { _buffer.Clear(); if (!ManualFreeze) _gameStart = DateTime.UtcNow; }
        }
        public void GameModeKeyPause()
        {
            if (!Settings.GameMode) return;
            _keyPauseUntil = DateTime.UtcNow.AddSeconds(3);
            _buffer.Clear();
        }

        private void SetInterval()
        {
            if (Settings.GameMode) { _timer.Interval = 33; return; }
            int minMs = Math.Max(1, 1000 / Math.Max(1, Settings.FpsMax));
            int maxMs = Math.Max(minMs + 1, 1000 / Math.Max(1, Settings.FpsMin));
            _timer.Interval = minMs + _rnd.Next(maxMs - minMs);
        }

        private void Tick(object sender, EventArgs e)
        {
            if (!_running) return;
            var now = DateTime.UtcNow;

            if (ManualFreeze) { ShowFrozen(); return; }
            if (now < _keyPauseUntil) { ShowFrozen(); SetInterval(); return; }
            if (now < _randFreezeUntil) { ShowFrozen(); SetInterval(); return; }
            if (Settings.GameMode && now >= _nextRandFreeze)
            {
                _randFreezeUntil = now.AddSeconds(2);
                _nextRandFreeze = now.AddSeconds(12 + _rnd.NextDouble() * 20);
                _buffer.Clear();
                ShowFrozen(); SetInterval(); return;
            }
            if (!Settings.GameMode && _rnd.NextDouble() < Settings.StutterChance) { SetInterval(); return; }

            var frame = Capture();
            if (frame == null) { SetInterval(); return; }

            if (Settings.GameMode)
            {
                _buffer.Enqueue(new KeyValuePair<DateTime, byte[]>(now, frame));
                var cutoff = now.AddSeconds(-12);
                while (_buffer.Count > 0 && _buffer.Peek().Key < cutoff) _buffer.Dequeue();
                if ((now - _gameStart).TotalSeconds < 10) { ShowFrozen(); SetInterval(); return; }

                var target = now.AddSeconds(-5);
                byte[] output = null;
                foreach (var kv in _buffer) if (kv.Key <= target) output = kv.Value;
                if (output != null) { _frozenFrame = output; FrameReady?.Invoke(output); }
                else ShowFrozen();
            }
            else
            {
                _frozenFrame = frame;
                FrameReady?.Invoke(frame);
            }
            SetInterval();
        }

        private void ShowFrozen() { if (_frozenFrame != null) FrameReady?.Invoke(_frozenFrame); }

        private byte[] Capture()
        {
            try
            {
                var r = Settings.CaptureRegion;
                if (r.Width <= 0 || r.Height <= 0) r = new Rectangle(0, 0, _screenW, _screenH);
                int w = Math.Min(r.Width, _screenW - r.X);
                int h = Math.Min(r.Height, _screenH - r.Y);
                if (w <= 0 || h <= 0) return null;

                if (w != _lastW || h != _lastH)
                {
                    _gfx?.Dispose(); _bmp?.Dispose();
                    _bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    _gfx = Graphics.FromImage(_bmp);
                    _lastW = w; _lastH = h;
                }

                _gfx.CopyFromScreen(r.X, r.Y, 0, 0, new Size(w, h));

                if (Settings.Pixelation > 1)
                {
                    int pw = Math.Max(1, w / Settings.Pixelation);
                    int ph = Math.Max(1, h / Settings.Pixelation);
                    using (var sm = new Bitmap(pw, ph))
                    using (var sg = Graphics.FromImage(sm))
                    {
                        sg.DrawImage(_bmp, 0, 0, pw, ph);
                        _gfx.InterpolationMode = InterpolationMode.NearestNeighbor;
                        _gfx.PixelOffsetMode = PixelOffsetMode.Half;
                        _gfx.DrawImage(sm, 0, 0, w, h);
                    }
                }

                if (Settings.FakeCursor)
                {
                    Point cur; GetCursorPos(out cur);
                    int cx = cur.X - r.X, cy = cur.Y - r.Y;
                    var icon = CursorHider.SavedCursorIcon;
                    if (icon != IntPtr.Zero)
                    {
                        ICONINFO info;
                        if (GetIconInfo(icon, out info))
                        {
                            IntPtr hdc = _gfx.GetHdc();
                            DrawIconEx(hdc, cx - info.xHotspot, cy - info.yHotspot, icon, 0, 0, 0, IntPtr.Zero, 3);
                            _gfx.ReleaseHdc(hdc);
                            if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
                            if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                        }
                    }
                    else
                    {
                        var pts = new Point[] {
                            new Point(cx, cy), new Point(cx, cy + 18), new Point(cx + 5, cy + 14),
                            new Point(cx + 8, cy + 21), new Point(cx + 11, cy + 19),
                            new Point(cx + 7, cy + 13), new Point(cx + 13, cy + 13)
                        };
                        _gfx.FillPolygon(Brushes.White, pts);
                        _gfx.DrawPolygon(Pens.Black, pts);
                    }
                }

                _encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)Settings.Quality);
                _ms.SetLength(0);
                _bmp.Save(_ms, _jpegCodec, _encParams);
                return _ms.ToArray();
            }
            catch { return null; }
        }

        public void Dispose()
        {
            _timer?.Stop(); _timer?.Dispose();
            _gfx?.Dispose(); _bmp?.Dispose(); _ms?.Dispose();
        }
    }
}
