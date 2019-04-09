#nowarn "44"

namespace Orleans.Functional

open System
open System.Collections.Generic
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
    member this.Dispatch (events: 'event list) = this.RaiseEvents events

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
    member this.confirm = ContinuationTaskBuilder this.ConfirmEvents

    member __.State = base.State.Value

    member __.GetStream<'t> provider ``namespace`` id = base.GetStreamProvider(provider).GetStream<'t>(id, ``namespace``)
    member __.GetStreamProvider name = base.GetStreamProvider name

    interface IGrainBase

exception NoStreamAttributeException
exception NoStreamProviderException

[<AttributeUsage (AttributeTargets.Class, AllowMultiple = false, Inherited = false)>]
type StreamAttribute (``namespace``: string, ?provider: string) =
    inherit ImplicitStreamSubscriptionAttribute (``namespace``)

    member val Namespace = ``namespace`` with get, set
    member val Provider = provider with get, set

[<AttributeUsage (AttributeTargets.Class)>]
type EventAttribute () =
    inherit Attribute ()

[<AbstractClass>]
type StreamedEventSourcedGrain<'state, 'event when 'event: not struct> (?provider: string) as this =
    inherit EventSourcedGrain<EventSourcedGrainState<'state>, 'event> ()

    let streamAttribute =
        this
            .GetType()
            .GetCustomAttributes(false)
        |> Array.tryPick (
            function
            | :? StreamAttribute as attribute -> Some attribute
            | _ -> None
        )
        |> Option.defaultWith (fun () -> raise NoStreamAttributeException)

    let ``namespace`` = streamAttribute.Namespace

    let provider =
        provider
        |> Option.orElse streamAttribute.Provider
        |> Option.defaultWith (fun () -> raise NoStreamProviderException)

    let validTags =
        FSharpType.GetUnionCases typeof<'event>
        |> Array.filter (
            fun case ->
                case.GetCustomAttributes ()
                |> Array.exists (fun attribute -> attribute :? EventAttribute)
        )
        |> Array.map (fun case -> case.Tag)
        |> HashSet

    let eventTagReader = FSharpValue.PreComputeUnionTagReader typeof<'event>

    let isValidEvent (event: 'event) =
        eventTagReader (box event)
        |> validTags.Contains

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
            if handles.Count = 0 then
                do! this.stream.SubscribeAsync this.OnStreamMessage :> Task
            else
                do!
                    handles
                    |> Seq.map (fun handle -> handle.ResumeAsync this.OnStreamMessage)
                    |> Task.WhenAll
                    :> Task
        }

    override this.OnEventsConfirmed events =
        unitTask {
            for event in events do
                if isValidEvent event
                then do! this.stream.OnNextAsync event
        }
