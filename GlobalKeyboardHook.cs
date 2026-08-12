using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Screen1
{
    public class GlobalKeyboardHook : IDisposable
    {
        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int id, LowLevelProc cb, IntPtr hMod, uint tid);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string name);

        delegate IntPtr LowLevelProc(int code, IntPtr wp, IntPtr lp);

        public event Action<Keys> KeyPressed;
        private IntPtr _hook;
        private LowLevelProc _proc;

        public GlobalKeyboardHook()
        {
            _proc = Callback;
            using (var p = Process.GetCurrentProcess())
            using (var m = p.MainModule)
                _hook = SetWindowsHookEx(13, _proc, GetModuleHandle(m.ModuleName), 0);
        }

        private IntPtr Callback(int code, IntPtr wp, IntPtr lp)
        {
            if (code >= 0 && wp == (IntPtr)0x100)
            {
                int vk = Marshal.ReadInt32(lp);
                KeyPressed?.Invoke((Keys)vk);
            }
            return CallNextHookEx(_hook, code, wp, lp);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        }
    }
}
