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

    abstract member OnActivate: unit -> unit Task
    abstract member OnDeactivate: unit -> unit Task

    default __.OnActivate () = Task.FromResult ()
    default __.OnDeactivate () = Task.FromResult ()

    override this.OnActivateAsync () =
        let baseMethodResult = base.OnActivateAsync ()
        upcast task {
            do! baseMethodResult
            do! this.OnActivate ()
        }

    override this.OnDeactivateAsync () =
        let baseMethodResult = base.OnDeactivateAsync ()
        upcast task {
            do! baseMethodResult
            do! this.OnDeactivate ()
        }

[<AbstractClass>]
type EventSourcedGrain<'state, 'event
                        when 'state : not struct
                        and 'state : (new: unit -> 'state)
                        and 'event : not struct> (identity, runtime, factory: IGrainFactory) =
    inherit JournaledGrain<'state, 'event> (identity, runtime)

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

    abstract member OnActivate: unit -> unit Task
    abstract member OnDeactivate: unit -> unit Task

    default __.OnActivate () = Task.FromResult ()
    default __.OnDeactivate () = Task.FromResult ()

    override this.OnActivateAsync () =
        let baseMethodResult = base.OnActivateAsync ()
        upcast task {
            do! baseMethodResult
            do! this.OnActivate ()
        }
    override this.OnDeactivateAsync () =
        let baseMethodResult = base.OnDeactivateAsync ()
        upcast task {
            do! baseMethodResult
            do! this.OnDeactivate ()
        }

    override this.TransitionState (state, event) = this.Reduce state event state

    member this.Dispatch (event: 'event) = Task.FromResult <| this.RaiseEvent event
    member this.Dispatch (events: 'event list) = Task.FromResult <| this.RaiseEvents events

    interface IGrainBase with
        member this.As<'grain when 'grain :> IGrain> () =
            erasedPrimaryKey this
            |> getGrain<'grain> factory

[<AbstractClass>]
type EventSourcedGrain<'state, 'event, 'parent
                        when 'state : not struct
                        and 'state : (new: unit -> 'state)
                        and 'event : not struct
                        and 'parent :> IGrain> (identity, runtime, factory) =
    inherit EventSourcedGrain<'state, 'event> (identity, runtime, factory)

    interface IGrainBase<'parent> with
        member this.Resolve f = f <| (this :> IGrainBase).As<'parent> ()

type IGrainActivityGrain =
    inherit IGrainWithStringKey

    abstract member IsActive: bool Task
    abstract member SetActive: bool -> unit Task

[<CLIMutable>]
type GrainActivityState = {
    Active: bool
}

type GrainActivityEvent =
| SetActive of Active: bool

type GrainActivityGrain (identity, runtime, factory) =
    inherit EventSourcedGrain<GrainActivityState, GrainActivityEvent> (identity, runtime, factory)

    override __.Reduce state = function
        | SetActive value -> Reducer.Update {state with Active = value}

    interface IGrainActivityGrain with
        member this.IsActive = Task.FromResult this.State.Active
        member this.SetActive value = this.Dispatch (SetActive value)

[<AbstractClass>]
type ActivatableGrain<'state, 'event
                        when 'state : not struct
                        and 'state : (new: unit -> 'state)
                        and 'event : not struct> (identity, runtime, factory) =
    inherit EventSourcedGrain<'state, 'event> (identity, runtime, factory)

    interface IActivatableGrain with
        member this.IsActive =
            factory
                .GetGrain<IGrainActivityGrain>(this.IdentityString)
                .IsActive

        member this.SetActive value =
            factory
                .GetGrain<IGrainActivityGrain>(this.IdentityString)
                .SetActive value


[<AbstractClass>]
type ActivatableGrain<'state, 'event, 'parent
                        when 'state : not struct
                        and 'state : (new: unit -> 'state)
                        and 'event : not struct
                        and 'parent :> IActivatableGrain> (identity, runtime, factory) =
    inherit EventSourcedGrain<'state, 'event, 'parent> (identity, runtime, factory)

    interface IActivatableGrain<'parent> with
        member this.IsActive =
            (this :> IGrainBase<'parent>)
                .As<'parent>()
                .IsActive

        member this.SetActive value =
            (this :> IGrainBase<'parent>)
                .As<'parent>()
                .SetActive value
