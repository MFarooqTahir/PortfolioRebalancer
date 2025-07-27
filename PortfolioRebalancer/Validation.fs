module Validation

open Types

// Validation functions
let validateCashAmount (cash: decimal) : Result<CashAmount, ValidationError> =
    if cash >= 0m then
        Ok (CashAmount cash)
    else
        Error (InvalidCashAmount cash)

let validateSymbol (symbol: string) : Result<Symbol, ValidationError> =
    if not (System.String.IsNullOrWhiteSpace symbol) then
        Ok (Symbol symbol)
    else
        Error (InvalidSymbol symbol)

let validateIndexNotEmpty (index: IndexTableRecord list) : Result<IndexTableRecord list, ValidationError> =
    if List.isEmpty index then
        Error EmptyIndex
    else
        Ok index

let validatePortfolioNotEmpty (portfolio: CurrentPortfolio list) : Result<CurrentPortfolio list, ValidationError> =
    if List.isEmpty portfolio then
        Error EmptyPortfolio
    else
        Ok portfolio

let validatePricesNotEmpty (prices: StockRecord list) : Result<StockRecord list, ValidationError> =
    if List.isEmpty prices then
        Error (InsufficientData "No price data available")
    else
        Ok prices

let findMissingSymbols (index: IndexTableRecord list) (current: CurrentPortfolio list) (prices: StockRecord list) : string list =
    let priceSymbols = prices |> List.map (fun p -> p.Symbol) |> Set.ofList
    let indexSymbols = index |> List.map (fun i -> i.Symbol) |> Set.ofList
    let currentSymbols = current |> List.map (fun c -> c.Symbol) |> Set.ofList
    
    let allRequiredSymbols = Set.union indexSymbols currentSymbols
    let missingSymbols = Set.difference allRequiredSymbols priceSymbols
    
    Set.toList missingSymbols

let validateRebalanceConfig (config: RebalanceConfig) : Result<ValidatedRebalanceConfig, ValidationError> =
    let missingSymbols = findMissingSymbols config.Index config.Current config.Prices
    
    match validateCashAmount config.Cash, validateIndexNotEmpty config.Index, validatePricesNotEmpty config.Prices with
    | Ok _, Ok _, Ok _ ->
        Ok {
            IncludeCommission = config.IncludeCommission
            Cash = config.Cash
            Index = config.Index
            Current = config.Current
            Prices = config.Prices
            MissingSymbols = missingSymbols
        }
    | Error e, _, _ -> Error e
    | _, Error e, _ -> Error e
    | _, _, Error e -> Error e

// Helper for Result computation expressions
type ResultBuilder() =
    member _.Return(x) = Ok x
    member _.ReturnFrom(m) = m
    member _.Bind(m, f) = Result.bind f m
    member _.Zero() = Ok ()

let result = ResultBuilder() 