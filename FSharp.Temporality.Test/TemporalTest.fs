﻿module TemporalTest

open Temporality
open Xunit
open FsCheck
open FsUnit.Xunit

let jan15 day = DateTime(2015, 01, day)

let toPositiveDuration (timeSpan : TimeSpan) = 
    if timeSpan < TimeSpan.Zero then timeSpan.Negate()
    else timeSpan

let getPeriod d1 d2 = 
    let minDate = min d1 d2
    let maxDate = max d1 d2
    { StartDate = minDate
      EndDate = maxDate }

let toPeriod (d1, d2) = 
    match d2 > d1 with
    | true -> { StartDate = d1; EndDate = d2 }
    | _ -> { StartDate = d2; EndDate = d1 }

let toTemporary (v, d1, d2) = 
    let p = toPeriod (d1, d2)
    { Period = p; Value = v}

[<Fact>]
let ``TimeSpan composition test``()=
    TimeSpan.forEver + TimeSpan.forNever |> should equal TimeSpan.forEver
    TimeSpan.forEver - TimeSpan.forNever |> should equal TimeSpan.forEver
    TimeSpan.forNever - TimeSpan.forNever |> should equal TimeSpan.forNever
    TimeSpan.forNever + TimeSpan.forNever |> should equal TimeSpan.forNever

    DateTime.MaxValue - DateTime.MinValue |> should equal TimeSpan.forEver

[<Fact>]
let ``Period should be display with math interval notation for half open interval``()=
    let p = { StartDate = jan15 1; EndDate = jan15 2 }
    
    p.ToString() |> should equal "[1/1/2015 0:00:00, 2/1/2015 0:00:00)" 

    Period.Always.ToString() |> should equal "Always"
    Period.Never.ToString() |> should equal "Never"

[<Fact>]
let ``Temporary should be displayed same as Period``()=
    let t = { Period = Period.Always; Value = "Hello" }

    t.ToString() |> should equal @"Always : ""Hello"""

[<Fact>]
let ``period intersection test``()=
    let arb = 
        Arb.generate<DateTime>
        |> Gen.four
        |> Gen.map(fun (d1, d2, d3, d4) -> ( getPeriod d1 d2, getPeriod d3 d4 ))
        |> Arb.fromGen
    
    let ``check that the largest period of two periods intersect with them`` (p1, p2) =
        
        let largestPeriod = 
            let minStartDate = min p1.StartDate p2.StartDate
            let maxEndDate = max p1.EndDate p2.EndDate
            { StartDate = minStartDate
              EndDate = maxEndDate }
        
        let ``largest period intersect with all period`` = 
            match (Period.intersect largestPeriod p1), (Period.intersect largestPeriod p2) with
            | Some a1, Some a2 -> a1 = p1 && a2 = p2
            | _, _ when p2.StartDate > p1.EndDate -> true
            | _, _ -> false

        let ``Never never intesect with period`` = 
            match (Period.intersect Period.Never p1), (Period.intersect p2 Period.Never) with
            | None, None -> true
            | _ -> false

        ``largest period intersect with all period``
        && ``Never never intesect with period``

    Check.QuickThrowOnFailure(Prop.forAll arb <| ``check that the largest period of two periods intersect with them``) 

[<Fact>]
let ``split period of 20 days by 5 should be 4 period of 5 days``()=
    let actual = 
        [ { Period = Period.from (jan15 1) (TimeSpan.forNDays 20); Value = "Hello" } ]
        |> Temporal.split (TimeSpan.forNDays 5)
    actual
    |> should equal 
        [ { Period = Period.from (jan15 1) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 6) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 11) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 16) (TimeSpan.forNDays 5); Value = "Hello" } ]

[<Fact>]
let ``split empty period is empty period``() = 
    let arb = 
        Arb.generate<DateTime>
        |> Gen.map (fun d -> 
               [ { Period = 
                       { StartDate = d
                         EndDate = d }
                   Value = "pouet" } ]
               |> Temporal.toTemporal)
        |> Arb.fromGen
    
    let ``check that startdate is equal to enddate`` t = 
        t |> Temporal.split (TimeSpan.forNDays 5) = t
    
    Check.QuickThrowOnFailure(Prop.forAll arb <| ``check that startdate is equal to enddate``)

