module Helpers
    open System.Globalization
    open System
    open Types

    let indianCulture = CultureInfo("hi-IN")

    let formatPKR (amount: decimal) = 
        amount.ToString("N2", indianCulture)

    let formatPKRShort (amount: decimal) = 
        amount.ToString("N0", indianCulture)

    let toPercentage (value: decimal) =
        value.ToString("P2")

    let (|ValidDecimal|_|) (str: string) =
        match Decimal.TryParse(str.Replace(",", "")) with
        | (true, value) -> Some value
        | _ -> None

    let (|ValidWeight|_|) (str: string) =
        let cleanStr = str.Replace("%", "").Trim()
        match Decimal.TryParse(cleanStr) with
        | (true, weight) when weight > 0m -> Some (weight / 100m)
        | _ -> None

    let ToWeightMap (indexRecord: IndexTableRecord list) =
        indexRecord
            |> List.map(fun x -> x.Symbol, x.Weight)
            |> Map.ofList

    let ToStockTableRecord indexRecords (individualStockData: StockRecord list)=
        let weightMap = indexRecords |> ToWeightMap

        individualStockData 
            |> List.choose (fun stock -> 
                let weight = weightMap |> Map.tryFind stock.Symbol
                match weight with
                    | None -> None
                    | Some d -> Some {
                            Weight = d
                            Symbol = stock.Symbol
                            Name = stock.Name
                            Price = stock.Price
                        })

    let calculateCommission sharePrice quantity =
        if sharePrice < 12m then
            0.03m * decimal quantity
        else
            (sharePrice * decimal quantity) * 0.0025m
