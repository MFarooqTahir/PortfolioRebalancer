module Db

open Npgsql
open Dapper
open Types
open Dapper.FSharp.PostgreSQL
open Configuration
open Logging

OptionTypes.register()



let private getConnection() = 
    try
        new NpgsqlConnection(Database.ConnectionString)
    with
    | ex -> 
        logger.Error (sprintf "Failed to create database connection: %s" ex.Message)
        reraise()

// Note: Retry logic removed for simplicity - can be added later with proper exception handling
let ensureTablesAsync() = async {
    use conn = getConnection()
    do! conn.OpenAsync() |> Async.AwaitTask
    let sql = """
    CREATE TABLE IF NOT EXISTS "IndexTableRecord" (
        "Symbol" TEXT PRIMARY KEY,
        "Weight" NUMERIC(12,4) NOT NULL
    );
    CREATE TABLE IF NOT EXISTS "StockRecord" (
        "Symbol" TEXT PRIMARY KEY,
        "Name" TEXT NOT NULL,
        "Price" NUMERIC(12,4) NOT NULL
    );
    CREATE TABLE IF NOT EXISTS "CurrentPortfolio" (
        "Id" SERIAL PRIMARY KEY,
        "Symbol" TEXT NOT NULL,
        "Shares" INTEGER NOT NULL,
        "Price" NUMERIC(12,8) NOT NULL
    );
    """
    do! conn.ExecuteAsync sql |> Async.AwaitTask |> Async.Ignore
}

let indexTable = table<IndexTableRecord>

let setIndexRecordsAsync (records: IndexTableRecord list) = async {
    use conn = getConnection()
    do! conn.OpenAsync() |> Async.AwaitTask

    do! delete { for _ in indexTable do deleteAll } 
        |> conn.DeleteAsync 
        |> Async.AwaitTask 
        |> Async.Ignore

    do! insert { into indexTable; values records } 
        |> conn.InsertAsync 
        |> Async.AwaitTask 
        |> Async.Ignore
}

let getIndexRecordsAsync() = async {
    use conn = getConnection()
    do! conn.OpenAsync() |> Async.AwaitTask

    let! results = 
        select { for _ in indexTable do selectAll } 
        |> conn.SelectAsync<IndexTableRecord> 
        |> Async.AwaitTask

    return results |> Seq.toList
}

let stockRecordTable = table<StockRecord>

let setStockRecordsAsync (records: StockRecord list) = async {
    use conn = getConnection()
    do! conn.OpenAsync() |> Async.AwaitTask
        
    do! delete { for _ in stockRecordTable do deleteAll } 
        |> conn.DeleteAsync 
        |> Async.AwaitTask 
        |> Async.Ignore

    do! insert { into stockRecordTable; values records } 
        |> conn.InsertAsync 
        |> Async.AwaitTask 
        |> Async.Ignore
}

let getStockRecordsAsync() = async {
    use conn = getConnection()
    do! conn.OpenAsync() |> Async.AwaitTask

    let! results = 
        select { for _ in stockRecordTable do selectAll } 
        |> conn.SelectAsync<StockRecord> 
        |> Async.AwaitTask

    return results |> Seq.toList
}

let currentPortfolioTable = table<CurrentPortfolio>

let setCurrentPortfolioAsync (records: CurrentPortfolio list) = async {
    use conn = getConnection()
    do! conn.OpenAsync() |> Async.AwaitTask

    do! delete { for _ in currentPortfolioTable do deleteAll } 
        |> conn.DeleteAsync 
        |> Async.AwaitTask 
        |> Async.Ignore

    do! insert { into currentPortfolioTable; values records } 
        |> conn.InsertAsync 
        |> Async.AwaitTask 
        |> Async.Ignore
}

let getCurrentPortfolioAsync() = async {
    use conn = getConnection()
    do! conn.OpenAsync() |> Async.AwaitTask

    let! results = 
        select { for _ in currentPortfolioTable do selectAll } 
        |> conn.SelectAsync<CurrentPortfolio> 
        |> Async.AwaitTask

    return results |> Seq.toList
}