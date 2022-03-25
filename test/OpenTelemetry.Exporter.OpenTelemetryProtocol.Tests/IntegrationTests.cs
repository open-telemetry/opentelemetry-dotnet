// <copyright file="IntegrationTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Linq;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public sealed class IntegrationTests : IDisposable
    {
        private const string CollectorHostnameEnvVarName = "OTEL_COLLECTOR_HOSTNAME";
        private static readonly string CollectorHostname = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(CollectorHostnameEnvVarName);
        private readonly OpenTelemetryEventListener openTelemetryEventListener;

        public IntegrationTests(ITestOutputHelper outputHelper)
        {
            this.openTelemetryEventListener = new(outputHelper);
        }

        public void Dispose()
        {
            this.openTelemetryEventListener.Dispose();
        }

        [InlineData(OtlpExportProtocol.Grpc, ":4317")]
        [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/traces")]
        [Trait("CategoryName", "CollectorIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
        public void TraceExportResultIsSuccess(OtlpExportProtocol protocol, string endpoint)
        {
#if NETCOREAPP3_1
            // Adding the OtlpExporter creates a GrpcChannel.
            // This switch must be set before creating a GrpcChannel when calling an insecure HTTP/2 endpoint.
            // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
            if (protocol == OtlpExportProtocol.Grpc)
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }
#endif

            var exporterOptions = new OtlpExporterOptions
            {
                Endpoint = new Uri($"http://{CollectorHostname}{endpoint}"),
                Protocol = protocol,
            };

            DelegatingTestExporter<Activity> delegatingExporter = null;

            var activitySourceName = "otlp.collector.test";

            var builder = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName);

            OtlpTraceExporterHelperExtensions.AddOtlpExporter(
                builder,
                exporterOptions,
                configure: null,
                serviceProvider: null,
                configureExporterInstance: otlpExporter =>
                {
                    delegatingExporter = new DelegatingTestExporter<Activity>(otlpExporter);
                    return delegatingExporter;
                });

            using var tracerProvider = builder.Build();

            using var source = new ActivitySource(activitySourceName);
            var activity = source.StartActivity($"{protocol} Test Activity");
            activity?.Stop();

            Assert.NotNull(delegatingExporter);
            Assert.True(tracerProvider.ForceFlush());
            Assert.Single(delegatingExporter.ExportResults);
            Assert.Equal(ExportResult.Success, delegatingExporter.ExportResults[0]);
        }

        [InlineData(OtlpExportProtocol.Grpc, ":4317")]
        [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/metrics")]
        [Trait("CategoryName", "CollectorIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
        public void MetricExportResultIsSuccess(OtlpExportProtocol protocol, string endpoint)
        {
#if NETCOREAPP3_1
            // Adding the OtlpExporter creates a GrpcChannel.
            // This switch must be set before creating a GrpcChannel when calling an insecure HTTP/2 endpoint.
            // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
            if (protocol == OtlpExportProtocol.Grpc)
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }
#endif

            var exporterOptions = new OtlpExporterOptions
            {
                Endpoint = new Uri($"http://{CollectorHostname}{endpoint}"),
                Protocol = protocol,
            };

            DelegatingTestExporter<Metric> delegatingExporter = null;

            var meterName = "otlp.collector.test";

            var builder = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meterName);

            OtlpMetricExporterExtensions.AddOtlpExporter(
                builder,
                exporterOptions,
                new MetricReaderOptions(),
                configureExporter: null,
                configureExporterAndMetricReader: null,
                serviceProvider: null,
                configureExporterInstance: otlpExporter =>
                {
                    delegatingExporter = new DelegatingTestExporter<Metric>(otlpExporter);
                    return delegatingExporter;
                });

            using var meterProvider = builder.Build();

            using var meter = new Meter(meterName);

            var counter = meter.CreateCounter<int>("test_counter");

            counter.Add(18);

            Assert.NotNull(delegatingExporter);
            Assert.True(meterProvider.ForceFlush());
            Assert.Single(delegatingExporter.ExportResults);
            Assert.Equal(ExportResult.Success, delegatingExporter.ExportResults[0]);
        }

        [Trait("CategoryName", "CollectorIntegrationTests")]
        [SkipUnlessEnvVarFoundFact(CollectorHostnameEnvVarName)]
        public void ConstructingGrpcExporterFailsWhenHttp2UnencryptedSupportIsDisabledForNetcoreapp31()
        {
            // Adding the OtlpExporter creates a GrpcChannel.
            // This switch must be set before creating a GrpcChannel/HttpClient when calling an insecure gRPC service.
            // We want to fail fast so we are disabling it
            // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", false);

            var exporterOptions = new OtlpExporterOptions
            {
                Endpoint = new Uri($"http://{CollectorHostname}:4317"),
            };

            var exception = Record.Exception(() => new OtlpTraceExporter(exporterOptions));

            if (Environment.Version.Major == 3)
            {
                Assert.NotNull(exception);
            }
            else
            {
                Assert.Null(exception);
            }
        }

        private sealed class OpenTelemetryEventListener : EventListener
        {
            private readonly ITestOutputHelper outputHelper;

            public OpenTelemetryEventListener(ITestOutputHelper outputHelper)
            {
                this.outputHelper = outputHelper;
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                base.OnEventSourceCreated(eventSource);

                if (eventSource.Name.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                string message;
                if (eventData.Message != null && (eventData.Payload?.Count ?? 0) > 0)
                {
                    message = string.Format(eventData.Message, eventData.Payload.ToArray());
                }
                else
                {
                    message = eventData.Message;
                }

                this.outputHelper.WriteLine(message);
            }
        }
    }
}
