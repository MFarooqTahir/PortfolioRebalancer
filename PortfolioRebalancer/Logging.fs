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
    


let logger = Logger() 