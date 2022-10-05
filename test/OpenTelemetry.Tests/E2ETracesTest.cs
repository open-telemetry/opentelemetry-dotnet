// <copyright file="BaseProcessorTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using OpenTelemetry.Logs;
    using OpenTelemetry.Trace;
    using Xunit;
    using Xunit.Abstractions;

    public class E2ETracesTest
    {
        internal readonly ITestOutputHelper OutputHelper;

        public E2ETracesTest(ITestOutputHelper output)
        {
            this.OutputHelper = output;
        }

        [Theory]
        [InlineData(LogLevel.Information)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        [InlineData(LogLevel.Critical)]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Trace)]
        public void LogWithinActivity(LogLevel logLevel)
        {
            // SETUP
            var uniqueTestId = Guid.NewGuid();

            var activitySourceName = $"activitySourceName{uniqueTestId}";
            using var activitySource = new ActivitySource(activitySourceName);

            var logCategoryName = $"logCategoryName{uniqueTestId}";

            List<Activity> exportedActivities = new List<Activity>();
            List<LogRecord> exportedLogs = new List<LogRecord>();

            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .AddInMemoryExporter(exportedActivities)
                .Build();

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter<OpenTelemetryLoggerProvider>(logCategoryName, logLevel)
                    .AddOpenTelemetry(options =>
                    {
                        options.AddInMemoryExporter(exportedLogs);
                    });
            });

            // ACT
            string spanId, traceId;
            string activityName = $"TestActivity {nameof(this.LogWithinActivity)} {logLevel}";

            using (var activity = activitySource.StartActivity(name: activityName))
            {
                spanId = activity.SpanId.ToHexString();
                traceId = activity.TraceId.ToHexString();

                var logger = loggerFactory.CreateLogger(logCategoryName);

                logger.Log(
                    logLevel: logLevel,
                    eventId: 0,
                    exception: null,
                    message: "Hello {name}.",
                    args: new object[] { "World" });
            }

            // CLEANUP
            tracerProvider.Dispose();
            loggerFactory.Dispose();

            // ASSERT
            try
            {
                Assert.True(exportedActivities.Count == 1, "Unexpected count of exported Activities.");
                Assert.True(exportedLogs.Count == 1, "Unexpected count of exported Logs.");
            }
            catch (Exception)
            {
                this.OutputHelper.WriteLine($"Activities Count:{exportedActivities.Count}  Logs Count:{exportedLogs.Count}\n");
                this.OutputHelper.WriteLine($"Expected TraceId:{traceId}  Expected SpanId:{spanId}\n");

                foreach (var activity in exportedActivities)
                {
                    this.OutputHelper.WriteLine("Exported Activity:");
                    this.OutputHelper.WriteLine($"\tDisplayName: {activity.DisplayName}");
                    this.OutputHelper.WriteLine($"\tId: {activity.Id}");
                    this.OutputHelper.WriteLine($"\tTraceId: {activity.TraceId.ToHexString()}");
                    this.OutputHelper.WriteLine($"\tSpanId: {activity.SpanId.ToHexString()}");
                }

                throw;
            }
        }

        [Fact]
        public void StressTest()
        {
            // Running this test on a loop to try and capture the intermittent failure.

            for (int i = 0; i < 100000; i++)
            {
                this.LogWithinActivity(LogLevel.Trace);
            }
        }
    }
}
