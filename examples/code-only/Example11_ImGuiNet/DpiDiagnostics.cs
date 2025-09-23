using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class DpiDiagnostics
{
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; public POINT(int x, int y) { X = x; Y = y; } }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
    [DllImport("shcore.dll")]
    private static extern int GetProcessDpiAwareness(IntPtr hprocess, out PROCESS_DPI_AWARENESS awareness);
    private enum PROCESS_DPI_AWARENESS { Process_DPI_Unaware = 0, Process_System_DPI_Aware = 1, Process_Per_Monitor_DPI_Aware = 2 }

    public static void LogDpiInfo(string prefix = "")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine($"{prefix}DPI diagnostics: not Windows");
            return;
        }

        try
        {
            // Query primary monitor DPI (use point 0,0)
            var hMon = MonitorFromPoint(new POINT(0, 0), 2 /*MONITOR_DEFAULTTOPRIMARY*/);
            if (hMon == IntPtr.Zero)
            {
                Console.WriteLine($"{prefix}DPI diagnostics: MonitorFromPoint returned null");
            }
            else
            {
                int hr = GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
                if (hr == 0)
                    Console.WriteLine($"{prefix}Monitor DPI (effective): {dpiX} x {dpiY} (scale {dpiX / 96.0:F2}x)");
                else
                    Console.WriteLine($"{prefix}GetDpiForMonitor failed (hr=0x{hr:X})");
            }

            // Query process DPI awareness
            var proc = Process.GetCurrentProcess().Handle;
            if (GetProcessDpiAwareness(proc, out var awareness) == 0)
            {
                Console.WriteLine($"{prefix}Process DPI awareness: {awareness}");
            }
            else
            {
                Console.WriteLine($"{prefix}GetProcessDpiAwareness failed");
            }

            // Desktop (system) DPI via desktop DC as fallback
            var gdi = GraphicsDC.GetDesktopDpi();
            Console.WriteLine($"{prefix}GDI desktop DPI (fallback): {gdi.dpiX} x {gdi.dpiY} (scale {gdi.dpiX / 96.0:F2}x)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{prefix}DPI diagnostics exception: {ex.Message}");
        }
    }

    // small helper to query desktop DPI via GetDeviceCaps (fallback when shcore not present)
    private static class GraphicsDC
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;

        public static (int dpiX, int dpiY) GetDesktopDpi()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return (96, 96);
            try
            {
                int dpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                int dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
                return (dpiX == 0 ? 96 : dpiX, dpiY == 0 ? 96 : dpiY);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
    }
}