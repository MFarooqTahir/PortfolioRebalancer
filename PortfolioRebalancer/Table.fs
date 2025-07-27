module Table

open Spectre.Console
open Types
open Helpers
open Db

let showSimpleCurrentPortfolio () = async {
    let! indexRecords = getIndexRecordsAsync()
    let! stockRecords = getStockRecordsAsync()
    let! records = getCurrentPortfolioAsync()
    let indexTable =
        Table()
            .Title("=== Index Records ===")
            .AddColumns [|"Symbol"; "Weight %"; "Price"|]
    let priceMap = stockRecords |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    match indexRecords |> List.sortByDescending (fun s -> s.Weight) with
    | [] -> indexTable.AddRow("[red]No data[/]", "-", "-") |> ignore
    | xs ->
        xs |> List.iter (fun s ->
            let price = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
            let weightPct = toPercentage s.Weight
            indexTable.AddRow(s.Symbol, weightPct, formatPKR price) |> ignore)
    AnsiConsole.Write(indexTable)
    let table =
        Table()
            .Title("=== My Current Portfolio ===")
            .AddColumns [|"Symbol"; "Shares"; "Buy Price"; "Current Price"; "Value"; "Gain/Loss"; "Gain/Loss %"|]
    let sortedRecords =
        records
        |> List.map (fun s ->
            let currentPrice = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
            let value = decimal s.Shares * currentPrice
            let cost = decimal s.Shares * s.Price
            let gainLoss = value - cost
            let gainLossPct = if cost <> 0m then gainLoss / cost else 0m
            s, currentPrice, value, cost, gainLoss, gainLossPct)
        |> List.sortByDescending (fun (_,_,value,_,_,_) -> value)
    match sortedRecords with
    | [] -> table.AddRow("[red]No data[/]", "-", "-", "-", "-", "-", "-") |> ignore
    | xs ->
        xs |> List.iter (fun (s, currentPrice, value, cost, gainLoss, gainLossPct) ->
            table.AddRow(
                s.Symbol,
                string s.Shares,
                formatPKR s.Price,
                formatPKR currentPrice,
                formatPKR value,
                formatPKR gainLoss,
                toPercentage gainLossPct) |> ignore)
    AnsiConsole.Write(table)
    let totalCost = sortedRecords |> List.sumBy (fun (_,_,_,cost,_,_) -> cost)
    let totalValue = sortedRecords |> List.sumBy (fun (_,_,value,_,_,_) -> value)
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
    let mutable totalValue = 0m
    let mutable totalCommission = 0m
    let sorted = results |> List.sortByDescending (fun r -> r.Value)
    sorted |> List.iter (fun r ->
        totalValue <- totalValue + r.Value
        totalCommission <- totalCommission + r.Commission
        let currentWeight = if totalValueAll > 0m then r.Value / totalValueAll else 0m
        if includeCommission then
            table.AddRow(r.Symbol, toPercentage r.Weight, toPercentage currentWeight, formatPKR r.Price, string r.Shares, formatPKR r.Value, formatPKR r.Commission) |> ignore
        else
            table.AddRow(r.Symbol, toPercentage r.Weight, toPercentage currentWeight, formatPKR r.Price, string r.Shares, formatPKR r.Value) |> ignore)
    table.AddEmptyRow() |> ignore
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
    let sortedSells = sells |> List.sortByDescending (fun r -> r.Value)
    let sellTable =
        if includeCommission then
            Table().Title("=== Sell Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"; "Commission"|]
        else
            Table().Title("=== Sell Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"|]
    sortedSells |> List.iter (fun r ->
        if includeCommission then
            sellTable.AddRow(r.Symbol, string r.Shares, formatPKR r.Price, formatPKR r.Value, formatPKR r.Commission) |> ignore
        else
            sellTable.AddRow(r.Symbol, string r.Shares, formatPKR r.Price, formatPKR r.Value) |> ignore)
    AnsiConsole.Write sellTable
    let totalSellValue = sortedSells |> List.sumBy (fun r -> r.Value)
    let totalSellCommission = sortedSells |> List.sumBy (fun r -> r.Commission)
    let cashAfterSelling = cash + totalSellValue - totalSellCommission
    printfn "Cash after selling: %s" (formatPKR cashAfterSelling)
    if includeCommission then printfn "Total sell commission: %s" (formatPKR totalSellCommission)
    let sortedBuys = buys |> List.sortByDescending (fun r -> r.Value)
    let buyTable =
        if includeCommission then
            Table().Title("=== Buy Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"; "Commission"|]
        else
            Table().Title("=== Buy Orders ===")
                .AddColumns [|"Symbol"; "Shares"; "Price"; "Value"|]
    sortedBuys |> List.iter (fun r ->
        if includeCommission then
            buyTable.AddRow(r.Symbol, string r.Shares, formatPKR r.Price, formatPKR r.Value, formatPKR r.Commission) |> ignore
        else
            buyTable.AddRow(r.Symbol, string r.Shares, formatPKR r.Price, formatPKR r.Value) |> ignore)
    AnsiConsole.Write buyTable
    let totalBuyValue = sortedBuys |> List.sumBy (fun r -> r.Value)
    let totalBuyCommission = sortedBuys |> List.sumBy (fun r -> r.Commission)
    let cashAfterBuying = cashAfterSelling - totalBuyValue - totalBuyCommission
    printfn "Cash after buying: %s" (formatPKR cashAfterBuying)
    if includeCommission then printfn "Total buy commission: %s" (formatPKR totalBuyCommission)
    let finalTable =
        if includeCommission then
            Table().Title("=== Final Portfolio After Rebalance ===")
                .AddColumns [|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"; "Commission"|]
        else
            Table().Title("=== Final Portfolio After Rebalance ===")
                .AddColumns [|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"|]
    let mutable totalValue = 0m
    let mutable totalCommission = 0m
    let sortedFinal = final |> List.sortByDescending (fun r -> r.Value)
    sortedFinal |> List.iter (fun r ->
        totalValue <- totalValue + r.Value
        totalCommission <- totalCommission + r.Commission
        if includeCommission then
            finalTable.AddRow(r.Symbol, toPercentage r.Weight, toPercentage r.FinalWeight, formatPKR r.Price, string r.Shares, formatPKR r.Value, formatPKR r.Commission) |> ignore
        else
            finalTable.AddRow(r.Symbol, toPercentage r.Weight, toPercentage r.FinalWeight, formatPKR r.Price, string r.Shares, formatPKR r.Value) |> ignore)
    finalTable.AddEmptyRow() |> ignore
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