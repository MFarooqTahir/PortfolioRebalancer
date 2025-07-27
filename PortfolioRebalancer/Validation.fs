module Validation

open Types
open Configuration

// Enhanced validation functions with comprehensive rules
let validateCashAmount (cash: decimal) : ValidationResult<CashAmount> =
    match cash with
    | c when c < Validation.MinimumCashAmount -> Error (InvalidCashAmount c)
    | c when c > Validation.MaximumCashAmount -> Error (InvalidCashAmount c)
    | c -> Ok (CashAmount c)

let validateSymbol (symbol: string) : ValidationResult<Symbol> =
    match symbol with
    | null -> Error (InvalidSymbol "")
    | s when System.String.IsNullOrWhiteSpace s -> Error (InvalidSymbol s)
    | s when s.Length < Validation.MinSymbolLength -> Error (InvalidSymbol s)
    | s when s.Length > Validation.MaxSymbolLength -> Error (InvalidSymbol s)
    | s -> Ok (Symbol s)

let validatePrice (price: decimal) : ValidationResult<Price> =
    match price with
    | p when p < Validation.MinimumPrice -> Error (InvalidPrice p)
    | p when p > Validation.MaximumPrice -> Error (InvalidPrice p)
    | p -> Ok (Price p)

let validateShares (shares: int) : ValidationResult<ShareCount> =
    match shares with
    | s when s < Validation.MinimumShares -> Error (InvalidShares s)
    | s when s > Validation.MaximumShares -> Error (InvalidShares s)
    | s -> Ok (ShareCount s)

let validateWeight (weight: decimal) : ValidationResult<Weight> =
    match weight with
    | w when w < Validation.MinimumWeight -> Error (InvalidWeight w)
    | w when w > Validation.MaximumWeight -> Error (InvalidWeight w)
    | w -> Ok (Weight w)

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