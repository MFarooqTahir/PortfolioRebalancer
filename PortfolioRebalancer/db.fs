module Db

open Npgsql
open Dapper
open Types
open Dapper.FSharp.PostgreSQL
open Configuration
open Logging
open Performance

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
    
    // Invalidate cache after updating data
    cacheManager.Remove "index_records"
    logger.Debug (sprintf "Updated %d index records and invalidated cache" records.Length)
}

let getIndexRecordsAsync() = async {
    // Check cache first
    match cacheManager.Get<IndexTableRecord list> "index_records" with
    | Some cached ->
        logger.Debug "Retrieved index records from cache"
        return cached
    | None ->
        logger.Debug "Cache miss for index records, fetching from database"
        use conn = getConnection()
        do! conn.OpenAsync() |> Async.AwaitTask

        let! results = 
            select { for _ in indexTable do selectAll } 
            |> conn.SelectAsync<IndexTableRecord> 
            |> Async.AwaitTask

        let records = results |> Seq.toList
        // Cache for 10 minutes (index data doesn't change frequently)
        cacheManager.Set("index_records", records, 10)
        logger.Debug (sprintf "Cached %d index records for 10 minutes" records.Length)
        return records
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
    
    // Invalidate cache after updating data
    cacheManager.Remove "stock_records"
    logger.Debug (sprintf "Updated %d stock records and invalidated cache" records.Length)
}

let getStockRecordsAsync() = async {
    // Check cache first
    match cacheManager.Get<StockRecord list> "stock_records" with
    | Some cached ->
        logger.Debug "Retrieved stock records from cache"
        return cached
    | None ->
        logger.Debug "Cache miss for stock records, fetching from database"
        use conn = getConnection()
        do! conn.OpenAsync() |> Async.AwaitTask

        let! results = 
            select { for _ in stockRecordTable do selectAll } 
            |> conn.SelectAsync<StockRecord> 
            |> Async.AwaitTask

        let records = results |> Seq.toList
        // Cache for 2 minutes (stock prices change frequently)
        cacheManager.Set("stock_records", records, 2)
        logger.Debug (sprintf "Cached %d stock records for 2 minutes" records.Length)
        return records
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
    
    // Invalidate cache after updating data
    cacheManager.Remove "current_portfolio"
    logger.Debug (sprintf "Updated %d current portfolio records and invalidated cache" records.Length)
}

let getCurrentPortfolioAsync() = async {
    // Check cache first
    match cacheManager.Get<CurrentPortfolio list> "current_portfolio" with
    | Some cached ->
        logger.Debug "Retrieved current portfolio from cache"
        return cached
    | None ->
        logger.Debug "Cache miss for current portfolio, fetching from database"
        use conn = getConnection()
        do! conn.OpenAsync() |> Async.AwaitTask

        let! results = 
            select { for _ in currentPortfolioTable do selectAll } 
            |> conn.SelectAsync<CurrentPortfolio> 
            |> Async.AwaitTask

        let records = results |> Seq.toList
        // Cache for 5 minutes (portfolio changes when imported)
        cacheManager.Set("current_portfolio", records, 5)
        logger.Debug (sprintf "Cached %d current portfolio records for 5 minutes" records.Length)
        return records
}