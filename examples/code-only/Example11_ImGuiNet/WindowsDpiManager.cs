using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Example11_ImGuiNet;

/// <summary>
/// Provides Windows DPI awareness configuration and diagnostic capabilities
/// </summary>
public static class WindowsDpiManager
{
    #region DPI Awareness Configuration

    // Windows 10+ best option for DPI awareness
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    [DllImport("User32.dll", ExactSpelling = true, SetLastError = false)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("Shcore.dll")]
    private static extern int SetProcessDpiAwareness(int value); // 2 = PerMonitor

    /// <summary>
    /// Enables Per-Monitor DPI awareness V2 for the current process.
    /// Falls back to Per-Monitor DPI awareness on older Windows versions.
    /// Only works on Windows - silently returns on other platforms.
    /// </summary>
    public static void EnablePerMonitorV2()
    {
        // Only attempt to call Win32 APIs when running on Windows at runtime.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            // Try Per-Monitor-V2 first (Win10+)
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            return;
        }
        catch
        {
            // ignore and fallback
        }

        try
        {
            // Fallback to Per-Monitor (older Windows 8.1+)
            // 0=Unaware, 1=System, 2=PerMonitor
            SetProcessDpiAwareness(2);
        }
        catch
        {
            // ignore
        }
    }

    #endregion

    #region DPI Diagnostics

    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("shcore.dll")]
    private static extern int GetProcessDpiAwareness(IntPtr hprocess, out PROCESS_DPI_AWARENESS awareness);

    public enum PROCESS_DPI_AWARENESS
    {
        Process_DPI_Unaware = 0,
        Process_System_DPI_Aware = 1,
        Process_Per_Monitor_DPI_Aware = 2
    }

    /// <summary>
    /// Logs comprehensive DPI information to the console, including monitor DPI,
    /// process DPI awareness, and fallback GDI information.
    /// </summary>
    /// <param name="prefix">Optional prefix for log messages</param>
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

    #endregion

    #region DPI Query Methods

    /// <summary>
    /// Gets the effective DPI for the primary monitor
    /// </summary>
    /// <returns>DPI values for X and Y axes, or (96, 96) if unable to determine</returns>
    public static (uint dpiX, uint dpiY) GetPrimaryMonitorDpi()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return (96, 96);
        }

        try
        {
            var hMon = MonitorFromPoint(new POINT(0, 0), 2 /*MONITOR_DEFAULTTOPRIMARY*/);
            if (hMon != IntPtr.Zero)
            {
                int hr = GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
                if (hr == 0)
                {
                    return (dpiX, dpiY);
                }
            }
        }
        catch
        {
            // Fall through to GDI fallback
        }

        // Fallback to GDI
        var gdi = GraphicsDC.GetDesktopDpi();
        return ((uint)gdi.dpiX, (uint)gdi.dpiY);
    }

    /// <summary>
    /// Gets the DPI scale factor for the primary monitor (1.0 = 96 DPI)
    /// </summary>
    /// <returns>Scale factor (e.g., 1.25 for 125% scaling)</returns>
    public static float GetDpiScaleFactor()
    {
        var (dpiX, _) = GetPrimaryMonitorDpi();
        return dpiX / 96.0f;
    }

    /// <summary>
    /// Gets the current process DPI awareness level
    /// </summary>
    /// <returns>DPI awareness level, or null if unable to determine</returns>
    public static PROCESS_DPI_AWARENESS? GetProcessDpiAwareness()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        try
        {
            var proc = Process.GetCurrentProcess().Handle;
            if (GetProcessDpiAwareness(proc, out var awareness) == 0)
            {
                return awareness;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    #endregion

    #region GDI Fallback Helper

    // Small helper to query desktop DPI via GetDeviceCaps (fallback when shcore not present)
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

    #endregion
}