module Table

open Spectre.Console
open Types
open Helpers
open Db

// Remove the getSharesAndAvgPrice and any other ShareLot-related helpers. Remove any open or code referencing ShareLot. Ensure all code uses only PortfolioTransaction and related helpers. This will resolve the linter errors.

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
                    | n, [] -> ([] : (int * decimal * System.DateTime) list), accCGT // Selling more than you have, ignore
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
            indexTable.Title("=== Index ===")
            |> ignore
            indexTable.AddColumns("Symbol", "Name", "Price", "Weight")
            |> ignore
            ToStockTableRecord indexRecords individualStocks
            |> List.sortByDescending (fun r -> r.Weight)
            |> List.iter (fun r -> indexTable.AddRow(r.Symbol, r.Name, sprintf "%.4f" (decimal r.Price), toPercentage (decimal r.Weight)) |> ignore)
            AnsiConsole.Write(indexTable)
    let! transactionsDb = getPortfolioTransactionsAsync()
    let transactions: PortfolioTransaction list = transactionsDb |> List.map toDomain
    match transactions with
    | [] -> ()
    | _ ->
        let symbolList = transactions |> List.map (fun t -> t.Symbol) |> List.distinct
        let stockMap = individualStocks |> List.map (fun s -> s.Symbol, s) |> Map.ofList
        let portfolioSymbols = symbolList
        Table()
        |> fun portfolioTable ->
            portfolioTable.Title("=== Current Portfolio ===")
            |> ignore
            portfolioTable.AddColumns("Symbol", "Name", "Shares", "Avg Price", "Market Price", "Market Value", "Invested Value", "Gain/Loss", "Gain/Loss %", "Total Tax Paid")
            |> ignore
            portfolioSymbols
            |> List.iter (fun symbol ->
                let txns = transactions |> List.filter (fun t -> t.Symbol = symbol)
                let totalShares, avgPrice = getSharesAndAvgPriceFromTxns txns
                let marketPrice = Map.tryFind symbol stockMap |> Option.map (fun s -> s.Price) |> Option.defaultValue 0m
                let marketValue = decimal totalShares * marketPrice
                let investedValue = decimal totalShares * avgPrice
                let gainLoss = marketValue - investedValue
                let gainLossPct = if investedValue <> 0m then (gainLoss / investedValue) * 100m else 0m
                let totalTax = getTotalCGTFromTxns txns
                portfolioTable.AddRow(
                    symbol,
                    Map.tryFind symbol stockMap |> Option.map (fun s -> s.Name) |> Option.defaultValue "-",
                    string totalShares,
                    sprintf "%.4f" avgPrice,
                    sprintf "%.4f" marketPrice,
                    formatPKR marketValue,
                    formatPKR investedValue,
                    formatPKR gainLoss,
                    sprintf "%.2f%%" gainLossPct,
                    formatPKR totalTax
                ) |> ignore)
            AnsiConsole.Write(portfolioTable)
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

let showTransactionHistory () = async {
    let! txnsDb = getPortfolioTransactionsAsync ()
    let txns: PortfolioTransaction list = txnsDb |> List.map toDomain
    let table = Table()
    table.Title("=== Transaction History ===")
        .AddColumns([|"Symbol"; "Type"; "Shares"; "Price"; "Date"|]) |> ignore
    txns
    |> List.sortBy (fun t -> t.Date)
    |> List.iter (fun t ->
        table.AddRow(
            t.Symbol,
            (match t.TransactionType with Types.TransactionType.Buy -> "BUY" | Types.TransactionType.Sell -> "SELL"),
            string t.Shares,
            t.Price.ToString("F2"),
            t.Date.ToString("yyyy-MM-dd")
        ) |> ignore)
    AnsiConsole.Write(table)
    return ()
}

let showCurrentPortfolio () = async {
    let! txnsDb = getPortfolioTransactionsAsync ()
    let txns: PortfolioTransaction list = txnsDb |> List.map toDomain
    let lots =
        txns
        |> List.groupBy (fun t -> t.Symbol)
        |> List.map (fun (symbol, txns) ->
            let shares = txns |> List.filter (fun t -> t.TransactionType = Types.TransactionType.Buy) |> List.sumBy (fun t -> t.Shares)
            let avgPrice =
                let buys = txns |> List.filter (fun t -> t.TransactionType = Types.TransactionType.Buy)
                let totalCost = buys |> List.sumBy (fun t -> decimal t.Shares * t.Price)
                let totalShares = buys |> List.sumBy (fun t -> t.Shares)
                if totalShares > 0 then totalCost / decimal totalShares else 0m
            symbol, shares, avgPrice)
    let table = Table()
    table.Title("=== Current Portfolio ===")
        .AddColumns([|"Symbol"; "Shares"; "Avg Price"|]) |> ignore
    lots
    |> List.filter (fun (_, shares, _) -> shares > 0)
    |> List.iter (fun (symbol, shares, avgPrice) ->
        table.AddRow(symbol, string shares, avgPrice.ToString("F2")) |> ignore)
    AnsiConsole.Write(table)
    return ()
}