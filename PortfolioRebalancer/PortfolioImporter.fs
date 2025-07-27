module PortfolioImporter

open System.IO
open System.Text.Json
open Types
open Db
open System

let options = JsonSerializerOptions()
do
    options.PropertyNameCaseInsensitive <- true
let importCurrentPortfolioFromJson (filePath: string) = async {
    try
        // Check if file exists
        if not (File.Exists(filePath)) then
            printfn "[ERROR] File not found: %s" filePath
            printfn "Please ensure the file exists and the path is correct."
            return ()
        
        // Check if file is empty
        let fileInfo = FileInfo(filePath)
        if fileInfo.Length = 0L then
            printfn "[ERROR] File is empty: %s" filePath
            return ()
        
        // Read and parse JSON
        let json = File.ReadAllText(filePath)
        let records = JsonSerializer.Deserialize<CurrentPortfolio list>(json, options) |> Option.ofObj
        
        match records with
        | None -> 
            printfn "[ERROR] Could not parse current portfolio JSON."
            printfn "Please check the JSON format and ensure it matches the required structure."
            return ()
        | Some xs ->
            // Validate that we have at least one record
            if List.isEmpty xs then
                printfn "[ERROR] Portfolio file contains no records."
                printfn "Please ensure the JSON contains at least one portfolio entry."
                return ()
            
            // Validate each record and filter out ETFs
            let validationErrors = 
                xs |> List.mapi (fun index record ->
                    let errors = ResizeArray<string>()
                    
                    if System.String.IsNullOrWhiteSpace(record.Symbol) then
                        errors.Add(sprintf "Record %d: Symbol is empty or null" (index + 1))
                    
                    if record.Shares <= 0 then
                        errors.Add(sprintf "Record %d: Shares must be greater than 0 (got %d)" (index + 1) record.Shares)
                    
                    if record.Price <= 0m then
                        errors.Add(sprintf "Record %d: Price must be greater than 0 (got %M)" (index + 1) record.Price)
                    
                    errors |> Seq.toList
                ) |> List.concat
            
            // Filter out ETFs (symbols ending with common ETF suffixes)
            let isETF (symbol:string) =
                symbol.EndsWith("ETF", StringComparison.OrdinalIgnoreCase) || 
                symbol.EndsWith("ETN", StringComparison.OrdinalIgnoreCase) || 
                symbol.EndsWith("ETP", StringComparison.OrdinalIgnoreCase) ||
                symbol.EndsWith("FUND", StringComparison.OrdinalIgnoreCase) ||
                symbol.EndsWith("TRUST", StringComparison.OrdinalIgnoreCase)
            
            let etfs, stocks = xs |> List.partition (fun record -> isETF record.Symbol)
            
            if not (List.isEmpty etfs) then
                printfn "[INFO] ETFs detected and will be excluded from import:"
                etfs |> List.iter (fun etf -> printfn "  - %s (ETF)" etf.Symbol)
                printfn ""
            
            if List.isEmpty stocks then
                printfn "[ERROR] No valid stocks found after filtering ETFs."
                printfn "Please ensure your portfolio contains individual stocks, not just ETFs."
                return ()
            
            if not (List.isEmpty validationErrors) then
                printfn "[ERROR] Validation errors found:"
                validationErrors |> List.iter (printfn "  - %s")
                return ()
            
            // Import to database (only stocks, not ETFs)
            do! setCurrentPortfolioAsync stocks
            printfn "[SUCCESS] Current portfolio imported and saved to database."
            printfn "  - File: %s" filePath
            printfn "  - Stocks imported: %d" stocks.Length
            printfn "  - ETFs excluded: %d" etfs.Length
            printfn "  - Total value: %M" (stocks |> List.sumBy (fun r -> decimal r.Shares * r.Price))
            
    with 
    | :? DirectoryNotFoundException as ex ->
        printfn "[ERROR] Directory not found: %s" ex.Message
        printfn "Please check the file path and ensure the directory exists."
    | :? FileNotFoundException as ex ->
        printfn "[ERROR] File not found: %s" ex.Message
        printfn "Please check the file path and ensure the file exists."
    | :? System.UnauthorizedAccessException as ex ->
        printfn "[ERROR] Access denied: %s" ex.Message
        printfn "Please check file permissions and ensure you have read access."
    | :? JsonException as ex ->
        printfn "[ERROR] Invalid JSON format: %s" ex.Message
        printfn "Please check the JSON syntax and structure."
    | ex ->
        printfn "[ERROR] Failed to import current portfolio: %s" ex.Message
        printfn "Please check the file and try again."
}