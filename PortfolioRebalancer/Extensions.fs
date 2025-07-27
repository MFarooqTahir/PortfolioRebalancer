module Extensions

open System
open System.Collections.Generic
open Types

// Only keep the extensions that are actually used
module MapExtensions =
    let tryFindOrDefault (map: Map<'K, 'V>) (key: 'K) (defaultValue: 'V) =
        Map.tryFind key map |> Option.defaultValue defaultValue 