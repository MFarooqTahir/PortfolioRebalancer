open System
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Console.Rebalancer
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Scraper.Scraper
open PortfolioRebalancer.Console.Table
open PortfolioRebalancer.Data.Db
open PortfolioRebalancer.Data.PortfolioImporter
open PortfolioRebalancer.Core.Configuration
open PortfolioRebalancer.Core.Validation
open PortfolioRebalancer.Core.Performance
open PortfolioRebalancer.Core.ServiceContainer
open PortfolioRebalancer.Core.Logger
open Spectre.Console

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

let displayMenu () =
    let menuTable = 
        Table()
            .Title("[bold blue]Portfolio Rebalancer[/]")
            .AddColumns([|"Option"; "Description"|])
            .AddRow("1", "Show Index and Current Portfolio")
            .AddRow("2", "Import Current Portfolio from JSON")
            .AddRow("3", "Rebalance Index with Cash (optionally show commission)")
            .AddRow("4", "Rebalance Current Portfolio with Cash (optionally show)")
            .AddRow("5", "Get Index and Stock Data (scrape and save both to DB)")
            .AddRow("6", "Show Performance Metrics")
            .AddRow("7", "Clear Cache")
            .AddRow("0", "Exit")
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
    
    AnsiConsole.Write(menuTable)
    printf "\nSelect an option: "

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
        AnsiConsole.MarkupLine "[red]❌ Operation failed:[/]"
        match error with
        | NavigationError msg -> AnsiConsole.MarkupLine(sprintf "[red]🧭 Navigation Issue: %s[/]" msg)
        | ElementNotFound msg -> AnsiConsole.MarkupLine(sprintf "[red]🔍 Element Not Found: %s[/]" msg)
        | DataExtractionError msg -> AnsiConsole.MarkupLine(sprintf "[red]📊 Data Extraction Issue: %s[/]" msg)
        | PortfolioCreationError msg -> AnsiConsole.MarkupLine(sprintf "[red]📁 Portfolio Creation Issue: %s[/]" msg)
        | FileOperationError msg -> AnsiConsole.MarkupLine(sprintf "[red]📂 File Operation Issue: %s[/]" msg)
        | TimeoutError msg -> AnsiConsole.MarkupLine(sprintf "[red]⏰ Timeout Issue: %s[/]" msg)
        | NetworkError msg -> AnsiConsole.MarkupLine(sprintf "[red]🌐 Network Issue: %s[/]" msg)
    | _ -> ()



let MainMenu() = async {
    try
        createServiceContainer (Some (createConfigurationBuilder()))
            |> LoggerContext.Initialize
            
        AnsiConsole.MarkupLine "[bold blue]🚀 Starting Portfolio Rebalancer...[/]"
        
        AnsiConsole.MarkupLine "[cyan]📋 Initializing database...[/]"
        do! ensureTablesAsync()
        AnsiConsole.MarkupLine "[green]✅ Database initialized successfully![/]"
        
        let rec runMenu() = async {
            try
                displayMenu()
                let input = Console.ReadLine()
                let option = parseMenuOption input
                
                match option with
                | ShowPortfolio ->
                    do! showSimpleCurrentPortfolio ()
                    return true
                    
                | ImportPortfolio ->
                    AnsiConsole.MarkupLine "\n[cyan]📥 Importing portfolio from JSON...[/]"
                    printf "Enter the path to your portfolio JSON file: "
                    let filePath = Console.ReadLine().Trim()
                    
                    if String.IsNullOrWhiteSpace filePath then
                        AnsiConsole.MarkupLine "[red]❌ No file path provided. Import cancelled.[/]"
                    else
                        do! importCurrentPortfolioFromJson filePath
                    return true
                    
                | RebalanceIndex ->
                    AnsiConsole.MarkupLine "\n[yellow]⚖️  Index Rebalancing[/]"
                    let includeCommission = getCommissionPreference()
                    do! handleRebalancing "index" includeCommission
                    return true
                    
                | RebalanceCurrent ->
                    AnsiConsole.MarkupLine "\n[magenta]🔄 Current Portfolio Rebalancing[/]"
                    let includeCommission = getCommissionPreference()
                    do! handleRebalancing "current" includeCommission
                    return true
                    
                | GetData ->
                    AnsiConsole.MarkupLine "\n[green]🌐 Fetching market data...[/]"
                    let logger: ILogger = LoggerContext.GetLoggerByName "Program"
                    logger.LogDebug("🔍 Starting data fetch operation...")
                    let! result = ConstructIndexCache true KSE30
                    logger.LogDebug("✅ Data fetch operation completed")
                    printErrors result
                    return true
                    
                | ShowPerformance ->
                    printfn "\n📈 Performance Metrics"
                    let stats = getPerformanceStats()
                    if List.isEmpty stats then
                        printfn "No performance metrics available yet."
                    else
                        let performanceTable = 
                            Table()
                                .Title("[bold green]Recent Performance Metrics[/]")
                                .AddColumns([|"Status"; "Operation"; "Duration"; "Time"|])
                                .Border(TableBorder.Rounded)
                                .BorderColor Color.Green
                        
                        stats 
                        |> List.take 10
                        |> List.iter (fun metric ->
                            let status = if metric.Success then "[green]✅[/]" else "[red]❌[/]"
                            let operation = metric.OperationName
                            let duration = metric.Duration.ToString()
                            let time = metric.Timestamp.ToString "HH:mm:ss"
                            performanceTable.AddRow(status, operation, duration, time) |> ignore)
                        
                        AnsiConsole.Write(performanceTable)
                    return true
                    
                | ClearCache ->
                    AnsiConsole.MarkupLine "\n[orange3]🗑️  Cache Management[/]"
                    let confirmed = getConfirmation "Are you sure you want to clear the cache?"
                    if confirmed then
                        cacheManager.Clear()
                        clearOldMetrics 24
                        AnsiConsole.MarkupLine "[green]✅ Cache and old metrics cleared successfully![/]"
                    else
                        AnsiConsole.MarkupLine "[red]❌ Cache clearing cancelled.[/]"
                    return true
                    
                | Exit ->
                    AnsiConsole.MarkupLine "\n[blue]👋 Thank you for using Portfolio Rebalancer![/]"
                    return false
                    
                | Invalid ->
                    AnsiConsole.MarkupLine "\n[red]❌ Invalid option. Please select a valid menu option.[/]"
                    return true
            with
            | ex ->
                AnsiConsole.MarkupLine(sprintf "\n[red]💥 An unexpected issue occurred: %s[/]" ex.Message)
                AnsiConsole.MarkupLine "[yellow]Please try again or contact support if the problem persists.[/]"
                return true
        }

        let rec runMenuLoop () = async {
            let! continueRunning = runMenu()
            if continueRunning then
                do! runMenuLoop()
        }
        do! runMenuLoop()
    with
    | ex ->
        AnsiConsole.MarkupLine(sprintf "\n[red]💥 Fatal issue: %s[/]" ex.Message)
        AnsiConsole.MarkupLine "[red]Application will exit. Please check your configuration and try again.[/]"
        Environment.Exit(1)
}

[<EntryPoint>]
let main argv = 
    Async.RunSynchronously(MainMenu())
    0