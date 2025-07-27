open System
open Types
open Rebalancer
open Helpers
open Scraper
open Table
open Db
open PortfolioImporter

let printErrors (result: Result<unit, ScrapingError>) =
    match result with
    | Error error -> 
        printfn "[ERROR] Operation failed:"
        match error with
        | NavigationError msg -> printfn "Navigation Error: %s" msg
        | ElementNotFound msg -> printfn "Element Not Found: %s" msg
        | DataExtractionError msg -> printfn "Data Extraction Error: %s" msg
        | PortfolioCreationError msg -> printfn "Portfolio Creation Error: %s" msg
        | FileOperationError msg -> printfn "File Operation Error: %s" msg
    | _ -> ()

let main() = async {
    do! ensureTablesAsync()
    let mutable continueRunning = true
    while continueRunning do
        printfn "\nPortfolio Rebalancer"
        printfn "1. Show Index and Current Portfolio"
        printfn "2. Import Current Portfolio from JSON"
        printfn "3. Rebalance Index with Cash (optionally show commission)"
        printfn "4. Rebalance Current Portfolio with Cash (optionally show commission)"
        printfn "5. Get Index and Stock Data (scrape and save both to DB)"
        printfn "0. Exit"
        printf "Select an option: "
        match Console.ReadLine() with
        | "1" ->
            do! showSimpleCurrentPortfolio ()
        | "2" ->
            do! importCurrentPortfolioFromJson ()
        | "3" ->
            printf "Enter cash amount: "
            match Console.ReadLine() with
            | ValidDecimal cash when cash > 0m ->
                printf "Include commission? (y/n): "
                let commissionInput = Console.ReadLine()
                let includeCommission = commissionInput.Trim().ToLower() = "y"
                let! index = getIndexRecordsAsync ()
                let! prices = getStockRecordsAsync ()
                rebalanceAndDisplay "index" includeCommission cash index [] prices
            | _ -> printfn "Invalid cash amount."
        | "4" ->
            printf "Enter cash amount: "
            match Console.ReadLine() with
            | ValidDecimal cash when cash >= 0m ->
                printf "Include commission? (y/n): "
                let commissionInput = Console.ReadLine()
                let includeCommission = commissionInput.Trim().ToLower() = "y"
                let! index = getIndexRecordsAsync ()
                let! current = getCurrentPortfolioAsync ()
                let! prices = getStockRecordsAsync ()
                rebalanceAndDisplay "current" includeCommission cash index current prices
            | _ -> printfn "Invalid cash amount."
        | "5" ->
            let! result = ConstructIndexCache true KSE30
            printErrors result
        | "0" ->
            printfn "Exiting."
            continueRunning <- false
        | _ ->
            printfn "Invalid option."
}

main() |> Async.RunSynchronously |> ignore