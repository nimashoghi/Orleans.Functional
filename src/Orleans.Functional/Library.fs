#nowarn "44"

namespace Orleans.Functional

open System
open System.Threading.Tasks
open FSharp.Quotations
open FSharp.Reflection
open FSharp.Utils.Quotations
open FSharp.Utils.Tasks
open Orleans
open Orleans.EventSourcing
open Orleans.Streams

open Orleans.Functional.Quotations
open Orleans.Functional.Types

open FSharp.Utils.Tasks.TplPrimitives

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
type EventSourcedGrain<'state, 'event when 'state: not struct and 'event: not struct> () =
    inherit JournaledGrain<EventSourcedGrainState<'state>, 'event> ()

    do
        assert FSharpType.IsRecord typeof<'state>
        assert FSharpType.IsUnion typeof<'event>

    abstract member Reduce: state: 'state -> ('event -> 'state -> unit)

    abstract member OnActivate: unit -> Task
    abstract member OnDeactivate: unit -> Task

    default __.OnActivate () = Task.CompletedTask
    default __.OnDeactivate () = Task.CompletedTask

    abstract member OnEventsConfirmed: events: 'event [] -> Task
    default __.OnEventsConfirmed _ = Task.CompletedTask

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
    member this.Dispatch (events: #seq<'event>) = this.RaiseEvents events

    member this.TryDispatch (event: 'event) = this.RaiseConditionalEvent event
    member this.TryDispatch (events: #seq<'event>) = this.RaiseConditionalEvents events

    // TODO: Should this be sequential or concurrent?
    member this.ConfirmEvents () =
        let events =
            this.UnconfirmedEvents
            |> Seq.toArray
        let baseConfirmEvents = base.ConfirmEvents ()
        unitTask {
            do! baseConfirmEvents
            do! this.OnEventsConfirmed events
        }
    member this.confirm = ConfirmationBuilder this

    member __.State = base.State.Value

    member __.GetStream<'t> provider ``namespace`` id = base.GetStreamProvider(provider).GetStream<'t>(id, ``namespace``)
    member __.GetStreamProvider name = base.GetStreamProvider name

    interface IGrainBase

and ConfirmationBuilder<'state, 'event when 'state: not struct and 'event: not struct> (this: EventSourcedGrain<'state, 'event>) =
    inherit AwaitableBuilder ()

    member __.Yield (event: 'event) =
        this.Dispatch event
        Ply (result = ())

    member __.YieldFrom (events: #seq<'event>) =
        this.Dispatch events
        Ply (result = ())

    member __.Run (f : unit -> Ply<'u>) =
        unitTask {
            let! _ = f ()
            do! this.ConfirmEvents ()
        }

type IStreamEvent =
    abstract member ShouldStream: unit -> bool

type IStreamEventAsync =
    abstract member ShouldStream: unit -> bool ValueTask

[<AbstractClass>]
type StreamedEventSourcedGrain<'state, 'event when 'state: not struct and 'event: not struct> (``namespace``: string, provider : string) =
    inherit EventSourcedGrain<'state, 'event> ()

    [<DefaultValue>]
    val mutable stream: IAsyncStream<'event>

    abstract member OnStreamMessage: event: 'event -> sequenceToken: StreamSequenceToken -> Task
    default __.OnStreamMessage _ _ = Task.CompletedTask

    override this.OnActivateAsync () =
        let baseMethodResult = base.OnActivateAsync ()

        unitTask {
            do! baseMethodResult
            this.stream <- this.GetStream provider ``namespace`` (this.GetPrimaryKey ())
            let! handles = this.stream.GetAllSubscriptionHandles ()
            do!
                if handles.Count = 0
                then this.stream.SubscribeAsync this.OnStreamMessage :> Task
                else
                    handles
                    |> Seq.map (fun handle -> handle.ResumeAsync this.OnStreamMessage)
                    |> Task.WhenAll
                    :> Task
        }

    override this.OnEventsConfirmed events =
        let shouldStreamEvent (event: 'event) =
            vtask {
                match box event with
                | :? IStreamEvent as streamedEvent -> return streamedEvent.ShouldStream ()
                | :? IStreamEventAsync as streamedEvent -> return! streamedEvent.ShouldStream ()
                | _ -> return true
            }

        unitTask {
            for event in events do
                match! shouldStreamEvent event with
                | true -> do! this.stream.OnNextAsync event
                | _ -> ()
        }

    interface IStreamedGrainBase<'event> with
        member this.GetStream () = ValueTask<_> this.stream

[<AbstractClass>]
type StreamListenerGrain<'event, 'grain when 'event: not struct and 'grain :> IStreamedGrainBase<'event>> (provider, ``namespace``) =
    inherit Grain ()

    abstract member OnStreamMessage: event: 'event -> sequenceToken: StreamSequenceToken -> Task

    [<DefaultValue>]
    val mutable private stream: IAsyncStream<'event>
    member this.Stream = this.stream

    [<DefaultValue>]
    val mutable private grain: 'grain
    member this.Grain = this.grain

    member __.GrainFactory = base.GrainFactory

    member private this.GetStream () =
        this
            .GetStreamProvider(provider)
            .GetStream<'event>(this.GetPrimaryKey (), ``namespace``)

    override this.OnActivateAsync () =
        let resumeSubscriptionHandles () =
            uunitTask {
                let! handles = this.stream.GetAllSubscriptionHandles ()
                do!
                    if handles.Count = 0
                    then this.stream.SubscribeAsync this.OnStreamMessage :> Task
                    else
                        handles
                        |> Seq.map (fun handle -> handle.ResumeAsync this.OnStreamMessage)
                        |> Task.WhenAll
                        :> Task
            }

        let baseMethodResult = base.OnActivateAsync ()
        unitTask {
            do! baseMethodResult

            // TODO: Fix this!
            this.grain <- Unchecked.defaultof<_> // this.GrainFactory.GetGrain<'grain> (this.GetPrimaryKey ())
            this.stream <- this.GetStream ()

            do! resumeSubscriptionHandles ()
        }
