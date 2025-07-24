module Table

open Spectre.Console
open Types
open Helpers
open Db

let getTotalCGTFromTxns (txns: PortfolioTransaction list) : decimal =
    let rec processTaxes (txns: PortfolioTransaction list) (lots: (int * decimal * System.DateTime) list) (accCGT: decimal) =
        match txns with
        | [] -> accCGT
        | t::rest ->
            match t.TransactionType with
            | TransactionType.Buy ->
                processTaxes rest ((t.Shares, t.Price, t.Date)::lots : (int * decimal * System.DateTime) list) accCGT
            | TransactionType.Sell ->
                let rec sell sharesToSell (lots: (int * decimal * System.DateTime) list) accCGT =
                    match sharesToSell, lots with
                    | 0, _ -> lots, accCGT
                    | _, [] -> ([] : (int * decimal * System.DateTime) list), accCGT // Selling more than you have, ignore
                    | n, (lotShares, lotPrice, lotDate)::restLots ->
                        let sellCount = min n lotShares
                        let gain = (t.Price - lotPrice) * decimal sellCount
                        let holdingPeriod = (t.Date - lotDate).TotalDays / 365.0
                        let cgtRate =
                            if holdingPeriod <= 1.0 then 0.15M
                            elif holdingPeriod <= 2.0 then 0.125M
                            elif holdingPeriod <= 3.0 then 0.10M
                            elif holdingPeriod <= 4.0 then 0.075M
                            elif holdingPeriod <= 5.0 then 0.05M
                            elif holdingPeriod <= 6.0 then 0.025M
                            else 0.0M
                        let cgt = max (gain * cgtRate) 0m
                        let newLot = if lotShares > sellCount then (lotShares - sellCount, lotPrice, lotDate)::restLots else restLots
                        sell (n - sellCount) (newLot : (int * decimal * System.DateTime) list) (accCGT + cgt)
                let newLots, newCGT = sell t.Shares lots accCGT
                processTaxes rest (newLots : (int * decimal * System.DateTime) list) newCGT
    processTaxes (List.sortBy (fun t -> t.Date) txns) ([] : (int * decimal * System.DateTime) list) 0m

let getSharesAndAvgPriceFromTxns (txns: PortfolioTransaction list) : int * decimal =
    let totalBuys = txns |> List.filter (fun t -> t.TransactionType = TransactionType.Buy)
    let totalShares = totalBuys |> List.sumBy (fun t -> t.Shares)
    let totalCost = totalBuys |> List.sumBy (fun t -> decimal t.Shares * t.Price)
    let avgPrice = if totalShares > 0 then totalCost / decimal totalShares else 0m
    totalShares, avgPrice

