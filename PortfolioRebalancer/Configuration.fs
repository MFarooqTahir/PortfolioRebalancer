module Configuration

open System

// Commission calculation constants
module Commission =
    let MinimumPrice = 12m
    let FixedRate = 0.03m
    let PercentageRate = 0.0025m

// Database configuration
module Database =
    let ConnectionString = "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234"
    let DefaultTimeout = TimeSpan.FromSeconds(30.0)
module App =
    let DefaultCulture = "hi-IN"
    let CurrencySymbol = "Rs."
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