using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Screen1
{
    public class CaptureForm : Form
    {
        private PictureBox _pictureBox;
        private CaptureEngine _engine;
        private ControlForm _controlForm;
        private QuickBarForm _quickBar;
        private GlobalKeyboardHook _keyHook;
        private MemoryStream _imgStream;

        public CaptureForm()
        {
            Text = "Screen 1";
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.Black;

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Black
            };
            Controls.Add(_pictureBox);

            _engine = new CaptureEngine();
            _engine.FrameReady += OnFrame;

            _controlForm = new ControlForm(_engine);
            _quickBar = new QuickBarForm(_engine, _controlForm);

            _keyHook = new GlobalKeyboardHook();
            _keyHook.KeyPressed += OnKey;

            Load += (s, e) => _quickBar.Show(this);
            FormClosed += OnClosed;
        }

        private void OnFrame(byte[] data)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<byte[]>(OnFrame), new object[] { data }); return; }

            var newStream = new MemoryStream(data);
            var newImg = Image.FromStream(newStream, false, false);
            var oldImg = _pictureBox.Image;
            var oldStream = _imgStream;
            _pictureBox.Image = newImg;
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
                _quickBar.UpdateUI();
                _controlForm.UpdateUI();
            }
            if (_engine.Settings.GameMode && (k == Keys.Z || k == Keys.X || k == Keys.C || k == Keys.V))
            {
                _engine.GameModeKeyPause();
            }
        }

        private void OnClosed(object sender, FormClosedEventArgs e)
        {
            _keyHook.Dispose();
            _engine.Stop();
            _engine.Dispose();
            CursorHider.RestoreCursors();
        }
    }
}
