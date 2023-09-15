// <copyright file="MockCollectorIntegrationTests.cs" company="OpenTelemetry Authors">
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

#if !NETFRAMEWORK
using System.Diagnostics;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class MockCollectorIntegrationTests
{
    [Fact]
    public async Task TestRecoveryAfterFailedExport()
    {
        using var host = await new HostBuilder()
           .ConfigureWebHostDefaults(webBuilder => webBuilder
                .ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(5050, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
                    options.ListenLocalhost(4317, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
                })
               .ConfigureServices(services =>
               {
                   services.AddSingleton(new MockCollectorState());
                   services.AddGrpc();
               })
               .Configure(app =>
               {
                   app.UseRouting();

                   app.UseEndpoints(endpoints =>
                   {
                       endpoints.MapGet(
                           "/MockCollector/SetResponseCodes/{responseCodesCsv}",
                           (MockCollectorState collectorState, string responseCodesCsv) =>
                           {
                               var codes = responseCodesCsv.Split(",").Select(x => int.Parse(x)).ToArray();
                               collectorState.SetStatusCodes(codes);
                           });

                       endpoints.MapGrpcService<MockTraceService>();
                   });
               }))
           .StartAsync().ConfigureAwait(false);

        var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5050") };

        var codes = new[] { Grpc.Core.StatusCode.Unimplemented, Grpc.Core.StatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}").ConfigureAwait(false);

        var exportResults = new List<ExportResult>();
        var otlpExporter = new OtlpTraceExporter(new OtlpExporterOptions() { Endpoint = new Uri("http://localhost:4317") });
        var delegatingExporter = new DelegatingExporter<Activity>
        {
            OnExportFunc = (batch) =>
            {
                var result = otlpExporter.Export(batch);
                exportResults.Add(result);
                return result;
            },
        };

        var activitySourceName = "otel.mock.collector.test";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .AddProcessor(new SimpleActivityExportProcessor(delegatingExporter))
            .Build();

        using var source = new ActivitySource(activitySourceName);

        source.StartActivity().Stop();

        Assert.Single(exportResults);
        Assert.Equal(ExportResult.Failure, exportResults[0]);

        source.StartActivity().Stop();

        Assert.Equal(2, exportResults.Count);
        Assert.Equal(ExportResult.Success, exportResults[1]);

        await host.StopAsync().ConfigureAwait(false);
    }

    private class MockCollectorState
    {
        private Grpc.Core.StatusCode[] statusCodes = { };
        private int statusCodeIndex = 0;

        public void SetStatusCodes(int[] statusCodes)
        {
            this.statusCodeIndex = 0;
            this.statusCodes = statusCodes.Select(x => (Grpc.Core.StatusCode)x).ToArray();
        }

        public Grpc.Core.StatusCode NextStatus()
        {
            return this.statusCodeIndex < this.statusCodes.Length
                ? this.statusCodes[this.statusCodeIndex++]
                : Grpc.Core.StatusCode.OK;
        }
    }

    private class MockTraceService : TraceService.TraceServiceBase
    {
        private readonly MockCollectorState state;

        public MockTraceService(MockCollectorState state)
        {
            this.state = state;
        }

        public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
        {
            var statusCode = this.state.NextStatus();
            if (statusCode != Grpc.Core.StatusCode.OK)
            {
                throw new RpcException(new Grpc.Core.Status(statusCode, "Error."));
            }

            return Task.FromResult(new ExportTraceServiceResponse());
        }
    }
}
#endif
