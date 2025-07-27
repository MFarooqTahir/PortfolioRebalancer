module Table

open Spectre.Console
open Types
open Helpers
open Db

let showSimpleCurrentPortfolio () = async {
    let! indexRecords = getIndexRecordsAsync()
    let! stockRecords = getStockRecordsAsync()
    let! records = getCurrentPortfolioAsync()
    
    // Display index table
    let indexTable =
        Table()
            .Title("=== Index Records ===")
            .AddColumns [|"Symbol"; "Weight %"; "Price"|]
    
    let priceMap = stockRecords |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    
    let indexRows = 
        indexRecords 
        |> List.sortByDescending (fun s -> s.Weight)
        |> List.map (fun s ->
            let price = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
            let weightPct = toPercentage s.Weight
            (s.Symbol, weightPct, formatPKR price))
    
    match indexRows with
    | [] -> indexTable.AddRow("[red]No data[/]", "-", "-") |> ignore
    | rows -> rows |> List.iter (fun (symbol, weight, price) -> indexTable.AddRow(symbol, weight, price) |> ignore)
    
    AnsiConsole.Write(indexTable)
    
    // Display current portfolio table
    let table =
        Table()
            .Title("=== My Current Portfolio ===")
            .AddColumns [|"Symbol"; "Shares"; "Buy Price"; "Current Price"; "Value"; "Gain/Loss"; "Gain/Loss %"|]
    
    let portfolioRows =
        records
        |> List.map (fun s ->
            let currentPrice = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
            let value = decimal s.Shares * currentPrice
            let cost = decimal s.Shares * s.Price
            let gainLoss = value - cost
            let gainLossPct = if cost <> 0m then gainLoss / cost else 0m
            (s, currentPrice, value, cost, gainLoss, gainLossPct))
        |> List.sortByDescending (fun (_,_,value,_,_,_) -> value)
        |> List.map (fun (s, currentPrice, value, cost, gainLoss, gainLossPct) ->
            (s.Symbol, string s.Shares, formatPKR s.Price, formatPKR currentPrice, 
             formatPKR value, formatPKR gainLoss, toPercentage gainLossPct))
    
    match portfolioRows with
    | [] -> table.AddRow("[red]No data[/]", "-", "-", "-", "-", "-", "-") |> ignore
    | rows -> rows |> List.iter (fun (symbol, shares, buyPrice, currentPrice, value, gainLoss, gainLossPct) -> 
        table.AddRow(symbol, shares, buyPrice, currentPrice, value, gainLoss, gainLossPct) |> ignore)
    
    AnsiConsole.Write(table)
    
    // Display summary
    let totalCost = records |> List.sumBy (fun s -> decimal s.Shares * s.Price)
    let totalValue = records |> List.sumBy (fun s -> 
        let currentPrice = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
        decimal s.Shares * currentPrice)
    let totalGainLoss = totalValue - totalCost
    let totalGainLossPct = if totalCost <> 0m then totalGainLoss / totalCost else 0m
    
    printfn "\nPortfolio Summary:"
    printfn "  Total Invested: %s" (formatPKR totalCost)
    printfn "  Current Value:  %s" (formatPKR totalValue)
    printfn "  Gain/Loss:      %s" (formatPKR totalGainLoss)
    printfn "  Gain/Loss %%:    %s" (toPercentage totalGainLossPct)
    
    return ()
}

