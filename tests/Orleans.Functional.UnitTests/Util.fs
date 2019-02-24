module Orleans.Functional.UnitTests.Utils

open System.Collections.Generic
open NUnit.Framework
open Swensen.Unquote
open Orleans
open Orleans.Functional.Types
open Orleans.Functional.Utils

module ``Ensure`` =
    type UnionType = A | B
    type RecordType = {Name: string}

    [<Test>]
    let ``ensureUnion`` () =
        trap <@ ensureUnion<UnionType> () @>
        raises<InvalidTypeException> <@ ensureUnion<RecordType> () @>

    [<Test>]
    let ``ensureRecord`` () =
        trap <@ ensureRecord<RecordType> () @>
        raises<InvalidTypeException> <@ ensureRecord<UnionType> () @>

module ``Dict`` =
    let internal dictEqual lst (dict: IDictionary<'key, 'value>) =
        dict.Count =! List.length lst
        for key, value in lst do
            dict.[key] =! value

    [<Test>]
    let ``empty`` () =
        dictEqual [] Dict.empty<int, string>

    [<Test>]
    let ``add`` () =
        Dict.add "hello" "world" Dict.empty
        |> dictEqual ["hello", "world"]

    [<Test>]
    let ``tryFind`` () =
        Dict.add "hello" "world" Dict.empty
        |> Dict.tryFind "hello"
        =! Some "world"

module ``Grain tests`` =
    let idenfitier = (|IntegerGrain|GuidGrain|StringGrain|IntegerCompoundGrain|GuidCompoundGrain|)

    [<Test>]
    let ``IntegerGrain`` () =
        let grain = {new IGrainWithIntegerKey}
        idenfitier grain =! Choice1Of5 grain

    [<Test>]
    let ``GuidGrain`` () =
        let grain = {new IGrainWithGuidKey}
        idenfitier grain =! Choice2Of5 grain

    [<Test>]
    let ``StringGrain`` () =
        let grain = {new IGrainWithStringKey}
        idenfitier grain =! Choice3Of5 grain

    [<Test>]
    let ``IntegerCompoundGrain`` () =
        let grain = {new IGrainWithIntegerCompoundKey}
        idenfitier grain =! Choice4Of5 grain

    [<Test>]
    let ``GuidCompoundGrain`` () =
        let grain = {new IGrainWithGuidCompoundKey}
        idenfitier grain =! Choice5Of5 grain
