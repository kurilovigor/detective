using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Detective.Configuration;
using Detective.ViewModels;

namespace Detective.Views.Pages
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();

            Loaded += HomePage_Loaded;
            NewButton.Click += NewButton_Click;
            OpenButton.Click += OpenButton_Click;
            RecentFilesListBox.MouseDoubleClick += RecentFilesListBox_MouseDoubleClick;
        }

        private MainWindowViewModel? GetMainViewModel()
        {
            return (System.Windows.Application.Current.MainWindow as MainWindow)?.DataContext as MainWindowViewModel;
        }

        private void HomePage_Loaded(object? sender, RoutedEventArgs e)
        {
            RecentFilesListBox.ItemsSource = AppSettingsService.Settings.RecentFiles;
        }

        private void NewButton_Click(object? sender, RoutedEventArgs e)
        {
            var vm = GetMainViewModel();
            vm?.NewCommand?.Execute(null);
        }

        private void OpenButton_Click(object? sender, RoutedEventArgs e)
        {
            var vm = GetMainViewModel();
            vm?.OpenCommand?.Execute(null);
        }

        private void RecentFilesListBox_MouseDoubleClick(object? sender, MouseButtonEventArgs e)
        {
            if (RecentFilesListBox.SelectedItem is string path)
            {
                var vm = GetMainViewModel();
                if (vm != null)
                {
                    vm.RecentFileCommands.TryGetValue(path, out var cmd);
                    cmd?.Execute(null);
                }
            }
        }
    }
}
