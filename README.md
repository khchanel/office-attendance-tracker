# Office Attendance Tracker

[![Github Release](https://img.shields.io/github/v/release/khchanel/office-attendance-tracker?style=flat)](https://github.com/khchanel/office-attendance-tracker/releases)
[![build](https://github.com/khchanel/office-attendance-tracker/actions/workflows/dotnet.yml/badge.svg)](https://github.com/khchanel/office-attendance-tracker/actions/workflows/dotnet.yml)
[![build](https://github.com/khchanel/office-attendance-tracker/actions/workflows/go-attendance.yml/badge.svg)](https://github.com/khchanel/office-attendance-tracker/actions/workflows/go-attendance.yml)
[![build](https://github.com/khchanel/office-attendance-tracker/actions/workflows/json2csv.yml/badge.svg)](https://github.com/khchanel/office-attendance-tracker/actions/workflows/json2csv.yml)

Automatically detect and track your office attendance using Windows system tray app or background service.

## Features
- Automatic detection of office presence based on configured network CIDR ranges
- Configurable
- Currently support file-based storage in CSV or JSON format.
- Lightweight with minimal resource usage
- Easy deployment as Windows Service or Desktop App
- Did I mention its fully automatic? - no manual check-in/out needed!

## Deployment Options

Two deployment modes are available:

1. **Windows Service** (`OfficeAttendanceTracker.Service`) - Runs as a background service with no UI
2. **Desktop App** (`OfficeAttendanceTracker.Desktop`) - System tray application with visual feedback

## Prerequisites
* .NET 8 SDK
* Windows OS
* Administrator privileges (for Windows Service installation)

---

## Build

Build all projects:
```
dotnet build
```

Publish a specific project:
```
dotnet publish OfficeAttendanceTracker.Service -c Release -o ./publish/service
dotnet publish OfficeAttendanceTracker.Desktop -c Release -o ./publish/desktop
```

---

## Configuration

### Desktop App - UI Settings

The Desktop app uses **`user-settings.json`** managed entirely through the UI. No manual file editing needed!

Access settings from the system tray menu:

1. Right-click the system tray icon
2. Select **Settings...**
3. Configure the following options:
   - **Office Networks (CIDR)**: Network ranges to detect office presence (one per line)
   - **Poll Interval**: How often to check network status (in seconds)
   - **Enable Background Worker**: Enable automatic background monitoring
   - **Compliance Threshold**: Percentage threshold for attendance compliance
   - **Data File Path**: Where to store attendance data (leave empty for app directory)
   - **Data File Name**: Name of the attendance file (supports .csv or .json)

**Settings file location:** `user-settings.json` in the application directory  
**Note:** Changes to networks, data path/filename, and background worker require an application restart.

### Windows Service - Configuration File

The Windows Service uses **`appsettings.json`** for configuration. Edit this file manually:

```json
{
  "Networks": ["10.8.1.0/24", "10.1.0.0/16"],
  "PollIntervalMs": 1800000,
  "ComplianceThreshold": 0.5,
  "DataFilePath": null,
  "DataFileName": "attendance.csv"
}
```

**Configuration Options:**
- `Networks`: Office network CIDR ranges used to detect office presence (observe your computer network IP while in office network)
- `PollIntervalMs`: Network check interval in milliseconds (default: 30 minutes)
- `ComplianceThreshold`: Threshold for compliance status (0.0 to 1.0, default: 0.5 = 50%)
- `DataFilePath`: Storage path for attendance data (null = application directory) e.g. "D:\\attendance"
- `DataFileName`: Name of attendance file (supports .csv or .json)

**Important:** Restart the Windows Service after editing `appsettings.json`.

---

## Running the Application

### Option 1: Windows Service (Background)

**Install:**
```
sc create "OfficeAttendanceTracker" binPath= "C:\path\to\publish\service\OfficeAttendanceTracker.Service.exe"
```

**Start:**
```
sc start "OfficeAttendanceTracker"
```

**Stop:**
```
sc stop "OfficeAttendanceTracker"
```

**Uninstall:**
```
sc delete "OfficeAttendanceTracker"
```

### Option 2: Desktop System Tray App

Run the executable:
```
.\publish\desktop\OfficeAttendanceTracker.Desktop.exe
```

Or during development:
```
dotnet run --project OfficeAttendanceTracker.Desktop
```

The app will start in the system tray showing current month's attendance count.

#### Desktop UI

The system tray app automatically track attendance and provides intuitive visual feedback for your office attendance:

![System Tray Icon](./docs/screenshots/Screenshot-1.png)

![Context Menu](./docs/screenshots/Screenshot-2.png)

![Tooltip with Count](./docs/screenshots/Screenshot-3.png)

---

## Development

Run as console app for testing:
```
dotnet run --project OfficeAttendanceTracker.Service
```
