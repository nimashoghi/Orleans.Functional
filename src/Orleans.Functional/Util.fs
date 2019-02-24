[<AutoOpen>]
module Orleans.Functional.Utils

open System
open System.Collections.Generic
open System.Reflection
open FSharp.Reflection
open Orleans

open Orleans.Functional.Types

[<AutoOpen>]
module Ensure =
    let ensure<'t> predicate =
        if not <| predicate typeof<'t>
        then raise (InvalidTypeException typeof<'t>)
    let ensureUnion<'t> () = ensure<'t> FSharpType.IsUnion
    let ensureRecord<'t> () = ensure<'t> FSharpType.IsRecord

module Dict =
    let empty<'key, 'value when 'key: equality> = Dictionary<'key, 'value> ()
    let add key value (dict: Dictionary<_, _>) =
        dict.[key] <- value
        dict
    let tryFind key (dict: Dictionary<_, _>) =
        match dict.TryGetValue key with
        | true, value -> Some value
        | _ -> None

let (|IntegerGrain|GuidGrain|StringGrain|IntegerCompoundGrain|GuidCompoundGrain|) (grain: #IGrain) =
    match grain :> IGrain with
    | :? IGrainWithIntegerKey as grain -> IntegerGrain grain
    | :? IGrainWithGuidKey as grain -> GuidGrain grain
    | :? IGrainWithStringKey as grain -> StringGrain grain
    | :? IGrainWithIntegerCompoundKey as grain -> IntegerCompoundGrain grain
    | :? IGrainWithGuidCompoundKey as grain -> GuidCompoundGrain grain
    | _ -> failwith "Invalid grain type!"

let isGrainOf<'t> (``type``: Type) = typeof<'t>.IsAssignableFrom ``type``

let (|GrainOf|) ``type`` =
    if isGrainOf<IGrainWithIntegerKey> ``type`` then IntegerGrainType
    elif isGrainOf<IGrainWithIntegerKey> ``type`` then GuidGrainType
    elif isGrainOf<IGrainWithStringKey> ``type`` then StringGrainType
    elif isGrainOf<IGrainWithIntegerCompoundKey> ``type`` then IntegerCompoundGrainType
    elif isGrainOf<IGrainWithGuidCompoundKey> ``type`` then GuidCompoundGrainType
    else failwith "Invalid grain type"

let getGrainHelper<'grain when 'grain :> IGrain> (factory: IGrainFactory) types args =
    factory.GetType()
        .GetMethod(
            "GetGrain",
            BindingFlags.Public ||| BindingFlags.Instance,
            null,
            CallingConventions.Any,
            types,
            null
        ).Invoke(
            box factory,
            args
        )
    :?> 'grain

let getGuidGrain<'grain when 'grain :> IGrain> (factory: IGrainFactory) (id: Guid) (grainClassNamePrefix: string option) =
    getGrainHelper<'grain>
        factory
        [|
            typeof<Guid>
            typeof<string>
        |]
        [|
            box id
            box (Option.toObj <| grainClassNamePrefix)
        |]

let getLongGrain<'grain when 'grain :> IGrain> (factory: IGrainFactory) (id: int64) (grainClassNamePrefix: string option) =
    getGrainHelper<'grain>
        factory
        [|
            typeof<int64>
            typeof<string>
        |]
        [|
            box id
            box (Option.toObj <| grainClassNamePrefix)
        |]

let getStringGrain<'grain when 'grain :> IGrain> (factory: IGrainFactory) (id: string) (grainClassNamePrefix: string option) =
    getGrainHelper<'grain>
        factory
        [|
            typeof<string>
            typeof<string>
        |]
        [|
            box id
            box (Option.toObj <| grainClassNamePrefix)
        |]

let getLongCompoundGrain<'grain when 'grain :> IGrain> (factory: IGrainFactory) ((id, strId): int64 * string) (grainClassNamePrefix: string option) =
    getGrainHelper<'grain>
        factory
        [|
            typeof<int64>
            typeof<string>
            typeof<string>
        |]
        [|
            box id
            box strId
            box (Option.toObj <| grainClassNamePrefix)
        |]

let getGuidCompoundGrain<'grain when 'grain :> IGrain> (factory: IGrainFactory) ((id, strId): Guid * string) (grainClassNamePrefix: string option) =
    getGrainHelper<'grain>
        factory
        [|
            typeof<Guid>
            typeof<string>
            typeof<string>
        |]
        [|
            box id
            box strId
            box (Option.toObj <| grainClassNamePrefix)
        |]

let getGrain<'grain when 'grain :> IGrain> factory id =
    match id, typeof<'grain> with
    | Integer id, GrainOf IntegerGrainType ->
        getLongGrain<'grain> factory id None
    | Guid id, GrainOf GuidGrainType ->
        getGuidGrain<'grain> factory id None
    | String id, GrainOf StringGrainType ->
        getStringGrain<'grain> factory id None
    | IntegerCompound (id, str), GrainOf IntegerCompoundGrainType ->
        getLongCompoundGrain<'grain> factory (id, str) None
    | GuidCompound (id, str), GrainOf GuidCompoundGrainType ->
        getGuidCompoundGrain<'grain> factory (id, str) None
    | _ -> failwith "invalid grain type"
