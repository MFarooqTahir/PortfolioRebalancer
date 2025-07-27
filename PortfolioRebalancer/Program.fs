open System
open Types
open Rebalancer
open Helpers
open Scraper
open Table
open Db
open PortfolioImporter

// Menu options as discriminated union for type safety
type MenuOption = 
    | ShowPortfolio
    | ImportPortfolio
    | RebalanceIndex
    | RebalanceCurrent
    | GetData
    | Exit
    | Invalid

// Parse user input to menu option
let parseMenuOption (input: string) : MenuOption =
    match input.Trim() with
    | "1" -> ShowPortfolio
    | "2" -> ImportPortfolio
    | "3" -> RebalanceIndex
    | "4" -> RebalanceCurrent
    | "5" -> GetData
    | "0" -> Exit
    | _ -> Invalid

// Display menu
let displayMenu () =
    printfn "\nPortfolio Rebalancer"
    printfn "1. Show Index and Current Portfolio"
    printfn "2. Import Current Portfolio from JSON"
    printfn "3. Rebalance Index with Cash (optionally show commission)"
    printfn "4. Rebalance Current Portfolio with Cash (optionally show commission)"
    printfn "5. Get Index and Stock Data (scrape and save both to DB)"
    printfn "0. Exit"
    printf "Select an option: "

// Get cash amount from user
let getCashAmount () : decimal option =
    printf "Enter cash amount: "
    match Console.ReadLine() with
    | ValidDecimal cash when cash >= 0m -> Some cash
    | _ -> 
        printfn "Invalid cash amount."
        None

// Get commission preference from user
let getCommissionPreference () : bool =
    printf "Include commission? (y/n): "
    let commissionInput = Console.ReadLine()
    commissionInput.Trim().ToLower() = "y"

// Handle rebalancing with cash input
let handleRebalancing (kind: string) (includeCommission: bool) =
    match getCashAmount() with
    | Some cash ->
        async {
            let! index = getIndexRecordsAsync()
            let! prices = getStockRecordsAsync()
            
            if kind = "index" then
                rebalanceAndDisplay kind includeCommission cash index [] prices
            else
                let! current = getCurrentPortfolioAsync()
                rebalanceAndDisplay kind includeCommission cash index current prices
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
    | _ -> ()

// Handle menu option
let handleMenuOption (option: MenuOption) : Async<bool> =
    async {
        match option with
        | ShowPortfolio ->
            do! showSimpleCurrentPortfolio()
            return true
            
        | ImportPortfolio ->
            do! importCurrentPortfolioFromJson()
            return true
            
        | RebalanceIndex ->
            let includeCommission = getCommissionPreference()
            do! handleRebalancing "index" includeCommission
            return true
            
        | RebalanceCurrent ->
            let includeCommission = getCommissionPreference()
            do! handleRebalancing "current" includeCommission
            return true
            
        | GetData ->
            let! result = ConstructIndexCache true KSE30
            printErrors result
            return true
            
        | Exit ->
            printfn "Exiting."
            return false
            
        | Invalid ->
            printfn "Invalid option."
            return true
    }

let main() = async {
    do! ensureTablesAsync()
    
    let rec runMenu () = async {
        displayMenu()
        let input = Console.ReadLine()
        let option = parseMenuOption input
        let! continueRunning = handleMenuOption option
        if continueRunning then
            do! runMenu()
    }
    
    do! runMenu()
}

main() |> Async.RunSynchronously |> ignore