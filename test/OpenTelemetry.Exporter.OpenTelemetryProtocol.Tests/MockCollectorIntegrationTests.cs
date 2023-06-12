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
using StatusCode = Grpc.Core.StatusCode;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class MockCollectorIntegrationTests : IDisposable
{
    private readonly IHost collectorHost;
    private readonly HttpClient httpClient;

    public MockCollectorIntegrationTests()
    {
        this.collectorHost = new HostBuilder()
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

                       endpoints.MapGet(
                           "/MockCollector/GetNumberOfRequests",
                           (MockCollectorState collectorState) =>
                           {
                               return collectorState.GetRequestCount();
                           });

                       endpoints.MapGrpcService<MockTraceService>();
                   });
               }))
           .Start();

        this.httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5050") };
    }

    public static IEnumerable<object[]> RetryTestCases =>
        new List<object[]>
        {
            new object[] { ExportResult.Success, 2, new[] { StatusCode.Cancelled, StatusCode.OK } },
            new object[] { ExportResult.Success, 2, new[] { StatusCode.DeadlineExceeded, StatusCode.OK } },
            new object[] { ExportResult.Success, 2, new[] { StatusCode.ResourceExhausted, StatusCode.OK } },
            new object[] { ExportResult.Success, 2, new[] { StatusCode.Aborted, StatusCode.OK } },
            new object[] { ExportResult.Success, 2, new[] { StatusCode.OutOfRange, StatusCode.OK } },
            new object[] { ExportResult.Success, 2, new[] { StatusCode.Unavailable, StatusCode.OK } },
            new object[] { ExportResult.Success, 2, new[] { StatusCode.DataLoss, StatusCode.OK } },
            new object[] { ExportResult.Success, 3, new[] { StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.OK } },
            new object[] { ExportResult.Failure, 2, new[] { StatusCode.Unavailable, StatusCode.Unimplemented, StatusCode.OK } },
            new object[] { ExportResult.Success, 5, new[] { StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.OK } },
            new object[] { ExportResult.Failure, 5, new[] { StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.Unavailable, StatusCode.OK } },
        };

    public void Dispose()
    {
        this.collectorHost.Dispose();
        this.httpClient.Dispose();
    }

    [Theory]
    [MemberData(nameof(RetryTestCases))]
    public async Task TestRetry(ExportResult expectedExportResult, int expectedRequestCount, StatusCode[] statusCodes)
    {
        await this.httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", statusCodes.Select(x => (int)x))}").ConfigureAwait(false);

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

        var activitySourceName = "otlp.collector.test";

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .AddProcessor(new SimpleActivityExportProcessor(delegatingExporter));

        using var tracerProvider = builder.Build();

        var source = new ActivitySource(activitySourceName);
        var activity = source.StartActivity();
        activity?.Stop();

        var requestsReceived = await this.httpClient.GetStringAsync("/MockCollector/GetNumberOfRequests");

        Assert.Single(exportResults);
        Assert.Equal(expectedExportResult, exportResults[0]);
        Assert.Equal(expectedRequestCount, Convert.ToInt32(requestsReceived));
    }

    [Fact]
    public async Task TestRecoveryAfterFailedExport()
    {
        var codes = new[] { StatusCode.Unimplemented, StatusCode.OK };
        await this.httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}").ConfigureAwait(false);

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
    }

    private class MockCollectorState
    {
        private StatusCode[] statusCodes = Array.Empty<StatusCode>();
        private int statusCodeIndex = 0;

        public void SetStatusCodes(int[] statusCodes)
        {
            this.statusCodeIndex = 0;
            this.statusCodes = statusCodes.Select(x => (StatusCode)x).ToArray();
        }

        public StatusCode NextStatus()
        {
            return this.statusCodeIndex < this.statusCodes.Length
                ? this.statusCodes[this.statusCodeIndex++]
                : StatusCode.OK;
        }

        public int GetRequestCount()
        {
            return this.statusCodeIndex;
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
            if (statusCode != StatusCode.OK)
            {
                throw new RpcException(new Grpc.Core.Status(statusCode, "Error."));
            }

            return Task.FromResult(new ExportTraceServiceResponse());
        }
    }
}
#endif