// === Main Portfolio Table Display ===
let ShowIndexAndPortfolio url = async {
    do! ensureTablesAsync()
    let! indexRecords = getIndexRecordsAsync()
    let! individualStocks = getStockRecordsAsync()
    match indexRecords with
    | [] -> ()
    | _ ->
        Table()
        |> fun indexTable ->
            indexTable.Title "=== Index ==="
            |> ignore
            indexTable.AddColumns("Symbol", "Name", "Price", "Weight")
            |> ignore
            ToStockTableRecord indexRecords individualStocks
            |> List.sortByDescending (fun r -> r.Weight)
            |> List.iter (fun r -> indexTable.AddRow(r.Symbol, r.Name, sprintf "%.4g" r.Price, toPercentage (decimal r.Weight)) |> ignore)
            AnsiConsole.Write(indexTable)
    let! transactionsDb = getPortfolioTransactionsAsync()
    match transactionsDb with
    | [] -> ()
    | _ ->
        let symbolList = transactionsDb |> List.map (fun t -> t.Symbol) |> List.distinct
        let stockMap = individualStocks |> List.map (fun s -> s.Symbol, s) |> Map.ofList
        let portfolioSymbols = symbolList
        Table()
        |> fun portfolioTable ->
            portfolioTable.Title "=== Current Portfolio ==="
            |> ignore
            portfolioTable.AddColumns("Symbol", "Name", "Shares", "Avg Price", "Market Price", "Market Value", "Invested Value", "Gain/Loss", "Gain/Loss %", "Total Tax Paid")
            |> ignore
            portfolioSymbols
            |> List.iter (fun symbol ->
                let txnsDb = transactionsDb |> List.filter (fun t -> t.Symbol = symbol)
                let totalShares, avgPrice = Db.getCurrentLotsAndAvgPrice txnsDb
                let marketPrice = Map.tryFind symbol stockMap |> Option.map (fun s -> s.Price) |> Option.defaultValue 0m
                let marketValue = decimal totalShares * marketPrice
                let investedValue = decimal totalShares * avgPrice
                let gainLoss = marketValue - investedValue
                let gainLossPct = if investedValue <> 0m then (gainLoss / investedValue) * 100m else 0m
                let totalTax = getTotalCGTFromTxns (txnsDb |> List.map Db.toDomain)
                portfolioTable.AddRow(
                    symbol,
                    Map.tryFind symbol stockMap |> Option.map (fun s -> s.Name) |> Option.defaultValue "-",
                    string totalShares,
                    sprintf "%.4g" avgPrice,
                    sprintf "%.4g" marketPrice,
                    formatPKR marketValue,
                    formatPKR investedValue,
                    formatPKR gainLoss,
                    sprintf "%.2f%%" gainLossPct,
                    formatPKR totalTax
                ) |> ignore)
            AnsiConsole.Write(portfolioTable)
            // --- Totals ---
            // Calculate stats using only current lots
            let lotsBySymbol =
                portfolioSymbols
                |> List.map (fun symbol ->
                    let txnsDb = transactionsDb |> List.filter (fun t -> t.Symbol = symbol)
                    let shares, avgPrice = Db.getCurrentLotsAndAvgPrice txnsDb
                    let marketPrice = Map.tryFind symbol stockMap |> Option.map (fun s -> s.Price) |> Option.defaultValue 0m
                    symbol, shares, avgPrice, marketPrice)
                |> List.filter (fun (_, shares, _, _) -> shares > 0)
            let totalInvested = lotsBySymbol |> List.sumBy (fun (_, shares, avgPrice, _) -> decimal shares * avgPrice)
            let totalCurrentValue = lotsBySymbol |> List.sumBy (fun (_, shares, _, marketPrice) -> decimal shares * marketPrice)
            let gainLoss = totalCurrentValue - totalInvested
            let gainLossPct = if totalInvested <> 0m then (gainLoss / totalInvested) * 100m else 0m
            let numHoldings = lotsBySymbol |> List.length
            AnsiConsole.MarkupLine $"[bold]Current Invested Value:[/] {formatPKR totalInvested}"
            AnsiConsole.MarkupLine $"[bold]Current Market Value:[/] {formatPKR totalCurrentValue}"
            AnsiConsole.MarkupLine $"[bold]Total Gain/Loss:[/] {formatPKR gainLoss} ({gainLossPct:N2}%%)"
            AnsiConsole.MarkupLine $"[bold]Number of Holdings:[/] {numHoldings}"
    return Ok ()
}
let ShowRebalanceResult portfolioPosition finalCash =
    let table =
            Table()
                .Title("=== Rebalanced Portfolio ===")
                .AddColumns("Symbol", "Name", "Weight", "Price", "Shares", "Value", "Commission", "Total Cost")

    portfolioPosition
        |> List.sortByDescending (fun p -> p.TargetWeight)
        |> List.iter (fun p ->
            table.AddRow(
                p.Symbol,
                p.Name,
                toPercentage p.TargetWeight,
                formatPKR p.CurrentPrice,
                p.ActualShares.ToString(),
                formatPKR p.ActualValue,
                formatPKR p.Commission,
                formatPKR p.TotalCost
            ) |> ignore
        )

    let totalUsed = portfolioPosition |> List.sumBy (fun p -> p.TotalCost)
    let totalCommission = portfolioPosition |> List.sumBy (fun p -> p.Commission)

    AnsiConsole.Write(table)
    AnsiConsole.MarkupLine $"[bold]Total Cash Used:[/] {formatPKR totalUsed}"
    AnsiConsole.MarkupLine $"[bold]Total Commission:[/] {formatPKR totalCommission}"
    AnsiConsole.MarkupLine $"[bold]Total Cash Left:[/] {formatPKR finalCash}"
        
    
