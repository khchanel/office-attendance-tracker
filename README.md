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
2. **Desktop App** (`OfficeAttendanceTracker.Desktop`) - System tray application with GUI

## Prerequisites
* .NET 8
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

### Option 1: Desktop System Tray App

Run the executable:
```
.\publish\desktop\OfficeAttendanceTracker.Desktop.exe
```

Or during development:
```
dotnet run --project OfficeAttendanceTracker.Desktop
```

The app will start in the system tray showing current month's attendance count.

### Option 2: Windows Service (Background)

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


#### Desktop UI

The system tray app automatically tracks attendance and provides intuitive visual feedback for your office attendance:

**System Tray Icon:**

![System Tray Icon](./docs/screenshots/Screenshot-1.png)

The icon displays your current month's office attendance count with color-coded compliance status:
- **Green** - Already met threshold for the rest of the month
- **Blue** - Compliant up to current date
- **Orange** - Warning below threshold
- **Red** - Critically below threshold

**Context Menu:**

![Context Menu](./docs/screenshots/Screenshot-2.png)

Right-click the tray icon to access:
- **Refresh** - Manually update attendance count
- **Settings...** - Open settings dialog
- **Exit** - Close the application

**Hover Tooltip:**

![Tooltip with Count](./docs/screenshots/Screenshot-3.png)

Hover over the icon to see detailed attendance information.

**Settings UI**

![Settings UI](./docs/screenshots/Screenshot-SettingsUI.png)


---


## FAQ

**Q: Why do I need to restart the application after changing some settings?**  
A: Settings like network ranges, background worker status, and data file paths are loaded during application startup. These require a restart to reinitialize the monitoring services.

**Q: Can I use this on multiple computers?**  
A: Yes! Each installation maintains its own attendance data. You can configure different settings on each computer, or you could put data file on shared drive.

**Q: Does this work with multiple office locations?**  
A: Yes! Configure multiple network ranges in the Office Networks setting. The app will detect presence on any of the configured networks.

**Q: What happens if my network configuration changes?**  
A: Use the "Detect Current" button in settings to detect the new network configuration. The app will continue tracking with previously configured networks until updated.

**Q: How is attendance calculated?**  
A: Attendance is tracked per day. If you're detected on an office network at any point during a day, that day counts toward your attendance.

**Q: Can I export my attendance data?**  
A: Yes! The attendance data is stored in CSV or JSON format (your choice) and can be easily opened in Excel or any data analysis tool.

**Q: What's the difference between Desktop and Service deployments?**  
A: Desktop provides a system tray icon with visual feedback and easy settings access. Service runs invisibly in the background. Both track attendance identically.


