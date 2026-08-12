using System.Drawing;
using System.Windows.Forms;

namespace Screen1
{
    public class CaptureSettings
    {
        public bool FakeCursor;
        public bool GameMode;
        public Rectangle CaptureRegion = new Rectangle(0, 0, 1920, 1080);
        public int FpsMin = 8;
        public int FpsMax = 15;
        public int Quality = 75;
        public int Pixelation = 1;
        public double StutterChance = 0.05;
        public Keys FreezeKey = Keys.F6;
    }
}
