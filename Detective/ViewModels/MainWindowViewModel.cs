using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Detective.Configuration;
using Detective.Database;
using Detective.Views.Pages;
using Detective.Yolo.Export;

namespace Detective.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private const string DetectiveFilter = "Detective projects (*.detective)|*.detective";
        private const string ApplicationTitle = "DETECTive";

        private string _titleText = ApplicationTitle;
        private readonly DetectionDatabaseFactory _databaseFactory;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string TitleText
        {
            get => _titleText;
            set
            {
                if (_titleText != value)
                {
                    _titleText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleText)));
                }
            }
        }

        public System.Windows.Input.ICommand NewCommand { get; }
        public System.Windows.Input.ICommand OpenCommand { get; }
        public System.Windows.Input.ICommand HomeCommand { get; }
        public System.Windows.Input.ICommand ExportToYoloCommand { get; }
        public System.Windows.Input.ICommand SettingsCommand { get; }
        public System.Windows.Input.ICommand ExitCommand { get; }
        public System.Windows.Input.ICommand UndoCommand { get; }
        public System.Windows.Input.ICommand RedoCommand { get; }
        public System.Windows.Input.ICommand DeleteLabelCommand { get; }
        public System.Windows.Input.ICommand AddFilesCommand { get; }
        public System.Windows.Input.ICommand AddFolderCommand { get; }
        public System.Windows.Input.ICommand DeleteFileCommand { get; }
        public System.Windows.Input.ICommand DeleteAllCommand { get; }
        public System.Windows.Input.ICommand SelectPriorCommand { get; }
        public System.Windows.Input.ICommand SelectNextCommand { get; }
        public Dictionary<string, System.Windows.Input.ICommand> RecentFileCommands { get; }

        public Action<Page>? Navigate { get; set; }

        private Page? _currentPage;
        public Page? CurrentPage
        {
            get => _currentPage;
            set
            {
                // Unhook from previous page
                if (_currentPage is LabelEditPage oldPage)
                {
                    oldPage.Editor.SelectedChanged -= OnEditorSelectedChanged;
                    oldPage.Editor.History.PropertyChanged -= OnHistoryPropertyChanged;
                    oldPage.Files.SelectedChanged -= OnFilesSelectedChanged;
                    oldPage.Files.Files!.CollectionChanged -= OnFilesCollectionChanged;
                }

                _currentPage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPage)));

                // Hook into new page
                if (_currentPage is LabelEditPage newPage)
                {
                    newPage.Editor.SelectedChanged += OnEditorSelectedChanged;
                    newPage.Editor.History.PropertyChanged += OnHistoryPropertyChanged;
                    newPage.Files.SelectedChanged += OnFilesSelectedChanged;
                    newPage.Files.Files!.CollectionChanged += OnFilesCollectionChanged;
                }

                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        private void OnEditorSelectedChanged(object? sender, LabelRect? e) => System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        private void OnHistoryPropertyChanged(object? sender, PropertyChangedEventArgs e) => System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        private void OnFilesSelectedChanged(object? sender, int e) => System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        private void OnFilesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

        public MainWindowViewModel()
        {
            _databaseFactory = new DetectionDatabaseFactory();

            NewCommand = new RelayCommand(_ => New());
            OpenCommand = new RelayCommand(_ => Open());
            HomeCommand = new RelayCommand(_ => Home());
            ExportToYoloCommand = new RelayCommand(_ => ExportToYolo(), _ => CanLabelEditPage());
            SettingsCommand = new RelayCommand(_ => Settings());
            ExitCommand = new RelayCommand(_ => Exit());
            UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo());
            RedoCommand = new RelayCommand(_ => Redo(), _ => CanRedo());
            DeleteLabelCommand = new RelayCommand(_ => DeleteLabel(), _ => CanDeleteLabel());
            AddFilesCommand = new RelayCommand(_ => AddFiles(), _ => CanLabelEditPage());
            AddFolderCommand = new RelayCommand(_ => AddFolder(), _ => CanLabelEditPage());
            DeleteFileCommand = new RelayCommand(_ => DeleteFile(), _ => CanDeleteFile());
            DeleteAllCommand = new RelayCommand(_ => DeleteAll(), _ => CanDeleteAll());
            SelectPriorCommand = new RelayCommand(_ => SelectPrior(), _ => CanSelectPrior());
            SelectNextCommand = new RelayCommand(_ => SelectNext(), _ => CanSelectNext());

            RecentFileCommands = new Dictionary<string, System.Windows.Input.ICommand>();
            UpdateRecentFileCommands();
        }

        public void Dispose()
        {
            _databaseFactory.Dispose();
        }


        private void New()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = DetectiveFilter };

            if (dlg.ShowDialog() == true)
            {
                AppSettingsService.Settings.AddRecentFile(dlg.FileName);
                AppSettingsService.Save();
                UpdateRecentFileCommands();
                Navigate?.Invoke(new LabelEditPage(_databaseFactory.CreateDatabase(dlg.FileName)));
                TitleText = $"{ApplicationTitle} - {dlg.FileName}";
            }
        }

        private void Open()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = DetectiveFilter };

            if (dlg.ShowDialog() == true)
            {
                AppSettingsService.Settings.AddRecentFile(dlg.FileName);
                AppSettingsService.Save();
                UpdateRecentFileCommands();
                Navigate?.Invoke(new LabelEditPage(_databaseFactory.CreateDatabase(dlg.FileName)));
                TitleText = $"{ApplicationTitle} - {dlg.FileName}";
            }
        }

        private DetectionDatabase? _currentDatabase;
        public DetectionDatabase? CurrentDatabase
        {
            get => (CurrentPage as LabelEditPage)?.DetectionDatabase ?? _currentDatabase;
            set => _currentDatabase = value;
        }

        public void ExportToYolo(string? destinationFilePath)
        {
            var page = CurrentPage as LabelEditPage;
            page?.SaveCurrentFileItem();

            var database = CurrentDatabase;
            if (database == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(destinationFilePath))
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "data.yml",
                    DefaultExt = ".yml",
                    Filter = "YAML files (*.yml)|*.yml|All files (*.*)|*.*"
                };

                if (dlg.ShowDialog() != true)
                {
                    return;
                }

                destinationFilePath = dlg.FileName;
            }

            var exporter = new YoloExporter();
            exporter.Export(database, destinationFilePath);
        }

        public void ExportToYolo()
        {
            ExportToYolo(null);
        }

        private void Settings()
        {
            Navigate?.Invoke(new SettingsPage());
        }

        private void Home()
        {
            Navigate?.Invoke(new HomePage());
        }

        private void Exit()
        {
            System.Windows.Application.Current.Shutdown();
        }

        private bool CanUndo() => CurrentPage is LabelEditPage page && page.Editor.History.CanUndo;
        private bool CanRedo() => CurrentPage is LabelEditPage page && page.Editor.History.CanRedo;

        private void Undo()
        {
            if (CurrentPage is LabelEditPage page)
                page.Editor.Undo();
        }

        private void Redo()
        {
            if (CurrentPage is LabelEditPage page)
                page.Editor.Redo();
        }

        private bool CanDeleteLabel() => CurrentPage is LabelEditPage page && page.Editor.Selected != null;

        private void DeleteLabel()
        {
            if (CurrentPage is LabelEditPage page)
                page.Editor.DeleteSelected();
        }

        private void AddFiles()
        {
            if (CurrentPage is LabelEditPage page)
                page.Files.AddFiles();
        }

        private void AddFolder()
        {
            if (CurrentPage is LabelEditPage page)
                page.Files.AddFolder();
        }

        private void DeleteFile()
        {
            if (CurrentPage is LabelEditPage page &&
                System.Windows.MessageBox.Show("Delete selected file?", "Confirm", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
                page.Files.DeleteSelected();
        }

        private void DeleteAll()
        {
            if (CurrentPage is LabelEditPage page &&
                System.Windows.MessageBox.Show("Delete all files?", "Confirm", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
                page.Files.Files!.Clear();
        }

        private bool CanDeleteFile() => CurrentPage is LabelEditPage page && page.Files.SelectedIndex >= 0;
        private bool CanDeleteAll() => CurrentPage is LabelEditPage page && page.Files.Files!.Count > 0;
        private bool CanSelectPrior() => CurrentPage is LabelEditPage page && page.Files.Files!.Count > 0 && (page.Files.SelectedIndex < 0 || page.Files.SelectedIndex > 0);
        private bool CanSelectNext() => CurrentPage is LabelEditPage page && page.Files.Files!.Count > 0 && (page.Files.SelectedIndex < 0 || page.Files.SelectedIndex < page.Files.Files.Count - 1);
        private bool CanLabelEditPage() => CurrentPage is LabelEditPage;

        private void SelectPrior()
        {
            if (CurrentPage is LabelEditPage page)
                page.Files.SelectPrior();
        }

        private void SelectNext()
        {
            if (CurrentPage is LabelEditPage page)
                page.Files.SelectNext();
        }

        private void UpdateRecentFileCommands()
        {
            RecentFileCommands.Clear();
            foreach (var file in AppSettingsService.Settings.RecentFiles)
            {
                var path = file;
                RecentFileCommands[path] = new RelayCommand(_ => OpenRecentFile(path));
            }
        }

        private void OpenRecentFile(string path)
        {
            if (File.Exists(path))
            {
                Navigate?.Invoke(new LabelEditPage(_databaseFactory.CreateDatabase(path)));
                TitleText = $"{ApplicationTitle} - {path}";
            }
        }
    }
}
