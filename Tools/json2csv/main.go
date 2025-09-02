package main

import (
	"encoding/csv"
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"sort"
	"strings"
)

type Record map[string]interface{}

func main() {
	var columnOrder = flag.String("columns", "", "Comma-separated list of column names to specify order")
	flag.Parse()

	if len(flag.Args()) < 1 {
		fmt.Println("Usage: json2csv [-columns col1,col2,col3] <inputfile>")
		return
	}

	inputFile := flag.Args()[0]
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
		// Get all available keys first
		allKeys := make(map[string]bool)
		for key := range records[0] {
			allKeys[key] = true
		}

		// If column order is specified, use it
		if *columnOrder != "" {
			specifiedCols := strings.Split(*columnOrder, ",")
			for _, col := range specifiedCols {
				col = strings.TrimSpace(col)
				if allKeys[col] {
					keys = append(keys, col)
					delete(allKeys, col) // remove from remaining keys
				}
			}
			// Add any remaining keys at the end
			for key := range allKeys {
				keys = append(keys, key)
			}
			// Sort the remaining keys for consistency
			if len(keys) > len(specifiedCols) {
				remainingKeys := keys[len(specifiedCols):]
				sort.Strings(remainingKeys)
				keys = append(keys[:len(specifiedCols)], remainingKeys...)
			}
		} else {
			// Default behavior: alphabetical sort
			for key := range allKeys {
				keys = append(keys, key)
			}
			sort.Strings(keys)
		}
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
