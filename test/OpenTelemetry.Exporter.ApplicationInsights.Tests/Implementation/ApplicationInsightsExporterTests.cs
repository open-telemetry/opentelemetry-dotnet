// <copyright file="ApplicationInsightsExporterTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of theLicense at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Extensibility;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Exporter.ApplicationInsights.Tests.Implementation
{
    public class ApplicationInsightsExporterTests
    {
        [Fact]
        public async Task StartStopExporter()
        {
            var config = new TelemetryConfiguration { TelemetryChannel = new StubTelemetryChannel(), };
            var exporter = new ApplicationInsightsExporter(SpanExporter.Create(), Stats.Stats.ViewManager,  config);

            exporter.Start();
            await Task.Delay(100);

            var sw = Stopwatch.StartNew();
            exporter.Stop();
            sw.Stop();

            Assert.InRange(sw.ElapsedMilliseconds, 0, 1000);
        }
    }
}
