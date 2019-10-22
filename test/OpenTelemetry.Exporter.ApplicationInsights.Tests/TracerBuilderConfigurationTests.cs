// <copyright file="TracerBuilderConfigurationTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Collections.Concurrent;
using Microsoft.ApplicationInsights.Channel;

using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Exporter.ApplicationInsights.Tests
{
    public class TracerBuilderConfigurationTest
    {
        [Fact]
        public void UseApplicationInsights_ConfiguresExporter()
        {
            var sentItems = new ConcurrentQueue<ITelemetry>();
            
            ITelemetryChannel channel = new StubTelemetryChannel
            {
                OnSend = t => sentItems.Enqueue(t),
                EndpointAddress = "http://foo",
            };

            var tracer = TracerFactory.Create(b => b
                    .UseApplicationInsights(
                        o => o.TelemetryChannel = channel,
                        p => p.SetExportingProcessor(e => new SimpleSpanProcessor(e))))
                .GetTracer(null);

            tracer.StartSpan("foo").End();

            Assert.Single(sentItems);
        }

        [Fact]
        public void UseApplicationInsights_BadArgs()
        {
            TracerBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.UseApplicationInsights(_ => { }));
            Assert.Throws<ArgumentNullException>(() => TracerFactory.Create(b => b.UseApplicationInsights(null)));
        }
    }
}
