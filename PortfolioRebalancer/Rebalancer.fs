module Rebalancer

open Types
open Helpers
open Table
open System
open Db

let private getCGTRate (acquisitionDate: DateTime) (disposalDate: DateTime) =
    let holdingPeriod = (disposalDate - acquisitionDate).TotalDays / 365.0
    let currentYear = disposalDate.Year
    let july2013 = DateTime(2013, 7, 1)
    let june2022 = DateTime(2022, 6, 30)
    let july2024 = DateTime(2024, 7, 1)
    if acquisitionDate < july2013 then
        0.0M
    elif acquisitionDate >= july2024 then
        0.15M
    elif acquisitionDate >= july2013 && acquisitionDate <= june2022 then
        0.125M
    else
        if holdingPeriod <= 1.0 then 0.15M
        elif holdingPeriod <= 2.0 then 0.125M
        elif holdingPeriod <= 3.0 then 0.10M
        elif holdingPeriod <= 4.0 then 0.075M
        elif holdingPeriod <= 5.0 then 0.05M
        elif holdingPeriod <= 6.0 then 0.025M
        else 0.0M

let private calculateCDCCharges (transactionValue: decimal) =
    let percentCharge = transactionValue * 0.000036M
    max 5.0M percentCharge

let BalanceIndexAsync (cash: decimal) (reinvestRemainingCash: bool) = async {
    let! indexRecords = getIndexRecordsAsync()
    let! individualStockData = getStockRecordsAsync()
    let stockData = ToStockTableRecord indexRecords individualStockData
    let initialPositions: PortfolioPosition list =
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
    let targetValues = initialPositions |> List.map (fun p -> p.Symbol, cash * p.TargetWeight) |> Map.ofList
    let rec invest (positions: PortfolioPosition list) (remainingCash: decimal) (iterationCount: int) : PortfolioPosition list * decimal =
        if iterationCount > 100 || remainingCash <= 0m then (positions, remainingCash)
        else
            let valueGaps =
                positions
                |> List.map (fun p ->
                    let targetValue = targetValues.[p.Symbol]
                    let gap = max 0m (targetValue - p.ActualValue)
                    p.Symbol, gap)
                |> Map.ofList
            let totalGap = valueGaps |> Map.values |> Seq.sum
            if totalGap = 0m then (positions, remainingCash)
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
                                if totalCost <= remainingCash then Some (p, sharesToBuy, totalCost) else None
                            else None
                        else None)
                if List.isEmpty purchaseCandidates then (positions, remainingCash)
                else
                    let updatedPositions, totalSpent =
                        purchaseCandidates
                        |> List.fold (fun (accPos: PortfolioPosition list, accSpent) (p, sharesToBuy, totalCost) ->
                            let newShares = p.ActualShares + sharesToBuy
                            let newActualValue = decimal newShares * p.CurrentPrice
                            let newCommission = calculateCommission p.CurrentPrice newShares
                            let newTotalCost = newActualValue + newCommission
                            let updatedPosition: PortfolioPosition = { p with ActualShares = newShares; ActualValue = newActualValue; Commission = newCommission; TotalCost = newTotalCost }
                            let updatedList = accPos |> List.map (fun pos -> if pos.Symbol = p.Symbol then updatedPosition else pos)
                            updatedList, accSpent + totalCost
                        ) (positions, 0m)
                    invest updatedPositions (remainingCash - totalSpent) (iterationCount + 1)
    let positionsAfterBalancing, cashAfterBalancing = invest initialPositions cash 0
    let rec buyIndividualShares (positions: PortfolioPosition list) (remainingCash: decimal) (iterationCount: int) : PortfolioPosition list * decimal =
        if iterationCount > 20 || remainingCash <= 0m then positions, remainingCash else
        let candidates =
            positions
            |> List.choose (fun p ->
                let targetValue = targetValues.[p.Symbol]
                let relativeGap = if targetValue > 0m then (targetValue - p.ActualValue) / targetValue else 0m
                if p.CurrentPrice > 0m && relativeGap > 0.01m then
                    let commission = calculateCommission p.CurrentPrice 1
                    let totalCost = p.CurrentPrice + commission
                    if totalCost <= remainingCash then Some (p, totalCost) else None
                else None)
            |> List.sortByDescending (fun (p, _) -> p.TargetWeight)
        match candidates with
        | [] -> positions, remainingCash
        | (bestPosition, totalCost) :: _ ->
            let newShares = bestPosition.ActualShares + 1
            let newActualValue = decimal newShares * bestPosition.CurrentPrice
            let newCommission = calculateCommission bestPosition.CurrentPrice newShares
            let newTotalCost = newActualValue + newCommission
            let updatedPosition: PortfolioPosition = { bestPosition with ActualShares = newShares; ActualValue = newActualValue; Commission = newCommission; TotalCost = newTotalCost }
            let updatedPositions = positions |> List.map (fun pos -> if pos.Symbol = bestPosition.Symbol then updatedPosition else pos)
            buyIndividualShares updatedPositions (remainingCash - totalCost) (iterationCount + 1)
    let finalPositions, finalCash =
        if reinvestRemainingCash && cashAfterBalancing > 0m then
            let positions, cash = buyIndividualShares positionsAfterBalancing cashAfterBalancing 0
            if cash < cashAfterBalancing then printfn "Note: Remaining cash was reinvested. This may reduce balance accuracy to minimize unused cash."
            positions, cash
        else positionsAfterBalancing, cashAfterBalancing
    ShowRebalanceResult finalPositions finalCash |> ignore
    return ()
}

