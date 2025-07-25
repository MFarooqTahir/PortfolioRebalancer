module Types

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
type StockRecord = {
    Symbol: string
    Name: string
    Price: decimal 
}

type CurrentPortfolio = {
    Symbol: string
    Shares: int
    Price: decimal 
}

type IndexType = 
    | KSE30
    | KSE100

type RebalanceAction = 
| Buy of Symbol: string * UnitPrice: decimal * Shares: int * Value: decimal * Commission: decimal
| Sell of Symbol: string * UnitPrice: decimal * Shares: int * Value: decimal * Commission: decimal
| Hold of Symbol: string * UnitPrice: decimal * Shares: int * Value: decimal
