open System
open Types
open Rebalancer
open Helpers
open Scraper
open Table
open Db
open PortfolioImporter
open Configuration
open Validation
open Performance

// Enhanced menu options with better organization
type MenuOption = 
    | ShowPortfolio
    | ImportPortfolio
    | RebalanceIndex
    | RebalanceCurrent
    | GetData
    | ShowPerformance
    | ClearCache
    | Exit
    | Invalid

// Parse user input to menu option with better validation
let parseMenuOption (input: string) : MenuOption =
    match input.Trim() with
    | "1" -> ShowPortfolio
    | "2" -> ImportPortfolio
    | "3" -> RebalanceIndex
    | "4" -> RebalanceCurrent
    | "5" -> GetData
    | "6" -> ShowPerformance
    | "7" -> ClearCache
    | "0" -> Exit
    | _ -> Invalid

// Enhanced menu display with better formatting
let displayMenu () =
    printfn "\n╔══════════════════════════════════════════════════════════════╗"
    printfn "║                    Portfolio Rebalancer                      ║"
    printfn "╠══════════════════════════════════════════════════════════════╣"
    printfn "║ 1. Show Index and Current Portfolio                          ║"
    printfn "║ 2. Import Current Portfolio from JSON                        ║"
    printfn "║ 3. Rebalance Index with Cash (optionally show commission)   ║"
    printfn "║ 4. Rebalance Current Portfolio with Cash (optionally show)  ║"
    printfn "║ 5. Get Index and Stock Data (scrape and save both to DB)     ║"
    printfn "║ 6. Show Performance Metrics                                  ║"
    printfn "║ 7. Clear Cache                                               ║"
    printfn "║ 0. Exit                                                      ║"
    printfn "╚══════════════════════════════════════════════════════════════╝"
    printf "Select an option: "

// Enhanced input validation with better user experience
let rec getCashAmount () : decimal option =
    printf "Enter cash amount: "
    match Console.ReadLine() with
    | ValidDecimal cash when cash >= 0m -> Some cash
    | _ -> 
        printfn "Invalid cash amount. Please enter a valid positive number."
        getCashAmount()

let rec getCommissionPreference () : bool =
    printf "Include commission? (y/n): "
    let commissionInput = Console.ReadLine().Trim().ToLower()
    match commissionInput with
    | "y" | "yes" -> true
    | "n" | "no" -> false
    | _ -> 
        printfn "Please enter 'y' for yes or 'n' for no."
        getCommissionPreference()

let rec getConfirmation (message: string) : bool =
    printf "%s (y/n): " message
    let input = Console.ReadLine().Trim().ToLower()
    match input with
    | "y" | "yes" -> true
    | "n" | "no" -> false
    | _ -> 
        printfn "Please enter 'y' for yes or 'n' for no."
        getConfirmation message

// Handle rebalancing with cash input and performance monitoring
let handleRebalancing (kind: string) (includeCommission: bool) =
    match getCashAmount() with
    | Some cash ->
        async {
            let operationName = sprintf "%s_rebalancing" kind
            let! result = measureOperation operationName (fun () -> async {
                let! index = getIndexRecordsAsync()
                let! prices = getStockRecordsAsync()
                
                if kind = "index" then
                    rebalanceAndDisplay kind includeCommission cash index [] prices
                else
                    let! current = getCurrentPortfolioAsync()
                    rebalanceAndDisplay kind includeCommission cash index current prices
            })
            return result
        }
    | None -> async { return () }

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
        | TimeoutError msg -> printfn "Timeout Error: %s" msg
        | NetworkError msg -> printfn "Network Error: %s" msg
    | _ -> ()

// Enhanced menu handler with better error handling and new options
let handleMenuOption (option: MenuOption) : Async<bool> =
    async {
        try
            match option with
            | ShowPortfolio ->
                printfn "\n📊 Loading portfolio data..."
                do! showSimpleCurrentPortfolio()
                return true
                
            | ImportPortfolio ->
                printfn "\n📥 Importing portfolio from JSON..."
                do! importCurrentPortfolioFromJson()
                return true
                
            | RebalanceIndex ->
                printfn "\n⚖️  Index Rebalancing"
                let includeCommission = getCommissionPreference()
                do! handleRebalancing "index" includeCommission
                return true
                
            | RebalanceCurrent ->
                printfn "\n🔄 Current Portfolio Rebalancing"
                let includeCommission = getCommissionPreference()
                do! handleRebalancing "current" includeCommission
                return true
                
            | GetData ->
                printfn "\n🌐 Fetching market data..."
                let! result = ConstructIndexCache true KSE30
                printErrors result
                return true
                
            | ShowPerformance ->
                printfn "\n📈 Performance Metrics"
                let stats = getPerformanceStats()
                if List.isEmpty stats then
                    printfn "No performance metrics available yet."
                else
                    printfn "Recent Performance Metrics:"
                    stats 
                    |> List.take 10
                    |> List.iter (fun metric ->
                        let status = if metric.Success then "✅" else "❌"
                        printfn "%s %s: %O (%s)" 
                            status 
                            metric.OperationName 
                            metric.Duration 
                            (metric.Timestamp.ToString("HH:mm:ss")))
                return true
                
            | ClearCache ->
                printfn "\n🗑️  Cache Management"
                let confirmed = getConfirmation "Are you sure you want to clear the cache?"
                if confirmed then
                    cacheManager.Clear()
                    clearOldMetrics 24
                    printfn "✅ Cache and old metrics cleared successfully!"
                else
                    printfn "❌ Cache clearing cancelled."
                return true
                
            | Exit ->
                printfn "\n👋 Thank you for using Portfolio Rebalancer!"
                return false
                
            | Invalid ->
                printfn "\n❌ Invalid option. Please select a valid menu option."
                return true
        with
        | ex ->
            printfn "\n💥 An unexpected error occurred: %s" ex.Message
            printfn "Please try again or contact support if the problem persists."
            return true
    }

let main() = async {
    try
        printfn "🚀 Starting Portfolio Rebalancer..."
        printfn "📋 Initializing database..."
        do! ensureTablesAsync()
        printfn "✅ Database initialized successfully!"
        
        let rec runMenu () = async {
            try
                displayMenu()
                let input = Console.ReadLine()
                let option = parseMenuOption input
                let! continueRunning = handleMenuOption option
                if continueRunning then
                    do! runMenu()
            with
            | ex ->
                printfn "\n💥 Menu error: %s" ex.Message
                printfn "Restarting menu..."
                do! runMenu()
        }
        
        do! runMenu()
    with
    | ex ->
        printfn "\n💥 Fatal error: %s" ex.Message
        printfn "Application will exit. Please check your configuration and try again."
        Environment.Exit(1)
}

main() |> Async.RunSynchronously |> ignore