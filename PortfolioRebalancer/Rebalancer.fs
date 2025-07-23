module Rebalancer

open Types
open Helpers
open Table

let BalanceIndexAsync indexCacheFile stockDataCacheFile cash reinvestRemainingCash = async {
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

    let targetValues = 
        initialPositions
        |> List.map (fun p -> p.Symbol, cash * p.TargetWeight)
        |> Map.ofList

    let rec invest positions remainingCash iterationCount =
        if iterationCount > 100 || remainingCash <= 0m then
            positions, remainingCash
        else
            let valueGaps =
                positions
                |> List.map (fun p ->
                    let targetValue = targetValues.[p.Symbol]
                    let currentValue = p.ActualValue
                    let gap = max 0m (targetValue - currentValue)
                    p.Symbol, gap)
                |> Map.ofList

            let totalGap = valueGaps |> Map.values |> Seq.sum

            if totalGap = 0m then
                positions, remainingCash
            else
                let purchaseCandidates =
                    positions
                    |> List.choose (fun p ->
                        let gap = valueGaps.[p.Symbol]
                        if p.CurrentPrice > 0m && gap > 0m then
                            let allocation = remainingCash * (gap / totalGap)
                            let maxSharesFromAllocation = int (allocation / p.CurrentPrice)
                            let maxSharesFromGap = int (gap / p.CurrentPrice)
                            let sharesToBuy = min maxSharesFromAllocation maxSharesFromGap
                            
                            if sharesToBuy > 0 then
                                let shareValue = decimal sharesToBuy * p.CurrentPrice
                                let commission = calculateCommission p.CurrentPrice sharesToBuy
                                let totalCost = shareValue + commission
                                
                                if totalCost <= remainingCash then
                                    Some (p, sharesToBuy, shareValue, commission, totalCost)
                                else
                                    None
                            else
                                None
                        else
                            None)

                if purchaseCandidates.IsEmpty then
                    positions, remainingCash
                else
                    let updated, totalSpent =
                        purchaseCandidates
                        |> List.fold (fun (accPositions, accSpent) (p, sharesToBuy, _, _, totalCost) ->
                            let newShares = p.ActualShares + sharesToBuy
                            let newActualValue = decimal newShares * p.CurrentPrice
                            let newCommission = calculateCommission p.CurrentPrice newShares
                            let newTotalCost = newActualValue + newCommission
                            
                            let updatedPosition = 
                                { p with 
                                    ActualShares = newShares
                                    ActualValue = newActualValue
                                    Commission = newCommission
                                    TotalCost = newTotalCost }
                            
                            let updatedPositions = accPositions |> List.map (fun pos -> 
                                if pos.Symbol = p.Symbol then updatedPosition else pos)
                            
                            updatedPositions, accSpent + totalCost
                        ) (positions, 0m)

                    invest updated (remainingCash - totalSpent) (iterationCount + 1)

    let positionsAfterBalancing, cashAfterBalancing = invest initialPositions cash 0

    let finalPositions, finalCash =
        if reinvestRemainingCash && cashAfterBalancing > 0m then
            let rec buyIndividualShares remainingCash currentPositions iterationCount =
                if iterationCount > 20 || remainingCash <= 0m then
                    currentPositions, remainingCash
                else
                    let candidates =
                        currentPositions
                        |> List.choose (fun p ->
                            let targetValue = targetValues.[p.Symbol]
                            let currentValue = p.ActualValue
                            let relativeGap = if targetValue > 0m then (targetValue - currentValue) / targetValue else 0m
                            
                            if p.CurrentPrice > 0m && relativeGap > 0.01m then
                                let commission = calculateCommission p.CurrentPrice 1
                                let totalCost = p.CurrentPrice + commission
                                
                                if totalCost <= remainingCash then
                                    Some (p, relativeGap, totalCost)
                                else
                                    None
                            else
                                None)
                        |> List.sortByDescending (fun (_, gap, _) -> gap)

                    match candidates with
                    | [] -> currentPositions, remainingCash
                    | (bestPosition, _, totalCost) :: _ ->
                        let newShares = bestPosition.ActualShares + 1
                        let newActualValue = decimal newShares * bestPosition.CurrentPrice
                        let newCommission = calculateCommission bestPosition.CurrentPrice newShares
                        let newTotalCost = newActualValue + newCommission
                        
                        let updatedPosition = 
                            { bestPosition with 
                                ActualShares = newShares
                                ActualValue = newActualValue
                                Commission = newCommission
                                TotalCost = newTotalCost }
                        
                        let updatedPositions = 
                            currentPositions 
                            |> List.map (fun pos -> 
                                if pos.Symbol = bestPosition.Symbol then updatedPosition else pos)
                        
                        buyIndividualShares (remainingCash - totalCost) updatedPositions (iterationCount + 1)

            let positions, cash = buyIndividualShares cashAfterBalancing positionsAfterBalancing 0
            
            if cash < cashAfterBalancing then
                printfn "Note: Remaining cash was reinvested. This may reduce balance accuracy to minimize unused cash."
            
            positions, cash
        else
            positionsAfterBalancing, cashAfterBalancing

    ShowRebalanceResult finalPositions finalCash |> ignore
    return ()
}

