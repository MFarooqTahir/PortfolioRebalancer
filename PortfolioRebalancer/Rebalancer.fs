module Rebalancer

open Types
open Helpers
open Table

let rebalanceIndexWithCash (index: IndexTableRecord list) (prices: StockRecord list) (cash: decimal) =
    let priceMap = prices |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let totalWeight = index |> List.sumBy (fun i -> i.Weight)
    index
    |> List.choose (fun idx ->
        match Map.tryFind idx.Symbol priceMap with
        | Some price when price > 0m ->
            let targetValue = cash * (idx.Weight / totalWeight)
            let shares = int (targetValue / price)
            let value = decimal shares * price
            let commission = calculateCommission price shares
            Some (idx.Symbol, shares, value, commission, price, idx.Weight)
        | _ -> None)
    |> List.map (fun (symbol, shares, value, commission, price, weight) ->
        {| Symbol = symbol; Shares = shares; Value = value; Commission = commission; Price = price; Weight = weight |})

// Rebalance current portfolio with cash, including commission
let rebalanceCurrentPortfolioWithCash (index: IndexTableRecord list) (current: CurrentPortfolio list) (prices: StockRecord list) (cash: decimal) =
    let priceMap = prices |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let currentMap = current |> List.map (fun c -> c.Symbol, c.Shares) |> Map.ofList
    let totalWeight = index |> List.sumBy (fun i -> i.Weight)
    index
    |> List.choose (fun idx ->
        match Map.tryFind idx.Symbol priceMap with
        | Some price when price > 0m ->
            let targetValue = cash * (idx.Weight / totalWeight)
            let currentShares = Map.tryFind idx.Symbol currentMap |> Option.defaultValue 0
            let sharesToBuy = int (targetValue / price)
            let value = decimal sharesToBuy * price
            let commission = calculateCommission price sharesToBuy
            Some (idx.Symbol, currentShares, sharesToBuy, value, commission, price, idx.Weight)
        | _ -> None)
    |> List.map (fun (symbol, currentShares, sharesToBuy, value, commission, price, weight) ->
        {| Symbol = symbol; CurrentShares = currentShares; SharesToBuy = sharesToBuy; Value = value; Commission = commission; Price = price; Weight = weight |})

// No-commission rebalance for index
let getRebalanceIndexPlan (index: IndexTableRecord list) (prices: StockRecord list) (cash: decimal) =
    let priceMap = prices |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let totalWeight = index |> List.sumBy (fun i -> i.Weight)
    index
    |> List.choose (fun idx ->
        match Map.tryFind idx.Symbol priceMap with
        | Some price when price > 0m ->
            let targetValue = cash * (idx.Weight / totalWeight)
            let shares = int (targetValue / price)
            let value = decimal shares * price
            Some (idx.Symbol, idx.Weight, price, shares, value)
        | _ -> None)

// No-commission rebalance for current portfolio
let getRebalanceCurrentPortfolioPlan (index: IndexTableRecord list) (current: CurrentPortfolio list) (prices: StockRecord list) (cash: decimal) =
    let priceMap = prices |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let currentMap = current |> List.map (fun c -> c.Symbol, c.Shares) |> Map.ofList
    let totalWeight = index |> List.sumBy (fun i -> i.Weight)
    index
    |> List.choose (fun idx ->
        match Map.tryFind idx.Symbol priceMap with
        | Some price when price > 0m ->
            let targetValue = cash * (idx.Weight / totalWeight)
            let currentShares = Map.tryFind idx.Symbol currentMap |> Option.defaultValue 0
            let sharesToBuy = int (targetValue / price)
            let value = decimal sharesToBuy * price
            Some (idx.Symbol, idx.Weight, price, currentShares, sharesToBuy, value)
        | _ -> None)

