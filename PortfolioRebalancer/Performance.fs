module Performance

open System
open System.Collections.Concurrent
open System.Diagnostics
open Types
open Logging

// Simple in-memory cache
type Cache<'T> = {
    Data: 'T option
    Timestamp: DateTime
    ExpiryMinutes: int
}

type CacheManager() =
    let cache = ConcurrentDictionary<string, obj>()
    
    member _.Get<'T>(key: string) : 'T option =
        match cache.TryGetValue(key) with
        | true, value ->
            let cached = value :?> Cache<'T>
            if DateTime.Now.Subtract(cached.Timestamp).TotalMinutes < float cached.ExpiryMinutes then
                Some cached.Data.Value
            else
                cache.TryRemove(key) |> ignore
                None
        | false, _ -> None
    
    member _.Set<'T>(key: string, data: 'T, expiryMinutes: int) =
        let cacheEntry = {
            Data = Some data
            Timestamp = DateTime.Now
            ExpiryMinutes = expiryMinutes
        }
        cache.AddOrUpdate(key, cacheEntry :> obj, fun _ _ -> cacheEntry :> obj) |> ignore
    
    member _.Clear() = cache.Clear()
    
    member _.Remove(key: string) = cache.TryRemove(key) |> ignore

let cacheManager = CacheManager()

// Performance monitoring
type PerformanceMetrics = {
    OperationName: string
    Duration: TimeSpan
    Timestamp: DateTime
    Success: bool
}

let private performanceMetrics = ConcurrentQueue<PerformanceMetrics>()

let measureOperation<'T> (operationName: string) (operation: unit -> 'T) : 'T =
    let stopwatch = Stopwatch.StartNew()
    let timestamp = DateTime.Now
    
    try
        let result = operation()
        stopwatch.Stop()
        
        let metrics = {
            OperationName = operationName
            Duration = stopwatch.Elapsed
            Timestamp = timestamp
            Success = true
        }
        
        performanceMetrics.Enqueue(metrics)
        logger.Debug (sprintf "Operation '%s' completed in %O" operationName stopwatch.Elapsed)
        result
    with
    | ex ->
        stopwatch.Stop()
        let metrics = {
            OperationName = operationName
            Duration = stopwatch.Elapsed
            Timestamp = timestamp
            Success = false
        }
        
        performanceMetrics.Enqueue(metrics)
        logger.Error (sprintf "Operation '%s' failed after %O: %s" operationName stopwatch.Elapsed ex.Message)
        reraise()

// Note: Async operation measurement removed due to F# version compatibility

// Get performance statistics
let getPerformanceStats() : PerformanceMetrics list =
    performanceMetrics.ToArray() |> Array.toList

// Clear old metrics (older than specified hours)
let clearOldMetrics (hoursOld: int) =
    let cutoffTime = DateTime.Now.AddHours(-float hoursOld)
    let mutable removed = 0
    
    while not performanceMetrics.IsEmpty do
        match performanceMetrics.TryDequeue() with
        | true, metric when metric.Timestamp < cutoffTime ->
            removed <- removed + 1
        | true, metric ->
            performanceMetrics.Enqueue(metric) // Put it back
            ()
        | false, _ -> ()
    
    logger.Info (sprintf "Cleared %d old performance metrics" removed) 