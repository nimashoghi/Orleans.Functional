namespace Orleans.Functional.Types

open System
open System.Threading.Tasks
open Orleans

exception InvalidTypeException of Type

type GrainType =
| IntegerGrainType
| GuidGrainType
| StringGrainType
| IntegerCompoundGrainType
| GuidCompoundGrainType

type GrainId =
| Integer of int64
| Guid of Guid
| String of string
| IntegerCompound of int64 * string
| GuidCompound of Guid * string

type IGrainBase =
    inherit IGrain
    abstract member As<'grain when 'grain :> IGrain> : unit -> 'grain

type IGrainBase<'grain when 'grain :> IGrain> =
    inherit IGrainBase

type IActivatableGrain =
    abstract member IsActive: unit -> bool Task
    abstract member SetActive: bool -> unit Task

type IActivatableGrain<'grain when 'grain :> IGrain and 'grain :> IActivatableGrain> =
    inherit IGrainBase<'grain>

type IIntegerGrain =
    inherit IGrainWithIntegerKey
    inherit IGrainBase

type IGuidGrain =
    inherit IGrainWithGuidKey
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

type IIntegerGrain<'grain when 'grain :> IIntegerGrain> =
    inherit IIntegerGrain
    inherit IGrainBase<'grain>

type IGuidGrain<'grain when 'grain :> IGuidGrain> =
    inherit IGuidGrain
    inherit IGrainBase<'grain>

type IStringGrain<'grain when 'grain :> IStringGrain> =
    inherit IStringGrain
    inherit IGrainBase<'grain>

type IIntegerCompoundGrain<'grain when 'grain :> IIntegerCompoundGrain> =
    inherit IIntegerCompoundGrain
    inherit IGrainBase<'grain>

type IGuidCompoundGrain<'grain when 'grain :> IGuidCompoundGrain> =
    inherit IGuidCompoundGrain
    inherit IGrainBase<'grain>
