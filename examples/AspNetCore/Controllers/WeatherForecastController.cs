// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Examples.AspNetCore.Controllers;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Examples.AspNetCore;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public partial class WeatherForecastController : ControllerBase
{
    private static readonly Uri RequestUri = new("http://google.com");

    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private readonly HttpClient httpClient;
    private readonly ILogger<WeatherForecastController> logger;
    private readonly ActivitySource activitySource;
    private readonly Counter<long> freezingDaysCounter;

    public WeatherForecastController(
        HttpClient httpClient,
        InstrumentationSource instrumentationSource,
        ILogger<WeatherForecastController> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(instrumentationSource);
        ArgumentNullException.ThrowIfNull(logger);

        this.httpClient = httpClient;
        this.logger = logger;
        this.activitySource = instrumentationSource.ActivitySource;
        this.freezingDaysCounter = instrumentationSource.FreezingDaysCounter;
    }

    [HttpGet]
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        // Making an HTTP call here to serve as an example of
        // how dependency calls will be captured and treated
        // automatically as child of incoming request.
        _ = await this.httpClient.GetStringAsync(RequestUri);

        // Optional: Manually create an activity. This will become a child of
        // the activity created from the instrumentation library for AspNetCore.
        // Manually created activities are useful when there is a desire to track
        // a specific subset of the request. In this example one could imagine
        // that calculating the forecast is an expensive operation and therefore
        // something to be distinguished from the overall request.
        // Note: Tags can be added to the current activity without the need for
        // a manual activity using Activity.Current?.SetTag()
        using var activity = this.activitySource.StartActivity("calculate forecast");

        var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = RandomNumberGenerator.GetInt32(-20, 55),
            Summary = Summaries[RandomNumberGenerator.GetInt32(Summaries.Length)],
        })
        .ToArray();

        // Optional: Count the freezing days
        this.freezingDaysCounter.Add(forecast.Count(f => f.TemperatureC < 0));

        Logger.WeatherForecastGenerated(this.logger, forecast.Length, forecast);

        return forecast;
    }

    private static partial class Logger
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "WeatherForecasts generated {Count}: {Forecasts}")]
        public static partial void WeatherForecastGenerated(ILogger logger, int count, WeatherForecast[] forecasts);
    }
}
