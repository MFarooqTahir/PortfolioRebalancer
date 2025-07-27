module Messages

open Configuration

// User-friendly error messages
module Errors =
    let InvalidCashAmount amount = 
        sprintf "Invalid cash amount: %M. Please enter a positive number between %M and %M." 
            amount Validation.MinimumCashAmount Validation.MaximumCashAmount
    
    let InvalidSymbol symbol = 
        sprintf "Invalid symbol: '%s'. Symbol must be between %d and %d characters." 
            symbol Validation.MinSymbolLength Validation.MaxSymbolLength
    
    let MissingPriceData symbols = 
        sprintf "Missing price data for symbols: %s" (String.concat ", " symbols)
    
    let EmptyIndex = "Index data is empty. Please fetch index data first."
    let EmptyPortfolio = "Portfolio data is empty. Please import portfolio data first."
    let InsufficientData reason = sprintf "Insufficient data: %s" reason
    let InvalidOption = "Invalid menu option selected. Please choose a valid option."
    let DatabaseConnectionFailed = "Failed to connect to database. Please check your connection settings."
    let OperationTimeout = "Operation timed out. Please try again."
    let InvalidConfiguration = "Invalid configuration detected. Please check your settings."

// Success messages
module Success =
    let DatabaseInitialized = "Database initialized successfully!"
    let PortfolioImported = "Portfolio imported successfully!"
    let DataFetched = "Market data fetched and saved successfully!"
    let RebalancingCompleted = "Rebalancing completed successfully!"
    let CacheCleared = "Cache cleared successfully!"
    let PerformanceMetricsCleared = "Performance metrics cleared successfully!"

// Information messages
module Info =
    let StartingApplication = "Starting Portfolio Rebalancer..."
    let InitializingDatabase = "Initializing database..."
    let LoadingPortfolioData = "Loading portfolio data..."
    let ImportingPortfolio = "Importing portfolio from JSON..."
    let FetchingMarketData = "Fetching market data..."
    let PerformingRebalancing = "Performing rebalancing calculations..."
    let ShowingPerformanceMetrics = "Showing performance metrics..."

// Warning messages
module Warnings =
    let MissingPriceData symbols = 
        sprintf "Warning: No price data for symbols: %s" (String.concat ", " symbols)
    let DatabaseRetry attempt maxAttempts = 
        sprintf "Database operation failed (attempt %d/%d), retrying..." attempt maxAttempts
    let CacheExpired = "Cache has expired, fetching fresh data..."
    let PerformanceDegraded = "Performance is degraded, consider clearing cache."

// Menu and UI messages
module UI =
    let Welcome = "Welcome to Portfolio Rebalancer!"
    let Goodbye = "Thank you for using Portfolio Rebalancer!"
    let InvalidInput = "Invalid input. Please try again."
    let ConfirmationRequired = "Please confirm your action."
    let OperationCancelled = "Operation cancelled by user."
    let PressAnyKey = "Press any key to continue..."
    let Loading = "Loading..."
    let Processing = "Processing..."
    let Completed = "Completed!"
    let Failed = "Failed!"

// Help messages
module Help =
    let CashAmountHelp = "Enter the amount of cash you want to invest (e.g., 10000)"
    let CommissionHelp = "Include commission in calculations? This affects the final portfolio value."
    let SymbolHelp = "Enter the stock symbol (e.g., AAPL, MSFT)"
    let MenuHelp = "Select an option from the menu above."
    let RebalancingHelp = "Rebalancing will adjust your portfolio to match the target weights." 