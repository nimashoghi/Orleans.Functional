namespace Orleans.Functional

open System
open System.Threading.Tasks
open FSharp.Quotations
open FSharp.Quotations.Patterns
open FSharp.Quotations.Evaluator
open FSharp.Reflection
open Orleans
open Orleans.EventSourcing

type IMessageGrain<'message when 'message : not struct> =
    abstract member Receive: 'message -> unit Task

type IMessageGrainWithIntegerKey<'message when 'message : not struct> =
    inherit IGrainWithIntegerKey
    inherit IMessageGrain<'message>

type IMessageGrainWithGuidKey<'message when 'message : not struct> =
    inherit IGrainWithGuidKey
    inherit IMessageGrain<'message>

type IMessageGrainWithStringKey<'message when 'message : not struct> =
    inherit IGrainWithStringKey
    inherit IMessageGrain<'message>

type IMessageGrainWithIntegerCompoundKey<'message when 'message : not struct> =
    inherit IGrainWithIntegerCompoundKey
    inherit IMessageGrain<'message>

type IMessageGrainWithGuidCompoundKey<'message when 'message : not struct> =
    inherit IGrainWithGuidCompoundKey
    inherit IMessageGrain<'message>

exception InvalidTypeException of Type

[<AutoOpen>]
module private Utils =
    let ensure<'t> predicate =
        if not <| predicate typeof<'t>
        then raise (InvalidTypeException typeof<'t>)

    let ensureUnion<'t>() = ensure<'t> FSharpType.IsUnion
    let ensureRecord<'t>() = ensure<'t> FSharpType.IsRecord

[<AbstractClass>]
type EventSourcedGrain<'state, 'event
                        when 'state : not struct
                        and 'state : (new: unit -> 'state)
                        and 'event : not struct>(identity, runtime) =
    inherit JournaledGrain<'state, 'event>(identity, runtime)

    do ensureRecord<'state>()
    do ensureUnion<'event>()

    abstract member Activate: unit -> unit Task
    abstract member Deactivate: unit -> unit Task
    abstract member Reduce: 'state -> 'event -> ('state -> 'unit)

    default __.Activate() = Task.FromResult()
    default __.Deactivate() = Task.FromResult()

    override this.OnActivateAsync() = this.Activate() :> Task
    override this.OnDeactivateAsync() = this.Deactivate() :> Task
    override this.TransitionState (state, event) = this.Reduce state event state

module private List =
    let some lst =
        lst
        |> List.filter Option.isSome
        |> List.map Option.get

[<AutoOpen>]
module internal EventSourcing =
    let (|Instance|_|) (expr: Expr<'state>) =
        match expr with
        | PropertyGet (None, prop, []) when prop.PropertyType = typeof<'state> -> Some prop
        | _ -> None

    let (|RecordSetFields|_|) (expr: Expr<'state>) =
        let rec run (expr: Expr) map =
            match expr with
            | Let (variable, value, inExpression) ->
                Map.add variable value map
                |> run inExpression
            | NewRecord (``type``, args) when ``type`` = typeof<'state> -> Some (args, map)
            | _ -> None
        run expr Map.empty

    let handleUpdate<'state when 'state : not struct and 'state : (new: unit -> 'state)> (expr: Expr<'state>) (state: 'state) =
        match expr with
        // if we get something like <@ state @>
        | Instance _ ->()
        | RecordSetFields (args, map) ->
            let properties = typeof<'state>.GetProperties()
            args
            |> List.mapi (fun i arg ->
                match arg with
                | PropertyGet (Some (PropertyGet (None, prop, _)), _, _) when prop.PropertyType = typeof<'state> -> None
                | _ -> Some (properties.[i], arg.Substitute (fun var' -> Map.tryFind var' map)))
            |> List.some
            |> List.iter (fun (prop, expr) ->
                prop.SetValue (state, QuotationEvaluator.EvaluateUntyped expr))
        | _ ->()

type Reducer =
    /// **Description**
    ///   * Updates the current `EventSourcedGrain`'s internal state using the `update` parameter.
    ///
    /// **Parameters**
    ///   * `update` - Record update expression for the internal state of our `EventSourcedGrain`.
    ///
    /// **Output Type**
    ///   * A function of type `'state -> unit` that represents the update operation returned.
    static member Update ([<ReflectedDefinition>] update: Expr<'state>) = handleUpdate<'state> update
