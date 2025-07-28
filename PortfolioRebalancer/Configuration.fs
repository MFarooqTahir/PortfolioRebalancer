module Configuration

open System
open Microsoft.Extensions.Configuration

// Global configuration instance
let private configuration : IConfiguration = 
    let builder = ConfigurationBuilder()
    
    // Try to find the configuration files in multiple possible locations
    let possiblePaths = [
        IO.Directory.GetCurrentDirectory()
        IO.Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
        IO.Path.Combine(IO.Directory.GetCurrentDirectory(), "..", "PortfolioRebalancer")
        IO.Path.Combine(IO.Directory.GetCurrentDirectory(), "..", "PortfolioRebalancer.Tests")
    ]
    
    let basePath = 
        possiblePaths 
        |> List.tryFind (fun path -> 
            IO.File.Exists(IO.Path.Combine(path, "appsettings.json")))
        |> Option.defaultValue (IO.Directory.GetCurrentDirectory())
    
    builder
        .SetBasePath(basePath)
        .AddJsonFile("appsettings.json", optional = true, reloadOnChange = true)
        .AddJsonFile("appsettings.Development.json", optional = true, reloadOnChange = true)
        .AddJsonFile("appsettings.Production.json", optional = true, reloadOnChange = true)
        .AddEnvironmentVariables()
        |> fun b -> b.Build()

// Commission calculation constants
module Commission =
    let MinimumPrice = configuration.GetValue<decimal>("Commission:MinimumPrice", 12m)
    let FixedRate = configuration.GetValue<decimal>("Commission:FixedRate", 0.03m)
    let PercentageRate = configuration.GetValue<decimal>("Commission:PercentageRate", 0.0025m)

// Database configuration
module Database =
    let ConnectionString = configuration.GetValue<string>("Database:ConnectionString", "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234")
    let DefaultTimeout = TimeSpan.FromSeconds(configuration.GetValue<float>("Database:DefaultTimeoutSeconds", 30.0))

module App =
    let DefaultCulture = configuration.GetValue<string>("App:DefaultCulture", "hi-IN")
    let CurrencySymbol = configuration.GetValue<string>("App:CurrencySymbol", "Rs.")

// Validation rules
module Validation =
    let MinimumCashAmount = configuration.GetValue<decimal>("Validation:MinimumCashAmount", 0m)
    let MaximumCashAmount = configuration.GetValue<decimal>("Validation:MaximumCashAmount", 1_000_000_000m)
    let MinimumShares = configuration.GetValue<int>("Validation:MinimumShares", 0)
    let MaximumShares = configuration.GetValue<int>("Validation:MaximumShares", 1_000_000_000)
    let MinimumPrice = configuration.GetValue<decimal>("Validation:MinimumPrice", 0.01m)
    let MaximumPrice = configuration.GetValue<decimal>("Validation:MaximumPrice", 1_000_000m)
    let MinimumWeight = configuration.GetValue<decimal>("Validation:MinimumWeight", 0m)
    let MaximumWeight = configuration.GetValue<decimal>("Validation:MaximumWeight", 1m)
    let MaxSymbolLength = configuration.GetValue<int>("Validation:MaxSymbolLength", 10)
    let MinSymbolLength = configuration.GetValue<int>("Validation:MinSymbolLength", 1) 