let private aggregateActions actions =
    let getActionKey = function
        | Buy(symbol, _, _, _, _) -> "Buy", symbol
        | Sell(symbol, _, _, _, _) -> "Sell", symbol
        | Hold(symbol, _, _, _) -> "Hold", symbol

    let aggregateGroup (actionType, symbol) (grouped: RebalanceAction list) =
        match grouped.Head with
        | Buy(_, unitPrice, _, _, _) ->
            let totalShares = grouped |> List.sumBy (function Buy(_, _, shares, _, _) -> shares | _ -> 0)
            let totalValue = grouped |> List.sumBy (function Buy(_, _, _, value, _) -> value | _ -> 0m)
            let totalCommission = grouped |> List.sumBy (function Buy(_, _, _, _, commission) -> commission | _ -> 0m)
            Buy(symbol, unitPrice, totalShares, totalValue, totalCommission)
        | Sell(_, unitPrice, _, _, _) ->
            let totalShares = grouped |> List.sumBy (function Sell(_, _, shares, _, _) -> shares | _ -> 0)
            let totalValue = grouped |> List.sumBy (function Sell(_, _, _, value, _) -> value | _ -> 0m)
            let totalCommission = grouped |> List.sumBy (function Sell(_, _, _, _, commission) -> commission | _ -> 0m)
            Sell(symbol, unitPrice, totalShares, totalValue, totalCommission)
        | Hold(_, unitPrice, _, _) ->
            let totalShares = grouped |> List.sumBy (function Hold(_, _, shares, _) -> shares | _ -> 0)
            let totalValue = grouped |> List.sumBy (function Hold(_, _, _, value) -> value | _ -> 0m)
            Hold(symbol, unitPrice, totalShares, totalValue)

    actions
    |> List.groupBy getActionKey
    |> List.map (fun (key, grouped) -> aggregateGroup key grouped)

