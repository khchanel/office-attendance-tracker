using System.Text.Json;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Manages application settings persistence
    /// </summary>
    public class SettingsManager
    {
        public const string DefaultSettingsFileName = "user-settings.json";
        private readonly string _settingsFilePath;
        private AppSettings _currentSettings;
        private readonly object _lock = new object();
        private readonly bool _isFirstRun;

        public event EventHandler<AppSettings>? SettingsChanged;

        public SettingsManager(string? settingsFilePath = null)
        {
            _settingsFilePath = settingsFilePath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                DefaultSettingsFileName);

            _isFirstRun = !File.Exists(_settingsFilePath);
            _currentSettings = LoadSettings();
            
            // Save default settings on first run
            if (_isFirstRun)
            {
                SaveSettings(_currentSettings);
            }
        }

        public bool IsFirstRun => _isFirstRun;

        public AppSettings CurrentSettings
        {
            get
            {
                lock (_lock)
                {
                    return _currentSettings.Clone();
                }
            }
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            lock (_lock)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);

                _currentSettings = settings.Clone();
                SettingsChanged?.Invoke(this, _currentSettings.Clone());
            }
        }

        public void ReloadSettings()
        {
            lock (_lock)
            {
                _currentSettings = LoadSettings();
                SettingsChanged?.Invoke(this, _currentSettings.Clone());
            }
        }
    }
}
