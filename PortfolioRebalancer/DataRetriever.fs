module CachedFileData
    open System.IO
    open System.Text.Json
    open Types

    let private JsonOptions = JsonSerializerOptions()
    JsonOptions.WriteIndented <- true

    let mutable private IndexRecordCache: IndexTableRecord list option = None

    let SetIndexRecords indexCacheFile (indexTableRecords: IndexTableRecord list) = async {

        let indexRecords = JsonSerializer.Serialize(indexTableRecords, JsonOptions)
        do! File.WriteAllTextAsync(indexCacheFile, indexRecords) |> Async.AwaitTask
        IndexRecordCache <- Some indexTableRecords
    }

    let GetIndexRecords indexCacheFile = async {
        match IndexRecordCache with
        | Some cachedRecords -> 
            return cachedRecords 
        | None -> 
            let! fileData = File.ReadAllTextAsync(indexCacheFile) |> Async.AwaitTask
            let records = JsonSerializer.Deserialize<IndexTableRecord list> fileData
            IndexRecordCache <- Some records
            return records
    }
    
    let mutable private IndividualStockRecordsCache: StockRecord list option = None

    let SetIndividualStockData stockDataCacheFile (stockRecords: StockRecord list) = async {

        let individualStockData = JsonSerializer.Serialize(stockRecords, JsonOptions)
        do! File.WriteAllTextAsync(stockDataCacheFile, individualStockData) |> Async.AwaitTask
        IndividualStockRecordsCache <- Some stockRecords
    }

    let GetIndividualStockData stockDataCacheFile = async {
        match IndividualStockRecordsCache with
        | Some cachedRecords -> 
            return cachedRecords
        | None -> 
            let! fileData = File.ReadAllTextAsync(stockDataCacheFile) |> Async.AwaitTask
            let records = JsonSerializer.Deserialize<StockRecord list> fileData
            IndividualStockRecordsCache <- Some records
            return records
    }

    let mutable private CurrentPortfolioRecordsCache: CurrentPortfolioRecord list option = None

    let GetCurrentPortfolioRecordsData currentPortfolioPath = async {
        match CurrentPortfolioRecordsCache with
        | Some cachedRecords -> 
            return cachedRecords
        | None -> 
            let! fileData = File.ReadAllTextAsync(currentPortfolioPath) |> Async.AwaitTask
            let records = JsonSerializer.Deserialize<CurrentPortfolioRecord list> fileData
            CurrentPortfolioRecordsCache <- Some records
            return records
    }
