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
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
                    CheckAttendance();
                }
                await Task.Delay(_pollIntervalMs, stoppingToken);
            }
        }

        private void CheckAttendance()
        {
            // if new day, set as not office to begin



            // check if currently in office
            // if we are, then set the day as office day
            // dont need to set non-office day since we assume everyday start as wfh
            var isAtOfficeNow = _attendanceService.CheckAttendance();
            if (isAtOfficeNow)
            {
                // set today as in office
                _logger.LogInformation("Today is office day!");

                // if data store not already updated

            }
            else
            {
                _logger.LogInformation("Not deteced at office now");
            }

        }
    }
}
