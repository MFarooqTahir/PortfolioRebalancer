module Configuration

open System

// Environment-aware configuration
type Environment = 
    | Development
    | Staging
    | Production

let getEnvironment() : Environment =
    match Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") with
    | "Production" -> Production
    | "Staging" -> Staging
    | _ -> Development

// Commission calculation constants
module Commission =
    let MinimumPrice = 12m
    let FixedRate = 0.03m
    let PercentageRate = 0.0025m
    let MaxCommission = 100m // Maximum commission per trade

// Database configuration with environment support
module Database =
    let private getConnectionString env =
        match env with
        | Production -> 
            Environment.GetEnvironmentVariable("DATABASE_URL") 
            |> Option.ofObj 
            |> Option.defaultValue "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234"
        | _ -> "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234"
    
    let ConnectionString = getConnectionString (getEnvironment())
    let DefaultTimeout = TimeSpan.FromSeconds(30.0)
    let MaxRetries = 3
    let RetryDelay = TimeSpan.FromSeconds(1.0)

// Application settings
module App =
    let DefaultCulture = "hi-IN"
    let CurrencySymbol = "Rs."
    let DefaultDecimalPlaces = 2
    let PercentageDecimalPlaces = 2
    let LogLevel = 
        match getEnvironment() with
        | Production -> "Warning"
        | Staging -> "Information"
        | Development -> "Debug"

// Performance settings
module Performance =
    let CacheExpirationMinutes = 15
    let MaxCacheSize = 1000
    let PerformanceMetricsRetentionHours = 24

// Validation rules
module Validation =
    let MinimumCashAmount = 0m
    let MaximumCashAmount = 1_000_000_000m // 1 billion
    let MinimumShares = 0
    let MaximumShares = 1_000_000_000 // 1 billion shares
    let MinimumPrice = 0.01m
    let MaximumPrice = 1_000_000m // 1 million per share
    let MinimumWeight = 0m
    let MaximumWeight = 1m // 100%
    let MaxSymbolLength = 10
    let MinSymbolLength = 1

// Error messages
module Messages =
    let InvalidCashAmount = "Cash amount must be non-negative"
    let InvalidSymbol = "Symbol cannot be empty or whitespace"
    let MissingPriceData = "Price data is missing for some symbols"
    let EmptyIndex = "Index data is empty"
    let EmptyPortfolio = "Portfolio data is empty"
    let InsufficientData = "Insufficient data for calculation"
    let InvalidOption = "Invalid menu option selected"
    let DatabaseConnectionFailed = "Failed to connect to database"
    let OperationTimeout = "Operation timed out"
    let InvalidConfiguration = "Invalid configuration detected" 