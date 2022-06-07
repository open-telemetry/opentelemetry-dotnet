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

using System.Diagnostics.Tracing;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Serilog;

var resourceBuilder = ResourceBuilder.CreateDefault().AddService("Examples.LogEmitter");

using var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
{
    options.IncludeFormattedMessage = true;
    options
        .SetResourceBuilder(resourceBuilder)
        .AddConsoleExporter();
});

// Creates an OpenTelemetryEventSourceLogEmitter for routing EventSources with
// names matching OpenTelemetry* into logs
using var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
    openTelemetryLoggerProvider,
    (name) => name.StartsWith("OpenTelemetry") ? EventLevel.LogAlways : null);

// Configue Serilog global logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(openTelemetryLoggerProvider) // <- Register OpenTelemetry Serilog sink
    .CreateLogger();

// Note: Serilog ForContext API is used to set "CategoryName" on log messages
ILogger programLogger = Log.Logger.ForContext<Program>();

programLogger.Information("Application started {Greeting} {Location}", "Hello", "World");

programLogger.Information("Message {Array}", new string[] { "value1", "value2" });

// Note: ForceFlush is only called here to demo
// OpenTelemetryEventSourceLogEmitter converting SDK events into log messages
openTelemetryLoggerProvider.ForceFlush();

Console.WriteLine("Press ENTER to exit...");

Console.ReadLine();
