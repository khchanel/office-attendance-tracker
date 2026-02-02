using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IAttendanceService _attendanceService;
        private readonly int _pollIntervalMs;

        public Worker(ILogger<Worker> logger, IConfiguration config, IAttendanceService attendanceService)
        {
            _logger = logger;
            _pollIntervalMs = config.GetValue("PollIntervalMs", 1800000);
            _attendanceService = attendanceService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Worker started. Polling interval is: {Interval}ms", _pollIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

                    _attendanceService.TakeAttendance();

                    await Task.Delay(_pollIntervalMs, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error in Worker execution");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("Worker stopped");
        }
    }
}
