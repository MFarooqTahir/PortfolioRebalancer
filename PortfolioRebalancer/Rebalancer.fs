module Rebalancer

open Types
open Helpers
open Table
open RebalancingCalculations

// Legacy functions for backward compatibility
let rebalanceIndexWithCash (index: IndexTableRecord list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = true
        Cash = cash
        Index = index
        Current = []
        Prices = prices
    }
    let result = rebalanceIndex config
    result.Orders

let rebalanceCurrentPortfolioWithCash (index: IndexTableRecord list) (current: CurrentPortfolio list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = true
        Cash = cash
        Index = index
        Current = current
        Prices = prices
    }
    let result = rebalanceCurrentPortfolio config
    result.Buys

let getRebalanceIndexPlan (index: IndexTableRecord list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = false
        Cash = cash
        Index = index
        Current = []
        Prices = prices
    }
    let result = rebalanceIndex config
    result.Orders |> List.map (fun order -> (order.Symbol, 0m, order.Price, order.Shares, order.Value))

let getRebalanceCurrentPortfolioPlan (index: IndexTableRecord list) (current: CurrentPortfolio list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = false
        Cash = cash
        Index = index
        Current = current
        Prices = prices
    }
    let result = rebalanceCurrentPortfolio config
    result.Buys |> List.map (fun order -> (order.Symbol, 0m, order.Price, 0, order.Shares, order.Value))

// Main rebalancing function with improved structure
let rebalanceAndDisplay 
    (kind: string) 
    (includeCommission: bool) 
    (cash: decimal) 
    (index: IndexTableRecord list) 
    (current: CurrentPortfolio list) 
    (prices: StockRecord list) =
    
    // Validate inputs
    let missingSymbols = 
        let priceMap = createPriceMap prices
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
        let config = { 
            IncludeCommission = includeCommission
            Cash = cash
            Index = index
            Current = current
            Prices = prices
        }
        
        if kind = "index" then
            let result = rebalanceIndex config
            let weightMap = createWeightMap index
            let displayOrders = result.Orders |> List.map (fun order -> 
                {| 
                    Symbol = order.Symbol
                    Shares = order.Shares
                    Value = order.Value
                    Commission = order.Commission
                    Price = order.Price
                    Weight = Map.tryFind order.Symbol weightMap |> Option.defaultValue 0m
                |})
            showRebalanceIndexResult displayOrders includeCommission cash
        else
            let result = rebalanceCurrentPortfolio config
            let displaySells = result.Sells |> List.map (fun order -> 
                {| 
                    Symbol = order.Symbol
                    Shares = order.Shares
                    Price = order.Price
                    Value = order.Value
                    Commission = order.Commission
                |})
            let displayBuys = result.Buys |> List.map (fun order -> 
                {| 
                    Symbol = order.Symbol
                    Shares = order.Shares
                    Price = order.Price
                    Value = order.Value
                    Commission = order.Commission
                |})
            let displayFinal = result.Final |> List.map (fun pos -> 
                {| 
                    Symbol = pos.Symbol
                    Shares = pos.Shares
                    Value = pos.Value
                    Commission = pos.Commission
                    Price = pos.Price
                    Weight = pos.Weight
                    FinalWeight = pos.FinalWeight
                |})
            showRebalanceCurrentPortfolioResult displaySells displayBuys displayFinal includeCommission cash