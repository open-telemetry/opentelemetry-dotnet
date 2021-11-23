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
using OtlpMetrics = Opentelemetry.Proto.Metrics.V1;

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

        [Theory]
        [InlineData("test_gauge", null, null, 123, null)]
        [InlineData("test_gauge", null, null, null, 123.45)]
        [InlineData("test_gauge", "description", "unit", 123, null)]
        public void TestGaugeToOtlpMetric(string name, string description, string unit, long? longValue, double? doubleValue, params object[] keysValues)
        {
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var attributes = ToAttributes(keysValues).ToArray();
            if (longValue.HasValue)
            {
                meter.CreateObservableGauge(name, () => longValue.Value, unit, description);
            }
            else
            {
                meter.CreateObservableGauge(name, () => doubleValue.Value, unit, description);
            }

            provider.ForceFlush();

            var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);

            var request = new OtlpCollector.ExportMetricsServiceRequest();
            request.AddMetrics(ResourceBuilder.CreateEmpty().Build().ToOtlpResource(), batch);

            var resourceMetric = request.ResourceMetrics.Single();
            var instrumentationLibraryMetrics = resourceMetric.InstrumentationLibraryMetrics.Single();
            var actual = instrumentationLibraryMetrics.Metrics.Single();

            Assert.Equal(name, actual.Name);
            Assert.Equal(description ?? string.Empty, actual.Description);
            Assert.Equal(unit ?? string.Empty, actual.Unit);

            Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Gauge, actual.DataCase);

            Assert.NotNull(actual.Gauge);
            Assert.Null(actual.Sum);
            Assert.Null(actual.Histogram);
            Assert.Null(actual.ExponentialHistogram);
            Assert.Null(actual.Summary);

            Assert.Single(actual.Gauge.DataPoints);
            var dataPoint = actual.Gauge.DataPoints.First();
            Assert.True(dataPoint.StartTimeUnixNano > 0);
            Assert.True(dataPoint.TimeUnixNano > 0);

            if (longValue.HasValue)
            {
                Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsInt, dataPoint.ValueCase);
                Assert.Equal(longValue, dataPoint.AsInt);
            }
            else
            {
                Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsDouble, dataPoint.ValueCase);
                Assert.Equal(doubleValue, dataPoint.AsDouble);
            }

            if (attributes.Length > 0)
            {
                OtlpTestHelpers.AssertOtlpAttributes(attributes, dataPoint.Attributes);
            }
            else
            {
                Assert.Empty(dataPoint.Attributes);
            }

            Assert.Empty(dataPoint.Exemplars);

#pragma warning disable CS0612 // Type or member is obsolete
            Assert.Null(actual.IntGauge);
            Assert.Null(actual.IntSum);
            Assert.Null(actual.IntHistogram);
            Assert.Empty(dataPoint.Labels);
#pragma warning restore CS0612 // Type or member is obsolete
        }

        private static IEnumerable<KeyValuePair<string, object>> ToAttributes(object[] keysValues)
        {
            var keys = keysValues?.Where((_, index) => index % 2 == 0).ToArray();
            var values = keysValues?.Where((_, index) => index % 2 != 0).ToArray();

            for (var i = 0; keys != null && i < keys.Length; ++i)
            {
                yield return new KeyValuePair<string, object>(keys[i].ToString(), values[i]);
            }
        }
    }
}
