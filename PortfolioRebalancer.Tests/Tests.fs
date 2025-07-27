module Tests

open Xunit
open PortfolioRebalancer.Types
open PortfolioRebalancer.Helpers
open PortfolioRebalancer.Validation
open PortfolioRebalancer.Configuration

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
    let index = [{ Symbol = "AAPL"; Weight = 0.5m }]
    let result = validateIndexNotEmpty index
    match result with
    | Ok validatedIndex -> Assert.Equal(index, validatedIndex)
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