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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    public static void Main()
    {
        Stress(prometheusPort: 9184);
    }

    protected static void Run()
    {
        var sourceName = "OpenTelemetry.Tests.Stress." + Guid.NewGuid().ToString("D");

        using (var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options
                .AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(new List<LogRecord>())))
                .AddProcessor(new SimpleLogRecordExportProcessor(new InMemoryExporter<LogRecord>(new List<LogRecord>()))));
        }))
        {
            var logger = loggerFactory.CreateLogger(sourceName);
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }

        using (var meter = new Meter(sourceName))
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(new List<Metric>()))
            {
                Temporality = AggregationTemporality.Cumulative,
            })
            .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(new List<Metric>()))
            {
                Temporality = AggregationTemporality.Delta,
            })
            .AddReader(new PeriodicExportingMetricReader(new InMemoryExporter<Metric>(new List<Metric>()))
            {
                Temporality = AggregationTemporality.Cumulative,
            })
            .AddReader(new PeriodicExportingMetricReader(new InMemoryExporter<Metric>(new List<Metric>()))
            {
                Temporality = AggregationTemporality.Delta,
            })
            .Build())
        {
            var counter = meter.CreateCounter<long>("meter");
            counter.Add(1, new("a", 1), new("b", 2));
        }

        using (var activitySource = new ActivitySource(sourceName))
        using (var sdk = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .AddProcessor(new BatchActivityExportProcessor(new InMemoryExporter<Activity>(new List<Activity>())))
            .AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter<Activity>(new List<Activity>())))
            .Build())
        {
            using (var parent = activitySource.StartActivity("parent"))
            using (var child = activitySource.StartActivity("child"))
            {
                child?.SetTag("foo", 1);
                child?.SetTag("bar", "Hello, World!");
            }
        }
    }
}
