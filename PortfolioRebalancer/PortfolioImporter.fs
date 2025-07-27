module PortfolioImporter

open System.IO
open System.Text.Json
open Types
open Db

let options = JsonSerializerOptions()
do
    options.PropertyNameCaseInsensitive <- true
let importCurrentPortfolioFromJson () = async {
    let filePath = "C:\\CurrentPortfolio.json"

    try
        let json = File.ReadAllText(filePath)
        let records = JsonSerializer.Deserialize<CurrentPortfolio list>(json, options) |> Option.ofObj
        match records with
        | None -> printfn "[ERROR] Could not parse current portfolio JSON."; return ()
        | Some xs ->
            do! setCurrentPortfolioAsync xs
            printfn "Current portfolio imported and saved to database."
    with ex ->
        printfn "[ERROR] Failed to import current portfolio: %s" ex.Message
}