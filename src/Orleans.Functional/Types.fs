[<AutoOpen>]
module Orleans.Functional.Types

open System
open System.Threading.Tasks
open Orleans

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

    abstract member As<'grain when 'grain :> IGrain> : unit -> 'grain ValueTask

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
