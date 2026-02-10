// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Examples.AspNetCore

open System
open System.Diagnostics.Metrics
open System.Diagnostics

// It is recommended to use a custom type to hold references for
// ActivitySource and Instruments. This avoids possible type collisions
// with other components in the DI container.
type InstrumentationSource() =
    
    static let activitySourceName = "Examples.AspNetCore"    
    static let meterName = "Examples.AspNetCore"
    
    let version = 
        typeof<InstrumentationSource>.Assembly.GetName().Version
        |> Option.ofObj
        |> Option.map string
        |> Option.toObj
    
    let activitySource = new ActivitySource(activitySourceName, version)
    let meter = new Meter(meterName, version)
    let freezingDaysCounter = 
        meter.CreateCounter<int64>("weather.days.freezing", description = "The number of days where the temperature is below freezing")
    
    member _.ActivitySource = activitySource
    member _.FreezingDaysCounter = freezingDaysCounter
    member _.MeterName = meterName
    
    interface IDisposable with
        member _.Dispose() =
            activitySource.Dispose()
            meter.Dispose()