[<Fact>]
let ``split period by n days``()=
    let toTestTemporary (n, date) = 
        { Period = { StartDate=date; EndDate = date + (TimeSpan.forNDays n) }
          Value = "hello" }
    
    let toTestTemporaries u = u |> Seq.map(toTestTemporary) |> Temporal.toTemporal

    let arb = 
        Arb.generate<int*DateTime>
        |> Gen.listOf
        |> Gen.map(toTestTemporaries)
        |> Arb.fromGen
    
    let splitPeriod = TimeSpan.forNDays 5

    let ``check that period are sorted by start date in order to have correct interval`` temporal = 
        let actual = temporal |> Temporal.split splitPeriod
        (actual |> Seq.sortBy(fun t -> t.Period.StartDate) |> Seq.toList) = (actual |> Seq.toList)

    let ``check that all period are less than split period`` temporal = 
        temporal 
        |> Temporal.split splitPeriod
        |> Seq.forall(fun v -> v.Period.Duration <= splitPeriod)

    Check.QuickThrowOnFailure(Prop.forAll arb <| ``check that all period are less than split period``)
    Check.QuickThrowOnFailure(Prop.forAll arb <| ``check that period are sorted by start date in order to have correct interval``)

[<Fact>]
let ``contigous period with same value should be merged``()=
    let arb = 
        Arb.generate<DateTime>
        |> Gen.four
        |> Gen.map(
            fun (d1, d2, d3, d4) -> 
                ( getPeriod d1 d2, getPeriod d3 d4 ))
        |> Arb.fromGen

    let ``check that largest period of two with same value are all merged`` (p1,p2) =
        let sameValue = "Hello"
        let getTemporary p v = { Period = p; Value = v }
        let maxPeriod = 
            let startDate = min p1.StartDate p2.StartDate 
            let endDate = max p1.EndDate p2.EndDate
            { StartDate = startDate
              EndDate = endDate }
        let tMax = getTemporary maxPeriod sameValue
        let t1 = getTemporary p1 sameValue
        let t2 = getTemporary p2 "World"

        let merged = [tMax; t1; t2] |> Temporal.toTemporal |> Temporal.merge
        
        if(t2.Period.StartDate < t1.Period.StartDate) 
        then merged = ([tMax; t1; t2] |> Temporal.toTemporal)
        else merged = ([tMax; t2] |> Temporal.toTemporal)
        
    Check.QuickThrowOnFailure(Prop.forAll arb <| ``check that largest period of two with same value are all merged``)

[<Fact>]
let ``when value are equal on intersect periods should merge``() =
    [] |> Temporal.toTemporal |> Temporal.merge |> should equal ([] |> Temporal.toTemporal)
    
    let actual = 
        [ { Period = Period.from (jan15 1) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 11) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 06) (TimeSpan.forNDays 5); Value = "World" }
          { Period = Period.from (jan15 21) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 16) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 26) (TimeSpan.forNDays 5); Value = "Hello" } ] 
        |> Temporal.toTemporal |> Temporal.merge

    let expected = 
        [ { Period = Period.from (jan15 01) (TimeSpan.forNDays 5); Value = "Hello" }
          { Period = Period.from (jan15 06) (TimeSpan.forNDays 5); Value = "World" }
          { Period = Period.from (jan15 11) (TimeSpan.forNDays 20); Value = "Hello" } ]
        |> Temporal.toTemporal

    actual |> should equal expected

[<Fact>]
let ``should list temporaries for a given period``()=
    let temporal = 
        [ { Period = Period.from (jan15 1) (TimeSpan.forNDays 10); Value = "Hello" }
          { Period = Period.from (jan15 11) (TimeSpan.forNDays 10); Value = "Toto" } ]
        |> Temporal.toTemporal
    
    
    temporal |> Temporal.view Period.Always |> should equal temporal
    temporal |> Temporal.view Period.Never = [] |> should equal true
//
//[<Fact>]
//let ``check that temporary grouped by value can't intersect beetween them``() =
//    let arb = 
//        Arb.generate<string*DateTime*DateTime>
//        |> Gen.listOf
//        |> Gen.oneof 
//            [ gen { ("Hello", jan15  1, jan15 10) }
//              gen { ("World", jan15 10, jan15 12) }
//              gen { ("Hello", jan15 13, jan15 15) }
//              gen { ("Hello", jan15 12, jan15 13) }
//              gen { ("Hello", jan15 15, jan15 17) } ]
//        |> Gen.map(toTemporary)
//        |> Arb.fromGen
//
//    let intersectProperty temporaries = 
//        let intersectWithList t l = l |> Seq.map(fun t2 -> Temporary<_>.Intersect t t2)
//        
//        let intersectBetweenLists l1 l2 = 
//            seq {
//                l1 |> Seq.map(fun i1 -> yield! intersectWithList i1 l2)
//            }
//        
//        let intersections = 
//            seq {
//                temporaries 
//                |> Seq.groupBy(fun t -> t.Value)
//                |> Seq.map(
//                    fun (_, groups) -> 
//                        groups |> Seq.
//                        let groupIntersections = intersectBetweenLists groups groups
//                        )
//            }
//
//    Check.QuickThrowOnFailure(Prop.forAll arb intersectProperty)