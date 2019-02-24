module Orleans.Functional.UnitTests.Quotations

open NUnit.Framework
open Swensen.Unquote

open Orleans.Functional.Quotations

module ``Instance`` =
    [<Test>]
    let ``some test`` () =
        let myVariable = 1
        test <@ Option.isSome <| (|Instance|_|) <@ myVariable @> @>

    [<Test>]
    let ``none test`` () =
        test <@ Option.isNone <| (|Instance|_|) <@ {| {||} with Name = "Hi" |} @> @>

module ``RecordSetFields`` =
    [<Test>]
    let ``some test`` () =
        let state = {|Name = "hello"|}
        test <@ Option.isSome <| (|RecordSetFields|_|) <@ {|state with Name = "hello"|} @> @>

    [<Test>]
    let ``none test`` () =
        let myVariable = 1
        test <@ Option.isNone <| (|RecordSetFields|_|) <@ myVariable @> @>

module ``handleUpdate`` =
    [<CLIMutable>]
    type State = {
        Name: string
        Value: int
    }

    [<Test>]
    let ``basic test`` () =
        let state = {
            Name = "Hello"
            Value = 1
        }
        handleUpdate <@ {state with Value = 2} @> state
        state.Value =! 2
        state.Name =! "Hello"

    [<Test>]
    let ``basic test string`` () =
        let state = {
            Name = "Hello"
            Value = 1
        }
        handleUpdate <@ {state with Name = "HelloWorld"} @> state
        state.Value =! 1
        state.Name =! "HelloWorld"
