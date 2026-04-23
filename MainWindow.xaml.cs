using WinUINav.Pages;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using Windows.Storage;
using AppWindow = Microsoft.UI.Windowing.AppWindow;
using AppWindowTitleBar = Microsoft.UI.Windowing.AppWindowTitleBar;

namespace WinUINav
{
    public sealed partial class MainWindow : Window
    {

        private NativeWindowPaintHook? _paintHook;

        private AppWindow? _appWindow;
        public Frame AppFrame => ContentFrame;

        // 【修复1】使用 Ptr 后缀和 IntPtr 适配 64位 AOT
        private const int GWL_STYLE = -16;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private const uint WM_GETMINMAXINFO = 0x0024;
        private SUBCLASSPROC? _subclassProc;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        // 【修复2】第五个参数 uIdSubclass 必须是 UIntPtr，避免 64 位 AOT 栈破坏
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        public const string BackdropSettingKey = "WindowBackdropMode";

        public const string ThemeDefault = "Default";
        public const string ThemeLight = "Light";
        public const string ThemeDark = "Dark";

        private void ApplySavedSettings()
        {
            var settings = AppSettings.Load();

            ApplyTheme(settings.Theme, save: false);
            ApplyBackdrop(settings.BackdropMode, save: false);
            ApplyNavMode(settings.NavMode, save: false);
        }

        public void ApplyTheme(string theme, bool save = true)
        {
            ElementTheme targetTheme = theme switch
            {
                "Dark" => ElementTheme.Dark,
                "Light" => ElementTheme.Light,
                _ => ElementTheme.Default
            };

            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = targetTheme;
            }

            RootNavView.RequestedTheme = targetTheme;
            ContentFrame.RequestedTheme = targetTheme;
            AppTitleBar.RequestedTheme = targetTheme;

            DispatcherQueue.TryEnqueue(() =>
            {
                if (Content is FrameworkElement root)
                {
                    RefreshCaptionButtonsTheme();

                    var settings = AppSettings.Load();
                    bool isDark = root.ActualTheme == ElementTheme.Dark;
                    DwmBackdropHelper.Apply(this, settings.BackdropMode, isDark);
                }
            });

            if (save)
            {
                var settings = AppSettings.Load();
                settings.Theme = theme;
                AppSettings.Save(settings);
            }
        }

        public void ApplyBackdrop(string mode, bool save = true)
        {
            switch (mode)
            {
                case "Mica":
                    this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                    break;

                case "MicaAlt":
                    this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
                    break;

                case "Acrylic":
                    this.SystemBackdrop = new DesktopAcrylicBackdrop();
                    break;

                default:
                    this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
                    mode = "MicaAlt";
                    break;
            }

            if (Content is FrameworkElement root)
            {
                bool isDark = root.ActualTheme == ElementTheme.Dark;
                DwmBackdropHelper.Apply(this, mode, isDark);
            }

            if (save)
            {
                var settings = AppSettings.Load();
                settings.BackdropMode = mode;
                AppSettings.Save(settings);
            }
        }

        public void ApplyNavMode(string navMode, bool save = true)
        {
            NavigationViewPaneDisplayMode paneMode = navMode switch
            {
                "Left" => NavigationViewPaneDisplayMode.Left,
                "Top" => NavigationViewPaneDisplayMode.Top,
                _ => NavigationViewPaneDisplayMode.Auto
            };

            if (RootNavView.PaneDisplayMode != paneMode)
            {
                RootNavView.PaneDisplayMode = paneMode;
            }

            AdjustNavViewMargin();

            if (save)
            {
                var settings = AppSettings.Load();
                settings.NavMode = navMode;
                AppSettings.Save(settings);
            }
        }

        private void ApplySavedNavMode(string navMode)
        {
            ApplyNavMode(navMode, save: false);
        }

