// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Examples.AspNetCore.Controllers

open System
open System.Net.Http
open System.Security.Cryptography
open Examples.AspNetCore
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging

[<ApiController>]
[<Route("[controller]")>]
type WeatherForecastController(
  httpClient: HttpClient,
  instrumentationSource: InstrumentationSource,
  logger: ILogger<WeatherForecastController>) =
    inherit ControllerBase()

    static let requestUri = Uri("http://example.com")
    static let summaries = 
        [| "Freezing"; "Bracing"; "Chilly"; "Cool"; "Mild"; "Warm"; "Balmy"; "Hot"; "Sweltering"; "Scorching" |]

    let activitySource = instrumentationSource.ActivitySource
    let freezingDaysCounter = instrumentationSource.FreezingDaysCounter

    [<HttpGet>]
    member this.Get() = task {

        // Making a http call here to serve as an example of
        // how dependency calls will be captured and treated
        // automatically as child of incoming request.
        let _ = httpClient.GetStringAsync(requestUri) |> Async.AwaitTask

        // Optional: Manually create an activity. This will become a child of
        // the activity created from the instrumentation library for AspNetCore.
        // Manually created activities are useful when there is a desire to track
        // a specific subset of the request. In this example one could imagine
        // that calculating the forecast is an expensive operation and therefore
        // something to be distinguished from the overall request.
        // Note: Tags can be added to the current activity without the need for
        // a manual activity using Activity.Current?.SetTag()
        use _ = activitySource.StartActivity("calculate forecast")

        let forecast =
            [| 1 .. 5 |]
            |> Array.map (fun index ->
                { Date = DateTime.Now.AddDays(float index)
                  TemperatureC = RandomNumberGenerator.GetInt32(-20, 55)
                  Summary = summaries.[RandomNumberGenerator.GetInt32(summaries.Length)] })

        // Optional: Count the freezing days
        let freezingDays = forecast |> Array.filter (fun f -> f.TemperatureC < 0) |> Array.length
        freezingDaysCounter.Add(int64 freezingDays)

        logger.LogInformation("WeatherForecasts generated {Count}: {Forecasts}", forecast.Length, forecast)

        return forecast :> WeatherForecast seq
    } 
