module PortfolioRebalancer.Core.Logger

open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.ServiceContainer

// Best Practice: Hybrid approach combining multiple patterns

// 1. Module-level logger (for simple cases)
let mutable private globalContainer: ServiceContainer option = None

let initializeLogger container =
    globalContainer <- Some container

let getModuleLogger<'T>() =
    match globalContainer with
    | Some container -> getLogger<'T> container
    | None -> failwith "Logger not initialized. Call initializeLogger first."

let getModuleLoggerByName name =
    match globalContainer with
    | Some container -> getLoggerByName name container
    | None -> failwith "Logger not initialized. Call initializeLogger first."



 