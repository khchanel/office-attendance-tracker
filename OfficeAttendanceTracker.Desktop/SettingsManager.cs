using System.IO;
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

        /// <summary>Resolves the Desktop attendance folder, using the user profile for a blank path.</summary>
        internal static string ResolveDataFolder(string? path) =>
            string.IsNullOrWhiteSpace(path) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : path;

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

        /// <summary>Persists settings and applies them to the running desktop services.</summary>
        public void SaveSettings(AppSettings settings)
        {
            lock (_lock)
            {
                var previous = _currentSettings;
                WriteSettings(settings);
                _currentSettings = settings.Clone();
                try
                {
                    SettingsChanged?.Invoke(this, _currentSettings.Clone());
                }
                catch
                {
                    // A replacement attendance store can reject an unreadable or unwritable file.
                    _currentSettings = previous;
                    WriteSettings(previous);
                    throw;
                }
            }
        }

        internal void ApplySettings(AppSettings settings)
        {
            using var key = Registry.CurrentUser.OpenSubKey(runRegistryKey);
            var previousCommand = key?.GetValue(applicationName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            var previousKind = previousCommand is null ? RegistryValueKind.String : key!.GetValueKind(applicationName);
            var command = settings.StartWithWindows ? GetStartupCommand() : null;
            var updateRegistration = !Equals(previousCommand, command) || previousKind != RegistryValueKind.String;
            if (updateRegistration)
                WriteStartupCommand(command, RegistryValueKind.String);

            try
            {
                SaveSettings(settings);
            }
            catch
            {
                if (updateRegistration)
                    WriteStartupCommand(previousCommand, previousKind);
                throw;
            }
        }

        private void WriteSettings(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = $"{_settingsFilePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _settingsFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
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
        public void SetStartupEnabled(bool enabled) =>
            WriteStartupCommand(enabled ? GetStartupCommand() : null, RegistryValueKind.String);

        private static string GetStartupCommand()
        {
            var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable.");
            return $"\"{executablePath}\"";
        }

        private static void WriteStartupCommand(object? command, RegistryValueKind kind)
        {
            try
            {
                if (command is null)
                {
                    using var key = Registry.CurrentUser.OpenSubKey(runRegistryKey, writable: true);
                    key?.DeleteValue(applicationName, throwOnMissingValue: false);
                }
                else
                {
                    using var key = Registry.CurrentUser.CreateSubKey(runRegistryKey);
                    key.SetValue(applicationName, command, kind);
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
