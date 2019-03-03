[<AutoOpen>]
module Orleans.Functional.TypeExtensions

open System
open FSharp.Utils.Tasks
open Orleans
open Orleans.Functional.Types

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

type IGrainFactory with
    member this.GetWorker<'t when 't :> IWorkerGrain> () = this.GetGrain<'t> Guid.Empty

    member this.Get<'t when 't :> IActivatableGrain and 't :> IGuidGrain> id =
        task {
            let grain = this.GetGrain<'t> id
            match! grain.IsActive () with
            | true -> return Some grain
            | false -> return None
        }

    member this.Get<'t when 't :> IActivatableGrain and 't :> IIntegerGrain> id =
        task {
            let grain = this.GetGrain<'t> id
            match! grain.IsActive () with
            | true -> return Some grain
            | false -> return None
        }

    member this.Get<'t when 't :> IActivatableGrain and 't :> IStringGrain> id =
        task {
            let grain = this.GetGrain<'t> id
            match! grain.IsActive () with
            | true -> return Some grain
            | false -> return None
        }

    member this.Get<'t when 't :> IActivatableGrain and 't :> IGuidCompoundGrain> (id, strId) =
        task {
            let grain = this.GetGrain<'t> (id, strId)
            match! grain.IsActive () with
            | true -> return Some grain
            | false -> return None
        }

    member this.Get<'t when 't :> IActivatableGrain and 't :> IIntegerCompoundGrain> (id, strId) =
        task {
            let grain = this.GetGrain<'t> (id, strId)
            match! grain.IsActive () with
            | true -> return Some grain
            | false -> return None
        }
