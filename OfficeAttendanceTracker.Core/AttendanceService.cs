using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Core
{

    public class AttendanceService : IAttendanceService
    {
        private readonly ILogger<AttendanceService> _logger;
        private readonly List<IPNetwork> _networks;
        private readonly Guid _instanceId;
        private readonly INetworkInfoProvider _networkInfoProvider;
        private readonly IAttendanceRecordStore _attendanceRecordStore;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly double _complianceThreshold;


        public AttendanceService(ILogger<AttendanceService> logger,
            IConfiguration config,
            INetworkInfoProvider networkInfoProvider,
            IAttendanceRecordStore attendanceRecordStore,
            IDateTimeProvider? dateTimeProvider = null)
        {
            _logger = logger;
            _instanceId = Guid.NewGuid();
            _networks = [];
            _networkInfoProvider = networkInfoProvider ?? new DefaultNetworkInfoProvider();
            _attendanceRecordStore = attendanceRecordStore;
            _dateTimeProvider = dateTimeProvider ?? new DefaultDateTimeProvider();
            _complianceThreshold = config.GetValue("ComplianceThreshold", 0.5); // Default 50%

            var networkConfig = config.GetSection("Networks").Get<List<string>>();
            if (networkConfig == null)
                throw new ArgumentException("Missing config named 'Networks' storing list of CIDR in appsettings");

            foreach (var cidr in networkConfig)
            {
                var ipNetwork = IPNetwork.Parse(cidr);
                _networks.Add(ipNetwork);
            }

        }

        private bool CheckAttendance()
        {
            _logger.LogInformation("instance id: {Instance}", _instanceId);

            bool isHostResolveToOffice = CheckUsingHostName();
            bool isNicAddressInOffice = CheckUsingNicIp();


            return isHostResolveToOffice || isNicAddressInOffice;

        }

        public void Reload()
        {
            _attendanceRecordStore.Reload();
        }

        public int GetCurrentMonthAttendance()
        {
            var currentMonthRecords = _attendanceRecordStore.GetMonth(_dateTimeProvider.Today);
            return currentMonthRecords.Count(r => r.IsOffice);
        }

        public int GetBusinessDaysInCurrentMonth()
        {
            var today = _dateTimeProvider.Today;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastDayOfMonth = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            int businessDays = 0;
            for (var date = firstDayOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
            {
                if (date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday)
                {
                    businessDays++;
                }
            }

            return businessDays;
        }

        public int GetBusinessDaysUpToToday()
        {
            var today = _dateTimeProvider.Today;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            int businessDays = 0;
            for (var date = firstDayOfMonth; date <= today; date = date.AddDays(1))
            {
                if (date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday)
                {
                    businessDays++;
                }
            }

            return businessDays;
        }

        public ComplianceStatus GetComplianceStatus()
        {
            var attendance = GetCurrentMonthAttendance();
            var businessDaysUpToToday = GetBusinessDaysUpToToday();
            var totalBusinessDaysInMonth = GetBusinessDaysInCurrentMonth();
            var remainingBusinessDays = totalBusinessDaysInMonth - businessDaysUpToToday;
            
            var requiredForEntireMonth = (int)Math.Ceiling(totalBusinessDaysInMonth * _complianceThreshold);
            var requiredForRolling = (int)Math.Ceiling(businessDaysUpToToday * _complianceThreshold);
            var maxPossibleAttendance = attendance + remainingBusinessDays;

            if (attendance >= requiredForEntireMonth)
                return ComplianceStatus.Secured;
            else if (attendance >= requiredForRolling)
                return ComplianceStatus.Compliant;
            else if (maxPossibleAttendance < requiredForEntireMonth)
                return ComplianceStatus.Critical;
            else
                return ComplianceStatus.Warning;
        }


        public bool TakeAttendance()
        {
            var attendance = _attendanceRecordStore.GetToday();
            if (attendance == null)
            {
                _logger.LogInformation("saving first record for the day");
                attendance = _attendanceRecordStore.Add(false, _dateTimeProvider.Today);
            }

            var isAtOfficeNow = CheckAttendance();
            if (isAtOfficeNow)
            {
                _logger.LogInformation("Detected in office");

                if (!attendance.IsOffice)
                {
                    _logger.LogInformation("updating office attendance for today");
                    _attendanceRecordStore.Update(true, _dateTimeProvider.Today);
                }
            }
            else
            {
                _logger.LogInformation("Not detected in office now");
            }
            
            // Automatically save changes to persist immediately
            // This ensures data is saved right after taking attendance
            _attendanceRecordStore.SaveChanges();
            
            return isAtOfficeNow;
        }


        private bool CheckUsingHostName()
        {
            var hostname = _networkInfoProvider.GetHostName();
            _logger.LogDebug("Hostname: {Host}", hostname);

            var ipArr = _networkInfoProvider.GetHostAddresses(hostname);

            for (int i = 0; i < ipArr.Length; i++)
            {
                _logger.LogDebug("Hostname resolved IP Address {0}: {1} ", i.ToString(), ipArr[i].ToString());


                if (_networks.Any(n => n.Contains(ipArr[i])))
                {
                    return true;
                }
            }
            return false;
        }


        private bool CheckUsingNicIp()
        {
            var networkInterfaces = _networkInfoProvider.GetAllNetworkInterfaces();

            foreach (var networkInterface in networkInterfaces)
            {
                // Filter out loopback and disconnected interfaces
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    _logger.LogDebug("Interface: {Name}", networkInterface.Name);


                    foreach (var ipAddress in ipProperties.UnicastAddresses)
                    {
                        _logger.LogDebug("Interface: {Name} IP Address: {Address}", networkInterface.Name, ipAddress.Address);


                        foreach (var n in _networks)
                        {
                            if (n.Contains(ipAddress.Address))
                            {
                                return true;
                            }
                        }

                    }

                }
            }


            return false;
        }

    }
}
