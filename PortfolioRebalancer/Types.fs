module Types
    type ScrapingError = 
        | NavigationError of string
        | ElementNotFound of string
        | DataExtractionError of string
        | PortfolioCreationError of string
        | FileOperationError of string

    type IndexTableRecord = {
        Symbol: string
        Weight: decimal
    }

    type IndexTableWithPrice = {
        Symbol: string
        Name: string
        Weight: decimal
        Price: decimal
    }

    type StockRecord = {
        Symbol: string
        Name: string
        Price: decimal
    }

    type CurrentPortfolioRecord = {
        Symbol: string
        Shares: int
        AveragePrice: decimal
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