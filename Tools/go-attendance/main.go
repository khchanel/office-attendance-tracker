package main

import (
	"encoding/csv"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

// AttendanceRecord represents a single attendance record
type AttendanceRecord struct {
	Date     string `json:"Date"`
	IsOffice bool   `json:"IsOffice"`
	IsDayOff bool   `json:"IsDayOff"`
}

func main() {
	if len(os.Args) < 2 {
		fmt.Println("Usage: ./go-attendance.exe attendance.[json|csv] [yyyy-MM]")
		return
	}

	dataPath := os.Args[1]

	// Get month from args or default to current
	currentMonth := time.Now().Month()
	currentYear := time.Now().Year()
	if len(os.Args) >= 3 {
		argMonth := os.Args[2]
		argTime, err := time.Parse("2006-01", argMonth)
		if err != nil {
			log.Fatalf("Invalid month argument: %v", err)
		}
		currentYear, currentMonth = argTime.Year(), argTime.Month()
	}

	// Load records based on file extension
	var records []AttendanceRecord
	var err error

	ext := strings.ToLower(filepath.Ext(dataPath))
	switch ext {
	case ".json":
		records, err = loadFromJSON(dataPath)
	case ".csv":
		records, err = loadFromCSV(dataPath)
	default:
		log.Fatalf("Unsupported file format: %s. Use .json or .csv", ext)
	}

	if err != nil {
		log.Fatalf("Error loading data: %v", err)
	}

	// Count office days for the specified month
	officeDaysInMonth := 0
	for _, record := range records {
		recordDate, err := time.Parse("2006-01-02", record.Date)
		if err != nil {
			log.Printf("Warning: Error parsing date %s: %v", record.Date, err)
			continue
		}

		if recordDate.Year() == currentYear && recordDate.Month() == currentMonth {
			if record.IsOffice {
				officeDaysInMonth++
			}
		}
	}

	fmt.Println(officeDaysInMonth)
}

func loadFromJSON(path string) ([]AttendanceRecord, error) {
	file, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}

	var records []AttendanceRecord
	err = json.Unmarshal(file, &records)
	return records, err
}

func loadFromCSV(path string) ([]AttendanceRecord, error) {
	file, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	defer file.Close()

	reader := csv.NewReader(file)
	rows, err := reader.ReadAll()
	if err != nil {
		return nil, err
	}

	var records []AttendanceRecord

	// Skip header row if it exists
	startRow := 0
	if len(rows) > 0 && rows[0][0] == "Date" {
		startRow = 1
	}

	for i := startRow; i < len(rows); i++ {
		row := rows[i]
		if len(row) != 3 {
			continue // Skip malformed rows
		}

		isOffice, err := strconv.ParseBool(row[1])
		if err != nil {
			log.Printf("Warning: Invalid IsOffice value %s in row %d", row[1], i+1)
			continue
		}

		isDayOff, err := strconv.ParseBool(row[2])
		if err != nil {
			log.Printf("Warning: Invalid IsDayOff value %s in row %d", row[2], i+1)
			continue
		}

		records = append(records, AttendanceRecord{
			Date:     row[0],
			IsOffice: isOffice,
			IsDayOff: isDayOff,
		})
	}

	return records, nil
}
