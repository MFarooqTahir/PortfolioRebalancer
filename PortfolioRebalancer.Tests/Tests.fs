module Tests

open Xunit
open System
open Types
open Helpers
open Validation
open Configuration
open Extensions
open Messages
open RebalancingCalculations
open Spectre.Console

// ============================================================================
// VALIDATION TESTS
// ============================================================================

[<Fact>]
let ``validateCashAmount should accept positive amounts`` () =
    let result = validateCashAmount 1000m
    match result with
    | Ok (CashAmount amount) -> Assert.Equal(1000m, amount)
    | Error _ -> Assert.True(false, "Should not return error for positive amount")

[<Fact>]
let ``validateCashAmount should accept zero`` () =
    let result = validateCashAmount 0m
    match result with
    | Ok (CashAmount amount) -> Assert.Equal(0m, amount)
    | Error _ -> Assert.True(false, "Should not return error for zero amount")

[<Fact>]
let ``validateCashAmount should reject negative amounts`` () =
    let result = validateCashAmount -100m
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative amount")
    | Error (InvalidCashAmount amount) -> Assert.Equal(-100m, amount)
    | Error _ -> Assert.True(false, "Should return InvalidCashAmount error")

[<Fact>]
let ``validateIndexNotEmpty should accept non-empty list`` () =
    let index: IndexTableRecord list = [{ Symbol = "AAPL"; Weight = 0.5m }]
    let result = validateIndexNotEmpty index
    match result with
    | Ok validatedIndex -> Assert.Equal<IndexTableRecord list>(index, validatedIndex)
    | Error _ -> Assert.True(false, "Should not return error for non-empty list")

[<Fact>]
let ``validateIndexNotEmpty should reject empty list`` () =
    let result = validateIndexNotEmpty []
    match result with
    | Ok _ -> Assert.True(false, "Should return error for empty list")
    | Error EmptyIndex -> Assert.True(true)
    | Error _ -> Assert.True(false, "Should return EmptyIndex error")

[<Fact>]
let ``validatePrice should accept valid prices`` () =
    let result = validatePrice 100.50m
    match result with
    | Ok (Price price) -> Assert.Equal(100.50m, price)
    | Error _ -> Assert.True(false, "Should not return error for valid price")

[<Fact>]
let ``validatePrice should reject negative prices`` () =
    let result = validatePrice -10m
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative price")
    | Error (InvalidPrice price) -> Assert.Equal(-10m, price)
    | Error _ -> Assert.True(false, "Should return InvalidPrice error")

[<Fact>]
let ``validateShares should accept valid share counts`` () =
    let result = validateShares 100
    match result with
    | Ok (ShareCount shares) -> Assert.Equal(100, shares)
    | Error _ -> Assert.True(false, "Should not return error for valid share count")

[<Fact>]
let ``validateShares should reject negative share counts`` () =
    let result = validateShares -10
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative share count")
    | Error (InvalidShares shares) -> Assert.Equal(-10, shares)
    | Error _ -> Assert.True(false, "Should return InvalidShares error")

[<Fact>]
let ``validateWeight should accept valid weights`` () =
    let result = validateWeight 0.5m
    match result with
    | Ok (Weight weight) -> Assert.Equal(0.5m, weight)
    | Error _ -> Assert.True(false, "Should not return error for valid weight")

[<Fact>]
let ``validateWeight should reject negative weights`` () =
    let result = validateWeight -0.1m
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative weight")
    | Error (InvalidWeight weight) -> Assert.Equal(-0.1m, weight)
    | Error _ -> Assert.True(false, "Should return InvalidWeight error")

[<Fact>]
let ``validateSymbol should accept valid symbols`` () =
    let result = validateSymbol "AAPL"
    match result with
    | Ok (Symbol symbol) -> Assert.Equal("AAPL", symbol)
    | Error _ -> Assert.True(false, "Should not return error for valid symbol")

[<Fact>]
let ``validateSymbol should reject empty symbols`` () =
    let result = validateSymbol ""
    match result with
    | Ok _ -> Assert.True(false, "Should return error for empty symbol")
    | Error (InvalidSymbol symbol) -> Assert.Equal("", symbol)
    | Error _ -> Assert.True(false, "Should return InvalidSymbol error")

[<Fact>]
let ``validateSymbol should reject null symbols`` () =
    let result = validateSymbol null
    match result with
    | Ok _ -> Assert.True(false, "Should return error for null symbol")
    | Error (InvalidSymbol symbol) -> Assert.Equal("", symbol)
    | Error _ -> Assert.True(false, "Should return InvalidSymbol error")

