# Office Attendance Tracker - Desktop Client

This is the Windows desktop client for the Office Attendance Tracker application. It provides a system tray interface to monitor your office attendance count for the current month.

## Features

- **System Tray Icon**: Runs in the background with a system tray icon
- **Current Month Count**: Displays the number of office days for the current month
- **Auto-refresh**: Updates the count every minute automatically
- **Manual Refresh**: Right-click context menu to manually refresh the count
- **Background Worker**: Runs the attendance tracking worker in the background

## Configuration

The application uses `appsettings.json` for configuration:

```json
{
  "Networks": ["10.8.1.0/24", "10.1.0.0/16"],
  "PollIntervalMs": 1800000,
  "DataFilePath": null,
  "DataFileName": "attendance.csv"
}
```

- **Networks**: Array of CIDR network ranges that identify your office network
- **PollIntervalMs**: Interval in milliseconds to check attendance (default: 30 minutes)
- **DataFilePath**: Path to store attendance data (null = application directory)
- **DataFileName**: Name of the data file (supports .csv or .json)

## Usage

1. Run the application: `OfficeAttendanceTracker.Desktop.exe`
2. The application will start minimized to the system tray
3. Hover over the tray icon to see the current month's office attendance count
4. Right-click the icon for options:
   - **Refresh**: Manually update the attendance count
   - **Exit**: Close the application

## Tooltip Format

The tooltip displays: `Office Days: X (Month Year)`

Example: `Office Days: 15 (January 2025)`

## How It Works

The desktop client:
1. Starts the background `Worker` service that monitors network connectivity
2. Displays a system tray icon with attendance statistics
3. Updates the count every minute automatically
4. Tracks when you're connected to the configured office networks
5. Stores attendance records in CSV or JSON format

## Differences from Service Project

- **Desktop**: Windows Forms application with UI (system tray)
- **Service**: Background Windows Service with no UI

Use the Desktop client for interactive monitoring and the Service project for running as a Windows Service.
