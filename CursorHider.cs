using System;
using System.Runtime.InteropServices;

namespace Screen1
{
    public static class CursorHider
    {
        [DllImport("user32.dll")]
        static extern bool SetSystemCursor(IntPtr hcur, uint id);
        [DllImport("user32.dll")]
        static extern IntPtr CreateCursor(IntPtr hInst, int xHot, int yHot, int w, int h, byte[] andMask, byte[] xorMask);
        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(ref CURSORINFO pci);
        [DllImport("user32.dll")]
        static extern IntPtr CopyIcon(IntPtr hIcon);
        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint init);

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public int x, y; }

        static uint[] IDs = { 32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650 };
        public static IntPtr SavedCursorIcon;

        public static void HideCursors()
        {
            if (SavedCursorIcon == IntPtr.Zero)
            {
                var ci = new CURSORINFO { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
                if (GetCursorInfo(ref ci) && ci.hCursor != IntPtr.Zero)
                    SavedCursorIcon = CopyIcon(ci.hCursor);
            }
            byte[] and = { 0xFF }, xor = { 0x00 };
            foreach (var id in IDs)
            {
                var blank = CreateCursor(IntPtr.Zero, 0, 0, 1, 1, and, xor);
                if (blank != IntPtr.Zero) SetSystemCursor(blank, id);
            }
        }

        public static void RestoreCursors()
        {
            SystemParametersInfo(0x57, 0, IntPtr.Zero, 0);
            SavedCursorIcon = IntPtr.Zero;
        }
    }
}
