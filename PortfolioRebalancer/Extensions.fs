module Extensions

open System
open System.Collections.Generic
open Types

// Utility functions for better code organization
module ListExtensions =
    let tryHead (list: 'T list) =
        if List.isEmpty list then None else Some (List.head list)
    
    let tryLast (list: 'T list) =
        if List.isEmpty list then None else Some (List.last list)
    
    let safeMap f list =
        list |> List.map f
    
    let safeFilter predicate list =
        list |> List.filter predicate

module MapExtensions =
    let tryFindOrDefault (map: Map<'K, 'V>) (key: 'K) (defaultValue: 'V) =
        Map.tryFind key map |> Option.defaultValue defaultValue
    
    let merge (map1: Map<'K, 'V>) (map2: Map<'K, 'V>) =
        Map.fold (fun acc k v -> Map.add k v acc) map1 map2

module DecimalExtensions =
    let toPercentage (value: decimal) =
        value.ToString("P2")
    
    let toCurrency (value: decimal) =
        value.ToString("C2")
    
    let isPositive (value: decimal) =
        value > 0m
    
    let isNegative (value: decimal) =
        value < 0m
    
    let isZero (value: decimal) =
        value = 0m

module IntExtensions =
    let toDecimal (value: int) =
        decimal value
    
    let isPositive (value: int) =
        value > 0
    
    let isNegative (value: int) =
        value < 0
    
    let isZero (value: int) =
        value = 0

// Utility functions
module Seq =
    let tryHead (source: seq<'T>) =
        if Seq.isEmpty source then None else Some (Seq.head source)
    
    let tryLast (source: seq<'T>) =
        if Seq.isEmpty source then None else Some (Seq.last source)
    
    let safeMap f source =
        source |> Seq.map f
    
    let safeFilter predicate source =
        source |> Seq.filter predicate

module Option =
    let defaultValue value opt =
        match opt with
        | Some v -> v
        | None -> value
    
    let defaultWith (f: unit -> 'T) opt =
        match opt with
        | Some v -> v
        | None -> f()
    
    let mapDefault f defaultValue opt =
        match opt with
        | Some v -> f v
        | None -> defaultValue

module Result =
    let mapError f result =
        match result with
        | Ok v -> Ok v
        | Error e -> Error (f e)
    
    let defaultValue value result =
        match result with
        | Ok v -> v
        | Error _ -> value
    
    let defaultWith f result =
        match result with
        | Ok v -> v
        | Error _ -> f()

// String utility functions
module StringExtensions =
    let isNullOrEmpty (str: string) =
        String.IsNullOrEmpty(str)
    
    let isNullOrWhiteSpace (str: string) =
        String.IsNullOrWhiteSpace(str)
    
    let toUpperSafe (str: string) =
        if isNullOrEmpty str then "" else str.ToUpper()
    
    let toLowerSafe (str: string) =
        if isNullOrEmpty str then "" else str.ToLower()
    
    let trimSafe (str: string) =
        if isNullOrEmpty str then "" else str.Trim() 