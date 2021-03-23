// <copyright file="SelfDiagnosticsEventLogForwarderTest.cs" company="OpenTelemetry Authors">
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

using System.Linq;
using System.Threading.Tasks;
using Foundatio.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests
{
    public class SelfDiagnosticsEventLogForwarderTest
    {
        [Fact]
        public async Task AddOpenTelemetryTracerProvider_HostBuilt_OpenTelemetrySdk_RegisteredAsSingleton()
        {
            const string TestSourceName = "TestSource";

            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor(
                onStart: a => { },
                onEnd: a => { },
                onFlush: () => { throw new System.Exception(); });

            var loggerFactory = new TestLoggerFactory
            {
                MinimumLevel = LogLevel.Trace,
            };

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddOpenTelemetrySelfDiagnosticsLogging();
                services.AddOpenTelemetryTracing(b => b
                    .SetSampler(new AlwaysOnSampler())
                    .AddSource(TestSourceName)
                    .AddProcessor(testActivityProcessor));
            });

            var host = builder.Build();
            var hostedServices = host.Services.GetServices<IHostedService>().ToList();

            Assert.Equal(2, hostedServices.Count);
            Assert.True(hostedServices.First().GetType() == typeof(SelfDiagnosticsLoggingHostedService));
            Assert.True(hostedServices.Last().GetType() == typeof(TelemetryHostedService));

            await host.StartAsync();

            var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
            Assert.NotNull(tracerProvider);

            tracerProvider.GetTracer(TestSourceName).StartRootSpan("Test Activity");
            tracerProvider.ForceFlush();

            Assert.NotEmpty(loggerFactory.LogEntries);
            Assert.Contains(loggerFactory.LogEntries, l => l.CategoryName == "OpenTelemetry.Sdk" && l.Message.Contains("Activity started") && l.Message.Contains("Test Activity"));
            Assert.Contains(loggerFactory.LogEntries, l => l.CategoryName == "OpenTelemetry.Sdk" && l.Message.Contains("ForceFlush") && l.Message.Contains("Exception"));
        }
    }
}
