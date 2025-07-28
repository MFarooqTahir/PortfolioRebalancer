module Logging

open System
open System.IO
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

// Simple file logger implementation
type FileLogger(path: string) =
    let lockObj = obj()
    
    member _.WriteLog(level: string, message: string) =
        let timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        let logEntry = sprintf "[%s] [%s] %s" timestamp level message
        
        lock lockObj (fun () ->
            try
                // Ensure directory exists
                let dir = Path.GetDirectoryName(path)
                if not (String.IsNullOrEmpty(dir)) && not (Directory.Exists(dir)) then
                    Directory.CreateDirectory(dir) |> ignore
                
                File.AppendAllText(path, logEntry + Environment.NewLine)
            with
            | _ -> () // Silently fail if logging fails
        )

// Global logger factory
let private loggerFactory : ILoggerFactory =
    let services = ServiceCollection()
    
    // Add console logging with simple format
    services.AddLogging(fun builder ->
        builder.AddConsole(fun options ->
            options.FormatterName <- "Simple"
        ) |> ignore
        
        // Configure simple formatter
        builder.AddSimpleConsole(fun options ->
            options.SingleLine <- true
            options.TimestampFormat <- "HH:mm:ss "
            options.UseUtcTimestamp <- false
        ) |> ignore
    ) |> ignore
    
    let serviceProvider = services.BuildServiceProvider()
    serviceProvider.GetRequiredService<ILoggerFactory>()

// Create logger for a specific category
let createLogger (categoryName: string) = loggerFactory.CreateLogger(categoryName)

// Default logger for the application
let private msLogger = createLogger "PortfolioRebalancer"

// File logger for production
let private fileLogger = 
    let environment = 
        match Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") with
        | null -> 
            match Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") with
            | null -> "Development"
            | env -> env
        | env -> env
    
    if environment = "Production" then
        Some (FileLogger("logs/portfolio.log"))
    else
        None

// Logger interface that matches the old API
type Logger() =
    member _.Debug(message: string) = 
        // Only log to file in production, not console
        fileLogger |> Option.iter (fun fl -> fl.WriteLog("DEBUG", message))
    
    member _.Info(message: string) = 
        // Only log to file in production, not console
        fileLogger |> Option.iter (fun fl -> fl.WriteLog("INFO", message))
    
    member _.Warning(message: string) = 
        // Only log to file in production, not console
        fileLogger |> Option.iter (fun fl -> fl.WriteLog("WARN", message))
    
    member _.Error(message: string) = 
        // Only log to file in production, not console
        fileLogger |> Option.iter (fun fl -> fl.WriteLog("ERROR", message))

// Global logger instance
let logger = Logger()

// Convenience functions for backward compatibility
let debug (message: string) = logger.Debug(message)
let info (message: string) = logger.Info(message)
let warning (message: string) = logger.Warning(message)
let error (message: string) = logger.Error(message)

// Formatted logging functions
let debugf format = Printf.kprintf debug format
let infof format = Printf.kprintf info format
let warningf format = Printf.kprintf warning format
let errorf format = Printf.kprintf error format 