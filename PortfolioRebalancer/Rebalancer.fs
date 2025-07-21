module Rebalancer

    open Types
    open Helpers
    open Table

    let BalanceIndexAsync indexCacheFile stockDataCacheFile cash = async {
        let! indexRecords = CachedFileData.GetIndexRecords indexCacheFile
        let! individualStockData = CachedFileData.GetIndividualStockData stockDataCacheFile

        let stockData = ToStockTableRecord indexRecords individualStockData

        let initialPositions =
            stockData
            |> List.map (fun stock ->
                {
                    Symbol = stock.Symbol
                    Name = stock.Name
                    TargetWeight = stock.Weight
                    CurrentPrice = stock.Price
                    ActualShares = 0
                    ActualValue = 0m
                    Commission = 0m
                    TotalCost = 0m
                }
            )

        let rec invest positions remainingCash =
            let allocations =
                positions
                |> List.map (fun p -> p.Symbol, remainingCash * p.TargetWeight)
                |> Map.ofList

            let updated, spent =
                positions
                |> List.map (fun p ->
                    let allocation = allocations.[p.Symbol]
                    if p.CurrentPrice > 0m && allocation >= p.CurrentPrice then
                        let sharesToBuy = int (allocation / p.CurrentPrice)
                        if sharesToBuy > 0 then
                            let newShares = p.ActualShares + sharesToBuy
                            let newActualValue = decimal newShares * p.CurrentPrice
                            let newCommission = calculateCommission p.CurrentPrice newShares
                            let newTotalCost = newActualValue + newCommission
                            let costThisRound = decimal sharesToBuy * p.CurrentPrice + calculateCommission p.CurrentPrice sharesToBuy
                            { p with ActualShares = newShares; ActualValue = newActualValue; Commission = newCommission; TotalCost = newTotalCost }, costThisRound
                        else
                            p, 0m
                    else
                        p, 0m
                )
                |> List.unzip

            let totalSpent = spent |> List.sum

            if totalSpent = 0m || remainingCash < (positions |> List.minBy (fun p -> p.CurrentPrice)).CurrentPrice then
                updated, remainingCash
            else
                invest updated (remainingCash - totalSpent)

        let finalPositions, finalCash = invest initialPositions cash

        ShowRebalanceResultAsync finalPositions cash |> ignore
        return ()
    }