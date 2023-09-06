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

// What should be used for logging before DI is available?
// Remove the comment here, mention in the doc (in the corner) that particular problems can be addressed using explicit logger factory.
// Don't provide the actual guidance, but link to an issue so we can collect feedback from users.

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

// Is this even an intended scenario?
//
// app.Logger.LogInformation("Stopping the app..."); // change to a strongly typed, compile time version
// if someone needs to log here, what do they do?
// Noah: use app.Logger here
// Reiley: something destroyed the DI logger so we cannot really do anything here besides changing our recommendation
// use global logger?
// Noah/Reiley: the solution should be the same as what we tell folks to use between entry point and DI readiness
// Tarek: normally folks don't want to log here

public static partial class ApplicationLogs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting the app...")]
    public static partial void StartingApp(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
