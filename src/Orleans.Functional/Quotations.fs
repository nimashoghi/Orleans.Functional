[<AutoOpen>]
module Orleans.Functional.Quotations

open System.Reflection

open FSharp.Quotations
open FSharp.Quotations.Patterns
open FSharp.Reflection
open FSharp.Utils

let inline private checkDefaultCtor<'state when 'state : not struct and 'state : (new: unit -> 'state)> () = ()

/// **Description**
///     Matches quotations of kind `<@ instance @>.`
let (|Instance|_|) (expr: Expr<'state>) =
    match expr with
    | PropertyGet (None, prop, []) when prop.PropertyType = typeof<'state> -> Some ()
    | ValueWithName (_, ``type``, _) when ``type`` = typeof<'state> -> Some ()
    | _ -> None

let isCopyExpr (expr: Expr) (expectedProp: PropertyInfo) =
    match expr with
    | PropertyGet (Some _, prop, []) when prop = expectedProp -> true
    | _ -> false

/// **Description**
///     Matches quotations of kind `<@ {instance with Property = value} @>`
let (|RecordSetFields|_|) (expr: Expr<'state>) =
    match expr with
    | NewRecord (``type``, args) when ``type`` = typeof<'state> ->
        let fields =
            FSharpType.GetRecordFields typeof<'state>
            |> Array.toList
        (args, fields)
        ||> List.zip
        |> List.filter (fun (expr, prop) -> not (isCopyExpr expr prop))
        |> List.map snd
        |> Some
    | _ -> None

// TODO: Add precompute

/// **Description**
///     Handles the update from a reducer method in a journaled grain.
let handleUpdate (expr: Expr<'state>) (newState: 'state) (state: 'state) =
    checkDefaultCtor<'state> ()

    match expr with
    // if we get something like <@ state @>
    | Instance -> ()
    | RecordSetFields props ->
        for prop in props do
            prop.SetValue (state, FSharpValue.GetRecordField (newState, prop))
    | _ -> ()