let ShowFinalPortfolio finalPortfolio finalCash =
    let table =
        Table()
            .Title("=== Final Portfolio After Rebalancing ===")
            .AddColumns("Symbol", "Shares", "Unit Price", "Value")

    finalPortfolio
    |> List.sortByDescending (fun (_, _, _, value) -> value)
    |> List.iter (fun (symbol, shares, unitPrice, value) ->
        table.AddRow(
            symbol,
            shares.ToString(),
            formatPKR unitPrice,
            formatPKR value
        ) |> ignore
    )

    AnsiConsole.Write(table)
    AnsiConsole.MarkupLine $"[bold]Final Cash Left:[/] {formatPKR finalCash}"

let ShowRebalanceActions (title: string) (actions: (PortfolioPosition * int * decimal * decimal) list) (isSell: bool) =
    let table =
        Table()
            .Title(title)
            .AddColumns([|"Action"; "Symbol"; "Name"; "Shares"; "Price"; "Value"; "Commission"; "Total"|])
    actions
    |> List.iter (fun (pos, shares, value, commission) ->
        let action = if isSell then "Sell" else "Buy"
        table.AddRow(
            action,
            pos.Symbol,
            pos.Name,
            shares.ToString(),
            formatPKR pos.CurrentPrice,
            formatPKR value,
            formatPKR commission,
            formatPKR (if isSell then value - commission else value + commission)
        ) |> ignore)
    AnsiConsole.Write(table)

let ShowFinalPortfolioWithNames (finalPortfolio: (string * int * decimal * decimal * string) list) =
    let table =
        Table()
            .Title("=== Final Portfolio After Rebalancing ===")
            .AddColumns([|"Symbol"; "Name"; "Shares"; "Unit Price"; "Value"|])
    finalPortfolio
    |> List.iter (fun (symbol, shares, price, value, name) ->
        table.AddRow(
            symbol,
            name,
            shares.ToString(),
            formatPKR price,
            formatPKR value
        ) |> ignore)
    AnsiConsole.Write(table)

let ShowRebalanceActionsWithTax (title: string) (actions: (PortfolioPosition * int * decimal * decimal * decimal) list) (isSell: bool) (totalCommission: decimal) (totalTax: decimal) (totalValue: decimal) (cashAfter: decimal) =
    let table =
        Table()
            .Title(title)
            .AddColumns([|"Action"; "Symbol"; "Name"; "Shares"; "Price"; "Value"; "Commission"; "Tax"; "Total"|])
    actions
    |> List.iter (fun (pos, shares, value, commission, tax) ->
        let action = if isSell then "Sell" else "Buy"
        table.AddRow(
            action,
            pos.Symbol,
            pos.Name,
            shares.ToString(),
            formatPKR pos.CurrentPrice,
            formatPKR value,
            formatPKR commission,
            formatPKR tax,
            formatPKR (if isSell then value - commission - tax else value + commission)
        ) |> ignore)
    AnsiConsole.Write(table)
    if isSell then
        AnsiConsole.MarkupLine $"[bold]Total Sell Commission:[/] {formatPKR totalCommission}"
        AnsiConsole.MarkupLine $"[bold]Total Sell Tax:[/] {formatPKR totalTax}"
        AnsiConsole.MarkupLine $"[bold]Total Proceeds After Commission & Tax:[/] {formatPKR totalValue}"
        AnsiConsole.MarkupLine $"[bold]Cash After Sells:[/] {formatPKR cashAfter}"
    else
        AnsiConsole.MarkupLine $"[bold]Total Buy Commission:[/] {formatPKR totalCommission}"
        AnsiConsole.MarkupLine $"[bold]Total Cost of Buys (incl. commission):[/] {formatPKR totalValue}"
        AnsiConsole.MarkupLine $"[bold]Cash After Buys:[/] {formatPKR cashAfter}"

