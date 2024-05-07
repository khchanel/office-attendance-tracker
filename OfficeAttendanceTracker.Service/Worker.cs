namespace OfficeAttendanceTracker.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IAttendanceService _attendanceService;
        private readonly IAttendanceRecordStore _attendanceRecordStore;
        private readonly int _pollIntervalMs;

        public Worker(ILogger<Worker> logger, IConfiguration config, IAttendanceService attendanceService, IAttendanceRecordStore attendanceRecordStore)
        {
            _logger = logger;
            _pollIntervalMs = config.GetValue("PollIntervalMs", 10000);
            _attendanceService = attendanceService;
            _attendanceRecordStore = attendanceRecordStore;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Polling interval is: {Interval}ms", _pollIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {

                _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

                TakeAttendance();

                await Task.Delay(_pollIntervalMs, stoppingToken);
            }
        }

        private void TakeAttendance()
        {
            var attendance = _attendanceRecordStore.GetToday();
            if (attendance == null)
            {
                _logger.LogInformation("saving first record for the day");
                attendance = _attendanceRecordStore.Add(false, DateTime.Today);
            }

            var isAtOfficeNow = _attendanceService.CheckAttendance();
            if (isAtOfficeNow)
            {
                _logger.LogInformation("Detected in office");

                if (!attendance.IsOffice)
                {
                    _logger.LogInformation("updating office attendance for today");
                    _attendanceRecordStore.Update(true, DateTime.Today);
                }
            }
            else
            {
                _logger.LogInformation("Not detected in office now");
            }

        }
    }
}