[<Fact>]
let ``ValidDecimal pattern should parse valid decimals`` () =
    let testCases = [("100", 100m); ("1,000", 1000m); ("0", 0m)]
    for (input, expected) in testCases do
        match input with
        | ValidDecimal value -> Assert.Equal(expected, value)
        | _ -> Assert.True(false, sprintf "Should parse '%s' as %M" input expected)

[<Fact>]
let ``ValidDecimal pattern should reject invalid decimals`` () =
    let testCases = ["invalid"; ""; "abc"]
    for input in testCases do
        match input with
        | ValidDecimal _ -> Assert.True(false, sprintf "Should not parse '%s' as decimal" input)
        | _ -> Assert.True(true)

// ============================================================================
// HELPER FUNCTION TESTS
// ============================================================================

[<Fact>]
let ``calculateCommission should use fixed rate for low prices`` () =
    let commission = calculateCommission 10m 100
    let expected = Commission.FixedRate * 100m
    Assert.Equal(expected, commission)

[<Fact>]
let ``calculateCommission should use percentage rate for high prices`` () =
    let price = 20m
    let shares = 50
    let commission = calculateCommission price shares
    let expected = price * decimal shares * Commission.PercentageRate
    Assert.Equal(expected, commission)

[<Fact>]
let ``createWeightMap should create correct map`` () =
    let index = [
        { Symbol = "AAPL"; Weight = 0.5m }
        { Symbol = "MSFT"; Weight = 0.3m }
        { Symbol = "GOOGL"; Weight = 0.2m }
    ]
    let weightMap = createWeightMap index
    Assert.Equal(0.5m, weightMap.["AAPL"])
    Assert.Equal(0.3m, weightMap.["MSFT"])
    Assert.Equal(0.2m, weightMap.["GOOGL"])
    Assert.Equal(3, weightMap.Count)

[<Fact>]
let ``formatPKR should format currency correctly`` () =
    let formatted = formatPKR 1234.56m
    Assert.Contains("Rs.", formatted)
    Assert.Contains("1,234.56", formatted)

[<Fact>]
let ``toPercentage should format correctly`` () =
    let formatted = toPercentage 0.1234m
    Assert.Equal("12.34%", formatted)

[<Fact>]
let ``formatPKRShort should format without decimals`` () =
    let formatted = formatPKRShort 1234.56m
    Assert.Contains("Rs.", formatted)
    Assert.Contains("1,235", formatted) // Should round up

// ============================================================================
// EXTENSIONS TESTS
// ============================================================================

[<Fact>]
let ``ListExtensions.tryHead should return Some for non-empty list`` () =
    let result = ListExtensions.tryHead [1; 2; 3]
    Assert.Equal(Some 1, result)

[<Fact>]
let ``ListExtensions.tryHead should return None for empty list`` () =
    let result = ListExtensions.tryHead []
    Assert.Equal(None, result)

[<Fact>]
let ``ListExtensions.tryLast should return Some for non-empty list`` () =
    let result = ListExtensions.tryLast [1; 2; 3]
    Assert.Equal(Some 3, result)

[<Fact>]
let ``ListExtensions.tryLast should return None for empty list`` () =
    let result = ListExtensions.tryLast []
    Assert.Equal(None, result)

[<Fact>]
let ``MapExtensions.tryFindOrDefault should return value when key exists`` () =
    let map = Map.ofList [("A", 1); ("B", 2)]
    let result = MapExtensions.tryFindOrDefault map "A" 0
    Assert.Equal(1, result)

[<Fact>]
let ``MapExtensions.tryFindOrDefault should return default when key doesn't exist`` () =
    let map = Map.ofList [("A", 1); ("B", 2)]
    let result = MapExtensions.tryFindOrDefault map "C" 0
    Assert.Equal(0, result)

[<Fact>]
let ``DecimalExtensions.toPercentage should format correctly`` () =
    let result = DecimalExtensions.toPercentage 0.1234m
    Assert.Equal("12.34%", result)

[<Fact>]
let ``DecimalExtensions.isPositive should return true for positive values`` () =
    Assert.True(DecimalExtensions.isPositive 5.5m)
    Assert.False(DecimalExtensions.isPositive -5.5m)
    Assert.False(DecimalExtensions.isPositive 0m)

[<Fact>]
let ``DecimalExtensions.isNegative should return true for negative values`` () =
    Assert.True(DecimalExtensions.isNegative -5.5m)
    Assert.False(DecimalExtensions.isNegative 5.5m)
    Assert.False(DecimalExtensions.isNegative 0m)

