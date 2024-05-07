using OfficeAttendanceTracker.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Office Attendance Tracker";
});
builder.Services.AddTransient<IAttendanceService, AttendanceService>();
builder.Services.AddTransient<IAttendanceRecordStore, AttendanceRecordFileStore>();

var host = builder.Build();
host.Run();