let RebalanceCurrentPortfolioAsync (cash: decimal) (reinvestRemainingCash: bool) = async {
    let! indexRecords = getIndexRecordsAsync()
    let! individualStockData = getStockRecordsAsync()
    let! transactionsDb = getPortfolioTransactionsAsync()
    let priceMap = individualStockData |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let weightMap = indexRecords |> List.map (fun r -> r.Symbol, r.Weight) |> Map.ofList
    let getName symbol = individualStockData |> List.tryFind (fun s -> s.Symbol = symbol) |> Option.map (fun s -> s.Name) |> Option.defaultValue "-"
    let getPrice symbol = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
    // 1. Get current portfolio (symbol -> shares) using FIFO lot logic
    let currentSharesMap =
        transactionsDb
        |> List.groupBy (fun t -> t.Symbol)
        |> Map.ofList
        |> Map.map (fun _ txnsDb ->
            let shares, _ = Db.getCurrentLotsAndAvgPrice txnsDb
            shares
        )
    // 2. Get all symbols in current portfolio or index
    let allSymbols =
        Set.union (currentSharesMap |> Map.toList |> List.map fst |> Set.ofList)
                  (indexRecords |> List.map (fun idx -> idx.Symbol) |> Set.ofList)
        |> Set.toList
    // 3. Compute current portfolio value
    let currentPortfolioValue =
        currentSharesMap |> Map.toList |> List.sumBy (fun (symbol, shares) -> decimal shares * getPrice symbol)
    let totalValue = currentPortfolioValue + cash
    // 4. Compute target shares for each symbol
    let targetSharesMap =
        allSymbols
        |> List.map (fun symbol ->
            let price = getPrice symbol
            let weight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m
            let targetValue = totalValue * weight
            let shares = if price > 0m then int (targetValue / price) else 0
            symbol, shares)
        |> Map.ofList
    // 5. Sells: for each symbol, if current > target, sell the difference
    let mutable cashAvailable = cash
    let mutable sharesMap = currentSharesMap
    let mutable sellActions = []
    for symbol in allSymbols do
        let currentShares = sharesMap |> Map.tryFind symbol |> Option.defaultValue 0
        let targetShares = targetSharesMap |> Map.tryFind symbol |> Option.defaultValue 0
        let price = getPrice symbol
        if currentShares > targetShares && price > 0m then
            let sharesToSell = currentShares - targetShares
            let value = decimal sharesToSell * price
            let commission = calculateCommission price sharesToSell
            let cgt = value * getCGTRate (System.DateTime.UtcNow.AddYears(-2)) System.DateTime.UtcNow
            let net = value - commission - cgt
            cashAvailable <- cashAvailable + net
            sharesMap <- sharesMap.Add(symbol, currentShares - sharesToSell)
            sellActions <- ({ Symbol = symbol; Name = getName symbol; TargetWeight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m; CurrentPrice = price; ActualShares = currentShares; ActualValue = decimal currentShares * price; Commission = commission; TotalCost = net }, sharesToSell, value, commission, cgt) :: sellActions
    // 6. Buys: for each symbol, if current < target, buy as many as possible up to the difference, but never more than available cash
    let mutable buyActions = []
    for symbol in allSymbols do
        let currentShares = sharesMap |> Map.tryFind symbol |> Option.defaultValue 0
        let targetShares = targetSharesMap |> Map.tryFind symbol |> Option.defaultValue 0
        let price = getPrice symbol
        if targetShares > currentShares && price > 0m then
            let sharesToBuy = targetShares - currentShares
            let value = decimal sharesToBuy * price
            let commission = calculateCommission price sharesToBuy
            let total = value + commission
            let maxAffordableShares =
                if total <= cashAvailable then sharesToBuy
                else
                    let perShareCost = price + (commission / decimal sharesToBuy)
                    let affordable = int (cashAvailable / perShareCost)
                    min affordable sharesToBuy
            if maxAffordableShares > 0 then
                let buyValue = decimal maxAffordableShares * price
                let buyCommission = calculateCommission price maxAffordableShares
                let buyTotal = buyValue + buyCommission
                cashAvailable <- cashAvailable - buyTotal
                sharesMap <- sharesMap.Add(symbol, currentShares + maxAffordableShares)
                buyActions <- ({ Symbol = symbol; Name = getName symbol; TargetWeight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m; CurrentPrice = price; ActualShares = currentShares; ActualValue = decimal currentShares * price; Commission = buyCommission; TotalCost = buyTotal }, maxAffordableShares, buyValue, buyCommission) :: buyActions
    // 7. Build final portfolio
    let finalPortfolio =
        allSymbols
        |> List.choose (fun symbol ->
            let shares = sharesMap |> Map.tryFind symbol |> Option.defaultValue 0
            let price = getPrice symbol
            if shares > 0 && price > 0m then Some (symbol, getName symbol, shares, price, decimal shares * price) else None)
        |> List.sortByDescending (fun (_,_,_,_,value) -> value)
    let sellActionsSorted = sellActions |> List.sortByDescending (fun (_,_,value,_,_) -> value)
    let buyActionsSorted = buyActions |> List.sortByDescending (fun (_,_,value,_) -> value)
    let totalSellCommission = sellActions |> List.sumBy (fun (_,_,_,commission,_) -> commission)
    let totalSellTax = sellActions |> List.sumBy (fun (_,_,_,_,tax) -> tax)
    let proceedsFromSells = sellActions |> List.sumBy (fun (_,_,value,commission,tax) -> value - commission - tax)
    let totalBuyCommission = buyActions |> List.sumBy (fun (_,_,_,commission) -> commission)
    let costOfBuys = buyActions |> List.sumBy (fun (_,_,value,commission) -> value + commission)
    Table.ShowRebalanceActionsWithTax "=== Sells Required ===" sellActionsSorted true totalSellCommission totalSellTax proceedsFromSells (cash + proceedsFromSells)
    let buyActionsWithTax = buyActionsSorted |> List.map (fun (a,b,c,d) -> (a,b,c,d,0m))
    Table.ShowRebalanceActionsWithTax "=== Buys Required ===" buyActionsWithTax false totalBuyCommission 0m costOfBuys cashAvailable
    Table.ShowFinalPortfolioWithNamesAndCash finalPortfolio cashAvailable
    return ()
}