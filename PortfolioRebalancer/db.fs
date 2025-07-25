module Db
open Npgsql
open Dapper
open Types
open Dapper.FSharp.PostgreSQL

// docker run --name portfolio-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres1234 -e POSTGRES_DB=portfolio -p 5432:5432 -d postgres
let connStr = "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234"
OptionTypes.register()
let ensureTablesAsync() = async {
    use conn = new NpgsqlConnection(connStr)
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
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync() |> Async.AwaitTask
    do! delete { for r in indexTable do deleteAll } |> conn.DeleteAsync |> Async.AwaitTask |> Async.Ignore
    do! insert { into indexTable; values records } |> conn.InsertAsync |> Async.AwaitTask |> Async.Ignore
}
let getIndexRecordsAsync() = async {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync() |> Async.AwaitTask
    let! results = select { for r in indexTable do selectAll } |> conn.SelectAsync<IndexTableRecord> |> Async.AwaitTask
    return results |> Seq.toList
}
let stockRecordTable = table<StockRecord>
let setStockRecordsAsync (records: StockRecord list) = async {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync() |> Async.AwaitTask
    do! delete { for r in stockRecordTable do deleteAll } |> conn.DeleteAsync |> Async.AwaitTask |> Async.Ignore
    do! insert { into stockRecordTable; values records } |> conn.InsertAsync |> Async.AwaitTask |> Async.Ignore
}

let getStockRecordsAsync() = async {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync() |> Async.AwaitTask
    let! results = select { for r in stockRecordTable do selectAll } |> conn.SelectAsync<StockRecord> |> Async.AwaitTask
    return results |> Seq.toList
}
let currentPortfolioTable = table<CurrentPortfolio>
let setCurrentPortfolioAsync (records: CurrentPortfolio list) = async {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync() |> Async.AwaitTask
    do! delete { for r in currentPortfolioTable do deleteAll } |> conn.DeleteAsync |> Async.AwaitTask |> Async.Ignore
    do! insert { into currentPortfolioTable; values records } |> conn.InsertAsync |> Async.AwaitTask |> Async.Ignore
}

let getCurrentPortfolioAsync() = async {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync() |> Async.AwaitTask
    let! results = select { for r in currentPortfolioTable do selectAll } |> conn.SelectAsync<CurrentPortfolio> |> Async.AwaitTask
    return results |> Seq.toList
}