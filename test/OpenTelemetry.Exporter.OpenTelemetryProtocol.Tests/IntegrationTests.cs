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
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class IntegrationTests
    {
        private const string CollectorHostnameEnvVarName = "OTEL_COLLECTOR_HOSTNAME";
        private static readonly string CollectorHostname = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(CollectorHostnameEnvVarName);

        [InlineData(OtlpExportProtocol.Grpc, ":4317")]
        [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/traces")]
        [Trait("CategoryName", "CollectorIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
        public void ExportResultIsSuccess(OtlpExportProtocol protocol, string endpoint)
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

            var otlpExporter = new OtlpTraceExporter(exporterOptions);
            var delegatingExporter = new DelegatingTestExporter<Activity>(otlpExporter);
            var exportActivityProcessor = new SimpleActivityExportProcessor(delegatingExporter);

            var activitySourceName = "otlp.collector.test";

            var builder = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .AddProcessor(exportActivityProcessor);

            using var tracerProvider = builder.Build();

            var source = new ActivitySource(activitySourceName);
            var activity = source.StartActivity($"{protocol} Test Activity");
            activity?.Stop();

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
    }
}
