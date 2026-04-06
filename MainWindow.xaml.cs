using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using WinUINav.Pages;
using AppWindow = Microsoft.UI.Windowing.AppWindow;

namespace WinUINav
{
    public sealed partial class MainWindow : Window
    {

        private AppWindow? _appWindow;

        public Frame AppFrame => ContentFrame;

        // 在类中定义 Win32 常量和方法
        private const int GWL_STYLE = -16;
        private const uint WS_THICKFRAME = 0x00040000; // 允许调整大小的边框
        private const uint WS_MAXIMIZEBOX = 0x00010000; // 最大化按钮

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MainWindow()
        {
            InitializeComponent();

            // 在初始化位置调用
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            int style = GetWindowLong(hWnd, GWL_STYLE);

            // 移除 WS_THICKFRAME 位
            SetWindowLong(hWnd, GWL_STYLE, style & ~(int)WS_THICKFRAME & ~(int)WS_MAXIMIZEBOX);

            LoadAndApplyTheme();
            EnsureAppWindow();
            CenterWindow(1200, 800);
            ExtendsContentIntoTitleBar = true;

            if (_appWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, false);
                presenter.IsMinimizable = true;
                presenter.IsMaximizable = true;
                presenter.IsResizable = true;
            }

            SetTitleBar(AppTitleBar);
            CaptionButtons.Attach(this, AppTitleBar);

            CaptionButtons.IsCustomMaxButtonEnabled = true;

            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;
        }


        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            Activated -= MainWindow_Activated;

            LoadAndApplyTheme();
            EnsureAppWindow();
            CenterWindow(1200, 800);

            CaptionButtons.Attach(this, AppTitleBar);
            RefreshCaptionButtonsTheme();

            if (Content is FrameworkElement root)
            {
                root.ActualThemeChanged += Root_ActualThemeChanged;
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (Content is FrameworkElement root)
            {
                root.ActualThemeChanged -= Root_ActualThemeChanged;
            }

            CaptionButtons.Dispose();
        }

        private void Root_ActualThemeChanged(FrameworkElement sender, object args)
        {
            RefreshCaptionButtonsTheme();
        }

        private void LoadAndApplyTheme()
        {
            var savedTheme = Windows.Storage.ApplicationData.Current.LocalSettings.Values["AppTheme"] as string;

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
        private void RootNavView_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustNavButtons();

            ContentFrame.Navigate(typeof(HomePage));
        }

        private void RootNavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            AdjustNavButtons();
        }

        private void AdjustNavButtons()
        {
            // 汉堡按钮
            var toggleButton =
                FindElementByName<Button>(RootNavView, "TogglePaneButton") ??
                FindElementByName<Button>(RootNavView, "PaneToggleButton");

            // 返回按钮
            var backButton =
                FindElementByName<Button>(RootNavView, "NavigationViewBackButton") ??
                FindElementByName<Button>(RootNavView, "BackButton");

            if (toggleButton != null)
                toggleButton.Translation = new Vector3(0, -48, 0); // 改这里，数值越大越往上

            if (backButton != null)
                backButton.Translation = new Vector3(0, -48, 0);   // 改这里，数值越大越往上

            // 找到包含菜单项的 ScrollViewer 并把它也往上提
            var menuItemsScrollViewer = FindElementByName<ScrollViewer>(RootNavView, "MenuItemsScrollViewer");

            if (menuItemsScrollViewer != null)
            {
                // 这里的数值可以根据你想要的间距微调
                menuItemsScrollViewer.Translation = new Vector3(0, -48, 0);
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
            else if (e.SourcePageType == typeof(FilesPage))
            {
                SelectItemByTag("files");
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

                    case "files":
                        NavigateToPage(typeof(FilesPage));
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


        private void NavigateToPage(System.Type pageType)
        {
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
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

                case "文件":
                case "files":
                    NavigateToPage(typeof(FilesPage));
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