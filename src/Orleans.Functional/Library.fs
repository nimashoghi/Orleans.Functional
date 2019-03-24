#nowarn "44"

namespace Orleans.Functional

open System
open System.Threading.Tasks
open FSharp.Quotations
open FSharp.Utils.Quotations
open FSharp.Utils.Tasks
open Orleans
open Orleans.EventSourcing

open Orleans.Functional.Quotations
open Orleans.Functional.Types

open FSharp.Utils.Tasks.TplPrimitives

type ContinuationTaskBuilder (cont: unit -> Task) =
    inherit AwaitableBuilder ()

    member __.Run (f : unit -> Ply<'u>) =
        unitTask {
            let! _ = f ()
            do! cont ()
        }

type Reducer private () =
    /// **Description**
    ///   * Updates the current `EventSourcedGrain`'s internal state using the `update` parameter.
    ///
    /// **Parameters**
    ///   * `update` - Record update expression for the internal state of our `EventSourcedGrain`.
    ///
    /// **Output Type**
    ///   * A function of type `'state -> unit` that represents the update operation returned.
    static member Update ([<ReflectedDefinition true>] update: Expr<'state>) =
        match update with
        | WithValueTyped (newState, expr) -> handleUpdate expr newState
        | _ -> failwith "Update method needs to be marked as a ReflectedDefinition."

[<AbstractClass>]
type WorkerGrain () =
    inherit Grain ()

    abstract member OnActivate: unit -> Task
    abstract member OnDeactivate: unit -> Task

    default __.OnActivate () = Task.CompletedTask
    default __.OnDeactivate () = Task.CompletedTask

    override this.OnActivateAsync () =
        let baseMethodResult = base.OnActivateAsync ()
        unitTask {
            do! baseMethodResult
            do! this.OnActivate ()
        }

    override this.OnDeactivateAsync () =
        let baseMethodResult = base.OnDeactivateAsync ()
        unitTask {
            do! baseMethodResult
            do! this.OnDeactivate ()
        }

[<AutoOpen>]
module EventSourcedGrainHeleprs =
    open System.Reflection

    let tryGetInitializer<'state> () =
        typeof<'state>.GetProperty (
            "Initial",
            BindingFlags.NonPublic
            ||| BindingFlags.Public
            ||| BindingFlags.Static
            ||| BindingFlags.FlattenHierarchy
        )

exception NoInitializerException

[<Serializable>]
type EventSourcedGrainState<'state> () =
    let initializer = tryGetInitializer<'state> ()
    do
        if isNull initializer
        then raise NoInitializerException

    member val Value = unbox<'state> (initializer.GetValue null) with get, set

[<AbstractClass>]
type EventSourcedGrain<'state, 'event when 'event: not struct> () =
    inherit JournaledGrain<EventSourcedGrainState<'state>, 'event> ()

    do ensureRecord<'state> ()
    do ensureUnion<'event> ()

    let erasedPrimaryKey grain =
        match grain with
        | GuidGrain grain -> Guid <| grain.GetPrimaryKey ()
        | IntegerGrain grain -> Integer <| grain.GetPrimaryKeyLong ()
        | StringGrain grain -> String <| grain.GetPrimaryKeyString ()
        | IntegerCompoundGrain grain -> IntegerCompound <| grain.GetPrimaryKeyLong ()
        | GuidCompoundGrain grain -> GuidCompound <| grain.GetPrimaryKey ()

    abstract member Reduce: 'state -> ('event -> 'state -> unit)

    abstract member OnActivate: unit -> Task
    abstract member OnDeactivate: unit -> Task

    default __.OnActivate () = Task.CompletedTask
    default __.OnDeactivate () = Task.CompletedTask

    override this.OnActivateAsync () =
        let baseMethodResult = base.OnActivateAsync ()
        unitTask {
            do! baseMethodResult
            do! this.OnActivate ()
        }

    override this.OnDeactivateAsync () =
        let baseMethodResult = base.OnDeactivateAsync ()
        unitTask {
            do! baseMethodResult
            do! this.OnDeactivate ()
        }

    override this.TransitionState (state, event) = this.Reduce state.Value event state.Value

    member this.Dispatch (event: 'event) = this.RaiseEvent event
    member this.Dispatch (events: 'event list) = this.RaiseEvents events

    member __.ConfirmEvents () = base.ConfirmEvents ()
    member this.confirm = ContinuationTaskBuilder this.ConfirmEvents

    member __.State = base.State.Value

    interface IGrainBase
