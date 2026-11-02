using System.IO;
using System.Text.Json;

namespace Detective.Configuration
{
    public class AppSettings
    {
        public double MainWindowWidth { get; set; } = 800;
        public double MainWindowHeight { get; set; } = 450;
        public WindowState MainWindowWindowState { get; set; } = WindowState.Normal;
        public double LabelEditSplitterPosition { get; set; } = 250;
        public List<string> RecentFiles { get; set; } = new();
        public StartPageEnum StartPage { get; set; } = StartPageEnum.Home;

        public void AddRecentFile(string path)
        {
            // Remove if already present
            RecentFiles.Remove(path);
            // Add to front
            RecentFiles.Insert(0, path);
            // Keep only last 5
            while (RecentFiles.Count > 5)
                RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }

        public static AppSettings Load(string path)
        {
            if (!File.Exists(path))
                return new AppSettings();

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(string path)
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }

    public enum WindowState
    {
        Normal,
        Maximized,
        Minimized
    }

    public enum StartPageEnum
    {
        Home,
        Last
    }
}
