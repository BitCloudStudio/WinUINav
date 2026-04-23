using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WinUINav.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isInitializing;
        private bool _backdropComboReady;
        private bool _themeComboReady;
        private bool _navComboReady;

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        private void BackdropModeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            _backdropComboReady = false;

            string savedMode = AppSettings.Load().BackdropMode;

            foreach (var item in BackdropModeComboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    (comboBoxItem.Tag as string) == savedMode)
                {
                    BackdropModeComboBox.SelectedItem = comboBoxItem;
                    break;
                }
            }

            if (BackdropModeComboBox.SelectedItem == null)
            {
                foreach (var item in BackdropModeComboBox.Items)
                {
                    if (item is ComboBoxItem comboBoxItem &&
                        (comboBoxItem.Tag as string) == "MicaAlt")
                    {
                        BackdropModeComboBox.SelectedItem = comboBoxItem;
                        break;
                    }
                }
            }

            _backdropComboReady = true;
        }

        private void BackdropModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_backdropComboReady)
                return;

            if (BackdropModeComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string mode &&
                App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.ApplyBackdrop(mode, save: true);
            }
        }

        private void ThemeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            _themeComboReady = false;

            string savedTheme = AppSettings.Load().Theme;

            foreach (var item in ThemeComboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    (comboBoxItem.Tag as string) == savedTheme)
                {
                    ThemeComboBox.SelectedItem = comboBoxItem;
                    break;
                }
            }

            if (ThemeComboBox.SelectedItem == null)
            {
                ThemeComboBox.SelectedIndex = 0;
            }

            _themeComboReady = true;
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_themeComboReady)
                return;

            if (ThemeComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string theme &&
                App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.ApplyTheme(theme, save: true);
            }
        }

        private void NavModeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            _navComboReady = false;

            string savedNavMode = AppSettings.Load().NavMode;

            foreach (var item in NavModeComboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    (comboBoxItem.Tag as string) == savedNavMode)
                {
                    NavModeComboBox.SelectedItem = comboBoxItem;
                    break;
                }
            }

            if (NavModeComboBox.SelectedItem == null)
            {
                NavModeComboBox.SelectedIndex = 2;
            }

            _navComboReady = true;
        }

        private void NavModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_navComboReady || _isInitializing)
                return;

            if (NavModeComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string mode)
                return;

            if (App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.ApplyNavMode(mode, save: true);
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }
    }
}