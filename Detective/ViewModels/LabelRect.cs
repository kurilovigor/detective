using System.Windows;

namespace Detective.ViewModels
{
    public class LabelRect
    {
        public Guid Id { get; }
        public Rect Rect { get; }
        public string Label { get; }

        public LabelRect(Guid id, Rect rect, string label)
        {
            Id = id;
            Rect = rect;
            Label = label;
        }
    }
}