[<Fact>]
let ``DecimalExtensions.isZero should return true for zero`` () =
    Assert.True(DecimalExtensions.isZero 0m)
    Assert.False(DecimalExtensions.isZero 5.5m)
    Assert.False(DecimalExtensions.isZero -5.5m)

[<Fact>]
let ``IntExtensions.toDecimal should convert correctly`` () =
    let result = IntExtensions.toDecimal 42
    Assert.Equal(42m, result)

[<Fact>]
let ``IntExtensions.isPositive should return true for positive values`` () =
    Assert.True(IntExtensions.isPositive 5)
    Assert.False(IntExtensions.isPositive -5)
    Assert.False(IntExtensions.isPositive 0)

[<Fact>]
let ``IntExtensions.isNegative should return true for negative values`` () =
    Assert.True(IntExtensions.isNegative -5)
    Assert.False(IntExtensions.isNegative 5)
    Assert.False(IntExtensions.isNegative 0)

[<Fact>]
let ``IntExtensions.isZero should return true for zero`` () =
    Assert.True(IntExtensions.isZero 0)
    Assert.False(IntExtensions.isZero 5)
    Assert.False(IntExtensions.isZero -5)

// ============================================================================
// REBALANCING CALCULATION TESTS
// ============================================================================

[<Fact>]
let ``createPriceMap should create correct map`` () =
    let prices = [
        { Symbol = "AAPL"; Name = "Apple Inc."; Price = 150.0m }
        { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 300.0m }
        { Symbol = "GOOGL"; Name = "Alphabet Inc."; Price = 2500.0m }
    ]
    let priceMap = createPriceMap prices
    Assert.Equal(150.0m, priceMap.["AAPL"])
    Assert.Equal(300.0m, priceMap.["MSFT"])
    Assert.Equal(2500.0m, priceMap.["GOOGL"])
    Assert.Equal(3, priceMap.Count)

[<Fact>]
let ``createCurrentSharesMap should create correct map`` () =
    let current = [
        { Symbol = "AAPL"; Shares = 100; Price = 150.0m }
        { Symbol = "MSFT"; Shares = 50; Price = 300.0m }
        { Symbol = "GOOGL"; Shares = 10; Price = 2500.0m }
    ]
    let sharesMap = createCurrentSharesMap current
    Assert.Equal(100, sharesMap.["AAPL"])
    Assert.Equal(50, sharesMap.["MSFT"])
    Assert.Equal(10, sharesMap.["GOOGL"])
    Assert.Equal(3, sharesMap.Count)

