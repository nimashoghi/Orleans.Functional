module Orleans.Functional.UnitTests.UnitTest1

open NUnit.Framework
open Swensen.Unquote

module ``List::some`` =
    open Orleans.Functional

    [<Test>]
    let ``basic test`` () =
        List.some [Some 1; Some 2; Some 3; None; Some 4] =! [1; 2; 3; 4; 5]
