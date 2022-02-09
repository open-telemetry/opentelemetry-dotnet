// <copyright file="WeatherForecastController.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace Examples.AspNetCore6.Controllers;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching",
    };

    private readonly ILogger<WeatherForecastController> logger;
    private readonly AspNetCoreMeter aspNet6Meter;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, AspNetCoreMeter aspNet6Meter)
    {
        this.logger = logger;
        this.aspNet6Meter = aspNet6Meter;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        using var activity = Tracing.ActivitySource.StartActivity("WeatherForecast GET", ActivityKind.Internal);
        activity?.SetStartTime(DateTime.UtcNow);
        this.logger.LogInformation("WeatherForecast GET called");
        var result = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)],
        })
        .ToArray();

        this.aspNet6Meter.WeatherTypeCounter.Add(1, KeyValuePair.Create<string, object?>("Type", result[0].Summary));
        return result;
    }
}
