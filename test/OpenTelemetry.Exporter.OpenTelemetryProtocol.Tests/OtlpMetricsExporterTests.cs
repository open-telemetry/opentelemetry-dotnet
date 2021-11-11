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

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using GrpcCore = Grpc.Core;
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

            var tags = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("key1", "value1"),
                new KeyValuePair<string, object>("key2", "value2"),
            };

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{includeServiceNameInResource}", "0.0.1");

            var metricReader = new BaseExportingMetricReader(new TestExporter<Metric>(RunTest))
            {
                PreferredAggregationTemporality = AggregationTemporality.Delta,
            };

            using var provider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(meter.Name)
                .AddReader(metricReader)
                .Build();

            var counter = meter.CreateCounter<int>("counter");

            counter.Add(100, tags);

            var testCompleted = false;

            // Invokes the TestExporter which will invoke RunTest
            metricReader.Collect();

            Assert.True(testCompleted);

            void RunTest(Batch<Metric> metrics)
            {
                var request = new OtlpCollector.ExportMetricsServiceRequest();
                request.AddMetrics(resourceBuilder.Build().ToOtlpResource(), metrics);

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

                Assert.Single(instrumentationLibraryMetrics.Metrics);

                foreach (var metric in instrumentationLibraryMetrics.Metrics)
                {
                    Assert.Equal(string.Empty, metric.Description);
                    Assert.Equal(string.Empty, metric.Unit);
                    Assert.Equal("counter", metric.Name);

                    Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Sum, metric.DataCase);
                    Assert.True(metric.Sum.IsMonotonic);
                    Assert.Equal(OtlpMetrics.AggregationTemporality.Delta, metric.Sum.AggregationTemporality);

                    Assert.Single(metric.Sum.DataPoints);
                    var dataPoint = metric.Sum.DataPoints.First();
                    Assert.True(dataPoint.StartTimeUnixNano > 0);
                    Assert.True(dataPoint.TimeUnixNano > 0);
                    Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsInt, dataPoint.ValueCase);
                    Assert.Equal(100, dataPoint.AsInt);

#pragma warning disable CS0612 // Type or member is obsolete
                    Assert.Empty(dataPoint.Labels);
#pragma warning restore CS0612 // Type or member is obsolete
                    OtlpTestHelpers.AssertOtlpAttributes(tags.ToList(), dataPoint.Attributes);

                    Assert.Empty(dataPoint.Exemplars);
                }

                testCompleted = true;
            }
        }

        private class NoopMetricsServiceClient : OtlpCollector.MetricsService.IMetricsServiceClient
        {
            public OtlpCollector.ExportMetricsServiceResponse Export(OtlpCollector.ExportMetricsServiceRequest request, GrpcCore.Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
            {
                return null;
            }
        }
    }
}
