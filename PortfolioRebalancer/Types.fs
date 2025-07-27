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

// New types for better structure
type RebalanceOrder = {
    Symbol: string
    Shares: int
    Price: decimal
    Value: decimal
    Commission: decimal
}

type FinalPortfolioPosition = {
    Symbol: string
    Shares: int
    Value: decimal
    Commission: decimal
    Price: decimal
    Weight: decimal
    FinalWeight: decimal
}

type RebalanceResult = {
    Sells: RebalanceOrder list
    Buys: RebalanceOrder list
    Final: FinalPortfolioPosition list
    CashAfterSelling: decimal
    CashAfterBuying: decimal
    TotalSellValue: decimal
    TotalBuyValue: decimal
    TotalSellCommission: decimal
    TotalBuyCommission: decimal
}

type IndexRebalanceResult = {
    Orders: RebalanceOrder list
    TotalValue: decimal
    TotalCommission: decimal
    RemainingCash: decimal
}

type RebalanceConfig = {
    IncludeCommission: bool
    Cash: decimal
    Index: IndexTableRecord list
    Current: CurrentPortfolio list
    Prices: StockRecord list
}
