module Helpers

open System.Globalization
open System
open Types
open Configuration

let indianCulture = CultureInfo "hi-IN"
do
    indianCulture.NumberFormat.CurrencySymbol <- "Rs."

let formatPKR (amount: decimal) = 
    amount.ToString("C2", indianCulture)

let formatPKRShort (amount: decimal) = 
    amount.ToString("C0", indianCulture)

let toPercentage (value: decimal) =
    value.ToString "P2"

let (|ValidDecimal|_|) (str: string) =
    match Decimal.TryParse(str.Replace(",", "")) with
    | true, value -> Some value
    | _ -> None

let (|ValidWeight|_|) (str: string) =
    let cleanStr = str.Replace("%", "").Trim()
    match Decimal.TryParse(cleanStr) with
    | true, weight when weight > 0m -> Some (weight / 100m)
    | _ -> None

let createWeightMap (indexRecords: IndexTableRecord list) =
    indexRecords
        |> List.map(fun x -> x.Symbol, x.Weight)
        |> Map.ofList

type StockDisplayTableRecord = {
    Weight: decimal
    Symbol: string
    Name: string
    Price: decimal
}

let createStockTableRecord (indexRecords: IndexTableRecord list) (individualStockData: StockRecord list) =
    let weightMap = indexRecords |> createWeightMap

    individualStockData 
        |> List.choose (fun stock -> 
            weightMap 
            |> Map.tryFind stock.Symbol
            |> Option.map (fun weight -> {
                Weight = weight
                Symbol = stock.Symbol
                Name = stock.Name
                Price = stock.Price
            }))

let calculateCommission sharePrice quantity =
    if sharePrice < Commission.MinimumPrice then
        Commission.FixedRate * decimal quantity
    else
        sharePrice * decimal quantity * Commission.PercentageRate
