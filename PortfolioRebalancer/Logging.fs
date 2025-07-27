module Logging

open System
open System.IO

type LogLevel = 
    | Debug
    | Info
    | Warning
    | Error

type Logger() =
    let logToFile level message =
        let timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        let logEntry = sprintf "[%s] [%A] %s" timestamp level message
        let logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portfolio.log")
        
        try
            File.AppendAllText(logFile, logEntry + Environment.NewLine)
        with
        | _ -> () // Silently fail if logging fails
    
    member _.Debug message = logToFile Debug message
    member _.Info message = logToFile Info message
    member _.Warning message = logToFile Warning message
    member _.Error message = logToFile Error message
    
    member this.LogRebalanceStart cash includeCommission =
        this.Info (sprintf "Starting rebalancing with cash: %M, commission: %b" cash includeCommission)
    
    member this.LogRebalanceComplete totalValue totalCommission =
        this.Info (sprintf "Rebalancing completed. Total value: %M, Total commission: %M" totalValue totalCommission)
    
    member this.LogValidationError error =
        this.Warning (sprintf "Validation error: %A" error)
    
    member this.LogMissingSymbols symbols =
        this.Warning (sprintf "Missing price data for symbols: %s" (String.concat ", " symbols))

let logger = Logger() 