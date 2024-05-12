package main

import (
	"encoding/json"
	"fmt"
	"log"
	"os"
	"time"
)

// AttendanceRecord represents a single event in the JSON data.
type AttendanceRecord struct {
	Date     string `json:"Date"`
	IsOffice bool   `json:"IsOffice"`
	IsDayOff bool   `json:"IsDayOff"`
}

func main() {

	if len(os.Args) < 2 {
		fmt.Println("Usage: ./go-attendance.exe attendance.json [yyyy-MM]")
		return
	}

	// get the data file path from the command argument
	jsonPath := os.Args[1]

	// get the month (optional) from the command-line argument, default to current month
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

	file, err := os.ReadFile(jsonPath)
	if err != nil {
		log.Fatalf("Error reading file: %v", err)
	}

	var records []AttendanceRecord
	err = json.Unmarshal(file, &records)
	if err != nil {
		log.Fatalf("Error unmarshaling JSON: %v", err)
	}

	officeDaysInMonth := 0
	for _, record := range records {
		recordDate, err := time.Parse("2006-01-02", record.Date)
		if err != nil {
			log.Fatalf("Error parsing date: %v", err)
		}

		if recordDate.Year() == currentYear && recordDate.Month() == currentMonth {
			if record.IsOffice {
				officeDaysInMonth++
			}
		}
	}

	// fmt.Printf("Number of office days for the month: %d\n", officeDaysInMonth)
	fmt.Println(officeDaysInMonth)
}
