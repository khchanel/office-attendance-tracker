using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Service
{

    public class AttendanceService : IAttendanceService
    {
        private readonly ILogger<AttendanceService> _logger;
        private readonly List<IPNetwork> _networks;
        private readonly Guid _instanceId;
        private readonly INetworkInfoProvider _networkInfoProvider;
        private readonly IAttendanceRecordStore _attendanceRecordStore;


        public AttendanceService(ILogger<AttendanceService> logger,
            IConfiguration config,
            INetworkInfoProvider networkInfoProvider,
            IAttendanceRecordStore attendanceRecordStore)
        {
            _logger = logger;
            _instanceId = Guid.NewGuid();
            _networks = [];
            _networkInfoProvider = networkInfoProvider ?? new DefaultNetworkInfoProvider();
            _attendanceRecordStore = attendanceRecordStore;

            var networkConfig = config.GetSection("Networks").Get<List<string>>();
            if (networkConfig == null)
                throw new ArgumentException("Missing config named 'Networks' storing list of CIDR in appsettings");

            foreach (var cidr in networkConfig)
            {
                var ipNetwork = IPNetwork.Parse(cidr);
                _networks.Add(ipNetwork);
            }

        }

        public bool CheckAttendance()
        {
            _logger.LogInformation("instance id: {Instance}", _instanceId);

            bool isHostResolveToOffice = CheckUsingHostName();
            bool isNicAddressInOffice = CheckUsingNicIp();


            return isHostResolveToOffice || isNicAddressInOffice;

        }

        public int GetCurrentMonthAttendance()
        {
            var currentMonthRecords = _attendanceRecordStore.GetMonth(DateTime.Today);
            return currentMonthRecords.Count(r => r.IsOffice);
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
