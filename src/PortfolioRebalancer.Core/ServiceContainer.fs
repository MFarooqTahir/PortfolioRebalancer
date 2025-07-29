module PortfolioRebalancer.Core.ServiceContainer

open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration
open Serilog
open Serilog.Events
open PortfolioRebalancer.Core.Configuration

// Functional service container - immutable and pure
type ServiceContainer = {
    ServiceProvider: IServiceProvider
}

// Pure function to create service container
let createServiceContainer (configBuilder: IConfigurationBuilder option) : ServiceContainer =
    let builder = Host.CreateDefaultBuilder()
    
    let host = 
        builder
            .ConfigureServices(fun context services ->
                // Add configuration first if provided
                match configBuilder with
                | Some config -> 
                    let configuration = config.Build()
                    services.AddSingleton<IConfiguration>(configuration) |> ignore
                    
                    // Configure Serilog with file logging
                    let logger = LoggerConfiguration()
                        .ReadFrom.Configuration(configuration)
                        .CreateLogger()
                    
                    // Add logging with Serilog
                    services.AddLogging(fun logging ->
                        logging
                            .ClearProviders()
                            .AddSerilog(logger, dispose = true)
                        |> ignore)
                | None -> 
                    // Add logging with default Serilog configuration
                    let logger = LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console()
                        .WriteTo.Debug()
                        .CreateLogger()
                    
                    services.AddLogging(fun logging ->
                        logging
                            .ClearProviders()
                            .AddSerilog(logger, dispose = true)
                        |> ignore)
            )
            .Build()
    
    { ServiceProvider = host.Services }

// Pure function to create default service container
let createDefaultServiceContainer() : ServiceContainer =
    createServiceContainer (Some (createConfigurationBuilder()))

// Functional logger factory
let createLogger<'T> (container: ServiceContainer) : Microsoft.Extensions.Logging.ILogger<'T> =
    container.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<'T>>()

let createLoggerByName (categoryName: string) (container: ServiceContainer) : Microsoft.Extensions.Logging.ILogger =
    container.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger(categoryName)

// Functional service resolution
let getService<'T when 'T: not struct> (container: ServiceContainer) : 'T option =
    match container.ServiceProvider.GetService<'T>() with
    | null -> None
    | service -> Some service

let getRequiredService<'T> (container: ServiceContainer) : 'T =
    container.ServiceProvider.GetRequiredService<'T>()

// Functional logger creation helpers
let getLogger<'T> (container: ServiceContainer) : Microsoft.Extensions.Logging.ILogger<'T> =
    createLogger<'T> container

let getLoggerByName (categoryName: string) (container: ServiceContainer) : Microsoft.Extensions.Logging.ILogger =
    createLoggerByName categoryName container 