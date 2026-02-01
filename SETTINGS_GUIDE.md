# Settings Quick Reference Guide

## Opening Settings

Right-click the **Office Attendance Tracker** icon in your system tray, then click **Settings...**

## Settings Options

### ?? Office Networks (CIDR)
**What it does:** Defines which networks are considered "office networks"

**How to configure:**
- Enter one network per line in CIDR notation
- Example:
  ```
  10.8.1.0/24
  10.1.0.0/16
  192.168.1.0/24
  ```

**Finding your office network:**
1. While at the office, open Command Prompt
2. Run: `ipconfig`
3. Look for your IPv4 Address (e.g., `10.8.1.50`)
4. Convert to CIDR notation (e.g., `10.8.1.0/24` for the 10.8.1.x network)

---

### ?? Poll Interval (seconds)
**What it does:** How often the app checks if you're on the office network

**Default:** 1800 seconds (30 minutes)

---

### ? Enable Background Worker
**What it does:** Automatically tracks attendance without manual intervention

**Default:** Enabled (checked)

**When to disable:**
- If you want to manually control attendance tracking
- Troubleshooting issues
- Reducing system resource usage

---

### ?? Compliance Threshold (%)
**What it does:** Defines what percentage of office days is considered "compliant"

**Default:** 50%


This affects the color of the tray icon:
- ?? **Green (Absolutely Fine):** Already meet complaince for the whole month
- ?? **Blue (Compliant):** Meeting compliance threshold so far (rolling)
- ?? **Orange (Warning):** Approaching threshold
- ?? **Red (Critical):** Well below threshold so far

---

### ?? Data File Path
**What it does:** Where attendance data is saved

**Default:** Empty (uses application directory)

**Custom path example:** `D:\attendance`

**When to change:**
- You want backups in a specific location
- Syncing with cloud storage (Dropbox, OneDrive, etc.)
- Multiple computers sharing same data file
- Avoid overwriting data when reinstalling

---

### ?? Data File Name
**What it does:** Name of the file storing attendance records

**Default:** `attendance.csv`

**Supported formats:**
- `.csv` - Comma-separated values (opens in Excel)
- `.json` - JSON format (for advanced users/scripts)

**Examples:**
- `attendance.csv`
- `office-days-2024.csv`
- `my-attendance.json`

---

## After Changing Settings

### Immediate Changes
? Poll Interval - updates immediately

### Requires Restart
?? Everything else requires restarting the application:
1. Right-click tray icon
2. Click **Exit**
3. Start the application again

---

## Tips

?? **First time setup:** The settings dialog will open automatically on first run

?? **Validation:** The app won't let you save invalid settings (e.g., empty networks)

?? **Hover help:** Hover over any field to see helpful tooltips

?? **Testing:** After configuring networks, click "Refresh" in the tray menu to test if you're detected as "in office"

---

## Troubleshooting

**Problem:** Not detecting office presence

**Solutions:**
1. Open settings and verify your office network CIDR
2. While at office, run `ipconfig` to confirm your IP
3. Ensure your IP falls within the configured CIDR range
4. Try reducing the poll interval for more frequent checks

**Problem:** App using too many resources

**Solutions:**
1. Increase the poll interval to 3600+ seconds
2. Temporarily disable background worker when not needed

---

## Settings File Location

Your settings are saved in:
```
[Application Directory]\user-settings.json
```

You can:
- Back up this file to preserve settings
- Copy it to other computers for identical configuration
- Edit it directly (JSON format) if needed