[<Fact>]
let ``calculateTargetShares should calculate correct target shares`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let targetShares = calculateTargetShares config
    Assert.Equal(60, targetShares.["AAPL"]) // 10000 * 0.6 / 100
    Assert.Equal(20, targetShares.["MSFT"]) // 10000 * 0.4 / 200

[<Fact>]
let ``rebalanceIndex should calculate correct orders`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(2, rebalanceResult.Orders.Length)
        let aaplOrder = rebalanceResult.Orders |> List.find (fun o -> o.Symbol = "AAPL")
        let msftOrder = rebalanceResult.Orders |> List.find (fun o -> o.Symbol = "MSFT")
        Assert.Equal(60, aaplOrder.Shares)
        Assert.Equal(20, msftOrder.Shares)
        Assert.Equal(6000m, aaplOrder.Value)
        Assert.Equal(4000m, msftOrder.Value)
        Assert.Equal(10000m, rebalanceResult.TotalValue)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``rebalanceCurrentPortfolio should calculate sell and buy orders`` () =
    let config = {
        IncludeCommission = false
        Cash = 5000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 100; Price = 100m } // Current value: 10000
            { Symbol = "MSFT"; Shares = 20; Price = 200m }  // Current value: 4000
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceCurrentPortfolio config
    match result with
    | Ok rebalanceResult ->
        // Total portfolio value: 5000 + 10000 + 4000 = 19000
        // Target AAPL: 19000 * 0.6 / 100 = 114 shares (need to buy 14)
        // Target MSFT: 19000 * 0.4 / 200 = 38 shares (need to buy 18)
        Assert.Equal(0, rebalanceResult.Sells.Length) // No sells needed
        Assert.Equal(2, rebalanceResult.Buys.Length)
        let aaplBuy = rebalanceResult.Buys |> List.find (fun o -> o.Symbol = "AAPL")
        let msftBuy = rebalanceResult.Buys |> List.find (fun o -> o.Symbol = "MSFT")
        Assert.Equal(14, aaplBuy.Shares)
        Assert.Equal(18, msftBuy.Shares)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``rebalanceCurrentPortfolio should calculate sell orders when overweight`` () =
    let config = {
        IncludeCommission = false
        Cash = 0m
        Index = [
            { Symbol = "AAPL"; Weight = 0.5m }
            { Symbol = "MSFT"; Weight = 0.5m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 100; Price = 100m } // Current value: 10000
            { Symbol = "MSFT"; Shares = 100; Price = 200m } // Current value: 20000
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceCurrentPortfolio config
    match result with
    | Ok rebalanceResult ->
        // Total portfolio value: 30000
        // Target AAPL: 30000 * 0.5 / 100 = 150 shares (need to buy 50)
        // Target MSFT: 30000 * 0.5 / 200 = 75 shares (need to sell 25)
        Assert.Equal(1, rebalanceResult.Sells.Length)
        Assert.Equal(1, rebalanceResult.Buys.Length)
        let msftSell = rebalanceResult.Sells |> List.find (fun o -> o.Symbol = "MSFT")
        let aaplBuy = rebalanceResult.Buys |> List.find (fun o -> o.Symbol = "AAPL")
        Assert.Equal(25, msftSell.Shares)
        Assert.Equal(50, aaplBuy.Shares)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``rebalanceIndex should return error for empty index`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = []
        Current = []
        Prices = []
    }
    let result = rebalanceIndex config
    match result with
    | Ok _ -> Assert.True(false, "Should return error for empty index")
    | Error EmptyIndex -> Assert.True(true)
    | Error _ -> Assert.True(false, "Should return EmptyIndex error")

[<Fact>]
let ``rebalanceIndex should handle missing price data`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            // MSFT price missing
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        // Should only include AAPL order, skip MSFT due to missing price
        Assert.Equal(1, rebalanceResult.Orders.Length)
        let aaplOrder = rebalanceResult.Orders |> List.find (fun o -> o.Symbol = "AAPL")
        Assert.Equal(60, aaplOrder.Shares)
    | Error _ -> Assert.True(false, "Should handle missing price data gracefully")

// ============================================================================
// CONFIGURATION TESTS
// ============================================================================

[<Fact>]
let ``Configuration should have valid commission rates`` () =
    Assert.True(Commission.FixedRate > 0m)
    Assert.True(Commission.PercentageRate > 0m)
    Assert.True(Commission.PercentageRate < 1m)

[<Fact>]
let ``Configuration should have valid database settings`` () =
    Assert.NotNull Database.ConnectionString
    Assert.True(Database.DefaultTimeout > TimeSpan.Zero)

[<Fact>]
let ``Configuration should have valid validation settings`` () =
    Assert.True(Validation.MaxSymbolLength > 0)
    Assert.True(Validation.MinSymbolLength >= 0)
    Assert.True(Validation.MaximumCashAmount > 0m)

// ============================================================================
// MESSAGES TESTS
// ============================================================================

[<Fact>]
let ``Messages should contain error messages`` () =
    Assert.NotNull Errors.InvalidCashAmount
    Assert.NotNull Errors.InvalidSymbol
    Assert.NotNull Errors.EmptyIndex

[<Fact>]
let ``Messages should contain success messages`` () =
    Assert.NotNull Success.RebalancingCompleted
    Assert.NotNull Success.PortfolioImported

[<Fact>]
let ``Messages should contain info messages`` () =
    Assert.NotNull Info.LoadingPortfolioData
    Assert.NotNull Info.PerformingRebalancing

// ============================================================================
// TEST OUTPUT ENHANCEMENT
// ============================================================================

// Custom test output with Spectre.Console
let printTestSummary (passed: int) (failed: int) (total: int) =
    let summaryTable = 
        Table()
            .Title("[bold blue]Test Summary[/]")
            .AddColumns([|"Metric"; "Count"|])
            .AddRow("[green]Passed[/]", string passed)
            .AddRow("[red]Failed[/]", string failed)
            .AddRow("[blue]Total[/]", string total)
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
    
    AnsiConsole.Write(summaryTable)

// Test categories for better organization
let testCategories = [
    ("Validation", 15)
    ("Helper Functions", 5)
    ("Extensions", 12)
    ("Rebalancing Calculations", 8)
    ("Configuration", 3)
    ("Messages", 3)
]

let printTestCategories () =
    let categoryTable = 
        Table()
            .Title("[bold green]Test Categories[/]")
            .AddColumns([|"Category"; "Test Count"|])
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
    
    for (category, count) in testCategories do
        categoryTable.AddRow(category, string count) |> ignore
    
    AnsiConsole.Write(categoryTable) 