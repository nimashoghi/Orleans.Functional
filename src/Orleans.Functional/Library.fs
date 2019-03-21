namespace Orleans.Functional

open System.Threading.Tasks
open FSharp.Quotations
open FSharp.Utils.Tasks
open Orleans
open Orleans.EventSourcing

open Orleans.Functional.Quotations
open Orleans.Functional.Types

type Reducer private () =
    /// **Description**
    ///   * Updates the current `EventSourcedGrain`'s internal state using the `update` parameter.
    ///
    /// **Parameters**
    ///   * `update` - Record update expression for the internal state of our `EventSourcedGrain`.
    ///
    /// **Output Type**
    ///   * A function of type `'state -> unit` that represents the update operation returned.
    static member Update ([<ReflectedDefinition>] update: Expr<'state>) = handleUpdate update

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
            BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.FlattenHierarchy
        )
        |> Option.ofObj
        |> Option.bind (fun property -> tryUnbox<'state> (property.GetValue null))
        |> Option.map (fun f -> fun () -> f)

[<AbstractClass>]
type EventSourcedGrain<'state, 'event
                        when 'state: not struct
                        and 'state: (new: unit -> 'state)
                        and 'event: not struct> (factory: IGrainFactory, ?constructor: unit -> 'state) =
    inherit JournaledGrain<'state, 'event> ()

    let constructor =
        constructor
        |> Option.orElse (tryGetInitializer<'state> ())

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

    override __.InstallAdaptor (factory, initialState, grainTypeName, grainStorage, services) =
        let initialState =
            match constructor with
            | Some constructor -> constructor () |> box
            | None -> initialState
        base.InstallAdaptor (factory, initialState, grainTypeName, grainStorage, services)

    override this.TransitionState (state, event) = this.Reduce state event state

    member this.Dispatch (event: 'event) = this.RaiseEvent event
    member this.Dispatch (events: 'event list) = this.RaiseEvents events

    member __.State = base.State

    interface IGrainBase with
        member this.As<'grain when 'grain :> IGrain> () =
            erasedPrimaryKey this
            |> getGrain<'grain> factory
            |> ValueTask<_>
