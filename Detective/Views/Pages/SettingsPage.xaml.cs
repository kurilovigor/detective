using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Detective.Configuration;

namespace Detective.Views.Pages
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            NavigateBackButton.Click += NavigateBackButton_Click;

            // Populate start page combo with enum values and set current value
            StartPageComboBox.ItemsSource = Enum.GetValues(typeof(StartPageEnum));
            StartPageComboBox.SelectedItem = AppSettingsService.Settings.StartPage;
            StartPageComboBox.SelectionChanged += StartPageComboBox_SelectionChanged;
        }

        private void NavigateBackButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var nav = this.NavigationService;
                if (nav != null && nav.CanGoBack)
                {
                    nav.GoBack();
                }
            }
            catch { }
        }

        private void StartPageComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (StartPageComboBox.SelectedItem is StartPageEnum sp)
            {
                AppSettingsService.Settings.StartPage = sp;
                AppSettingsService.Save();
            }
        }
    }
}
