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

    let ShowRebalanceResultAsync portfolioPosition cash =
        let table =
                Table()
                    .Title("=== Rebalanced Portfolio ===")
                    .AddColumns("Symbol", "Name", "Price", "Weight", "Shares", "Value", "Commission", "Total Cost")

        portfolioPosition
            |> List.sortByDescending (fun p -> p.TargetWeight)
            |> List.iter (fun p ->
                table.AddRow(
                    p.Symbol,
                    p.Name,
                    Helpers.formatPKR p.CurrentPrice,
                    Helpers.toPercentage p.TargetWeight,
                    p.ActualShares.ToString(),
                    Helpers.formatPKR p.ActualValue,
                    Helpers.formatPKR p.Commission,
                    Helpers.formatPKR p.TotalCost
                ) |> ignore
            )

        let totalUsed = portfolioPosition |> List.sumBy (fun p -> p.TotalCost)
        let totalCommission = portfolioPosition |> List.sumBy (fun p -> p.Commission)
        let cashLeft = cash - totalUsed

        AnsiConsole.Write(table)
        AnsiConsole.MarkupLine $"[bold]Total Cash Used:[/] {Helpers.formatPKR totalUsed}"
        AnsiConsole.MarkupLine $"[bold]Total Commission:[/] {Helpers.formatPKR totalCommission}"
        AnsiConsole.MarkupLine $"[bold]Total Cash Left:[/] {Helpers.formatPKR cashLeft}"
        ()
    
