// <copyright file="OpenTelemetrySerilogEnricherTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace OpenTelemetry.Extensions.Serilog.Tests
{
    public class OpenTelemetrySerilogEnricherTests
    {
        [Fact]
        public void SerilogLogWithoutActivityTest()
        {
            List<LogEvent> emittedLogs = new();

            Log.Logger = new LoggerConfiguration()
                .Enrich.WithOpenTelemetry()
                .WriteTo.Sink(new InMemorySink(emittedLogs))
                .CreateLogger();

            Log.Logger.Information("Hello {greeting}", "World");

            Log.CloseAndFlush();

            Assert.Single(emittedLogs);

            LogEvent logEvent = emittedLogs[0];

            Assert.Single(logEvent.Properties);
        }

        [Fact]
        public void SerilogLogWithActivityAndNoParentTest()
        {
            using var activity = new Activity("Test");
            activity.Start();

            List<LogEvent> emittedLogs = new();

            Log.Logger = new LoggerConfiguration()
                .Enrich.WithOpenTelemetry()
                .WriteTo.Sink(new InMemorySink(emittedLogs))
                .CreateLogger();

            Log.Logger.Information("Hello {greeting}", "World");

            Log.CloseAndFlush();

            Assert.Single(emittedLogs);

            LogEvent logEvent = emittedLogs[0];

            AssertPropertyExistsAndHaveValue(logEvent, nameof(Activity.SpanId), $"\"{activity.SpanId.ToHexString()}\"");
            AssertPropertyExistsAndHaveValue(logEvent, nameof(Activity.TraceId), $"\"{activity.TraceId.ToHexString()}\"");
            Assert.False(logEvent.Properties.ContainsKey(nameof(Activity.ParentSpanId)));
            AssertPropertyExistsAndHaveValue(logEvent, "TraceFlags", activity.ActivityTraceFlags.ToString());
            Assert.False(logEvent.Properties.ContainsKey("TraceState"));
        }

        [Fact]
        public void SerilogLogWithActivityAndParentTest()
        {
            using var activity = new Activity("ParentActivity");
            activity.Start();

            using var childActivity = new Activity("ChildActivity");
            childActivity.SetParentId(activity.TraceId, activity.SpanId, activity.ActivityTraceFlags);
            childActivity.Start();

            Assert.NotEqual(default, childActivity.ParentSpanId);

            List<LogEvent> emittedLogs = new();

            Log.Logger = new LoggerConfiguration()
                .Enrich.WithOpenTelemetry()
                .WriteTo.Sink(new InMemorySink(emittedLogs))
                .CreateLogger();

            Log.Logger.Information("Hello {greeting}", "World");

            Log.CloseAndFlush();

            Assert.Single(emittedLogs);

            LogEvent logEvent = emittedLogs[0];

            AssertPropertyExistsAndHaveValue(logEvent, nameof(Activity.SpanId), $"\"{childActivity.SpanId.ToHexString()}\"");
            AssertPropertyExistsAndHaveValue(logEvent, nameof(Activity.TraceId), $"\"{childActivity.TraceId.ToHexString()}\"");
            AssertPropertyExistsAndHaveValue(logEvent, nameof(Activity.ParentSpanId), $"\"{childActivity.ParentSpanId.ToHexString()}\"");
            AssertPropertyExistsAndHaveValue(logEvent, "TraceFlags", childActivity.ActivityTraceFlags.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("key1=value1")]
        public void SerilogLogWithActivityAndTraceState(string? traceState)
        {
            using var activity = new Activity("Test");
            activity.Start();
            activity.TraceStateString = traceState;

            List<LogEvent> emittedLogs = new();

            Log.Logger = new LoggerConfiguration()
                .Enrich.WithOpenTelemetry(new() { IncludeTraceState = true })
                .WriteTo.Sink(new InMemorySink(emittedLogs))
                .CreateLogger();

            Log.Logger.Information("Hello {greeting}", "World");

            Log.CloseAndFlush();

            Assert.Single(emittedLogs);

            LogEvent logEvent = emittedLogs[0];

            AssertPropertyExistsAndHaveValue(logEvent, nameof(Activity.SpanId), $"\"{activity.SpanId.ToHexString()}\"");
            AssertPropertyExistsAndHaveValue(logEvent, nameof(Activity.TraceId), $"\"{activity.TraceId.ToHexString()}\"");
            Assert.False(logEvent.Properties.ContainsKey(nameof(Activity.ParentSpanId)));
            AssertPropertyExistsAndHaveValue(logEvent, "TraceFlags", activity.ActivityTraceFlags.ToString());

            if (string.IsNullOrEmpty(traceState))
            {
                Assert.False(logEvent.Properties.ContainsKey("TraceState"));
            }
            else
            {
                AssertPropertyExistsAndHaveValue(logEvent, "TraceState", $"\"{traceState}\"");
            }
        }

        private static void AssertPropertyExistsAndHaveValue(
            LogEvent logEvent,
            string propertyName,
            string expectedValue)
        {
            Assert.True(logEvent.Properties.ContainsKey(propertyName));

            using var writer = new StringWriter();

            logEvent.Properties[propertyName].Render(writer);

            Assert.Equal(expectedValue, writer.ToString());
        }

        private sealed class InMemorySink : ILogEventSink
        {
            private readonly List<LogEvent> emittedLogs;

            public InMemorySink(List<LogEvent> emittedLogs)
            {
                this.emittedLogs = emittedLogs;
            }

            public void Emit(LogEvent logEvent)
            {
                this.emittedLogs.Add(logEvent);
            }
        }
    }
}
