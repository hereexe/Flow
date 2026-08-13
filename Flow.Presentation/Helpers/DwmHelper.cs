using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Flow.Presentation.Helpers;

public static class DwmHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public static void EnableMicaEffect(IntPtr hwnd)
    {
        EnableBackdrop(hwnd, 2); // 2 = DWMSBT_MAINMICA
    }

    public static void EnableAcrylicEffect(IntPtr hwnd)
    {
        EnableBackdrop(hwnd, 3); // 3 = DWMSBT_TRANSIENTACCENT (Acrylic)
    }

    private static void EnableBackdrop(IntPtr hwnd, int backdropType)
    {
        // Require Windows 11 (Build 22000+)
        if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
        {
            int trueValue = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, Marshal.SizeOf(typeof(int)));
            
            // Build 22523+ supports DWMWA_SYSTEMBACKDROP_TYPE
            if (Environment.OSVersion.Version.Build >= 22523)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));
            }
        }
    }
}
