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
using Examples.LoggingExtensions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Serilog;

var openTelemetryLoggerProvider = Sdk.CreateLoggerProviderBuilder()
    .SetIncludeFormattedMessage(true)
    .ConfigureResource(builder => builder.AddService("Examples.LoggingExtensions"))
    .AddConsoleExporter()
    .Build();

// Creates an OpenTelemetryEventSourceLogEmitter for routing ExampleEventSource
// events into logs
using var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
    openTelemetryLoggerProvider, // <- Events will be written to openTelemetryLoggerProvider
    (name) => name == ExampleEventSource.EventSourceName ? EventLevel.Informational : null,
    disposeProvider: false); // <- Do not dispose the provider with OpenTelemetryEventSourceLogEmitter since in this case it is shared with Serilog

// Configure Serilog global logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(
        openTelemetryLoggerProvider, // <- Register OpenTelemetry Serilog sink writing to openTelemetryLoggerProvider
        disposeProvider: false) // <- Do not dispose the provider with Serilog since in this case it is shared with OpenTelemetryEventSourceLogEmitter
    .CreateLogger();

ExampleEventSource.Log.ExampleEvent("Startup complete");

// Note: Serilog ForContext API is used to set "CategoryName" on log messages
ILogger programLogger = Log.Logger.ForContext<Program>();

programLogger.Information("Application started {Greeting} {Location}", "Hello", "World");

// Note: For Serilog this call flushes all logs
Log.CloseAndFlush();

// Manually dispose OpenTelemetryLoggerProvider since it is being shared
openTelemetryLoggerProvider.Dispose();
