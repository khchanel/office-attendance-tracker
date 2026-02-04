using System.Text.Json;
using Microsoft.Win32;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Manages application settings persistence
    /// </summary>
    public class SettingsManager
    {
        private const string DefaultSettingsFileName = "user-settings.json";
        private const string runRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string applicationName = TrayApplicationContext.AppName;

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
                    return settings ?? AppSettings.CreateDesktopDefaults();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return AppSettings.CreateDesktopDefaults();
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

        /// <summary>
        /// Enables or disables startup with Windows
        /// </summary>
        public void SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(runRegistryKey, true);
                
                if (key == null)
                    return;

                if (enabled)
                {
                    var executablePath = System.Windows.Forms.Application.ExecutablePath;
                    key.SetValue(applicationName, $"\"{executablePath}\"");
                }
                else
                {
                    if (key.GetValue(applicationName) != null)
                    {
                        key.DeleteValue(applicationName);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to update startup settings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if the application is configured to start with Windows
        /// </summary>
        public bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(runRegistryKey, false);
                
                if (key == null)
                    return false;

                var value = key.GetValue(applicationName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                return false;
            }
        }
    }
}
