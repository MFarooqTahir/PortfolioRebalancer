module Tests

open Xunit
open Types
open Helpers
open Validation
open Configuration
open Extensions
open Messages

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
let ``formatPKR should format currency correctly`` () =
    let formatted = formatPKR 1234.56m
    Assert.Contains("Rs.", formatted)
    Assert.Contains("1,234.56", formatted)

[<Theory>]
[<InlineData("100", 100m)>]
[<InlineData("1,000", 1000m)>]
[<InlineData("0", 0m)>]
let ``ValidDecimal pattern should parse valid decimals`` (input: string, expected: decimal) =
    match input with
    | ValidDecimal value -> Assert.Equal(expected, value)
    | _ -> Assert.True(false, $"Should parse '{input}' as {expected}")

[<Theory>]
[<InlineData("invalid")>]
[<InlineData("")>]
[<InlineData("abc")>]
let ``ValidDecimal pattern should reject invalid decimals`` (input: string) =
    match input with
    | ValidDecimal _ -> Assert.True(false, $"Should not parse '{input}' as decimal")
    | _ -> Assert.True(true)

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
let ``toPercentage should format correctly`` () =
    let formatted = toPercentage 0.1234m
    Assert.Equal("12.34%", formatted)

[<Fact>]
let ``formatPKRShort should format without decimals`` () =
    let formatted = formatPKRShort 1234.56m
    Assert.Contains("Rs.", formatted)
    Assert.Contains("1,235", formatted) // Should round up 