let ShowFinalPortfolioWithNamesAndCash (finalPortfolio: (string * string * int * decimal * decimal) list) (cashLeft: decimal) =
    let table =
        Table()
            .Title("=== Final Portfolio After Rebalancing ===")
            .AddColumns([|"Symbol"; "Name"; "Shares"; "Unit Price"; "Value"|])
    finalPortfolio
    |> List.iter (fun (symbol, name, shares, price, value) ->
        table.AddRow(
            symbol,
            name,
            shares.ToString(),
            formatPKR price,
            formatPKR value
        ) |> ignore)
    AnsiConsole.Write(table)
    AnsiConsole.MarkupLine $"[bold]Final Cash Left:[/] {formatPKR cashLeft}"

let showTransactionHistory () = async {
    let! txnsDb = getPortfolioTransactionsAsync ()
    let txns: PortfolioTransaction list = txnsDb |> List.map toDomain
    let table = 
        Table()
            .Title("=== Transaction History ===")
            .AddColumns("Symbol", "Type", "Shares", "Price", "Date")

    txns
    |> List.sortBy (fun t -> t.Date)
    |> List.iter (fun t ->
        table.AddRow(
            t.Symbol,
            (match t.TransactionType with TransactionType.Buy -> "BUY" | TransactionType.Sell -> "SELL"),
            string t.Shares,
            t.Price.ToString "F2",
            t.Date.ToString "yyyy-MM-dd"
        ) |> ignore)
    AnsiConsole.Write(table)
    return ()
}

let showCurrentPortfolio () = async {
    let! txnsDb = getPortfolioTransactionsAsync ()
    let lots =
        txnsDb
        |> List.groupBy (fun t -> t.Symbol)
        |> List.map (fun (symbol, txnsDb) ->
            let shares, avgPrice = Db.getCurrentLotsAndAvgPrice txnsDb
            symbol, shares, avgPrice)
    let table = 
        Table()
            .Title("=== Current Portfolio ===")
            .AddColumns [|"Symbol"; "Shares"; "Avg Price"|]
    lots
    |> List.filter (fun (_, shares, _) -> shares > 0)
    |> List.iter (fun (symbol, shares, avgPrice) ->
        table.AddRow(symbol, string shares, avgPrice.ToString("F2")) |> ignore)
    AnsiConsole.Write(table)
    // --- Totals ---
    let totalBuys =
        txnsDb
        |> List.filter (fun t -> t.TransactionType = "Buy")
        |> List.sumBy (fun t -> decimal t.Shares * t.Price)
    let totalSells =
        txnsDb
        |> List.filter (fun t -> t.TransactionType = "Sell")
        |> List.sumBy (fun t -> decimal t.Shares * t.Price)
    let netInvested = totalBuys - totalSells
    AnsiConsole.MarkupLine $"[bold]Total Invested (All Buys):[/] {formatPKR totalBuys}"
    AnsiConsole.MarkupLine $"[bold]Total Proceeds (All Sells):[/] {formatPKR totalSells}"
    AnsiConsole.MarkupLine $"[bold]Net Invested (Buys - Sells):[/] {formatPKR netInvested}"
    AnsiConsole.MarkupLine $"[grey]Current market value is not shown in this view. Use the main index/portfolio view for more info.[/]"
    return ()
}