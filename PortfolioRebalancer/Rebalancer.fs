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

let RebalanceCurrentPortfolioAsync cash reinvestRemainingCash = async {
    let! indexRecords = getIndexRecordsAsync()
    let! individualStockData = getStockRecordsAsync()
    let! transactionsDb = getPortfolioTransactionsAsync()
    let transactions = transactionsDb |> List.map toDomain
    let priceMap : Map<string, decimal> = individualStockData |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let weightMap : Map<string, decimal> = indexRecords |> List.map (fun r -> r.Symbol, r.Weight) |> Map.ofList
    let currentMap : Map<string, PortfolioTransaction list> =
        transactions
        |> List.groupBy (fun t -> t.Symbol)
        |> Map.ofList
    let currentPortfolioValue =
        currentMap
        |> Map.toList
        |> List.sumBy (fun (symbol, txns) ->
            let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
            let totalShares = txns |> List.sumBy (fun t -> t.Shares)
            decimal totalShares * price)
    let targetPortfolioValue = currentPortfolioValue + cash
    let getTargetShares symbol =
        let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
        let weight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m
        if price > 0m then int (targetPortfolioValue * weight / price) else 0
    let getTargetValue symbol =
        let weight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m
        targetPortfolioValue * weight
    // --- New portfolio state for transactions ---
    let mutable portfolioTxnsMap : Map<string, PortfolioTransaction list> =
        transactions
        |> List.groupBy (fun t -> t.Symbol)
        |> Map.ofList
    let mutable totalCGT = 0m
    let mutable totalCDC = 0m
    // Process sells
    for symbol in currentMap |> Map.toList |> List.map fst do
        let txns = portfolioTxnsMap |> Map.tryFind symbol |> Option.defaultValue []
        let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
        let weight = weightMap |> Map.tryFind symbol |> Option.defaultValue 0m
        let totalShares = txns |> List.sumBy (fun t -> t.Shares)
        let targetShares = getTargetShares symbol
        let today = System.DateTime.UtcNow
        if weight = 0m && totalShares > 0 && price > 0m then
            let sharesToSell = totalShares
            let cgtRate = getCGTRate (txns |> List.head).Date today
            let cgt = max (decimal sharesToSell * (price - (txns |> List.head).Price) * cgtRate) 0m
            let cdc = calculateCDCCharges (decimal sharesToSell * price)
            let txn: PortfolioTransaction = { Symbol = symbol; TransactionType = TransactionType.Sell; Shares = sharesToSell; Price = price; Date = today }
            let updatedTxns = txn::(txns |> List.filter (fun t -> t.Symbol <> symbol))
            portfolioTxnsMap <- portfolioTxnsMap.Add(symbol, updatedTxns)
            totalCGT <- totalCGT + cgt
            totalCDC <- totalCDC + cdc
        elif totalShares > targetShares && price > 0m then
            let sharesToSell = totalShares - targetShares
            let cgtRate = getCGTRate (txns |> List.head).Date today
            let cgt = max (decimal sharesToSell * (price - (txns |> List.head).Price) * cgtRate) 0m
            let cdc = calculateCDCCharges (decimal sharesToSell * price)
            let txn: PortfolioTransaction = { Symbol = symbol; TransactionType = TransactionType.Sell; Shares = sharesToSell; Price = price; Date = today }
            let updatedTxns = txn::(txns |> List.filter (fun t -> t.Symbol <> symbol))
            portfolioTxnsMap <- portfolioTxnsMap.Add(symbol, updatedTxns)
            totalCGT <- totalCGT + cgt
            totalCDC <- totalCDC + cdc
    // Process buys
    for idx in indexRecords do
        let price = priceMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0m
        let targetShares = getTargetShares idx.Symbol
        let currentShares = currentMap |> Map.tryFind idx.Symbol |> Option.defaultValue [] |> List.sumBy (fun t -> t.Shares)
        let sharesToBuy = targetShares - currentShares
        let today = System.DateTime.UtcNow
        if sharesToBuy > 0 && price > 0m then
            let txns = portfolioTxnsMap |> Map.tryFind idx.Symbol |> Option.defaultValue []
            let cdc = calculateCDCCharges (price * decimal sharesToBuy)
            totalCDC <- totalCDC + cdc
    // --- Reporting ---
    printfn "Total CGT paid: %M" totalCGT
    printfn "Total CDC charges: %M" totalCDC
    // Reinvest remaining cash if requested
    let mutable finalPortfolioTxnsMap = portfolioTxnsMap
    let mutable finalCash = decimal cash
    let mutable reinvested = false
    if reinvestRemainingCash then
        let rec reinvestLoop remainingCash iterationCount =
            if iterationCount > 20 || remainingCash <= 0m then
                ()
            else
                let candidates =
                    indexRecords
                    |> List.choose (fun idx ->
                        let price = priceMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0m
                        let txns = finalPortfolioTxnsMap |> Map.tryFind idx.Symbol |> Option.defaultValue []
                        if price > 0m then
                            let commission = calculateCommission price 1
                            let totalCost = price + commission
                            if totalCost <= remainingCash then
                                Some (idx.Symbol, price, txns, commission, totalCost)
                            else None
                        else None)
                    |> List.sortByDescending (fun (_, _, txns, _, _) -> txns |> List.sumBy (fun t -> t.Shares))
                match candidates with
                | [] -> ()
                | (symbol, price, txns, commission, totalCost) :: _ ->
                    let today = System.DateTime.UtcNow
                    let cdc = calculateCDCCharges (price * decimal 1)
                    totalCDC <- totalCDC + cdc
                    reinvestLoop (remainingCash - totalCost) (iterationCount + 1)
        let initialCash =
            indexRecords
            |> List.sumBy (fun idx ->
                let price = priceMap |> Map.tryFind idx.Symbol |> Option.defaultValue 0m
                let txns = finalPortfolioTxnsMap |> Map.tryFind idx.Symbol |> Option.defaultValue []
                decimal (txns |> List.sumBy (fun t -> t.Shares)) * price)
        let cashLeft = decimal cash - initialCash
        reinvestLoop cashLeft 0
    // Show final portfolio
    let finalPortfolio =
        finalPortfolioTxnsMap
        |> Map.toList
        |> List.choose (fun (symbol, txns) ->
            let price = priceMap |> Map.tryFind symbol |> Option.defaultValue 0m
            let totalShares = txns |> List.sumBy (fun t -> t.Shares)
            if totalShares > 0 && price > 0m then
                Some (symbol, totalShares, price, decimal totalShares * price)
            else None)
    ShowFinalPortfolio finalPortfolio finalCash
    if reinvested then
        printfn "Note: Remaining cash was reinvested. This may reduce balance accuracy to minimize unused cash."
    ()
}