let RebalanceCurrentPortfolioAsync indexCacheFile stockDataCacheFile currentPortfolioFile cash reinvestRemainingCash = async {
    let! indexRecords = CachedFileData.GetIndexRecords indexCacheFile
    let! individualStockData = CachedFileData.GetIndividualStockData stockDataCacheFile
    let! currentPortfolio = CachedFileData.GetCurrentPortfolioRecordsData currentPortfolioFile

    let priceMap : Map<string, decimal> = individualStockData |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let weightMap : Map<string, decimal> = indexRecords |> List.map (fun r -> r.Symbol, r.Weight) |> Map.ofList
    let currentMap : Map<string, _> = currentPortfolio |> List.map (fun p -> p.Symbol, p) |> Map.ofList

    let currentPortfolioValue = 
        currentPortfolio 
        |> List.sumBy (fun p -> 
            let price = priceMap |> Map.tryFind p.Symbol |> Option.defaultValue 0m
            decimal p.Shares * price)

    let targetPortfolioValue = currentPortfolioValue + cash

    let getTargetShares symbol =
        let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
        let weight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m
        if price > 0m then 
            int (targetPortfolioValue * weight / price)
        else 
            0

    let getTargetValue symbol =
        let weight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m
        targetPortfolioValue * weight

    let sellActions, cashAfterSells =
        currentPortfolio
        |> List.fold (fun (actions, availableCash) p ->
            let price = priceMap |> Map.tryFind p.Symbol |> Option.defaultValue 0m
            let weight = weightMap |> Map.tryFind p.Symbol |> Option.defaultValue 0m
            let targetShares = getTargetShares p.Symbol

            if weight = 0m then
                if p.Shares > 0 && price > 0m then
                    let value = decimal p.Shares * price
                    let commission = calculateCommission price p.Shares
                    Sell(p.Symbol, price, p.Shares, value, commission) :: actions, availableCash + value - commission
                else
                    actions, availableCash
            elif p.Shares > targetShares then
                let sharesToSell = p.Shares - targetShares
                if sharesToSell > 0 && price > 0m then
                    let value = decimal sharesToSell * price
                    let commission = calculateCommission price sharesToSell
                    Sell(p.Symbol, price, sharesToSell, value, commission) :: actions, availableCash + value - commission
                else
                    actions, availableCash
            else
                (actions, availableCash)
        ) ([], cash)

    let buyActionsInit =
        indexRecords
        |> List.choose (fun idx ->
            let price = priceMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0m
            let targetShares = getTargetShares idx.Symbol
            let currentShares = currentMap |> Map.tryFind idx.Symbol |> Option.map (fun p -> p.Shares) |> Option.defaultValue 0
            let sharesToBuy = targetShares - currentShares

            if sharesToBuy > 0 && price > 0m then
                let value = decimal sharesToBuy * price
                let commission = calculateCommission price sharesToBuy
                Some (Buy(idx.Symbol, price, sharesToBuy, value, commission))
            elif sharesToBuy = 0 && targetShares > 0 then
                let value = decimal currentShares * price
                Some (Hold(idx.Symbol, price, currentShares, value))
            else
                None)

    let totalBuyValueInit =
        buyActionsInit |> List.sumBy (function
            | Buy(_, _, _, value, _) -> value
            | _ -> 0m)
    let totalBuyCommissionInit =
        buyActionsInit |> List.sumBy (function
            | Buy(_, _, _, _, commission) -> commission
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
            | Sell(symbol, _, shares, _, _) ->
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
            | Buy(symbol, _, shares, _, _) ->
                let current = acc |> Map.tryFind symbol |> Option.defaultValue 0
                acc |> Map.add symbol (current + shares)
            | _ -> acc
        ) portfolioMap

    let rec allocateRemainingCash remainingCash portfolioMap extraBuyActions iterationCount =
        if iterationCount > 50 || remainingCash <= 0m then
            portfolioMap, extraBuyActions, remainingCash
        else
            let valueGaps =
                indexRecords
                |> List.choose (fun idx ->
                    let price = priceMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0m
                    if price > 0m then
                        let currentShares = portfolioMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0
                        let currentValue = decimal currentShares * price
                        let targetValue = getTargetValue idx.Symbol
                        let gap = max 0m (targetValue - currentValue)
                        
                        if gap >= price then
                            Some (idx.Symbol, gap, price)
                        else
                            None
                    else
                        None)

            if valueGaps.IsEmpty then
                portfolioMap, extraBuyActions, remainingCash
            else
                let totalGap = valueGaps |> List.sumBy (fun (_, gap, _) -> gap)
                
                let purchaseCandidates =
                    valueGaps
                    |> List.choose (fun (symbol, gap, price) ->
                        let proportionalAllocation = remainingCash * (gap / totalGap)
                        let maxAllocationFromGap = gap
                        let actualAllocation = min proportionalAllocation maxAllocationFromGap
                        let sharesToBuy = int (actualAllocation / price)
                        
                        if sharesToBuy > 0 then
                            let shareValue = decimal sharesToBuy * price
                            let commission = calculateCommission price sharesToBuy
                            let totalCost = shareValue + commission
                            
                            if totalCost <= remainingCash then
                                Some (symbol, sharesToBuy, price, shareValue, commission, totalCost)
                            else
                                let rec tryFewerShares shares =
                                    if shares <= 0 then None
                                    else
                                        let testValue = decimal shares * price
                                        let testCommission = calculateCommission price shares
                                        let testTotalCost = testValue + testCommission
                                        if testTotalCost <= remainingCash then
                                            Some (symbol, shares, price, testValue, testCommission, testTotalCost)
                                        else
                                            tryFewerShares (shares - 1)
                                tryFewerShares (sharesToBuy - 1)
                        else
                            None)

                if purchaseCandidates.IsEmpty then
                    portfolioMap, extraBuyActions, remainingCash
                else
                    let newPortfolioMap, newExtraBuyActions, newRemainingCash =
                        purchaseCandidates
                        |> List.fold (fun (accPortfolio, accActions, accCash) (symbol, sharesToBuy, price, shareValue, commission, totalCost) ->
                            let current = accPortfolio |> Map.tryFind symbol |> Option.defaultValue 0
                            let updatedPortfolio = accPortfolio |> Map.add symbol (current + sharesToBuy)
                            let updatedActions = Buy(symbol, price, sharesToBuy, shareValue, commission) :: accActions
                            let updatedCash = accCash - totalCost
                            updatedPortfolio, updatedActions, updatedCash
                        ) (portfolioMap, extraBuyActions, remainingCash)
                    
                    let progressMade = newRemainingCash < remainingCash
                    if progressMade then
                        allocateRemainingCash newRemainingCash newPortfolioMap newExtraBuyActions (iterationCount + 1)
                    else
                        newPortfolioMap, newExtraBuyActions, newRemainingCash

    let portfolioMap, extraBuyActions, cashAfterSmartAllocation =
        allocateRemainingCash finalCash portfolioMap [] 0

    let portfolioMapFinal, finalExtraBuyActions, finalCash =
        if reinvestRemainingCash && cashAfterSmartAllocation > 0m then
            let rec buyIndividualShares remainingCash currentPortfolioMap additionalActions iterationCount =
                if iterationCount > 20 || remainingCash <= 0m then
                    currentPortfolioMap, additionalActions, remainingCash
                else
                    let candidates =
                        indexRecords
                        |> List.choose (fun idx ->
                            let price = priceMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0m
                            if price > 0m then
                                let currentShares = currentPortfolioMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0
                                let currentValue = decimal currentShares * price
                                let targetValue = getTargetValue idx.Symbol
                                let relativeGap = if targetValue > 0m then (targetValue - currentValue) / targetValue else 0m
                                
                                let commission = calculateCommission price 1
                                let totalCost = price + commission
                                
                                if totalCost <= remainingCash && relativeGap > 0.01m then
                                    Some (idx.Symbol, relativeGap, price, commission, totalCost)
                                else
                                    None
                            else
                                None)
                        |> List.sortByDescending (fun (_, gap, _, _, _) -> gap)

                    match candidates with
                    | [] -> (currentPortfolioMap, additionalActions, remainingCash)
                    | (symbol, _, price, commission, totalCost) :: _ ->
                        let updatedPortfolio = 
                            let current = currentPortfolioMap |> Map.tryFind symbol |> Option.defaultValue 0
                            currentPortfolioMap |> Map.add symbol (current + 1)
                        let newAction = Buy(symbol, price, 1, price, commission)
                        let newRemainingCash = remainingCash - totalCost
                        
                        buyIndividualShares newRemainingCash updatedPortfolio (newAction :: additionalActions) (iterationCount + 1)

            buyIndividualShares cashAfterSmartAllocation portfolioMap [] 0
        else
            portfolioMap, [], cashAfterSmartAllocation

    let allBuyActions = buyActionsInit @ (List.rev extraBuyActions) @ (List.rev finalExtraBuyActions)
    let totalBuyValue =
        allBuyActions |> List.sumBy (function
            | Buy(_, _, _, value, _) -> value
            | _ -> 0m)
    let totalBuyCommission =
        allBuyActions |> List.sumBy (function
            | Buy(_, _, _, _, commission) -> commission
            | _ -> 0m)

    let totalSellCommission =
        sellActions |> List.sumBy (function
            | Sell(_, _, _, _, commission) -> commission
            | _ -> 0m)

    let aggregatedActions = aggregateActions (List.rev sellActions @ allBuyActions)
    ShowPortfolioChanges aggregatedActions

    ShowRebalanceStats cash cashAfterSells totalBuyValue totalBuyCommission totalSellCommission finalCash

    if reinvestRemainingCash && cashAfterSmartAllocation > finalCash then
        printfn "Note: Remaining cash was reinvested. This may reduce balance accuracy to minimize unused cash."

    let finalPortfolio =
        portfolioMapFinal
        |> Map.toList
        |> List.choose (fun (symbol, shares) ->
            let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
            if shares > 0 && price > 0m then
                Some (symbol, shares, price, decimal shares * price)
            else None)

    ShowFinalPortfolio finalPortfolio finalCash

    ()
}