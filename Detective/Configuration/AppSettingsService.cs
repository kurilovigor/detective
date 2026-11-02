using System.IO;

namespace Detective.Configuration
{
    public static class AppSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Detective",
            "settings.json");

        private static AppSettings? _settings;

        public static AppSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = AppSettings.Load(SettingsPath);
                }
                return _settings;
            }
        }

        public static void Save()
        {
            if (_settings == null) return;

            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            _settings.Save(SettingsPath);
        }
    }
}
