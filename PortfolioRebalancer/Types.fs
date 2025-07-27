module Types

// Enhanced error types with better categorization
type ScrapingError = 
    | NavigationError of string
    | ElementNotFound of string
    | DataExtractionError of string
    | PortfolioCreationError of string
    | FileOperationError of string
    | TimeoutError of string
    | NetworkError of string

type ValidationError =
    | InvalidCashAmount of decimal
    | InvalidSymbol of string
    | MissingPriceData of string list
    | EmptyIndex
    | EmptyPortfolio
    | InsufficientData of string
    | InvalidWeight of decimal
    | InvalidPrice of decimal
    | InvalidShares of int

type DatabaseError =
    | ConnectionError of string
    | QueryError of string
    | DataError of string
    | TransactionError of string

type ApplicationError =
    | Validation of ValidationError
    | Scraping of ScrapingError
    | Database of DatabaseError
    | Configuration of string
    | Unexpected of string

// Enhanced result types for better error handling
type OperationResult<'T> = Result<'T, ApplicationError>
type ValidationResult<'T> = Result<'T, ValidationError>
type DatabaseResult<'T> = Result<'T, DatabaseError>

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

// Validation types
type ValidatedRebalanceConfig = {
    IncludeCommission: bool
    Cash: decimal
    Index: IndexTableRecord list
    Current: CurrentPortfolio list
    Prices: StockRecord list
    MissingSymbols: string list
}

// Domain types for better semantics
type Symbol = Symbol of string
type CashAmount = CashAmount of decimal
type ShareCount = ShareCount of int
type Price = Price of decimal
type Weight = Weight of decimal
