﻿module SplitProperties

open FsCheck.Xunit
open Temporality
open Xunit
open Bdd

let jan15 n = (DateTime(2015,1,n))
let days n = TimeSpan.FromDays(float n)

let ``I want to split temporaries`` days temporaries = Temporality.split days temporaries |> Seq.toList
let ``five days`` = days 5

let add a b = a+b

[<Fact>]
let ``simple add``() = When add |> With 1 |> And 2 |> Expect 3

[<Fact>]
let ``simple split test``()=
    When ``I want to split temporaries`` 
    |> For ``five days``
    |> With [ jan15 01 => jan15 11 := "HelloWorld" ]
    |> Expect
        [ jan15 01 => jan15 06 := "HelloWorld"
          jan15 06 => jan15 11 := "HelloWorld" ]
    
[<Arbitrary(typeof<TestData.RandomStringTemporal>)>]
module SplitTemporaries = 

    let splitPeriod = System.TimeSpan.FromDays(1000.)

    [<Property>]
    let ``check that all period are less than split period`` (temporaries:string Temporary list) = 
        temporaries
        |> Temporality.split splitPeriod
        |> Seq.forall(fun v -> v.period |> Period.duration <= splitPeriod)
