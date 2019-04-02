module Orleans.Functional.UnitTests.Quotations

open NUnit.Framework
open FsCheck.NUnit
open Swensen.Unquote

open Orleans.Functional.Quotations

[<CLIMutable>]
type State = {
    Name: string
}


module ``Instance`` =
    [<Test>]
    let ``some test`` () =
        let stateInstance = {Name = "Hello"}
        test <@ Option.isSome <| (|Instance|_|) <@ stateInstance @> @>

    [<Test>]
    let ``none test`` () =
        let stateInstance = {Name = "Hello"}
        test <@ Option.isNone <| (|Instance|_|) <@ { stateInstance with Name = "Hi" } @> @>

module ``RecordSetFields`` =
    [<Test>]
    let ``some test`` () =
        let state = {Name = "hello"}
        test <@ Option.isSome <| (|RecordSetFields|_|) <@ {state with Name = "hello"} @> @>

    [<Test>]
    let ``none test`` () =
        let stateInstance = {Name = "Hello"}
        test <@ Option.isNone <| (|RecordSetFields|_|) <@ stateInstance @> @>

    type State = {
        Name: string
        Client: string voption
    }

    [<Property>]
    let ``ValueSome edge case`` (initialName: string) (client: string) =
        let state = {Name = initialName; Client = ValueNone}
        (|RecordSetFields|_|) <@ {state with Client = ValueSome client} @>
        =! Some [typeof<State>.GetProperty "Client"]

module ``handleUpdate`` =
    [<CLIMutable>]
    type State = {
        Name: string
        Value: int
    }

    [<Property>]
    let ``basic test`` newName newValue =
        let state = {
            Name = "Hello"
            Value = 1
        }
        handleUpdate <@ {state with Name = newName; Value = newValue} @> {state with Name = newName; Value = newValue} state
        state.Value =! newValue
        state.Name =! newName

    [<Test>]
    let ``basic test string`` () =
        let state = {
            Name = "Hello"
            Value = 1
        }
        handleUpdate <@ {state with Name = "HelloWorld"} @> {state with Name = "HelloWorld"} state
        state.Value =! 1
        state.Name =! "HelloWorld"
