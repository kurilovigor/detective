using Detective.Configuration;
using Detective.Database;
using Detective.Database.Entities;
using Detective.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Detective.Views.Pages
{
        /// <summary>
        /// Interaction logic for LabelEditPage.xaml
        /// </summary>
    public partial class LabelEditPage : Page
    {
        private readonly DetectionDatabase detectionDatabase;
        public DetectionDatabase DetectionDatabase => detectionDatabase;
        private FileItem? currentFileItem;

        public event EventHandler<CurrentFileItemChangedEventArgs>? CurrentFileItemChanged;

        public FileItem? CurrentFileItem
        {
            get => currentFileItem;
            set
            {
                if (currentFileItem != value)
                {
                    currentFileItem = value;
                    CurrentFileItemChanged?.Invoke(this, new CurrentFileItemChangedEventArgs(value));
                }
            }
        }

        public LabelEditPage(DetectionDatabase detectionDatabase) : base()
        {
            InitializeComponent();
            this.detectionDatabase = detectionDatabase;
            Loaded += LabelEditPage_Loaded;
            Unloaded += LabelEditPage_Unloaded;
        }


        private void LabelsHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            var collapsed = LabelsIcon.Symbol == Wpf.Ui.Controls.SymbolRegular.ChevronRight20;
            LabelsIcon.Symbol = collapsed ?
                Wpf.Ui.Controls.SymbolRegular.ChevronDown20 :
                Wpf.Ui.Controls.SymbolRegular.ChevronRight20;
            Labels.Visibility = collapsed ?
                Visibility.Visible :
                Visibility.Collapsed;
        }

        private void FilesHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            var collapsed = FilesIcon.Symbol == Wpf.Ui.Controls.SymbolRegular.ChevronRight20;
            FilesIcon.Symbol = collapsed ?
                Wpf.Ui.Controls.SymbolRegular.ChevronDown20 :
                Wpf.Ui.Controls.SymbolRegular.ChevronRight20;
            Files.Visibility = collapsed ?
                Visibility.Visible :
                Visibility.Collapsed;
        }

        private void LabelEditPage_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveCurrentFileItem();
            SaveSplitterPosition();
        }

        private void RestoreSplitterPosition()
        {
            var settings = AppSettingsService.Settings;
            if (settings.LabelEditSplitterPosition > 0)
            {
                SidebarColumn.Width = new GridLength(settings.LabelEditSplitterPosition);
            }
        }

        private void SaveSplitterPosition()
        {
            AppSettingsService.Settings.LabelEditSplitterPosition = SidebarColumn.ActualWidth;
        }

        private void LabelEditPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.Editor.SelectedChanged += Editor_SelectedChanged;
            this.Labels.SelectedChanged += Labels_SelectedChanged;
            this.Files.SelectedChanged += Files_SelectedChanged;

            this.detectionDatabase.Labels
                .Query()
                .Select(x => x.Name)
                .ToList()
                .ForEach(x => this.Labels.Labels.Add(x!));

            this.detectionDatabase.Files
                .Query()
                .ToEnumerable()
                .Select(x => new FileItem(
                    x.Id,
                    System.IO.Path.GetFileName(x.Path ?? string.Empty),
                    x.Path ?? string.Empty,
                    x.Detections?.Select(x => 
                        new LabelRect(x.Id, new Rect(x.X, x.Y, x.Width, x.Height), x.Label ?? string.Empty))))
                .ToList()
                .ForEach(file => this.Files.Files!.Add(file));

            this.Labels.Labels.CollectionChanged += Labels_CollectionChanged;
            this.Files.Files!.CollectionChanged += Files_CollectionChanged;
            this.PreviewMouseWheel += Editor_PreviewMouseWheel;

            // Run handler once.
            Loaded -= LabelEditPage_Loaded;

            RestoreSplitterPosition();
        }

        private void Editor_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                this.Files.SelectPrior();
            }
            else if (e.Delta < 0)
            {
                this.Files.SelectNext();
            }
        }

        private void Files_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach (FileItem newItem in e.NewItems!)
                    {
                        var entity = new FileEntity
                        {
                            Path = newItem.Path,
                        };
                        this.detectionDatabase.Files.Insert(entity);
                        newItem.Id = entity.Id;
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    foreach (FileItem oldItem in e.OldItems!)
                    {
                        var entity = this.detectionDatabase.Files.FindById(oldItem.Id);
                        if (entity != null)
                        {
                            this.detectionDatabase.Files.Delete(oldItem.Id);
                        }
                    }
                    break;
            }
        }

        private void Labels_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach (string newItem in e.NewItems!)
                    {
                        var entity = new LabelEntity
                        {
                            Name = newItem,
                        };

                        this.detectionDatabase.Labels.Insert(entity);
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    foreach (string oldItem in e.OldItems!)
                    {
                        var entities = this.detectionDatabase.Labels.Find(x => x.Name == oldItem);
                        if (entities != null)
                        {
                            foreach (var entity in entities)
                            {
                                this.detectionDatabase.Labels.Delete(entity.Id);
                            }
                        }
                    }
                    break;
            }
        }

        private void Files_SelectedChanged(object? sender, int e)
        {
            if (e < 0)
            {
                EditFile(null);
            }
            else
            {
                var fileItem = this.Files.Files?[e];
                if (fileItem == null)
                {
                    EditFile(null);
                }
                else
                {
                    EditFile(fileItem);
                }
            }
        }

        public void SaveCurrentFileItem()
        {
            if (CurrentFileItem != null)
            {
                CurrentFileItem.Detections.Clear();
                CurrentFileItem.Detections.AddRange(this.Editor.Rectangles.ToList());
                CurrentFileItem.DoDetectionsChanged();

                var currentFile = detectionDatabase.Files.FindById(CurrentFileItem.Id);
                if (currentFile != null)
                {
                    if (currentFile.Detections == null)
                    {
                        currentFile.Detections = new List<Database.Entities.DetectionEntity>();
                    }
                    currentFile.Detections = this.Editor.Rectangles.Select(x => new DetectionEntity
                        {
                            Id = x.Id,
                            X = (int)x.Rect.X,
                            Y = (int)x.Rect.Y,
                            Width = (int)x.Rect.Width,
                            Height = (int)x.Rect.Height,
                            Label = x.Label,
                        })
                        .ToList();

                    this.detectionDatabase.Files.Update(currentFile);
                }
            }
        }

        private void EditFile(FileItem? fileItem)
        {
            if (CurrentFileItem != null)
            {
                SaveCurrentFileItem();
                CurrentFileItem = null;
            }

            this.Editor.Rectangles.Clear();
            this.Editor.Selected = null;

            if (fileItem == null)
            {
                this.Editor.Image = null;
            }
            else
            {
                this.Editor.Image = new BitmapImage(new Uri(fileItem.Path));
                fileItem.Detections.ForEach(detection => this.Editor.Rectangles.Add(detection));
                CurrentFileItem = fileItem;
            }
        }

        private void Labels_SelectedChanged(object? sender, int e)
        {
            if (e < 0)
            {
                this.Editor.DefaultLabel = string.Empty;
            }
            else
            {
                this.Editor.DefaultLabel = this.Labels.Labels[e];
            }
        }

        private void Editor_SelectedChanged(object? sender, LabelRect? e)
        {
            this.SelectedRectInfo.Text = e == null ?
                string.Empty :
                $"X:{e.Rect.X} | Y:{e.Rect.Y} | W:{e.Rect.Width} | H:{e.Rect.Height} | Label:{e.Label}";
        }
    }
}
