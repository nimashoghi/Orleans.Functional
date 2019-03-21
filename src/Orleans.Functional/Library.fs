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
                        and 'event : not struct> (factory: IGrainFactory) =
    inherit JournaledGrain<'state, 'event> ()

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

    member __.State = base.State

    interface IGrainBase with
        member this.As<'grain when 'grain :> IGrain> () =
            erasedPrimaryKey this
            |> getGrain<'grain> factory
            |> Task.FromResult
