using System.IO;
using System.Text.Json;
using ICOforge.Models;
using ICOforge.Utilities;

namespace ICOforge.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        public SettingsService()
        {
            _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ICOforge", "settings.json");
            _settings = LoadSettings();
        }

        private AppSettings LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var jsonString = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                    if (settings != null)
                    {
                        // Set a default if the loaded path is invalid/empty, using NativeMethods
                        if (string.IsNullOrEmpty(settings.LastOutputDirectory) || !Directory.Exists(settings.LastOutputDirectory))
                        {
                            settings.LastOutputDirectory = NativeMethods.GetDownloadsPath();
                        }
                        return settings;
                    }
                }
                catch (Exception ex)
                {
                    // Log or handle error loading settings
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                }
            }

            // Default settings if file does not exist or loading failed
            return new AppSettings { LastOutputDirectory = NativeMethods.GetDownloadsPath() };
        }

        public void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                var jsonString = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, jsonString);
            }
            catch (Exception ex)
            {
                // Log or handle error saving settings
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}