        public void ApplySavedBackdrop()
        {
            var settings = AppSettings.Load();
            ApplyBackdrop(settings.BackdropMode, false);
        }
        private IntPtr WindowSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_GETMINMAXINFO)
            {
                // 【修复3】使用泛型方法避免 AOT 反射异常
                MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                uint dpi = GetDpiForWindow(hWnd);
                float scalingFactor = (float)dpi / 96f;

                int minWidth = 800;
                int minHeight = 600;

                mmi.ptMinTrackSize.x = (int)(minWidth * scalingFactor);
                mmi.ptMinTrackSize.y = (int)(minHeight * scalingFactor);

                // 【修复3】使用泛型写回
                Marshal.StructureToPtr<MINMAXINFO>(mmi, lParam, false);

                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void SetWindowIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");

                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                appWindow.SetIcon(iconPath);
            }
            catch
            {
                // 这里先别抛异常，避免图标问题影响启动
            }
        }
        public MainWindow()
        {
            InitializeComponent();

            SetWindowIcon();

            IntPtr hWnd = WindowNative.GetWindowHandle(this);

            _subclassProc = new SUBCLASSPROC(WindowSubclassProc);
            SetWindowSubclass(hWnd, _subclassProc, (UIntPtr)1, IntPtr.Zero);

            this.ExtendsContentIntoTitleBar = true;

            EnsureAppWindow();
            CenterWindow(1200, 800);

            if (_appWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, false);
                presenter.IsMinimizable = true;
                presenter.IsMaximizable = true;
                presenter.IsResizable = true;
            }

            HideSystemTitleBarButtons();

            SetTitleBar(AppTitleBar);
            CaptionButtons.Attach(this, AppTitleBar);
            CaptionButtons.IsCustomMaxButtonEnabled = true;

            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;

            RootNavView.RegisterPropertyChangedCallback(
                NavigationView.PaneDisplayModeProperty,
                OnPaneDisplayModeChanged);

            ApplySavedSettings();
        }

        // 新增方法：用于在 AOT 下彻底抹除系统按钮的影响
        private void HideSystemTitleBarButtons()
        {
            if (_appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = _appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;

                // 【终极必杀技】
                // 直接将系统按钮的物理高度折叠为 0。
                // 这不仅仅是变透明，而是彻底把系统按钮连根拔起！
                // 这样你的自定义按钮就能完美接收到鼠标点击了。
                titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitleBarMargin();
        }

        private void RootNavView_Loaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(HomePage));
            UpdateTitleBarMargin();

            AdjustNavViewMargin();
        }

        private void RootNavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            UpdateTitleBarMargin();
        }

        private void UpdateTitleBarMargin()
        {
            // 如果当前是 Top 模式，标题栏左侧不需要给汉堡包按钮留空位
            if (RootNavView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
            {
                // 留 16 像素的基础边距，让图标不会完全紧贴窗口边缘，符合 Windows 原生观感
                AppTitleBar.Margin = new Thickness(16, 0, 0, 0);
            }
            else
            {
                // 如果是 左侧 / 自动 模式，依然按照原逻辑计算避让距离
                AppTitleBar.Margin = new Thickness(
                    RootNavView.CompactPaneLength * (RootNavView.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
                    0,
                    0,
                    0);
            }
        }


        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            Activated -= MainWindow_Activated;

            EnsureAppWindow();
            CenterWindow(1200, 800);

            CaptionButtons.Attach(this, AppTitleBar);
            RefreshCaptionButtonsTheme();

            if (Content is FrameworkElement root)
            {
                root.ActualThemeChanged += Root_ActualThemeChanged;
            }

            if (_paintHook is not null)
                return;

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            _paintHook = new NativeWindowPaintHook(hwnd);
            _paintHook.Attach();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (Content is FrameworkElement root)
            {
                root.ActualThemeChanged -= Root_ActualThemeChanged;
            }

            CaptionButtons.Dispose();

            _paintHook?.Detach();
            _paintHook = null;
        }

        private void Root_ActualThemeChanged(FrameworkElement sender, object args)
        {
            RefreshCaptionButtonsTheme();

            var settings = AppSettings.Load();
            bool isDark = sender.ActualTheme == ElementTheme.Dark;
            DwmBackdropHelper.Apply(this, settings.BackdropMode, isDark);
        }

        private void LoadAndApplyTheme()
        {
            // 【修改点】因为现在是免安装(Unpackaged)应用，不能再使用 ApplicationData.Current
            // 我们改用原生的 .NET 方法读取本地配置
            string savedTheme = "Default";
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(localAppData, "WinUINav");
                string settingsFile = Path.Combine(appFolder, "theme_setting.txt");

                if (File.Exists(settingsFile))
                {
                    savedTheme = File.ReadAllText(settingsFile).Trim();
                }
            }
            catch
            {
                // 如果读取失败（比如没有权限等），默认使用系统主题
                savedTheme = "Default";
            }

            if (Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = savedTheme switch
                {
                    "Dark" => ElementTheme.Dark,
                    "Light" => ElementTheme.Light,
                    _ => ElementTheme.Default
                };
            }
        }

        private void RefreshCaptionButtonsTheme()
        {
            if (Content is not FrameworkElement root)
            {
                return;
            }

            CaptionButtons.RefreshForTheme(root.ActualTheme);
            TitleText.Foreground = root.ActualTheme == ElementTheme.Dark
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
        }

        private void EnsureAppWindow()
        {
            if (_appWindow != null)
            {
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
        }

        private void CenterWindow(int width, int height)
        {
            EnsureAppWindow();
            if (_appWindow is null)
            {
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

            int screenWidth = displayArea.WorkArea.Width;
            int screenHeight = displayArea.WorkArea.Height;

            _appWindow.MoveAndResize(new RectInt32(
                (screenWidth - width) / 2,
                (screenHeight - height) / 2,
                width,
                height));
        }

        private void RootNavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private static T? FindElementByName<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T fe && fe.Name == name)
                    return fe;

                var result = FindElementByName<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void InitWindowSizeAndCenter()
        {
            int width = 1200;
            int height = 800;

            // 当前窗口所在显示器的可用工作区（不含任务栏）
            DisplayArea? displayArea = DisplayArea.GetFromWindowId(
                AppWindow.Id,
                DisplayAreaFallback.Nearest);

            if (displayArea is null)
                return;

            RectInt32 workArea = displayArea.WorkArea;

            int x = workArea.X + (workArea.Width - width) / 2;
            int y = workArea.Y + (workArea.Height - height) / 2;

            AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            RootNavView.IsBackEnabled = ContentFrame.CanGoBack;

            if (e.SourcePageType == typeof(HomePage))
            {
                SelectItemByTag("home");
            }
            else if (e.SourcePageType == typeof(AboutPage))
            {
                SelectItemByTag("about");
            }
            else if (e.SourcePageType == typeof(SettingsPage))
            {
                RootNavView.SelectedItem = RootNavView.SettingsItem;
            }
        }

        private void InitTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        private void RootNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigateToPage(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItemContainer is NavigationViewItem item)
            {
                string tag = item.Tag?.ToString() ?? string.Empty;

                switch (tag)
                {
                    case "home":
                        NavigateToPage(typeof(HomePage));
                        break;

                    case "about":
                        NavigateToPage(typeof(AboutPage));
                        break;
                }
            }
        }

        private void AppTitleBar_BackRequested(TitleBar sender, object args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            RootNavView.IsPaneOpen = !RootNavView.IsPaneOpen;
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(SettingsPage))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(HomePage))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(AboutPage))]
        private void NavigateToPage(System.Type pageType)
        {
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void OnPaneDisplayModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            AdjustNavViewMargin();
        }

        private void AdjustNavViewMargin()
        {
            if (RootNavView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
            {
                // Top模式下，向下偏移标题栏的高度
                RootNavView.Margin = new Thickness(0, RootNavView.CompactPaneLength, 0, 0);
            }
            else
            {
                // 其他模式恢复正常
                RootNavView.Margin = new Thickness(0, 0, 0, 0);
            }

            // 【新增】每次调整导航栏模式时，同步更新标题栏里图标的位置
            UpdateTitleBarMargin();
        }

        private void SelectItemByTag(string tag)
        {
            foreach (var item in RootNavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == tag)
                {
                    RootNavView.SelectedItem = navItem;
                    break;
                }
            }
        }

        private void MainSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string text = sender.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return;

            switch (text.ToLower())
            {
                case "首页":
                case "home":
                    NavigateToPage(typeof(HomePage));
                    break;

                case "关于":
                case "about":
                    NavigateToPage(typeof(AboutPage));
                    break;

                case "设置":
                case "settings":
                    NavigateToPage(typeof(SettingsPage));
                    break;
            }
        }
    }
}