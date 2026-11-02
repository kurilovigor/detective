using System.Windows;
using Detective.Configuration;
using Detective.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Detective
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            _viewModel.Navigate = page =>
            {
                MainFrame.Navigate(page);
                _viewModel.CurrentPage = page;
            };

            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            SystemThemeWatcher.Watch(this);

            RestoreWindowState();
            NavigateToStartPage();
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            SaveWindowState();
            AppSettingsService.Save();
            _viewModel.Dispose();
        }

        private void RecentFile_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem item &&
                item.DataContext is string path &&
                System.IO.File.Exists(path))
            {
                _viewModel.RecentFileCommands.TryGetValue(path, out var cmd);
                cmd?.Execute(null);
                e.Handled = true;
            }
        }

        private void RestoreWindowState()
        {
            var settings = AppSettingsService.Settings;
            Width = settings.MainWindowWidth;
            Height = settings.MainWindowHeight;
            this.WindowState = settings.MainWindowWindowState switch
            {
                Configuration.WindowState.Maximized => System.Windows.WindowState.Maximized,
                Configuration.WindowState.Minimized => System.Windows.WindowState.Minimized,
                _ => System.Windows.WindowState.Normal
            };
        }

        private void SaveWindowState()
        {
            var settings = AppSettingsService.Settings;
            // Only save size when not maximized (restored size is meaningful)
            if (this.WindowState == System.Windows.WindowState.Normal)
            {
                settings.MainWindowWidth = Width;
                settings.MainWindowHeight = Height;
            }
            settings.MainWindowWindowState = this.WindowState == System.Windows.WindowState.Maximized
                ? Configuration.WindowState.Maximized
                : Configuration.WindowState.Normal;
        }

        private void NavigateToStartPage()
        {
            var settings = AppSettingsService.Settings;
            if (settings.StartPage == StartPageEnum.Home)
            {
                _viewModel.Navigate?.Invoke(new Views.Pages.HomePage());
                return;
            }

            if (settings.StartPage == StartPageEnum.Last)
            {
                if (settings.RecentFiles != null && settings.RecentFiles.Count > 0)
                {
                    var path = settings.RecentFiles[0];
                    if (System.IO.File.Exists(path))
                    {
                        _viewModel.RecentFileCommands.TryGetValue(path, out var cmd);
                        cmd?.Execute(null);
                        return;
                    }
                }
            }

            // Fallback to home
            _viewModel.Navigate?.Invoke(new Views.Pages.HomePage());
        }
    }
}