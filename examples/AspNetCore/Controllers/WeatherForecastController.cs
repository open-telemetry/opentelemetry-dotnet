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

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching",
    };

    private static readonly HttpClient HttpClient = new();

    private readonly ILogger<WeatherForecastController> logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public IEnumerable<WeatherForecast> Get()
    {
        using var scope = this.logger.BeginScope("{Id}", Guid.NewGuid().ToString("N"));

        // Making an http call here to serve as an example of
        // how dependency calls will be captured and treated
        // automatically as child of incoming request.
        var res = HttpClient.GetStringAsync("http://google.com").Result;
        var rng = new Random();
        var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = rng.Next(-20, 55),
            Summary = Summaries[rng.Next(Summaries.Length)],
        })
        .ToArray();

        this.logger.LogInformation(
            "WeatherForecasts generated {count}: {forecasts}",
            forecast.Length,
            forecast);

        return forecast;
    }
}
