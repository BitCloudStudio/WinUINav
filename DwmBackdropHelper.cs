// DwmBackdropHelper.cs
using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace WinUINav;

internal static class DwmBackdropHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;

        public MARGINS(int uniformMargin)
        {
            cxLeftWidth = uniformMargin;
            cxRightWidth = uniformMargin;
            cyTopHeight = uniformMargin;
            cyBottomHeight = uniformMargin;
        }
    }

    private enum DWMWINDOWATTRIBUTE
    {
        DWMWA_SYSTEMBACKDROP_TYPE = 38,
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20
    }

    public enum DwmSystemBackdropType
    {
        Auto = 0,
        None = 1,
        MainWindow = 2,      // Mica
        TransientWindow = 3, // Acrylic
        TabbedWindow = 4     // MicaAlt
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        DWMWINDOWATTRIBUTE dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    public static void Apply(Window window, DwmSystemBackdropType backdropType, bool followDarkMode = true)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
            return;

        // 对应文章里的：MARGINS{-1, -1, -1, -1}
        var margins = new MARGINS(-1);
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // 可选：让标题栏跟随深浅色
        if (followDarkMode)
        {
            int dark = Application.Current.RequestedTheme == ApplicationTheme.Dark ? 1 : 0;
            _ = DwmSetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref dark,
                sizeof(int));
        }

        // 对应文章里的：DWMWA_SYSTEMBACKDROP_TYPE
        int type = (int)backdropType;
        _ = DwmSetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
            ref type,
            sizeof(int));
    }
}