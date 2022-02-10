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

namespace Examples.AspNetCore.Controllers;
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
    private readonly ExampleMeter exampleMeter;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, ExampleMeter exampleMeter)
    {
        this.logger = logger;
        this.exampleMeter = exampleMeter;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        this.logger.LogInformation("WeatherForecast GET called");
        WeatherForecast[] result;
        using (var activity = Tracing.ActivitySource.StartActivity("Getting weather data.", ActivityKind.Internal))
        {
            result = GetWeatherData();
            activity?.SetTag("Got data.", result);
        }

        this.exampleMeter.WeatherTypeCounter.Add(1, KeyValuePair.Create<string, object?>("Type", result[0].Summary));
        return result;
    }

    private static WeatherForecast[] GetWeatherData()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)],
        })
        .ToArray();
    }
}
