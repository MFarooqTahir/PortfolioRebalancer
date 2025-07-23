open System
open System.IO
open Types
open Helpers

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
    let url = "https://dps.psx.com.pk/indices"
    let savePath = @"C:\Users\Administrator\Documents\Portfolio Data"
    let indexCacheFile = Path.Combine(savePath, "IndexCache.json")
    let stockDataCacheFile = Path.Combine(savePath, "StocksCache.json")
    let currentPortfolioFile = Path.Combine(savePath, "CurrentPortfolio.json")
    
    let mutable continueRunning = true
    
    while continueRunning do
        printfn "\nChoose an option:"
        printfn "1. Re Construct index cache"
        printfn "2. Show index and current porfolio"
        printfn "3. Balance index using cash"
        printfn "4. Re-Balance current portfolio with cash"
        printfn "5. Exit"
        printf "Enter your choice (1-5): "
        let choice = Console.ReadLine()
            
        match choice with
        | "1" ->
            printf "\nDo you want to get latest prices as well? (y/n): "
            let getLatestPricesInput = Console.ReadLine()
            let getLatest = getLatestPricesInput.Equals("y")
     
            let! result = Scraper.ConstructIndexCache url indexCacheFile stockDataCacheFile currentPortfolioFile getLatest
            printErrors result
            
        | "2" ->
            let! result = Table.ShowIndexAndPortfolio url indexCacheFile stockDataCacheFile currentPortfolioFile
            printErrors result
        | "3" ->
            printf "\nEnter the amount of cash available for rebalancing: "
            let cashInput = Console.ReadLine()

            match Decimal.TryParse(cashInput.Replace(",", "")) with
            | true, cash when cash > 0m ->
                do! Rebalancer.BalanceIndexAsync indexCacheFile stockDataCacheFile cash
            | _ ->
                printfn "Invalid cash amount. Please enter a valid decimal number."  
        | "4" ->
            printf "\nEnter the amount of cash available for rebalancing: "
            let cashInput = Console.ReadLine()
            match cashInput with
            | ValidDecimal cash ->
                do! Rebalancer.RebalanceCurrentPortfolioAsync indexCacheFile stockDataCacheFile currentPortfolioFile cash
            | _ ->
                printfn "Invalid cash amount. Please enter a valid decimal number."
        | "5" ->
            printfn "Exiting program..."
            continueRunning <- false
     
        | _ ->
            printfn "Invalid choice. Please try again."
 
    printfn "------------ Program Ended ------------"
}
 
main() |> Async.RunSynchronously |> ignore
