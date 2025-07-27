module Messages

open Configuration

// Only keep the messages that are actually used
module Errors =
    let InvalidCashAmount amount = 
        sprintf "Invalid cash amount: %M. Please enter a positive number between %M and %M." 
            amount Validation.MinimumCashAmount Validation.MaximumCashAmount
    
    let InvalidSymbol symbol = 
        sprintf "Invalid symbol: '%s'. Symbol must be between %d and %d characters." 
            symbol Validation.MinSymbolLength Validation.MaxSymbolLength
    
    let EmptyIndex = "Index data is empty. Please fetch index data first."
    let EmptyPortfolio = "Portfolio data is empty. Please import portfolio data first."

module Success =
    let RebalancingCompleted = "Rebalancing completed successfully!"
    let PortfolioImported = "Portfolio imported successfully!" 