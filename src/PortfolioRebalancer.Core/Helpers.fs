module PortfolioRebalancer.Core.Helpers

open System
open System.Globalization
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Configuration

let indianCulture = CultureInfo Configuration.App.DefaultCulture
do
    indianCulture.NumberFormat.CurrencySymbol <- Configuration.App.CurrencySymbol

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

let calculateCommission sharePrice quantity =
    if sharePrice < Configuration.Commission.MinimumPrice then
        Configuration.Commission.FixedRate * decimal quantity
    else
        sharePrice * decimal quantity * Configuration.Commission.PercentageRate
