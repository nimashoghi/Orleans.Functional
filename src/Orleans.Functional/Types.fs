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

type IWorkerGrain =
    inherit IGrainWithGuidKey

type IGrainBase =
    inherit IGrain

type IStreamedGrainBase<'event when 'event: not struct> =
    inherit IGrain

    abstract member GetStream: unit -> IAsyncStream<'event> ValueTask

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

type IStreamedGuidGrain<'event when 'event : not struct> =
    inherit IGrainWithGuidKey
    inherit IStreamedGrainBase<'event>

type IStreamedIntegerGrain<'event when 'event : not struct> =
    inherit IGrainWithIntegerKey
    inherit IStreamedGrainBase<'event>

type IStreamedStringGrain<'event when 'event : not struct> =
    inherit IGrainWithStringKey
    inherit IStreamedGrainBase<'event>

type IStreamedIntegerCompoundGrain<'event when 'event : not struct> =
    inherit IGrainWithIntegerCompoundKey
    inherit IStreamedGrainBase<'event>

type IStreamedGuidCompoundGrain<'event when 'event : not struct> =
    inherit IGrainWithGuidCompoundKey
    inherit IStreamedGrainBase<'event>
