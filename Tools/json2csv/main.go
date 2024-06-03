package main

import (
	"encoding/csv"
	"encoding/json"
	"fmt"
	"os"
	"sort"
)

type Record map[string]interface{}

func main() {
	if len(os.Args) < 2 {
		fmt.Println("Usage: json2csv <inputfile>")
		return
	}

	inputFile := os.Args[1]
	jsonData, err := os.ReadFile(inputFile)
	if err != nil {
		fmt.Println("Error reading JSON file:", err)
		return
	}

	var records []Record
	err = json.Unmarshal(jsonData, &records)
	if err != nil {
		fmt.Println("Error parsing JSON:", err)
		return
	}

	var keys []string
	if len(records) > 0 {
		for key := range records[0] {
			keys = append(keys, key)
		}
		sort.Strings(keys) // sort keys for consistent order
	}

	writer := csv.NewWriter(os.Stdout)
	defer writer.Flush()

	// write headers
	writer.Write(keys)

	// write records
	for _, record := range records {
		var row []string
		for _, key := range keys {
			row = append(row, fmt.Sprint(record[key]))
		}
		writer.Write(row)
	}
}