let rebalanceIndexWithCash (index: IndexTableRecord list) (prices: StockRecord list) (cash: decimal) =
    let priceMap = prices |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let totalWeight = index |> List.sumBy (fun i -> i.Weight)
    
    let table =
        Table()
            .Title("=== Rebalance Index With Cash ===")
            .AddColumns [|"Symbol"; "Target Weight %"; "Price"; "Shares to Buy"; "Value"|]
    
    let plan =
        index
        |> List.choose (fun idx ->
            match Map.tryFind idx.Symbol priceMap with
            | Some price when price > 0m ->
                let targetValue = cash * (idx.Weight / totalWeight)
                let shares = int (targetValue / price)
                let value = decimal shares * price
                Some (idx.Symbol, idx.Weight * 100m, price, shares, value)
            | _ -> None)
    
    let totalUsed = plan |> List.sumBy (fun (_,_,_,_,v) -> v)
    
    plan |> List.iter (fun (symbol, weight, price, shares, value) ->
        table.AddRow(symbol, sprintf "%.2f" weight, price.ToString("F2"), shares.ToString(), value.ToString("F2")) |> ignore)
    
    table.AddRow("[bold]Total[/]", "", "", "", sprintf "%.2f" totalUsed) |> ignore
    AnsiConsole.Write table
    totalUsed

