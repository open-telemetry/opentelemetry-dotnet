// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;

    var resourceBuilder = ResourceBuilder
        .CreateDefault()
        .AddService(builder.Environment.ApplicationName);

    logging.SetResourceBuilder(resourceBuilder)
        .AddConsoleExporter();
});

var app = builder.Build();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.FoodPriceChanged("artichoke", 9.99);

    return "Hello from OpenTelemetry Logs!";
});

app.Logger.StartingApp();

app.Run();

public static partial class ApplicationLogs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting the app...")]
    public static partial void StartingApp(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
