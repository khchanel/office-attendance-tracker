using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Manages IAttendanceService instance lifecycle based on configuration changes
    /// </summary>
    public class AttendanceServiceProvider : IAttendanceServiceProvider, IDisposable
    {
        private readonly ILogger<AttendanceService> _logger;
        private readonly INetworkInfoProvider _networkInfoProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly SettingsManager _settingsManager;
        private readonly object _lock = new object();
        
        private IAttendanceService _currentService;
        private IAttendanceRecordStore? _currentStore;

        public event EventHandler<IAttendanceService>? ServiceRecreated;

        public AttendanceServiceProvider(
            ILogger<AttendanceService> logger,
            INetworkInfoProvider networkInfoProvider,
            IDateTimeProvider dateTimeProvider,
            SettingsManager settingsManager)
        {
            _logger = logger;
            _networkInfoProvider = networkInfoProvider;
            _dateTimeProvider = dateTimeProvider;
            _settingsManager = settingsManager;

            _currentService = CreateService(_settingsManager.CurrentSettings);
            _settingsManager.SettingsChanged += OnSettingsChanged;
        }

        public IAttendanceService Current
        {
            get
            {
                lock (_lock)
                {
                    return _currentService;
                }
            }
        }

        private void OnSettingsChanged(object? sender, AppSettings settings)
        {
            lock (_lock)
            {
                _logger.LogInformation("Recreating AttendanceService with new configuration");

                var oldStore = _currentStore;
                
                try
                {
                    var newService = CreateService(settings);
                    _currentService = newService;
                    
                    ServiceRecreated?.Invoke(this, newService);
                    
                    _logger.LogInformation("AttendanceService recreated successfully");
                }
                finally
                {
                    // Dispose old store after transition
                    oldStore?.Dispose();
                }
            }
        }

        private IAttendanceService CreateService(AppSettings settings)
        {
            var config = CreateConfiguration(settings);
            var store = CreateStore(settings.DataFileName, config);
            store.Initialize();
            _currentStore = store;

            return new AttendanceService(_logger, config, _networkInfoProvider, store, _dateTimeProvider);
        }

        private static IAttendanceRecordStore CreateStore(string fileName, IConfiguration config)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".csv" => new AttendanceRecordCsvFileStore(config),
                ".json" => new AttendanceRecordJsonFileStore(config),
                _ => throw new NotSupportedException($"Unsupported file extension '{extension}'. Supported: .csv, .json")
            };
        }

        private static IConfiguration CreateConfiguration(AppSettings settings)
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Networks:0"] = settings.Networks.Count > 0 ? settings.Networks[0] : null,
                ["PollIntervalMs"] = settings.PollIntervalMs.ToString(),
                ["ComplianceThreshold"] = settings.ComplianceThreshold.ToString(),
                ["OfficeTimeTrackingMode"] = settings.OfficeTimeTrackingMode.ToString(),
                ["MinimumOfficeHours"] = settings.MinimumOfficeHours.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["DataFilePath"] = SettingsManager.ResolveDataFolder(settings.DataFilePath),
                ["DataFileName"] = settings.DataFileName
            };

            for (int i = 1; i < settings.Networks.Count; i++)
            {
                configDict[$"Networks:{i}"] = settings.Networks[i];
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configDict)
                .Build();
        }

        public void Dispose()
        {
            _settingsManager.SettingsChanged -= OnSettingsChanged;
            
            lock (_lock)
            {
                _currentStore?.Dispose();
            }
        }
    }
}
