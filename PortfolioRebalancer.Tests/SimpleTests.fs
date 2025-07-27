module SimpleTests

open Xunit
open System

// Test utilities for detailed logging
let logTestStart testName =
    printfn "🧪 Starting test: %s" testName
    printfn "   └─ Timestamp: %s" (DateTime.Now.ToString("HH:mm:ss.fff"))

let logTestEnd testName duration =
    printfn "✅ Completed test: %s" testName
    printfn "   └─ Duration: %O" duration

let logAssertion message =
    printfn "   ├─ Assertion: %s" message

let logError message =
    printfn "   ├─ ❌ Error: %s" message

[<Fact>]
let ``Simple arithmetic test with detailed logging`` () =
    let testName = "Simple arithmetic test"
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    logTestStart testName
    
    try
        let a = 5
        let b = 3
        logAssertion (sprintf "Testing addition: %d + %d" a b)
        
        let result = a + b
        logAssertion (sprintf "Result: %d" result)
        
        Assert.Equal(8, result)
        logAssertion "Addition test passed"
        
        let multiplication = a * b
        logAssertion (sprintf "Testing multiplication: %d * %d = %d" a b multiplication)
        Assert.Equal(15, multiplication)
        logAssertion "Multiplication test passed"
        
        stopwatch.Stop()
        logTestEnd testName stopwatch.Elapsed
        Assert.True(true)
    with
    | ex ->
        stopwatch.Stop()
        logError ex.Message
        logTestEnd testName stopwatch.Elapsed
        reraise()

[<Fact>]
let ``String operations test with detailed logging`` () =
    let testName = "String operations test"
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    logTestStart testName
    
    try
        let str1 = "Hello"
        let str2 = "World"
        logAssertion (sprintf "Testing string concatenation: '%s' + ' %s'" str1 str2)
        
        let result = str1 + " " + str2
        logAssertion (sprintf "Result: '%s'" result)
        
        Assert.Equal("Hello World", result)
        logAssertion "String concatenation test passed"
        
        let length = result.Length
        logAssertion (sprintf "String length: %d" length)
        Assert.Equal(11, length)
        logAssertion "String length test passed"
        
        let containsHello = result.Contains("Hello")
        logAssertion (sprintf "Contains 'Hello': %b" containsHello)
        Assert.True(containsHello)
        logAssertion "String contains test passed"
        
        stopwatch.Stop()
        logTestEnd testName stopwatch.Elapsed
        Assert.True(true)
    with
    | ex ->
        stopwatch.Stop()
        logError ex.Message
        logTestEnd testName stopwatch.Elapsed
        reraise()

[<Fact>]
let ``List operations test with detailed logging`` () =
    let testName = "List operations test"
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    logTestStart testName
    
    try
        let numbers = [1; 2; 3; 4; 5]
        logAssertion (sprintf "Testing list operations with: %A" numbers)
        
        let sum = List.sum numbers
        logAssertion (sprintf "Sum: %d" sum)
        Assert.Equal(15, sum)
        logAssertion "List sum test passed"
        
        let doubled = List.map (fun x -> x * 2) numbers
        logAssertion (sprintf "Doubled list: %A" doubled)
        Assert.Equal<int list>([2; 4; 6; 8; 10], doubled)
        logAssertion "List map test passed"
        
        let filtered = List.filter (fun x -> x % 2 = 0) numbers
        logAssertion (sprintf "Even numbers: %A" filtered)
        Assert.Equal<int list>([2; 4], filtered)
        logAssertion "List filter test passed"
        
        let count = List.length numbers
        logAssertion (sprintf "List length: %d" count)
        Assert.Equal(5, count)
        logAssertion "List length test passed"
        
        stopwatch.Stop()
        logTestEnd testName stopwatch.Elapsed
        Assert.True(true)
    with
    | ex ->
        stopwatch.Stop()
        logError ex.Message
        logTestEnd testName stopwatch.Elapsed
        reraise()

[<Theory>]
[<InlineData(2, 3, 5)>]
[<InlineData(0, 5, 5)>]
[<InlineData(-1, 1, 0)>]
[<InlineData(10, 20, 30)>]
let ``Addition theory test with detailed logging`` (a: int, b: int, expected: int) =
    let testName = sprintf "Addition theory test: %d + %d = %d" a b expected
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    logTestStart testName
    
    try
        logAssertion (sprintf "Testing addition: %d + %d" a b)
        
        let result = a + b
        logAssertion (sprintf "Result: %d, Expected: %d" result expected)
        
        Assert.Equal(expected, result)
        logAssertion "Addition theory test passed"
        
        // Additional checks
        let isPositive = result > 0
        let isZero = result = 0
        let isNegative = result < 0
        
        logAssertion (sprintf "Result characteristics: Positive=%b, Zero=%b, Negative=%b" isPositive isZero isNegative)
        
        stopwatch.Stop()
        logTestEnd testName stopwatch.Elapsed
        Assert.True(true)
    with
    | ex ->
        stopwatch.Stop()
        logError ex.Message
        logTestEnd testName stopwatch.Elapsed
        reraise()

[<Fact>]
let ``Performance simulation test with detailed logging`` () =
    let testName = "Performance simulation test"
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    logTestStart testName
    
    try
        // Simulate different operations with varying performance
        let operations = [
            ("Quick calculation", 5)
            ("Database query", 150)
            ("File I/O", 25)
            ("Network call", 300)
            ("Complex algorithm", 75)
        ]
        
        logAssertion "Simulating performance metrics for various operations"
        
        for (operation, duration) in operations do
            logAssertion (sprintf "Operation: %s, Duration: %dms" operation duration)
            
            // Performance assertions
            let isFast = duration < 50
            let isMedium = duration >= 50 && duration < 200
            let isSlow = duration >= 200
            
            logAssertion (sprintf "Performance category: Fast=%b, Medium=%b, Slow=%b" isFast isMedium isSlow)
            
            Assert.True(duration >= 0, "Duration should be non-negative")
            
            if isSlow then
                logAssertion (sprintf "⚠️  Slow operation detected: %s (%dms)" operation duration)
        
        let totalDuration = operations |> List.sumBy snd
        logAssertion (sprintf "Total simulated duration: %dms" totalDuration)
        
        let averageDuration = totalDuration / operations.Length
        logAssertion (sprintf "Average duration: %dms" averageDuration)
        
        stopwatch.Stop()
        logTestEnd testName stopwatch.Elapsed
        Assert.True(true)
    with
    | ex ->
        stopwatch.Stop()
        logError ex.Message
        logTestEnd testName stopwatch.Elapsed
        reraise() 