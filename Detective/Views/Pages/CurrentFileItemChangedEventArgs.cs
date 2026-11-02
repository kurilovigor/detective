using Detective.ViewModels;

namespace Detective.Views.Pages
{
    public class CurrentFileItemChangedEventArgs : EventArgs
    {
        public FileItem? FileItem { get; }

        public CurrentFileItemChangedEventArgs(FileItem? fileItem)
        {
            FileItem = fileItem;
        }
    }
}
