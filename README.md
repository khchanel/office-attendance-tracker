# Office Attendance Tracker

[![build](https://github.com/khchanel/office-attendance-tracker/actions/workflows/dotnet.yml/badge.svg)](https://github.com/khchanel/office-attendance-tracker/actions/workflows/dotnet.yml)
[![build](https://github.com/khchanel/office-attendance-tracker/actions/workflows/go-attendance.yml/badge.svg)](https://github.com/khchanel/office-attendance-tracker/actions/workflows/go-attendance.yml)

This is a daemon service which runs on your computer in the background to keep track of your attendance

Current implementation relies on checking whether OS network interfaces are in office subnet addresses that are defined in appsettings


# Build and Run Instructions

## Prerequisites
* .NET 8 SDK installed
* Windows OS (for Windows Service deployment)
* Administrator privileges (for service installation)
---

## Build and publish release binary
```
dotnet build
dotnet publish -c Release -o ./publish
```

## Configure the Service
Edit appsettings.json to configure settings as needed

'Networks' should be set to your office network address which is used to detect office presence

example:
```
  "Networks": ["10.8.1.0/24", "10.1.0.0/16"],
  "PollIntervalMs": 1800000,
  "DataFilePath":  "D:\\attendance",
  "DataFileName" :  "attendance.csv"
```

## Run as Console App
```
dotnet run
```

## Install the Windows Service

```
sc create "OfficeAttendanceTracker" binPath= "D:\src\office-attendance-tracker\publish\OfficeAttendanceTracker.Service.exe"
```

## Start windows service
```
sc start "OfficeAttendanceTracker"
```


## Uninstall Windows Service (if needed)
```
sc stop "OfficeAttendanceTracker"
sc delete "OfficeAttendanceTracker"
```