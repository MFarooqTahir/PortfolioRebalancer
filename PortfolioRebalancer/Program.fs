open System
open Types
open Helpers
open Rebalancer
open Scraper
open Table
open Db
open Spectre.Console

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
    do! Db.ensureTablesAsync()
    let! indexRecords = Db.getIndexRecordsAsync()
    let url = "https://dps.psx.com.pk/indices"
    let portfolioImport = "C:\\portfolio_trades.json" // Hardcoded path


    let mutable continueRunning = true
    while continueRunning do
        printfn "\nChoose an option:"
        printfn "1. Re Construct index cache"
        printfn "2. Show index and current porfolio"
        printfn "3. Balance index using cash"
        printfn "4. Re-Balance current portfolio with cash"
        printfn "5. Import portfolio from JSON file (hardcoded path)"
        printfn "6. View transaction history and current portfolio"
        printfn "7. Exit"
        printf "Enter your choice (1-7): "
        let choice = Console.ReadLine()
        match choice with
        | "1" ->
            printf "\nDo you want to get latest prices as well? (y/n): "
            let getLatestPricesInput = Console.ReadLine()
            let getLatest = getLatestPricesInput.Equals("y")
            let! result = ConstructIndexCache url getLatest
            printErrors result
        | "2" ->
            let! result = ShowIndexAndPortfolio url
            printErrors result
        | "3" ->
            printf "\nEnter the amount of cash available for rebalancing: "
            let cashInput = Console.ReadLine()
            match Decimal.TryParse(cashInput.Replace(",", "")) with
            | true, cash when cash > 0m ->
                printf "\nReinvest remaining cash? (This may reduce balance accuracy but minimizes unused cash) (y/n): "
                let reinvestInput = Console.ReadLine()
                let reinvestRemainingCash = reinvestInput.Equals("y", StringComparison.OrdinalIgnoreCase)
                do! BalanceIndexAsync cash reinvestRemainingCash
            | _ ->
                printfn "Invalid cash amount. Please enter a valid decimal number."  
        | "4" ->
            printf "\nEnter the amount of cash available for rebalancing: "
            let cashInput = Console.ReadLine()
            match cashInput with
            | ValidDecimal cash ->
                printf "\nReinvest remaining cash? (This may reduce balance accuracy but minimizes unused cash) (y/n): "
                let reinvestInput = Console.ReadLine()
                let reinvestRemainingCash = reinvestInput.Equals("y", StringComparison.OrdinalIgnoreCase)
                printf "\nShow estimated taxes? (y/n): "
                let showTaxInput = Console.ReadLine()
                let showTaxes = showTaxInput.Equals("y", StringComparison.OrdinalIgnoreCase)
                do! RebalanceCurrentPortfolioAsync cash reinvestRemainingCash showTaxes
            | _ ->
                printfn "Invalid cash amount. Please enter a valid decimal number."
        | "5" ->
            do! importPortfolioFromFile portfolioImport
            printfn "Portfolio imported and upserted from %s" portfolioImport
        | "6" ->
            do! showTransactionHistory ()
            do! showCurrentPortfolio ()
        | "7" ->
            printfn "Exiting program..."
            continueRunning <- false
        | _ ->
            printfn "Invalid choice. Please try again."
    printfn "------------ Program Ended ------------"
}
main() |> Async.RunSynchronously |> ignore