let showRebalanceIndexResult (results: {| Symbol: string; Shares: int; Value: decimal; Commission: decimal; Price: decimal; Weight: decimal |} list) (includeCommission: bool) (cash: decimal) =
    let totalValueAll = results |> List.sumBy (fun r -> r.Value)
    
    let table =
        if includeCommission then
            Table().Title("=== Rebalance Index With Cash (With Commission) ===")
                .AddColumns [|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"; "Commission"|]
        else
            Table().Title("=== Rebalance Index With Cash (No Commission) ===")
                .AddColumns [|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"|]
    
    let sorted = results |> List.sortByDescending (fun r -> r.Value)
    
    let rows = 
        sorted 
        |> List.map (fun r ->
            let currentWeight = if totalValueAll > 0m then r.Value / totalValueAll else 0m
            if includeCommission then
                [r.Symbol; toPercentage r.Weight; toPercentage currentWeight; formatPKR r.Price; string r.Shares; formatPKR r.Value; formatPKR r.Commission]
            else
                [r.Symbol; toPercentage r.Weight; toPercentage currentWeight; formatPKR r.Price; string r.Shares; formatPKR r.Value])
    
    rows |> List.iter (fun row -> table.AddRow(row |> Array.ofList) |> ignore)
    
    table.AddEmptyRow() |> ignore
    
    let totalValue = sorted |> List.sumBy (fun r -> r.Value)
    let totalCommission = sorted |> List.sumBy (fun r -> r.Commission)
    
    if includeCommission then
        table.AddRow("[bold]Total[/]", "", "", "", "", formatPKR totalValue, formatPKR totalCommission) |> ignore
    else
        table.AddRow("[bold]Total[/]", "", "", "", "", formatPKR totalValue) |> ignore
    
    AnsiConsole.Write table
    
    let spent = if includeCommission then sorted |> List.sumBy (fun r -> r.Value + r.Commission) else sorted |> List.sumBy (fun r -> r.Value)
    let remaining = cash - spent
    
    printfn "Total spent on stocks: %s" (formatPKR spent)
    printfn "Remaining cash: %s" (formatPKR remaining)
    if includeCommission then printfn "Total commission: %s" (formatPKR totalCommission)

let showRebalanceCurrentPortfolioResult
    (sells: {| Symbol: string; Shares: int; Price: decimal; Value: decimal; Commission: decimal |} list)
    (buys: {| Symbol: string; Shares: int; Price: decimal; Value: decimal; Commission: decimal |} list)
    (final: {| Symbol: string; Shares: int; Value: decimal; Commission: decimal; Price: decimal; Weight: decimal; FinalWeight: decimal |} list)
    (includeCommission: bool)
    (cash: decimal) =
    
    // Display sell orders
    let sortedSells = sells |> List.sortByDescending (fun r -> r.Value)
    let sellTable =
        if includeCommission then
            Table().Title("=== Sell Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"; "Commission"|]
        else
            Table().Title("=== Sell Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"|]
    
    let sellRows = 
        sortedSells 
        |> List.map (fun r ->
            if includeCommission then
                [r.Symbol; string r.Shares; formatPKR r.Price; formatPKR r.Value; formatPKR r.Commission]
            else
                [r.Symbol; string r.Shares; formatPKR r.Price; formatPKR r.Value])
    
    sellRows |> List.iter (fun row -> sellTable.AddRow(row |> Array.ofList) |> ignore)
    AnsiConsole.Write sellTable
    
    let totalSellValue = sortedSells |> List.sumBy (fun r -> r.Value)
    let totalSellCommission = sortedSells |> List.sumBy (fun r -> r.Commission)
    let cashAfterSelling = cash + totalSellValue - totalSellCommission
    
    printfn "Cash after selling: %s" (formatPKR cashAfterSelling)
    if includeCommission then printfn "Total sell commission: %s" (formatPKR totalSellCommission)
    
    // Display buy orders
    let sortedBuys = buys |> List.sortByDescending (fun r -> r.Value)
    let buyTable =
        if includeCommission then
            Table().Title("=== Buy Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"; "Commission"|]
        else
            Table().Title("=== Buy Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"|]
    
    let buyRows = 
        sortedBuys 
        |> List.map (fun r ->
            if includeCommission then
                [r.Symbol; string r.Shares; formatPKR r.Price; formatPKR r.Value; formatPKR r.Commission]
            else
                [r.Symbol; string r.Shares; formatPKR r.Price; formatPKR r.Value])
    
    buyRows |> List.iter (fun row -> buyTable.AddRow(row |> Array.ofList) |> ignore)
    AnsiConsole.Write buyTable
    
    let totalBuyValue = sortedBuys |> List.sumBy (fun r -> r.Value)
    let totalBuyCommission = sortedBuys |> List.sumBy (fun r -> r.Commission)
    let cashAfterBuying = cashAfterSelling - totalBuyValue - totalBuyCommission
    
    printfn "Cash after buying: %s" (formatPKR cashAfterBuying)
    if includeCommission then printfn "Total buy commission: %s" (formatPKR totalBuyCommission)
    
    // Display final portfolio
    let finalTable =
        if includeCommission then
            Table().Title("=== Final Portfolio After Rebalance ===")
                .AddColumns [|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"; "Commission"|]
        else
            Table().Title("=== Final Portfolio After Rebalance ===")
                .AddColumns [|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"|]
    
    let sortedFinal = final |> List.sortByDescending (fun r -> r.Value)
    
    let finalRows = 
        sortedFinal 
        |> List.map (fun r ->
            if includeCommission then
                [r.Symbol; toPercentage r.Weight; toPercentage r.FinalWeight; formatPKR r.Price; string r.Shares; formatPKR r.Value; formatPKR r.Commission]
            else
                [r.Symbol; toPercentage r.Weight; toPercentage r.FinalWeight; formatPKR r.Price; string r.Shares; formatPKR r.Value])
    
    finalRows |> List.iter (fun row -> finalTable.AddRow(row |> Array.ofList) |> ignore)
    
    finalTable.AddEmptyRow() |> ignore
    
    let totalValue = sortedFinal |> List.sumBy (fun r -> r.Value)
    let totalCommission = sortedFinal |> List.sumBy (fun r -> r.Commission)
    
    if includeCommission then
        finalTable.AddRow("[bold]Total[/]", "", "", "", "", formatPKR totalValue, formatPKR totalCommission) |> ignore
    else
        finalTable.AddRow("[bold]Total[/]", "", "", "", "", formatPKR totalValue) |> ignore
    
    AnsiConsole.Write finalTable
    
    let spent = if includeCommission then sortedFinal |> List.sumBy (fun r -> r.Value + r.Commission) else sortedFinal |> List.sumBy (fun r -> r.Value)
    let remaining = cashAfterBuying
    
    printfn "Total spent on stocks: %s" (formatPKR spent)
    printfn "Remaining cash: %s" (formatPKR remaining)
    if includeCommission then printfn "Total commission: %s" (formatPKR totalCommission)