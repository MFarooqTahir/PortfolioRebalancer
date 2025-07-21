module Table

open System.IO
open Spectre.Console
open Types
open Helpers
open CachedFileData
open Scraper

let ShowIndexAsync url indexCacheFile stockDataCacheFile currentPortfolioFile = async {
    if not (File.Exists(indexCacheFile)) then
        let! constructResult = ConstructIndexCache url indexCacheFile stockDataCacheFile currentPortfolioFile true
        match constructResult with
            | Error err -> 
                printf "Error constructing cache: %A" err
                return ()
            | Ok () -> ()
        
    let table = 
        Table()
            .Title("=== Index ===")
            .AddColumns("Symbol", "Name", "Price", "Weight")

    let! indexRecords = GetIndexRecords indexCacheFile
    let! individualStocks = GetIndividualStockData stockDataCacheFile

    ToStockTableRecord indexRecords individualStocks
        |> List.sortByDescending _.Weight
        |> List.iter (fun r -> table.AddRow(r.Symbol, r.Name, string r.Price, toPercentage r.Weight) |> ignore)

    AnsiConsole.Write(table)
    
    return Ok ()
}

let ShowRebalanceResultAsync portfolioPosition finalCash =
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
        
    
let ShowPortfolioChanges rebalanceActions =
    let buyActions, sellActions, holdActions =
        rebalanceActions
        |> List.fold (fun (buys, sells, holds) action ->
            match action with
            | Buy _ -> action :: buys, sells, holds
            | Sell _ -> buys, action :: sells, holds
            | Hold _ -> buys, sells, action :: holds
        ) ([], [], [])

    let addActionRow (table: Table) (action: RebalanceAction) =
        match action with
        | Buy(symbol, unitPrice, shares, value, commission) ->
            table.AddRow(symbol, Helpers.formatPKR unitPrice, shares.ToString(), Helpers.formatPKR value, Helpers.formatPKR commission) |> ignore
        | Sell(symbol, unitPrice, shares, value, commission) ->
            table.AddRow(symbol, Helpers.formatPKR unitPrice, shares.ToString(), Helpers.formatPKR value, Helpers.formatPKR commission) |> ignore
        | Hold(symbol, unitPrice, shares, value) ->
            table.AddRow(symbol, Helpers.formatPKR unitPrice, shares.ToString(), Helpers.formatPKR value, "-") |> ignore

    let renderTable (title: string) actions =
        let table =
            Table()
                .Title(title)
                .AddColumns("Symbol", "Unit Price", "Shares", "Value", "Commission")
        actions
        |> List.sortByDescending (function
            | Buy(_, _, _, _, value) -> value
            | Sell(_, _, _, _, value) -> value
            | Hold(_, _, _, value) -> value)
        |> List.iter (addActionRow table)
        AnsiConsole.Write(table)

    if buyActions.Length > 0 then renderTable "=== Buy Actions ===" buyActions
    if sellActions.Length > 0 then renderTable "=== Sell Actions ===" sellActions
    if holdActions.Length > 0 then renderTable "=== Hold Actions ===" holdActions

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

let ShowRebalanceStats
    (initialCash: decimal)
    (cashAfterSells: decimal)
    (totalBuyValue: decimal)
    (totalBuyCommission: decimal)
    (totalSellCommission: decimal)
    (finalCash: decimal) =

    AnsiConsole.MarkupLine $"[bold]Initial Cash:[/] {formatPKR initialCash}"
    AnsiConsole.MarkupLine $"[bold]Cash After Selling (Before Buying):[/] {formatPKR cashAfterSells}"
    AnsiConsole.MarkupLine $"[bold]Total Cash Spent on Buying:[/] {formatPKR totalBuyValue}"
    AnsiConsole.MarkupLine $"[bold]Total Commission on Buying:[/] {formatPKR totalBuyCommission}"
    AnsiConsole.MarkupLine $"[bold]Total Commission on Selling:[/] {formatPKR totalSellCommission}"
    AnsiConsole.MarkupLine $"[bold]Final Cash Left:[/] {formatPKR finalCash}"