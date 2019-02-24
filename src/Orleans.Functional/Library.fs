namespace Orleans.Functional

open System
open System.Threading.Tasks
open FSharp.Control.Reactive
open FSharp.Quotations
open Orleans
open Orleans.EventSourcing

open Orleans.Functional.Quotations
open Orleans.Functional.Types

[<AbstractClass>]
type EventSourcedGrain<'state, 'event
                        when 'state : not struct
                        and 'state : (new: unit -> 'state)
                        and 'event : not struct>(identity, runtime, factory: IGrainFactory) =
    inherit JournaledGrain<'state, 'event>(identity, runtime)

    do ensureRecord<'state> ()
    do ensureUnion<'event> ()

    let erasedPrimaryKey grain =
        match grain with
        | IntegerGrain grain -> Integer <| grain.GetPrimaryKeyLong ()
        | GuidGrain grain -> Guid <| grain.GetPrimaryKey ()
        | StringGrain grain -> String <| grain.GetPrimaryKeyString ()
        | IntegerCompoundGrain grain -> IntegerCompound <| grain.GetPrimaryKeyLong ()
        | GuidCompoundGrain grain -> GuidCompound <| grain.GetPrimaryKey ()

    let activate = Subject<unit>.broadcast
    let deactivate = Subject<unit>.broadcast

    member val Activate = activate :> IObservable<_>
    member val Deactivate = deactivate :> IObservable<_>

    abstract member Reduce: 'state -> 'event -> ('state -> unit)

    override __.OnActivateAsync() =
        Subject.onNext () activate |> ignore
        Task.CompletedTask

    override __.OnDeactivateAsync() =
        Subject.onNext () deactivate |> ignore
        Task.CompletedTask

    override this.TransitionState (state, event) = this.Reduce state event state

    member this.Dispatch (event: 'event) = Task.FromResult <| this.RaiseEvent event
    member this.Dispatch (event: 'event list) = Task.FromResult <| this.RaiseEvents event

    interface IGrainBase with
        member this.As<'grain when 'grain :> IGrain> () =
            erasedPrimaryKey this
            |> getGrain<'grain> factory

type Reducer private () =
    /// **Description**
    ///   * Updates the current `EventSourcedGrain`'s internal state using the `update` parameter.
    ///
    /// **Parameters**
    ///   * `update` - Record update expression for the internal state of our `EventSourcedGrain`.
    ///
    /// **Output Type**
    ///   * A function of type `'state -> unit` that represents the update operation returned.
    static member Update ([<ReflectedDefinition>] update: Expr<'state>) = handleUpdate<'state> update
