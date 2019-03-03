[<AutoOpen>]
module Orleans.Functional.Quotations

open FSharp.Quotations
open FSharp.Quotations.Patterns
open FSharp.Quotations.Evaluator
open FSharp.Utils
open FSharp.Utils.Quotations


let inline private checkDefaultCtor<'state when 'state : not struct and 'state : (new: unit -> 'state)> () = ()

/// **Description**
///     Matches quotations of kind `<@ instance @>.`
let (|Instance|_|) (expr: Expr<'state>) =
    checkDefaultCtor<'state> ()

    match expr with
    | PropertyGet (None, prop, []) when prop.PropertyType = typeof<'state> -> Some ()
    | ValueWithName (_, ``type``, _) when ``type`` = typeof<'state> -> Some ()
    | _ -> None

/// **Description**
///     Matches quotations of kind `<@ {instance with Property = value} @>`
let (|RecordSetFields|_|) (expr: Expr<'state>) =
    checkDefaultCtor<'state> ()

    let rec run (expr: Expr) map =
        match expr with
        | Let (variable, value, inExpression) ->
            Dictionary.add variable value map
            |> run inExpression
        | NewRecord (``type``, args) when ``type`` = typeof<'state> -> Some (args, map)
        | _ -> None
    run expr Dictionary.empty

/// **Description**
///     Handles the update from a reducer method in a journaled grain.
let handleUpdate (expr: Expr<'state>) (state: 'state) =
    checkDefaultCtor<'state> ()

    match expr with
    // if we get something like <@ state @>
    | Instance -> ()
    | RecordSetFields (args, map) ->
        let properties = typeof<'state>.GetProperties ()
        args
        |> List.mapi (fun i arg ->
            match arg with
            | PropertyGet (Some (PropertyGet (None, prop, _)), _, _) when prop.PropertyType = typeof<'state> -> None
            | _ -> Some (properties.[i], arg.Substitute (fun var' -> Dictionary.tryFind var' map))
        )
        |> List.choose id
        |> List.iter (fun (prop, expr) -> prop.SetValue (state, QuotationEvaluator.EvaluateUntyped expr))
    | _ -> ()
