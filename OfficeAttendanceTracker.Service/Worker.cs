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
            _pollIntervalMs = config.GetValue("PollIntervalMs", 10000);
            _attendanceService = attendanceService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Polling interval is: {Interval}ms", _pollIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {

                _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

                _attendanceService.TakeAttendance();

                await Task.Delay(_pollIntervalMs, stoppingToken);
            }
        }
    }
}
