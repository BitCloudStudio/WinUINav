using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUINav;
/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    // 1. 解决未能找到 "HWND" 的报错
    public struct HWND
    {
        public IntPtr Value;
        public HWND(IntPtr value) { Value = value; }
        public static implicit operator IntPtr(HWND hwnd) => hwnd.Value;
    }

    // 2. 解决不存在 "User32" 的报错
    public static class User32
    {
        // 指定 CharSet 为 Unicode，并明确入口点为 SetWindowLongW
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowLongW")]
        public static extern int SetWindowLong(HWND hWnd, WindowLongFlags nIndex, int dwNewLong);

        // （可选）如果你在 64 位下遇到指针截断的警告，也可以使用这个更标准的 64 位兼容版本：
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowLongPtrW")]
        public static extern IntPtr SetWindowLongPtr(HWND hWnd, WindowLongFlags nIndex, IntPtr dwNewLong);

        public enum WindowLongFlags : int
        {
            GWL_EXSTYLE = -20,
        }

        public enum WindowStylesEx : uint
        {
            WS_EX_NOREDIRECTIONBITMAP = 0x00200000
        }
    }

    // 3. 解决不存在 "DwmApi" 的报错
    public static class DwmApi
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(HWND hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        public static extern unsafe int DwmSetWindowAttribute(HWND hwnd, DWMWINDOWATTRIBUTE attr, nint attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight;
            public MARGINS(int defaultMargin)
            {
                cxLeftWidth = cxRightWidth = cyTopHeight = cyBottomHeight = defaultMargin;
            }
        }

        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_SYSTEMBACKDROP_TYPE = 38
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override unsafe void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
