using System.ComponentModel;
using Wpf.Ui.Controls;

namespace Detective.ViewModels
{
    public class FileItem : INotifyPropertyChanged
    {
        public FileItem(string name, string path)
        {
            Name = name;
            Path = path;
        }
        public FileItem(int id, string name, string path, IEnumerable<LabelRect>? detections)
        {
            Id = id;
            Name = name;
            Path = path;
            if (detections != null)
            {
                Detections.AddRange(detections);
            }
        }

        public List<LabelRect> Detections { get; } = new List<LabelRect>();

        public int Id { get; set; }
        public string Name { get; }
        public string Path { get; }
        public int DetectionCount => Detections.Count;
        public InfoBadgeSeverity DetectionCountSeverity =>
            Detections.Count > 0 ? InfoBadgeSeverity.Success : InfoBadgeSeverity.Informational;
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void DoDetectionsChanged() 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetectionCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetectionCountSeverity)));
        }
    }
}
