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
using Microsoft.Extensions.DependencyInjection;
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
    public class OtlpMetricsExporterTests : Http2UnencryptedSupportTests
    {
        [Fact]
        public void TestAddOtlpExporter_SetsCorrectMetricReaderDefaults()
        {
            if (Environment.Version.Major == 3)
            {
                // Adding the OtlpExporter creates a GrpcChannel.
                // This switch must be set before creating a GrpcChannel when calling an insecure HTTP/2 endpoint.
                // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddOtlpExporter()
                .Build();

            var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            var metricReader = typeof(MetricReader)
                .Assembly
                .GetType("OpenTelemetry.Metrics.MeterProviderSdk")
                .GetField("reader", bindingFlags)
                .GetValue(meterProvider) as PeriodicExportingMetricReader;

            Assert.NotNull(metricReader);

            var exportIntervalMilliseconds = (int)typeof(PeriodicExportingMetricReader)
                .GetField("exportIntervalMilliseconds", bindingFlags)
                .GetValue(metricReader);

            Assert.Equal(60000, exportIntervalMilliseconds);
        }

        [Fact]
        public void UserHttpFactoryCalled()
        {
            OtlpExporterOptions options = new OtlpExporterOptions();

            var defaultFactory = options.HttpClientFactory;

            int invocations = 0;
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.HttpClientFactory = () =>
            {
                invocations++;
                return defaultFactory();
            };

            using (var exporter = new OtlpMetricExporter(options))
            {
                Assert.Equal(1, invocations);
            }

            using (var provider = Sdk.CreateMeterProviderBuilder()
                .AddOtlpExporter(o =>
                {
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.HttpClientFactory = options.HttpClientFactory;
                })
                .Build())
            {
                Assert.Equal(2, invocations);
            }

            options.HttpClientFactory = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var exporter = new OtlpMetricExporter(options);
            });

            options.HttpClientFactory = () => null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var exporter = new OtlpMetricExporter(options);
            });
        }

        [Fact]
        public void ServiceProviderHttpClientFactoryInvoked()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddHttpClient();

            int invocations = 0;

            services.AddHttpClient("OtlpMetricExporter", configureClient: (client) => invocations++);

            services.AddOpenTelemetryMetrics(builder => builder.AddOtlpExporter(
                o => o.Protocol = OtlpExportProtocol.HttpProtobuf));

            using var serviceProvider = services.BuildServiceProvider();

            var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

            Assert.Equal(1, invocations);
        }

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
        public void TestGaugeToOtlpMetric(string name, string description, string unit, long? longValue, double? doubleValue)
        {
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

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

            Assert.Empty(dataPoint.Attributes);

            Assert.Empty(dataPoint.Exemplars);

#pragma warning disable CS0612 // Type or member is obsolete
            Assert.Null(actual.IntGauge);
            Assert.Null(actual.IntSum);
            Assert.Null(actual.IntHistogram);
            Assert.Empty(dataPoint.Labels);
#pragma warning restore CS0612 // Type or member is obsolete
        }

        [Theory]
        [InlineData("test_counter", null, null, 123, null, AggregationTemporality.Cumulative, true)]
        [InlineData("test_counter", null, null, null, 123.45, AggregationTemporality.Cumulative, true)]
        [InlineData("test_counter", null, null, 123, null, AggregationTemporality.Delta, true)]
        [InlineData("test_counter", "description", "unit", 123, null, AggregationTemporality.Cumulative, true)]
        [InlineData("test_counter", null, null, 123, null, AggregationTemporality.Delta, true, "key1", "value1", "key2", 123)]
        public void TestCounterToOltpMetric(string name, string description, string unit, long? longValue, double? doubleValue, AggregationTemporality aggregationTemporality, bool isMonotonic, params object[] keysValues)
        {
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics, metricReaderOptions =>
                {
                    metricReaderOptions.Temporality = aggregationTemporality;
                })
                .Build();

            var attributes = ToAttributes(keysValues).ToArray();
            if (longValue.HasValue)
            {
                var counter = meter.CreateCounter<long>(name, unit, description);
                counter.Add(longValue.Value, attributes);
            }
            else
            {
                var counter = meter.CreateCounter<double>(name, unit, description);
                counter.Add(doubleValue.Value, attributes);
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

            Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Sum, actual.DataCase);

            Assert.Null(actual.Gauge);
            Assert.NotNull(actual.Sum);
            Assert.Null(actual.Histogram);
            Assert.Null(actual.ExponentialHistogram);
            Assert.Null(actual.Summary);

            Assert.Equal(isMonotonic, actual.Sum.IsMonotonic);

            var otlpAggregationTemporality = aggregationTemporality == AggregationTemporality.Cumulative
                ? OtlpMetrics.AggregationTemporality.Cumulative
                : OtlpMetrics.AggregationTemporality.Delta;
            Assert.Equal(otlpAggregationTemporality, actual.Sum.AggregationTemporality);

            Assert.Single(actual.Sum.DataPoints);
            var dataPoint = actual.Sum.DataPoints.First();
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

        [Theory]
        [InlineData("test_histogram", null, null, 123, null, AggregationTemporality.Cumulative)]
        [InlineData("test_histogram", null, null, null, 123.45, AggregationTemporality.Cumulative)]
        [InlineData("test_histogram", null, null, 123, null, AggregationTemporality.Delta)]
        [InlineData("test_histogram", "description", "unit", 123, null, AggregationTemporality.Cumulative)]
        [InlineData("test_histogram", null, null, 123, null, AggregationTemporality.Delta, "key1", "value1", "key2", 123)]
        public void TestHistogramToOltpMetric(string name, string description, string unit, long? longValue, double? doubleValue, AggregationTemporality aggregationTemporality, params object[] keysValues)
        {
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics, metricReaderOptions =>
                {
                    metricReaderOptions.Temporality = aggregationTemporality;
                })
                .Build();

            var attributes = ToAttributes(keysValues).ToArray();
            if (longValue.HasValue)
            {
                var histogram = meter.CreateHistogram<long>(name, unit, description);
                histogram.Record(longValue.Value, attributes);
            }
            else
            {
                var histogram = meter.CreateHistogram<double>(name, unit, description);
                histogram.Record(doubleValue.Value, attributes);
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

            Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Histogram, actual.DataCase);

            Assert.Null(actual.Gauge);
            Assert.Null(actual.Sum);
            Assert.NotNull(actual.Histogram);
            Assert.Null(actual.ExponentialHistogram);
            Assert.Null(actual.Summary);

            var otlpAggregationTemporality = aggregationTemporality == AggregationTemporality.Cumulative
                ? OtlpMetrics.AggregationTemporality.Cumulative
                : OtlpMetrics.AggregationTemporality.Delta;
            Assert.Equal(otlpAggregationTemporality, actual.Histogram.AggregationTemporality);

            Assert.Single(actual.Histogram.DataPoints);
            var dataPoint = actual.Histogram.DataPoints.First();
            Assert.True(dataPoint.StartTimeUnixNano > 0);
            Assert.True(dataPoint.TimeUnixNano > 0);

            Assert.Equal(1UL, dataPoint.Count);

            if (longValue.HasValue)
            {
                Assert.Equal((double)longValue, dataPoint.Sum);
            }
            else
            {
                Assert.Equal(doubleValue, dataPoint.Sum);
            }

            int bucketIndex;
            for (bucketIndex = 0; bucketIndex < dataPoint.ExplicitBounds.Count; ++bucketIndex)
            {
                if (dataPoint.Sum <= dataPoint.ExplicitBounds[bucketIndex])
                {
                    break;
                }

                Assert.Equal(0UL, dataPoint.BucketCounts[bucketIndex]);
            }

            Assert.Equal(1UL, dataPoint.BucketCounts[bucketIndex]);

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