let rebalanceAndDisplay 
    (kind: string) 
    (includeCommission: bool) 
    (cash: decimal) 
    (index: IndexTableRecord list) 
    (current: CurrentPortfolio list) 
    (prices: StockRecord list) =
    let priceMap = prices |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    let totalWeight = index |> List.sumBy (fun i -> i.Weight)
    let missingSymbols =
        let indexSymbols = index |> List.map (fun i -> i.Symbol)
        let currentSymbols = current |> List.map (fun c -> c.Symbol)
        let allSymbols =
            if kind = "index" then indexSymbols
            else Set.union (Set.ofList indexSymbols) (Set.ofList currentSymbols) |> Set.toList
        allSymbols |> List.filter (fun sym -> not (priceMap.ContainsKey sym))
    if not (List.isEmpty missingSymbols) then
        printfn "[WARNING] No price data for: %s" (String.concat ", " missingSymbols)
    if List.isEmpty index then
        printfn "[ERROR] No index records found."
    elif List.isEmpty prices then
        printfn "[ERROR] No stock price records found."
    elif kind = "current" && List.isEmpty current then
        printfn "[ERROR] No current portfolio records found."
    else
        if includeCommission then
            if kind = "index" then
                let results = rebalanceIndexWithCash index prices cash
                showRebalanceIndexResult results true cash
            else
                let indexMap = index |> List.map (fun i -> i.Symbol, i.Weight) |> Map.ofList
                let currentMap = current |> List.map (fun c -> c.Symbol, c.Shares) |> Map.ofList
                let allSymbols = Set.union (Set.ofList (index |> List.map (fun i -> i.Symbol))) (Set.ofList (current |> List.map (fun c -> c.Symbol))) |> Set.toList
                let totalValue = cash + (current |> List.sumBy (fun c -> match Map.tryFind c.Symbol priceMap with Some p -> decimal c.Shares * p | _ -> 0m))
                let targetSharesMap =
                    allSymbols
                    |> List.choose (fun symbol ->
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m ->
                            let weight = Map.tryFind symbol indexMap |> Option.defaultValue 0m
                            let targetValue = totalValue * weight
                            let shares = if price > 0m then int (targetValue / price) else 0
                            Some (symbol, shares)
                        | _ -> None)
                    |> Map.ofList
                let sells =
                    allSymbols
                    |> List.choose (fun symbol ->
                        let currentShares = Map.tryFind symbol currentMap |> Option.defaultValue 0
                        let targetShares = Map.tryFind symbol targetSharesMap |> Option.defaultValue 0
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m && currentShares > targetShares ->
                            let shares = currentShares - targetShares
                            let value = decimal shares * price
                            let commission = calculateCommission price shares
                            Some {| Symbol = symbol; Shares = shares; Price = price; Value = value; Commission = commission |}
                        | _ -> None)
                let totalSellValue = sells |> List.sumBy (fun r -> r.Value)
                let totalSellCommission = sells |> List.sumBy (fun r -> r.Commission)
                let cashAfterSelling = cash + totalSellValue - totalSellCommission
                let buys =
                    allSymbols
                    |> List.choose (fun symbol ->
                        let currentShares = Map.tryFind symbol currentMap |> Option.defaultValue 0
                        let targetShares = Map.tryFind symbol targetSharesMap |> Option.defaultValue 0
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m && targetShares > currentShares ->
                            let shares = targetShares - currentShares
                            let value = decimal shares * price
                            let commission = calculateCommission price shares
                            Some {| Symbol = symbol; Shares = shares; Price = price; Value = value; Commission = commission |}
                        | _ -> None)
                let totalBuyValue = buys |> List.sumBy (fun r -> r.Value)
                let totalBuyCommission = buys |> List.sumBy (fun r -> r.Commission)
                let cashAfterBuying = cashAfterSelling - totalBuyValue - totalBuyCommission
                let final =
                    allSymbols
                    |> List.choose (fun symbol ->
                        let shares = Map.tryFind symbol targetSharesMap |> Option.defaultValue 0
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m ->
                            let value = decimal shares * price
                            let commission = 0m
                            let weight = Map.tryFind symbol indexMap |> Option.defaultValue 0m
                            let finalWeight = if totalValue > 0m then value / totalValue else 0m
                            Some {| Symbol = symbol; Shares = shares; Value = value; Commission = commission; Price = price; Weight = weight; FinalWeight = finalWeight |}
                        | _ -> None)
                showRebalanceCurrentPortfolioResult sells buys final true cash
        else
            if kind = "index" then
                let results = getRebalanceIndexPlan index prices cash |> List.map (fun (symbol, weight, price, shares, value) -> {| Symbol = symbol; Shares = shares; Value = value; Commission = 0m; Price = price; Weight = weight |})
                showRebalanceIndexResult results false cash
            else
                let indexMap = index |> List.map (fun i -> i.Symbol, i.Weight) |> Map.ofList
                let currentMap = current |> List.map (fun c -> c.Symbol, c.Shares) |> Map.ofList
                let allSymbols = Set.union (Set.ofList (index |> List.map (fun i -> i.Symbol))) (Set.ofList (current |> List.map (fun c -> c.Symbol))) |> Set.toList
                let totalValue = cash + (current |> List.sumBy (fun c -> match Map.tryFind c.Symbol priceMap with Some p -> decimal c.Shares * p | _ -> 0m))
                let targetSharesMap =
                    allSymbols
                    |> List.choose (fun symbol ->
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m ->
                            let weight = Map.tryFind symbol indexMap |> Option.defaultValue 0m
                            let targetValue = totalValue * weight
                            let shares = if price > 0m then int (targetValue / price) else 0
                            Some (symbol, shares)
                        | _ -> None)
                    |> Map.ofList
                let sells =
                    allSymbols
                    |> List.choose (fun symbol ->
                        let currentShares = Map.tryFind symbol currentMap |> Option.defaultValue 0
                        let targetShares = Map.tryFind symbol targetSharesMap |> Option.defaultValue 0
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m && currentShares > targetShares ->
                            let shares = currentShares - targetShares
                            let value = decimal shares * price
                            let commission = 0m
                            Some {| Symbol = symbol; Shares = shares; Price = price; Value = value; Commission = commission |}
                        | _ -> None)
                let totalSellValue = sells |> List.sumBy (fun r -> r.Value)
                let totalSellCommission = sells |> List.sumBy (fun r -> r.Commission)
                let cashAfterSelling = cash + totalSellValue - totalSellCommission
                let buys =
                    allSymbols
                    |> List.choose (fun symbol ->
                        let currentShares = Map.tryFind symbol currentMap |> Option.defaultValue 0
                        let targetShares = Map.tryFind symbol targetSharesMap |> Option.defaultValue 0
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m && targetShares > currentShares ->
                            let shares = targetShares - currentShares
                            let value = decimal shares * price
                            let commission = 0m
                            Some {| Symbol = symbol; Shares = shares; Price = price; Value = value; Commission = commission |}
                        | _ -> None)
                let totalBuyValue = buys |> List.sumBy (fun r -> r.Value)
                let totalBuyCommission = buys |> List.sumBy (fun r -> r.Commission)
                let cashAfterBuying = cashAfterSelling - totalBuyValue - totalBuyCommission
                let final =
                    allSymbols
                    |> List.choose (fun symbol ->
                        let shares = Map.tryFind symbol targetSharesMap |> Option.defaultValue 0
                        match Map.tryFind symbol priceMap with
                        | Some price when price > 0m ->
                            let value = decimal shares * price
                            let commission = 0m
                            let weight = Map.tryFind symbol indexMap |> Option.defaultValue 0m
                            let finalWeight = if totalValue > 0m then value / totalValue else 0m
                            Some {| Symbol = symbol; Shares = shares; Value = value; Commission = commission; Price = price; Weight = weight; FinalWeight = finalWeight |}
                        | _ -> None)
                showRebalanceCurrentPortfolioResult sells buys final false cash