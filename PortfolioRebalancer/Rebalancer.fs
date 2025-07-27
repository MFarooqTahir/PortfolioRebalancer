module Rebalancer

open Types
open Helpers
open Table
open RebalancingCalculations
open Spectre.Console

// Legacy functions for backward compatibility
let rebalanceIndexWithCash (index: IndexTableRecord list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = true
        Cash = cash
        Index = index
        Current = []
        Prices = prices
    }
    rebalanceIndex config
    |> Result.map (fun result -> result.Orders)
    |> Result.defaultValue []

let rebalanceCurrentPortfolioWithCash (index: IndexTableRecord list) (current: CurrentPortfolio list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = true
        Cash = cash
        Index = index
        Current = current
        Prices = prices
    }
    rebalanceCurrentPortfolio config
    |> Result.map (fun result -> result.Buys)
    |> Result.defaultValue []

let getRebalanceIndexPlan (index: IndexTableRecord list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = false
        Cash = cash
        Index = index
        Current = []
        Prices = prices
    }
    rebalanceIndex config
    |> Result.map (fun result -> 
        result.Orders 
        |> List.map (fun order -> (order.Symbol, 0m, order.Price, order.Shares, order.Value)))
    |> Result.defaultValue []

let getRebalanceCurrentPortfolioPlan (index: IndexTableRecord list) (current: CurrentPortfolio list) (prices: StockRecord list) (cash: decimal) =
    let config = { 
        IncludeCommission = false
        Cash = cash
        Index = index
        Current = current
        Prices = prices
    }
    rebalanceCurrentPortfolio config
    |> Result.map (fun result -> 
        result.Buys 
        |> List.map (fun order -> (order.Symbol, 0m, order.Price, 0, order.Shares, order.Value)))
    |> Result.defaultValue []

// Main rebalancing function with improved structure
let rebalanceAndDisplay 
    (kind: string) 
    (includeCommission: bool) 
    (cash: decimal) 
    (index: IndexTableRecord list) 
    (current: CurrentPortfolio list) 
    (prices: StockRecord list) =
    
    // Helper functions for better organization
    let findMissingSymbols () = 
        let priceMap = createPriceMap prices
        let indexSymbols = index |> List.map (fun i -> i.Symbol)
        let currentSymbols = current |> List.map (fun c -> c.Symbol)
        let allSymbols =
            if kind = "index" then indexSymbols
            else Set.union (Set.ofList indexSymbols) (Set.ofList currentSymbols) |> Set.toList
        allSymbols |> List.filter (fun sym -> not (priceMap.ContainsKey sym))
    
    let validateInputs () =
        let missingSymbols = findMissingSymbols()
        
        if not (List.isEmpty missingSymbols) then
            AnsiConsole.MarkupLine(sprintf "[yellow][WARNING] No price data for: %s[/]" (String.concat ", " missingSymbols))
        
        if List.isEmpty index then
            AnsiConsole.MarkupLine("[red][ERROR] No index records found.[/]")
            false
        elif List.isEmpty prices then
            AnsiConsole.MarkupLine("[red][ERROR] No stock price records found.[/]")
            false
        elif kind = "current" && List.isEmpty current then
            AnsiConsole.MarkupLine("[red][ERROR] No current portfolio records found.[/]")
            false
        else
            true
    
    let createConfig () = { 
        IncludeCommission = includeCommission
        Cash = cash
        Index = index
        Current = current
        Prices = prices
    }
    
    let handleIndexRebalancing config =
        match rebalanceIndex config with
        | Ok result ->
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
        | Error _ -> 
            AnsiConsole.MarkupLine("[red][ERROR] Failed to rebalance index.[/]")
    
    let handleCurrentPortfolioRebalancing config =
        match rebalanceCurrentPortfolio config with
        | Ok result ->
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
        | Error _ -> 
            AnsiConsole.MarkupLine("[red][ERROR] Failed to rebalance current portfolio.[/]")
    
    // Main execution flow
    if validateInputs() then
        let config = createConfig()
        if kind = "index" then
            handleIndexRebalancing config
        else
            handleCurrentPortfolioRebalancing config