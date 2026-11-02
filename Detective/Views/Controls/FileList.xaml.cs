using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Collections.ObjectModel;
using Detective.ViewModels;
using System.ComponentModel;
using System.Windows.Data;
using UserControl = System.Windows.Controls.UserControl;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Detective.Views.Controls
{
    /// <summary>
    /// Interaction logic for FileList.xaml
    /// </summary>
    public partial class FileList : UserControl
    {
        public static readonly DependencyProperty FilesProperty = DependencyProperty.Register(
            nameof(Files),
            typeof(ObservableCollection<FileItem>),
            typeof(FileList),
            new FrameworkPropertyMetadata(null, OnFilesPropertyChanged));

        public ObservableCollection<FileItem>? Files
        {
            get => (ObservableCollection<FileItem>?)GetValue(FilesProperty);
            set => SetValue(FilesProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
            nameof(SelectedItem), typeof(FileItem), typeof(FileList), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public FileItem? SelectedItem
        {
            get => (FileItem?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileList fl && fl.FilesListView != null)
            {
                fl.FilesListView.SelectedItem = e.NewValue;
            }
        }

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(FileList), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        // Raised when the SelectedIndex changes. Provides the new index as the event argument.
        public event EventHandler<int>? SelectedChanged;

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileList fl && fl.FilesListView != null)
            {
                fl.FilesListView.SelectedIndex = (int)e.NewValue;
                try
                {
                    int oldIndex = (int)e.OldValue;
                    int newIndex = (int)e.NewValue;
                    if (oldIndex != newIndex)
                    {
                        fl.SelectedChanged?.Invoke(fl, newIndex);
                    }
                }
                catch { }
            }
        }

        public FileList()
        {
            InitializeComponent();
            // ensure Files is not null so binding works
            if (Files == null)
                Files = new ObservableCollection<FileItem>();
            AddFileButton.Click += AddFileButton_Click;
            AddFolderButton.Click += AddFolderButton_Click;
            // AutoSuggestBox.TextChanged has a different delegate signature; use lambda to avoid referencing its event args type
            SearchTextBox.TextChanged += (s, e) => OnSearchTextChanged();
            SetupFilesView();
        }

        private static void OnFilesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileList fl)
            {
                if (e.OldValue is ObservableCollection<FileItem> oldCol)
                {
                    oldCol.CollectionChanged -= fl.Files_CollectionChanged;
                }
                if (e.NewValue is ObservableCollection<FileItem> newCol)
                {
                    newCol.CollectionChanged += fl.Files_CollectionChanged;
                }
                fl.UpdateSuggestions();
            }
        }

        private void Files_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateSuggestions();
            // refresh view when collection content changes
            _filesView?.Refresh();
        }

        private ICollectionView? _filesView;

        private void UpdateSuggestions()
        {
            try
            {
                if (Files == null)
                {
                    SearchTextBox.OriginalItemsSource = new List<string>();
                }
                else
                {
                    var names = Files.Select(f => f.Name).ToList();
                    SearchTextBox.OriginalItemsSource = names;
                }
            }
            catch
            {
                // ignore if SearchTextBox not ready
            }
        }

        private void SetupFilesView()
        {
            if (Files == null)
            {
                FilesListView.ItemsSource = null;
                _filesView = null;
                return;
            }

            _filesView = CollectionViewSource.GetDefaultView(Files);
            FilesListView.ItemsSource = _filesView;
            // no filter initially
            _filesView.Filter = null;
        }

        private void OnSearchTextChanged()
        {
            try
            {
                var text = SearchTextBox.Text?.Trim() ?? string.Empty;
                if (_filesView == null)
                {
                    SetupFilesView();
                }

                if (string.IsNullOrEmpty(text))
                {
                    if (_filesView != null)
                    {
                        _filesView.Filter = null;
                        _filesView.Refresh();
                    }
                    return;
                }

                if (_filesView != null)
                {
                    _filesView.Filter = o =>
                    {
                        if (o is FileItem fi)
                        {
                            return fi.Name?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        return false;
                    };
                    _filesView.Refresh();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Select the next file in the list. If nothing selected, select the first item.
        /// Does nothing if the list is empty or already at the end.
        /// </summary>
        public void SelectNext()
        {
            if (Files == null || Files.Count == 0) return;

            if (SelectedIndex < 0)
            {
                SelectedIndex = 0;
                return;
            }

            if (SelectedIndex < Files.Count - 1)
            {
                SelectedIndex++;
            }
        }

        /// <summary>
        /// Select the prior file in the list. If nothing selected, select the last item.
        /// Does nothing if the list is empty or already at the beginning.
        /// </summary>
        public void SelectPrior()
        {
            if (Files == null || Files.Count == 0) return;

            if (SelectedIndex < 0)
            {
                SelectedIndex = Files.Count - 1;
                return;
            }

            if (SelectedIndex > 0)
            {
                SelectedIndex--;
            }
        }

        private void DeleteItemButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement fe && fe.DataContext is FileItem fi && Files != null)
                {
                    Files.Remove(fi);
                }
            }
            catch { }
        }

        public void DeleteSelected()
        {
            if (this.SelectedItem != null)
            {
                Files?.Remove(this.SelectedItem);
            }
        }

        private void AddFileButton_Click(object? sender, RoutedEventArgs e)
        {
            AddFiles();
        }

        public void AddFiles()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };

            var ok = dlg.ShowDialog();
            if (ok == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    try { AddFile(file); }
                    catch { /* ignore single file errors */ }
                }
            }
        }

        private void AddFolderButton_Click(object? sender, RoutedEventArgs e)
        {
            AddFolder();
        }

        public void AddFolder()
        {
            using var fbd = new System.Windows.Forms.FolderBrowserDialog();
            var res = fbd.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK || res == System.Windows.Forms.DialogResult.Yes)
            {
                var dir = fbd.SelectedPath;
                try
                {
                    var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };
                    var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                        .Where(f => exts.Contains(Path.GetExtension(f)));
                    foreach (var file in files)
                    {
                        try { AddFile(file); }
                        catch { }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Add a file to the Files collection. Name is derived from the path and DetectionCount is initialized to 0.
        /// </summary>
        public void AddFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (Files == null) Files = new ObservableCollection<FileItem>();

            var name = System.IO.Path.GetFileName(path);
            var item = new FileItem(name ?? path, path);
            Files.Add(item);
        }
    }
}
