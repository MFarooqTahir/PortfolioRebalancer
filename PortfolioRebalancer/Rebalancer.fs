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
            })

    let rec invest positions remainingCash =
        let allocations : Map<string, decimal> =
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
                        { p with 
                            ActualShares = newShares
                            ActualValue = newActualValue
                            Commission = newCommission
                            TotalCost = newTotalCost }, costThisRound
                    else
                        p, 0m
                else
                    p, 0m)
            |> List.unzip

        let totalSpent = spent |> List.sum
        let minPrice = positions |> List.minBy (fun p -> p.CurrentPrice)

        if totalSpent = 0m || remainingCash < minPrice.CurrentPrice then
            updated, remainingCash
        else
            invest updated (remainingCash - totalSpent)

    let finalPositions, finalCash = invest initialPositions cash

    ShowRebalanceResultAsync finalPositions finalCash |> ignore
    return ()
}

let private aggregateActions actions =
    let getActionKey = function
        | RebalanceAction.Buy(symbol, _, _, _, _) -> ("Buy", symbol)
        | RebalanceAction.Sell(symbol, _, _, _, _) -> ("Sell", symbol)
        | RebalanceAction.Hold(symbol, _, _, _) -> ("Hold", symbol)

    let aggregateGroup (actionType, symbol) (grouped: RebalanceAction list) =
        match grouped.Head with
        | RebalanceAction.Buy(_, unitPrice, _, _, _) ->
            let totalShares = grouped |> List.sumBy (function RebalanceAction.Buy(_, _, shares, _, _) -> shares | _ -> 0)
            let totalValue = grouped |> List.sumBy (function RebalanceAction.Buy(_, _, _, value, _) -> value | _ -> 0m)
            let totalCommission = grouped |> List.sumBy (function RebalanceAction.Buy(_, _, _, _, commission) -> commission | _ -> 0m)
            RebalanceAction.Buy(symbol, unitPrice, totalShares, totalValue, totalCommission)
        | RebalanceAction.Sell(_, unitPrice, _, _, _) ->
            let totalShares = grouped |> List.sumBy (function RebalanceAction.Sell(_, _, shares, _, _) -> shares | _ -> 0)
            let totalValue = grouped |> List.sumBy (function RebalanceAction.Sell(_, _, _, value, _) -> value | _ -> 0m)
            let totalCommission = grouped |> List.sumBy (function RebalanceAction.Sell(_, _, _, _, commission) -> commission | _ -> 0m)
            RebalanceAction.Sell(symbol, unitPrice, totalShares, totalValue, totalCommission)
        | RebalanceAction.Hold(_, unitPrice, _, _) ->
            let totalShares = grouped |> List.sumBy (function RebalanceAction.Hold(_, _, shares, _) -> shares | _ -> 0)
            let totalValue = grouped |> List.sumBy (function RebalanceAction.Hold(_, _, _, value) -> value | _ -> 0m)
            RebalanceAction.Hold(symbol, unitPrice, totalShares, totalValue)
        | _ -> failwith "Unexpected action type"

    actions
    |> List.groupBy getActionKey
    |> List.map (fun (key, grouped) -> aggregateGroup key grouped)

