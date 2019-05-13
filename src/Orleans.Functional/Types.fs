[<AutoOpen>]
module Orleans.Functional.Types

open System
open System.Threading.Tasks
open Orleans
open Orleans.Streams

exception InvalidTypeException of Type

type GrainType =
| GuidGrainType
| IntegerGrainType
| StringGrainType
| IntegerCompoundGrainType
| GuidCompoundGrainType

type GrainId =
| Guid of Guid
| Integer of int64
| String of string
| IntegerCompound of int64 * string
| GuidCompound of Guid * string

type IGrainBase =
    inherit IGrain

type IWorkerGrain =
    inherit IGrainWithGuidKey
    inherit IGrainBase

type IActivatableGrain =
    inherit IGrainBase

    abstract member IsActive: unit -> bool ValueTask
    abstract member SetActive: bool -> Task

type IGuidGrain =
    inherit IGrainWithGuidKey
    inherit IGrainBase

type IIntegerGrain =
    inherit IGrainWithIntegerKey
    inherit IGrainBase

type IStringGrain =
    inherit IGrainWithStringKey
    inherit IGrainBase

type IIntegerCompoundGrain =
    inherit IGrainWithIntegerCompoundKey
    inherit IGrainBase

type IGuidCompoundGrain =
    inherit IGrainWithGuidCompoundKey
    inherit IGrainBase

type IStreamedGrainBase<'event when 'event: not struct> =
    inherit IGrain

    abstract member GetStream: unit -> IAsyncStream<'event> ValueTask

type IStreamedGuidGrain<'event when 'event : not struct> =
    inherit IGuidGrain
    inherit IStreamedGrainBase<'event>

type IStreamedIntegerGrain<'event when 'event : not struct> =
    inherit IIntegerGrain
    inherit IStreamedGrainBase<'event>

type IStreamedStringGrain<'event when 'event : not struct> =
    inherit IStringGrain
    inherit IStreamedGrainBase<'event>

type IStreamedIntegerCompoundGrain<'event when 'event : not struct> =
    inherit IIntegerCompoundGrain
    inherit IStreamedGrainBase<'event>

type IStreamedGuidCompoundGrain<'event when 'event : not struct> =
    inherit IGuidCompoundGrain
    inherit IStreamedGrainBase<'event>

type IListenerGrainBase<'event, 'grain when 'event: not struct and 'grain :> IStreamedGrainBase<'event>> =
    inherit IGrain

type IGuidListenerGrain<'event, 'grain when 'event: not struct and 'grain :> IStreamedGuidGrain<'event>> =
    inherit IGuidGrain
    inherit IListenerGrainBase<'event, 'grain>

type IIntegerListenerGrain<'event, 'grain when 'event: not struct and 'grain :> IStreamedIntegerGrain<'event>> =
    inherit IIntegerGrain
    inherit IListenerGrainBase<'event, 'grain>

type IStringListenerGrain<'event, 'grain when 'event: not struct and 'grain :> IStreamedStringGrain<'event>> =
    inherit IStringGrain
    inherit IListenerGrainBase<'event, 'grain>

type IGuidCompoundListenerGrain<'event, 'grain when 'event: not struct and 'grain :> IStreamedGuidCompoundGrain<'event>> =
    inherit IGuidCompoundGrain
    inherit IListenerGrainBase<'event, 'grain>

type IIntegerCompoundListenerGrain<'event, 'grain when 'event: not struct and 'grain :> IStreamedIntegerCompoundGrain<'event>> =
    inherit IIntegerCompoundGrain
    inherit IListenerGrainBase<'event, 'grain>
