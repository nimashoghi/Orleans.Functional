[<AutoOpen>]
module Orleans.Functional.Types.Extensions

open System
open Orleans
open Orleans.Functional.Types

type IGrainBase<'grain when 'grain :> IGrain> with
    member this.Resolve f = f (this.As<'grain> ())

type IActivatableGrain<'grain when 'grain :> IGrain and 'grain :> IActivatableGrain> with
    member this.IsActive () = this.Resolve (fun grain -> grain.IsActive ())
    member this.SetActive value = this.Resolve (fun grain -> grain.SetActive value)

type IIntegerGrain with
    member this.PrimaryKey = this.GetPrimaryKeyLong ()

type IGuidGrain with
    member this.PrimaryKey = this.GetPrimaryKey ()

type IStringGrain with
    member this.PrimaryKey = this.GetPrimaryKeyString ()

type IIntegerCompoundGrain with
    member this.PrimaryKey: int64 * string = this.GetPrimaryKeyLong ()

type IGuidCompoundGrain with
    member this.PrimaryKey: Guid * string = this.GetPrimaryKey ()

type IIntegerGrain<'grain when 'grain :> IIntegerGrain> with
    member this.PrimaryKey = this.GetPrimaryKeyLong ()

type IGuidGrain<'grain when 'grain :> IGuidGrain> with
    member this.PrimaryKey = this.GetPrimaryKey ()

type IStringGrain<'grain when 'grain :> IStringGrain> with
    member this.PrimaryKey = this.GetPrimaryKeyString ()

type IIntegerCompoundGrain<'grain when 'grain :> IIntegerCompoundGrain> with
    member this.PrimaryKey: int64 * string = this.GetPrimaryKeyLong ()

type IGuidCompoundGrain<'grain when 'grain :> IGuidCompoundGrain> with
    member this.PrimaryKey: Guid * string = this.GetPrimaryKey ()
