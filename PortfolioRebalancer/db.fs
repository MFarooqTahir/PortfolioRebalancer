module Db
    open Npgsql
    open Dapper
    open Types
    open Dapper.FSharp.PostgreSQL
    open System.IO
    open System.Text.Json
    open System.Globalization

    // docker run --name portfolio-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres1234 -e POSTGRES_DB=portfolio -p 5432:5432 -d postgres
    let dbPath = "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234"
    let connStr = dbPath
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
        CREATE TABLE IF NOT EXISTS "PortfolioTransaction" (
            "Id" SERIAL PRIMARY KEY,
            "Symbol" TEXT NOT NULL,
            "TransactionType" TEXT NOT NULL,
            "Shares" INTEGER NOT NULL,
            "Price" NUMERIC(12,4) NOT NULL,
            "Date" TIMESTAMP NOT NULL
        );
        """
        do! conn.ExecuteAsync sql |> Async.AwaitTask |> Async.Ignore
    }

    let indexTable = table'<IndexTableRecord> "IndexTableRecord"
    let stockTable = table'<StockRecord> "StockRecord"

    [<CLIMutable>]
    type PortfolioTransactionDb = {
        Symbol: string
        TransactionType: string
        Shares: int
        Price: decimal
        Date: System.DateTime
    }

    let toDomain (db: PortfolioTransactionDb) : PortfolioTransaction =
        {
            Symbol = db.Symbol
            TransactionType =
                match db.TransactionType.ToUpperInvariant() with
                | "BUY" -> Types.TransactionType.Buy
                | "SELL" -> Types.TransactionType.Sell
                | _ -> failwithf "Invalid TransactionType: %s" db.TransactionType
            Shares = db.Shares
            Price = db.Price
            Date = db.Date
        }

    let fromDomain (tx: PortfolioTransaction) : PortfolioTransactionDb =
        {
            Symbol = tx.Symbol
            TransactionType = match tx.TransactionType with Types.TransactionType.Buy -> "Buy" | Types.TransactionType.Sell -> "Sell"
            Shares = tx.Shares
            Price = tx.Price
            Date = tx.Date
        }

    let txnTable = table'<PortfolioTransactionDb> "PortfolioTransaction"

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

    let setStockRecordsAsync (records: StockRecord list) = async {
        use conn = new NpgsqlConnection(connStr)
        do! conn.OpenAsync() |> Async.AwaitTask
        do! delete { for _ in stockTable do deleteAll } |> conn.DeleteAsync |> Async.AwaitTask |> Async.Ignore
        do! insert { into stockTable; values records } |> conn.InsertAsync |> Async.AwaitTask |> Async.Ignore
    }

    let getStockRecordsAsync () = async {
        use conn = new NpgsqlConnection(connStr)
        do! conn.OpenAsync() |> Async.AwaitTask
        let! results = select { for r in stockTable do selectAll } |> conn.SelectAsync<StockRecord> |> Async.AwaitTask
        return results |> Seq.toList
    }

    let getPortfolioTransactionsAsync () = async {
        use conn = new NpgsqlConnection(connStr)
        do! conn.OpenAsync() |> Async.AwaitTask
        let! results = select { for t in txnTable do selectAll } |> conn.SelectAsync<PortfolioTransactionDb> |> Async.AwaitTask
        return results |> Seq.toList
    }

    let setPortfolioTransactionsAsync (transactions: PortfolioTransaction list) = async {
        use conn = new NpgsqlConnection(connStr)
        do! conn.OpenAsync() |> Async.AwaitTask
        do! delete { for t in txnTable do deleteAll } |> conn.DeleteAsync |> Async.AwaitTask |> Async.Ignore
        do! insert { into txnTable; values (transactions |> List.map fromDomain) } |> conn.InsertAsync |> Async.AwaitTask |> Async.Ignore
    }

    let getCurrentLotsAndAvgPriceFromTxns (txns: PortfolioTransaction list) : int * decimal =
        let sorted = txns |> List.sortBy (fun t -> t.Date)
        let rec processLotCalculations lots (txns: PortfolioTransaction list) =
            match txns with
            | [] -> lots
            | t::rest ->
                match t.TransactionType with
                | TransactionType.Buy -> processLotCalculations (lots @ [t.Shares, t.Price]) rest
                | TransactionType.Sell ->
                    let rec remove n lots =
                        match n, lots with
                        | 0, _ -> lots
                        | _, [] -> []
                        | n, (s, p)::restLots ->
                            if s > n then (s - n, p)::restLots
                            else remove (n - s) restLots
                    processLotCalculations (remove t.Shares lots) rest
        let finalLots = processLotCalculations [] sorted
        let totalShares = finalLots |> List.sumBy fst
        let totalCost = finalLots |> List.sumBy (fun (s, p) -> decimal s * p)
        let avgPrice = if totalShares > 0 then totalCost / decimal totalShares else 0m
        totalShares, avgPrice

    let getCurrentLotsAndAvgPrice (txnsDb: PortfolioTransactionDb list) : int * decimal =
        txnsDb |> List.map toDomain |> getCurrentLotsAndAvgPriceFromTxns
