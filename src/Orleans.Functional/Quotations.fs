[<AutoOpen>]
module Orleans.Functional.Quotations

open FSharp.Quotations
open FSharp.Quotations.Patterns
open FSharp.Quotations.Evaluator

let (|Instance|_|) (expr: Expr<'state>) =
    match expr with
    | PropertyGet (None, prop, []) when prop.PropertyType = typeof<'state> -> Some prop
    | _ -> None

let (|RecordSetFields|_|) (expr: Expr<'state>) =
    let rec run (expr: Expr) map =
        match expr with
        | Let (variable, value, inExpression) ->
            Dict.add variable value map
            |> run inExpression
        | NewRecord (``type``, args) when ``type`` = typeof<'state> -> Some (args, map)
        | _ -> None
    run expr Dict.empty

let handleUpdate<'state when 'state : not struct and 'state : (new: unit -> 'state)> (expr: Expr<'state>) (state: 'state) =
    match expr with
    // if we get something like <@ state @>
    | Instance _ -> ()
    | RecordSetFields (args, map) ->
        let properties = typeof<'state>.GetProperties ()
        args
        |> List.mapi (fun i arg ->
            match arg with
            | PropertyGet (Some (PropertyGet (None, prop, _)), _, _) when prop.PropertyType = typeof<'state> -> None
            | _ -> Some (properties.[i], arg.Substitute (fun var' -> Dict.tryFind var' map))
        )
        |> List.choose id
        |> List.iter (fun (prop, expr) ->
            prop.SetValue (state, QuotationEvaluator.EvaluateUntyped expr)
        )
    | _ -> ()
