using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WinUINav.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isInitializing;

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        private void NavModeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var navView = FindParent<NavigationView>(this);
            if (navView == null)
                return;

            _isInitializing = true;
            try
            {
                // 这里同步“用户设置的模式”，所以看 PaneDisplayMode
                switch (navView.PaneDisplayMode)
                {
                    case NavigationViewPaneDisplayMode.Left:
                        NavModeComboBox.SelectedIndex = 0;
                        break;

                    case NavigationViewPaneDisplayMode.Top:
                        NavModeComboBox.SelectedIndex = 1;
                        break;

                    case NavigationViewPaneDisplayMode.Auto:
                    default:
                        NavModeComboBox.SelectedIndex = 2;
                        break;
                }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void NavModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            if (NavModeComboBox.SelectedItem is not ComboBoxItem item || item.Tag == null)
                return;

            var navView = FindParent<NavigationView>(this);
            if (navView == null)
                return;

            NavigationViewPaneDisplayMode paneMode = item.Tag.ToString() switch
            {
                "Left" => NavigationViewPaneDisplayMode.Left,
                "Top" => NavigationViewPaneDisplayMode.Top,
                _ => NavigationViewPaneDisplayMode.Auto
            };

            if (navView.PaneDisplayMode != paneMode)
            {
                navView.PaneDisplayMode = paneMode;
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
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