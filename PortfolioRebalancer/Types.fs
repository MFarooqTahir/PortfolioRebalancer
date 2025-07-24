module Types
open System

type ScrapingError = 
    | NavigationError of string
    | ElementNotFound of string
    | DataExtractionError of string
    | PortfolioCreationError of string
    | FileOperationError of string

[<CLIMutable>]
type IndexTableRecord = {
    Symbol: string
    Weight: decimal
}

[<CLIMutable>]
type IndexTableWithPrice = {
    Symbol: string
    Name: string
    Weight: decimal 
    Price: decimal 
}

[<CLIMutable>]
type StockRecord = {
    Symbol: string
    Name: string
    Price: decimal 
}

type TransactionType = Buy | Sell

type PortfolioTransaction = {
    Symbol: string
    TransactionType: TransactionType
    Shares: int
    Price: decimal 
    Date: DateTime
}

[<CLIMutable>]
type PortfolioTransactionDb = {
    Symbol: string
    TransactionType: string
    Shares: int
    Price: decimal 
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
