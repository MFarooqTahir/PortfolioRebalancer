open System
open Types
open Helpers
open Rebalancer
open Scraper
open Table
open Db
open Spectre.Console
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
            showSimpleCurrentPortfolio () |> Async.RunSynchronously
        | "2" ->
            importCurrentPortfolioFromJson () |> Async.RunSynchronously
        | "3" ->
            printf "Enter cash amount: "
            match System.Console.ReadLine() |> System.Decimal.TryParse with
            | true, cash when cash > 0m ->
                printf "Include commission? (y/n): "
                let commissionInput = System.Console.ReadLine()
                let includeCommission = commissionInput.Trim().ToLower() = "y"
                let index = getIndexRecordsAsync () |> Async.RunSynchronously
                let prices = getStockRecordsAsync () |> Async.RunSynchronously
                Rebalancer.rebalanceAndDisplay "index" includeCommission cash index [] prices
            | _ -> printfn "Invalid cash amount."
        | "4" ->
            printf "Enter cash amount: "
            match System.Console.ReadLine() |> System.Decimal.TryParse with
            | true, cash when cash >= 0m ->
                printf "Include commission? (y/n): "
                let commissionInput = System.Console.ReadLine()
                let includeCommission = commissionInput.Trim().ToLower() = "y"
                let index = getIndexRecordsAsync () |> Async.RunSynchronously
                let current = getCurrentPortfolioAsync () |> Async.RunSynchronously
                let prices = getStockRecordsAsync () |> Async.RunSynchronously
                Rebalancer.rebalanceAndDisplay "current" includeCommission cash index current prices
            | _ -> printfn "Invalid cash amount."
        | "5" ->
            let url = "https://dps.psx.com.pk/indices"
            let! result = Scraper.ConstructIndexCache url true
            printErrors result
        | "0" ->
            printfn "Exiting."
            continueRunning <- false
        | _ ->
            printfn "Invalid option."
}

main() |> Async.RunSynchronously |> ignore