let RebalanceCurrentPortfolioAsync indexCacheFile stockDataCacheFile currentPortfolioFile cash = async {
    let! indexRecords = CachedFileData.GetIndexRecords indexCacheFile
    let! individualStockData = CachedFileData.GetIndividualStockData stockDataCacheFile
    let! currentPortfolio = CachedFileData.GetCurrentPortfolioRecordsData currentPortfolioFile

    let priceMap : Map<string, decimal> = individualStockData |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let weightMap : Map<string, decimal> = indexRecords |> List.map (fun r -> r.Symbol, r.Weight) |> Map.ofList
    let currentMap : Map<string, _> = currentPortfolio |> List.map (fun p -> p.Symbol, p) |> Map.ofList

    let sellActions, cashAfterSells =
        currentPortfolio
        |> List.fold (fun (actions, availableCash) p ->
            let price = priceMap |> Map.tryFind p.Symbol |> Option.defaultValue 0m
            let weight = weightMap |> Map.tryFind p.Symbol |> Option.defaultValue 0m
            let targetValue = cash * weight
            let targetShares = if price > 0m then int (targetValue / price) else 0
            let commission qty = Helpers.calculateCommission price qty

            if weight = 0m then
                let value = decimal p.Shares * price
                let comm = commission p.Shares
                (RebalanceAction.Sell(p.Symbol, price, p.Shares, value, comm) :: actions, availableCash + value - comm)
            elif p.Shares > targetShares then
                let sharesToSell = p.Shares - targetShares
                let value = decimal sharesToSell * price
                let comm = commission sharesToSell
                (RebalanceAction.Sell(p.Symbol, price, sharesToSell, value, comm) :: actions, availableCash + value - comm)
            else
                (actions, availableCash)
        ) ([], cash)

    let totalSellCommission =
        sellActions |> List.sumBy (function
            | RebalanceAction.Sell(_, _, _, _, commission) -> commission
            | _ -> 0m)

    let buyActionsInit =
        indexRecords
        |> List.choose (fun idx ->
            let price = priceMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0m
            let weight = idx.Weight
            let targetValue = cashAfterSells * weight
            let targetShares = if price > 0m then int (targetValue / price) else 0
            let currentShares = currentMap |> Map.tryFind idx.Symbol |> Option.map (fun p -> p.Shares) |> Option.defaultValue 0
            let sharesToBuy = targetShares - currentShares
            let commission qty = Helpers.calculateCommission price qty

            if sharesToBuy > 0 then
                let value = decimal sharesToBuy * price
                let comm = commission sharesToBuy
                Some (RebalanceAction.Buy(idx.Symbol, price, sharesToBuy, value, comm))
            elif sharesToBuy = 0 && targetShares > 0 then
                let value = decimal currentShares * price
                Some (RebalanceAction.Hold(idx.Symbol, price, currentShares, value))
            else
                None)

    let totalBuyValueInit =
        buyActionsInit |> List.sumBy (function
            | RebalanceAction.Buy(_, _, _, value, _) -> value
            | _ -> 0m)
    let totalBuyCommissionInit =
        buyActionsInit |> List.sumBy (function
            | RebalanceAction.Buy(_, _, _, _, commission) -> commission
            | _ -> 0m)

    let mutable finalCash : decimal = cashAfterSells - totalBuyValueInit - totalBuyCommissionInit

    let mutable portfolioMap : Map<string, int> =
        currentPortfolio
        |> List.map (fun p -> p.Symbol, p.Shares)
        |> Map.ofList

    portfolioMap <-
        sellActions
        |> List.fold (fun acc action ->
            match action with
            | RebalanceAction.Sell(symbol, _, shares, _, _) ->
                let current = acc |> Map.tryFind symbol |> Option.defaultValue 0
                let newShares = current - shares
                if newShares > 0 then acc |> Map.add symbol newShares
                else acc |> Map.remove symbol
            | _ -> acc
        ) portfolioMap

    portfolioMap <-
        buyActionsInit
        |> List.fold (fun acc action ->
            match action with
            | RebalanceAction.Buy(symbol, _, shares, _, _) ->
                let current = acc |> Map.tryFind symbol |> Option.defaultValue 0
                acc |> Map.add symbol (current + shares)
            | _ -> acc
        ) portfolioMap

    let rec allocateExtraCash finalCash portfolioMap extraBuyActions =
        let allocations : Map<string, decimal> =
            indexRecords
            |> List.map (fun idx -> idx.Symbol, finalCash * idx.Weight)
            |> Map.ofList

        let buysThisRound =
            indexRecords
            |> List.choose (fun idx ->
                let symbol = idx.Symbol
                let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
                if price > 0m && finalCash >= price then
                    let allocation = allocations.[symbol]
                    let sharesToBuy = int (allocation / price)
                    if sharesToBuy > 0 then
                        let commission = Helpers.calculateCommission price sharesToBuy
                        let totalCost = decimal sharesToBuy * price + commission
                        if totalCost <= finalCash then
                            Some (symbol, sharesToBuy, price, commission, totalCost)
                        else None
                    else None
                else None)

        if buysThisRound.IsEmpty then
            (portfolioMap, extraBuyActions, finalCash)
        else
            let newPortfolioMap, newExtraBuyActions, newFinalCash =
                buysThisRound
                |> List.fold (fun (accPortfolio, accActions, accCash) (symbol, sharesToBuy, price, commission, totalCost) ->
                    let current = accPortfolio |> Map.tryFind symbol |> Option.defaultValue 0
                    let updatedPortfolio = accPortfolio |> Map.add symbol (current + sharesToBuy)
                    let updatedActions = RebalanceAction.Buy(symbol, price, sharesToBuy, decimal sharesToBuy * price, commission) :: accActions
                    let updatedCash = accCash - totalCost
                    (updatedPortfolio, updatedActions, updatedCash)
                ) (portfolioMap, extraBuyActions, finalCash)
            allocateExtraCash newFinalCash newPortfolioMap newExtraBuyActions

    let portfolioMap, extraBuyActions, finalCash =
        allocateExtraCash finalCash portfolioMap []

    let allBuyActions = buyActionsInit @ (List.rev extraBuyActions)
    let totalBuyValue =
        allBuyActions |> List.sumBy (function
            | RebalanceAction.Buy(_, _, _, value, _) -> value
            | _ -> 0m)
    let totalBuyCommission =
        allBuyActions |> List.sumBy (function
            | RebalanceAction.Buy(_, _, _, _, commission) -> commission
            | _ -> 0m)

    let aggregatedActions = aggregateActions (List.rev sellActions @ allBuyActions)
    Table.ShowPortfolioChanges aggregatedActions

    Table.ShowRebalanceStats cash cashAfterSells totalBuyValue totalBuyCommission totalSellCommission finalCash

    let finalPortfolio =
        portfolioMap
        |> Map.toList
        |> List.choose (fun (symbol, shares) ->
            let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
            if shares > 0 && price > 0m then
                Some (symbol, shares, price, decimal shares * price)
            else None)

    Table.ShowFinalPortfolio finalPortfolio finalCash

    ()
}