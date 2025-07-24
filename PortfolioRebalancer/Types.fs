module Types
open System
open System.Text.Json.Serialization

type ScrapingError = 
    | NavigationError of string
    | ElementNotFound of string
    | DataExtractionError of string
    | PortfolioCreationError of string
    | FileOperationError of string

[<CLIMutable>]
type IndexTableRecord = {
    Symbol: string
    Weight: decimal // Always 4 decimal places
}

[<CLIMutable>]
type IndexTableWithPrice = {
    Symbol: string
    Name: string
    Weight: decimal // Always 4 decimal places
    Price: decimal // Always 4 decimal places
}

[<CLIMutable>]
type StockRecord = {
    Symbol: string
    Name: string
    Price: decimal // Always 4 decimal places
}

type TransactionType = Buy | Sell

type PortfolioTransaction = {
    Symbol: string
    TransactionType: TransactionType
    Shares: int
    Price: decimal // Always 4 decimal places
    Date: DateTime
}

[<CLIMutable>]
type PortfolioTransactionDb = {
    Symbol: string
    TransactionType: string
    Shares: int
    Price: decimal // Always 4 decimal places
    Date: DateTime
}

type CurrentPortfolioRecord = {
    Symbol: string
    Transactions: PortfolioTransaction list
}

type IndexType = 
    | KSE30
    | KSE100

type TableStockRecord = {
    Symbol: string
    Name: string
    Price: decimal 
    Weight: decimal
}

type PortfolioPosition = {
    Symbol: string
    Name: string
    TargetWeight: decimal
    CurrentPrice: decimal
    ActualShares: int
    ActualValue: decimal
    Commission: decimal
    TotalCost: decimal
}
type RebalanceAction = 
| Buy of Symbol: string * UnitPrice: decimal * Shares: int * Value: decimal * Commission: decimal
| Sell of Symbol: string * UnitPrice: decimal * Shares: int * Value: decimal * Commission: decimal
| Hold of Symbol: string * UnitPrice: decimal * Shares: int * Value: decimal
