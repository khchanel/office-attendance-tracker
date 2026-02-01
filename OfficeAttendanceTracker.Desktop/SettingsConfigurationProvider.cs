using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Configuration provider that wraps SettingsManager for compatibility with IConfiguration
    /// </summary>
    public class SettingsConfigurationProvider : IConfigurationProvider, IDisposable
    {
        private readonly SettingsManager _settingsManager;
        private ConfigurationReloadToken _reloadToken = new ConfigurationReloadToken();

        public SettingsConfigurationProvider(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _settingsManager.SettingsChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(object? sender, AppSettings settings)
        {
            var previousToken = Interlocked.Exchange(ref _reloadToken, new ConfigurationReloadToken());
            previousToken.OnReload();
        }

        public bool TryGet(string key, out string? value)
        {
            var settings = _settingsManager.CurrentSettings;

            value = key switch
            {
                "Networks" => null, // Array handled by GetChildKeys
                "PollIntervalMs" => settings.PollIntervalMs.ToString(),
                "EnableBackgroundWorker" => settings.EnableBackgroundWorker.ToString(),
                "ComplianceThreshold" => settings.ComplianceThreshold.ToString(),
                "DataFilePath" => settings.DataFilePath ?? string.Empty,
                "DataFileName" => settings.DataFileName,
                _ when key.StartsWith("Networks:") => GetNetworkValue(key, settings),
                _ => null
            };

            return value != null;
        }

        private string? GetNetworkValue(string key, AppSettings settings)
        {
            var parts = key.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int index))
            {
                if (index >= 0 && index < settings.Networks.Count)
                {
                    return settings.Networks[index];
                }
            }
            return null;
        }

        public void Set(string key, string? value)
        {
            // Settings are managed through SettingsManager, not directly
            throw new NotSupportedException("Settings should be modified through SettingsManager");
        }

        public IChangeToken GetReloadToken()
        {
            return _reloadToken;
        }

        public void Load()
        {
            // Settings are loaded by SettingsManager
        }

        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
        {
            var settings = _settingsManager.CurrentSettings;
            var keys = new List<string>();

            if (string.IsNullOrEmpty(parentPath))
            {
                keys.AddRange(new[] { "Networks", "PollIntervalMs", "EnableBackgroundWorker", "ComplianceThreshold", "DataFilePath", "DataFileName" });
            }
            else if (parentPath == "Networks")
            {
                keys.AddRange(Enumerable.Range(0, settings.Networks.Count).Select(i => i.ToString()));
            }

            return keys.Concat(earlierKeys).Distinct();
        }

        public void Dispose()
        {
            _settingsManager.SettingsChanged -= OnSettingsChanged;
        }
    }

    /// <summary>
    /// Configuration source for SettingsManager
    /// </summary>
    public class SettingsConfigurationSource : IConfigurationSource
    {
        private readonly SettingsManager _settingsManager;

        public SettingsConfigurationSource(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new SettingsConfigurationProvider(_settingsManager);
        }
    }
}
