// <copyright file="OtlpMetricsExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Linq;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using OtlpCollector = Opentelemetry.Proto.Collector.Metrics.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpMetricsExporterTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ToOtlpResourceMetricsTest(bool includeServiceNameInResource)
        {
            var resourceBuilder = ResourceBuilder.CreateEmpty();
            if (includeServiceNameInResource)
            {
                resourceBuilder.AddAttributes(
                    new List<KeyValuePair<string, object>>
                    {
                        new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "service-name"),
                        new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceNamespace, "ns1"),
                    });
            }

            var metrics = new List<Metric>();

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{includeServiceNameInResource}", "0.0.1");
            using var provider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var counter = meter.CreateCounter<int>("counter");
            counter.Add(100);

            provider.ForceFlush();

            var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);

            var request = new OtlpCollector.ExportMetricsServiceRequest();
            request.AddMetrics(resourceBuilder.Build().ToOtlpResource(), batch);

            Assert.Single(request.ResourceMetrics);
            var resourceMetric = request.ResourceMetrics.First();
            var oltpResource = resourceMetric.Resource;

            if (includeServiceNameInResource)
            {
                Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
                Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
            }
            else
            {
                Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service:"));
            }

            Assert.Single(resourceMetric.InstrumentationLibraryMetrics);
            var instrumentationLibraryMetrics = resourceMetric.InstrumentationLibraryMetrics.First();
            Assert.Equal(string.Empty, instrumentationLibraryMetrics.SchemaUrl);
            Assert.Equal(meter.Name, instrumentationLibraryMetrics.InstrumentationLibrary.Name);
            Assert.Equal("0.0.1", instrumentationLibraryMetrics.InstrumentationLibrary.Version);
        }
    }
}
