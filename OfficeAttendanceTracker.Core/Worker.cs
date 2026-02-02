using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OfficeAttendanceTracker.Core
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly Func<IAttendanceService> _serviceProvider;
        private readonly object _lock = new object();
        
        private int _pollIntervalMs;
        private CancellationTokenSource? _restartCts;

        /// <summary>
        /// Gets the current AttendanceService instance (may be recreated on settings change)
        /// </summary>
        private IAttendanceService AttendanceService => _serviceProvider();

        public Worker(ILogger<Worker> logger, IConfiguration config, Func<IAttendanceService> serviceProvider)
        {
            _logger = logger;
            _pollIntervalMs = config.GetValue("PollIntervalMs", 10000);
            _serviceProvider = serviceProvider;
        }

        public void UpdateInterval(int newIntervalMs)
        {
            lock (_lock)
            {
                if (newIntervalMs != _pollIntervalMs)
                {
                    _logger.LogInformation("Updating Worker poll interval from {Old}ms to {New}ms", 
                        _pollIntervalMs, newIntervalMs);
                    
                    _pollIntervalMs = newIntervalMs;
                    _restartCts?.Cancel();
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int currentInterval;
            lock (_lock)
            {
                currentInterval = _pollIntervalMs;
            }

            if (_logger.IsEnabled(LogLevel.Information)) 
                _logger.LogInformation("Worker started. Polling interval is: {Interval}ms", currentInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

                    AttendanceService.TakeAttendance();

                    lock (_lock)
                    {
                        currentInterval = _pollIntervalMs;
                        _restartCts = new CancellationTokenSource();
                    }

                    try
                    {
                        CancellationTokenSource localCts;
                        lock (_lock)
                        {
                            localCts = _restartCts;
                        }

                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            stoppingToken, localCts.Token);
                        
                        await Task.Delay(currentInterval, linkedCts.Token);
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Worker delay cancelled for interval change");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error in Worker execution");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("Worker stopped");
        }

        public override void Dispose()
        {
            lock (_lock)
            {
                _restartCts?.Dispose();
            }
            base.Dispose();
        }
    }
}
