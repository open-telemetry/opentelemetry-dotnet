// <copyright file="JaegerActivityExporterTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Trace.Configuration;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests
{
    public class JaegerActivityExporterTests
    {
        [Fact]
        public void UseJaegerActivityExporterWithCustomActivityProcessor()
        {
            const string ActivitySourceName = "jaeger.test";
            TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    endCalled = true;
                };

            var openTelemetrySdk = OpenTelemetrySdk.EnableOpenTelemetry(b => b
                            .AddActivitySource(ActivitySourceName)
                            .UseJaegerActivityExporter(
                                null, p => p.AddProcessor((next) => testActivityProcessor)));

            var source = new ActivitySource(ActivitySourceName);
            var activity = source.StartActivity("Test Jaeger Activity");
            activity?.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);
        }
    }
}
