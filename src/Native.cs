using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Clawdling
{
    static class N
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant, colors;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

        public static readonly IntPtr TOPMOST = new IntPtr(-1);

        public static void BringTop(IntPtr h) { SetWindowPos(h, TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); }

        public static bool Rect(IntPtr h, out RECT r)
        {
            if (DwmGetWindowAttribute(h, 9, out r, 16) == 0) return true;
            return GetWindowRect(h, out r);
        }
        public static bool Cloaked(IntPtr h)
        {
            int v; return DwmGetWindowAttribute(h, 14, out v, 4) == 0 && v != 0;
        }
        public static string Cls(IntPtr h)
        {
            var sb = new StringBuilder(128); GetClassName(h, sb, 128); return sb.ToString();
        }
        public static void TrimMemory()
        {
            try { GC.Collect(); SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1)); } catch { }
        }
    }

    static class U
    {
        public static readonly Random R = new Random();
        public static float Rf(float a, float b) { return a + (float)R.NextDouble() * (b - a); }
        public static bool Ch(double p) { return R.NextDouble() < p; }
        public static float Cl(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static string Pick(params string[] s) { return s[R.Next(s.Length)]; }
        public static float Sin(double v) { return (float)Math.Sin(v); }
        public static float Cos(double v) { return (float)Math.Cos(v); }
        public static float Hyp(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }
        public static Color A(Color c, float a) { return Color.FromArgb((int)(c.A * Cl(a, 0, 1)), c); }

        public static GraphicsPath RR(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>32-bit premultiplied DIB that GDI+ draws into and UpdateLayeredWindow presents. No per-frame allocations.</summary>
    class Surface : IDisposable
    {
        public int W, H; public Bitmap Bmp; public Graphics G;
        IntPtr hdc, hbmp, old, bits;
        public Surface(int w, int h)
        {
            W = Math.Max(1, w); H = Math.Max(1, h);
            IntPtr screen = N.GetDC(IntPtr.Zero);
            hdc = N.CreateCompatibleDC(screen);
            N.ReleaseDC(IntPtr.Zero, screen);
            var bi = new N.BITMAPINFO();
            bi.biSize = 40; bi.biWidth = W; bi.biHeight = -H; bi.biPlanes = 1; bi.biBitCount = 32;
            hbmp = N.CreateDIBSection(hdc, ref bi, 0, out bits, IntPtr.Zero, 0);
            old = N.SelectObject(hdc, hbmp);
            Bmp = new Bitmap(W, H, W * 4, PixelFormat.Format32bppPArgb, bits);
            G = Graphics.FromImage(Bmp);
        }
        public void Present(IntPtr hwnd, int x, int y, byte alpha)
        {
            G.Flush(FlushIntention.Sync);
            var pt = new N.POINT(x, y); var sz = new N.SIZE(W, H); var src = new N.POINT(0, 0);
            var bf = new N.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
            N.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref pt, ref sz, hdc, ref src, 0, ref bf, 2);
        }
        public void Dispose()
        {
            if (G != null) { G.Dispose(); G = null; }
            if (Bmp != null) { Bmp.Dispose(); Bmp = null; }
            if (hdc != IntPtr.Zero) { N.SelectObject(hdc, old); N.DeleteObject(hbmp); N.DeleteDC(hdc); hdc = IntPtr.Zero; }
        }
    }

    /// <summary>Borderless, topmost, per-pixel-alpha window that never steals focus and never shows in the taskbar / Alt+Tab.</summary>
    class LayeredWindow : Form
    {
        public Surface S;
        readonly bool clickThrough;
        public LayeredWindow(int w, int h, bool clickThrough)
        {
            this.clickThrough = clickThrough;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            S = new Surface(w, h);
            Bounds = new Rectangle(-32000, -32000, w, h);
            var hnd = Handle;
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 /*LAYERED*/ | 0x80 /*TOOLWINDOW*/ | 0x8 /*TOPMOST*/ | 0x08000000 /*NOACTIVATE*/;
                if (clickThrough) cp.ExStyle |= 0x20 /*TRANSPARENT*/;
                return cp;
            }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref m);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
        public void SetCanvas(int w, int h)
        {
            if (S != null && S.W == w && S.H == h) return;
            if (S != null) S.Dispose();
            S = new Surface(w, h);
        }
        public void Present(int x, int y, byte a) { if (S != null) S.Present(Handle, x, y, a); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && S != null) { S.Dispose(); S = null; }
            base.Dispose(disposing);
        }
    }
}
