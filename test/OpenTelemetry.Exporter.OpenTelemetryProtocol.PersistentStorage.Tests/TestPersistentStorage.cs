// <copyright file="TestPersistentStorage.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Extensions.PersistentStorage.Abstractions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.PersistentStorage.Tests;

public sealed class TestPersistentStorage : IAsyncLifetime
{
    private readonly HttpClient httpClient;
    private IHost host;

    public TestPersistentStorage()
    {
        this.httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5050") };
    }

    public async Task InitializeAsync()
    {
        this.host = await new HostBuilder()
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
    }

    public async Task DisposeAsync()
    {
        if (this.host != null)
        {
            await this.host.StopAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task TestOtlpTraceExportWithPersistentStorage()
    {
        await this.SetCollectorStatusCodes(new[]
        {
            Grpc.Core.StatusCode.Cancelled,
        });

        var activitySourceName = "otel.mock.collector.test.persistent-storage";
        MockFileProvider mockFileProvider = new();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .AddOtlpExporterWithPersistentStorage(
            otlpExporterOptions =>
            {
                otlpExporterOptions.Endpoint = new Uri("http://localhost:4317");
                otlpExporterOptions.ExportProcessorType = ExportProcessorType.Simple;
            },
            _ => mockFileProvider)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        source.StartActivity().Stop();
        tracerProvider.ForceFlush();

        var blobs = mockFileProvider.TryGetBlobs();
        Assert.Single(blobs);
    }

    private async Task SetCollectorStatusCodes(Grpc.Core.StatusCode[] codes)
    {
        await this.httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");
    }

    private class MockCollectorState
    {
        private Grpc.Core.StatusCode[] statusCodes = Array.Empty<Grpc.Core.StatusCode>();
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

    private class MockFileProvider : PersistentBlobProvider
    {
        private readonly List<PersistentBlob> mockStorage = new();

        public IEnumerable<PersistentBlob> TryGetBlobs() => this.mockStorage.AsEnumerable();

        protected override IEnumerable<PersistentBlob> OnGetBlobs()
        {
            return this.mockStorage.AsEnumerable();
        }

        protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, out PersistentBlob blob)
        {
            blob = new MockFileBlob(this.mockStorage);
            return blob.TryWrite(buffer);
        }

        protected override bool OnTryCreateBlob(byte[] buffer, out PersistentBlob blob)
        {
            blob = new MockFileBlob(this.mockStorage);
            return blob.TryWrite(buffer);
        }

        protected override bool OnTryGetBlob(out PersistentBlob blob)
        {
            blob = this.GetBlobs().First();
            return true;
        }
    }

    private class MockFileBlob : PersistentBlob
    {
        private readonly List<PersistentBlob> mockStorage;
        private byte[] buffer = Array.Empty<byte>();

        public MockFileBlob(List<PersistentBlob> mockStorage)
        {
            this.mockStorage = mockStorage;
        }

        protected override bool OnTryRead(out byte[] buffer)
        {
            buffer = this.buffer;
            return true;
        }

        protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0)
        {
            this.buffer = buffer;
            this.mockStorage.Add(this);
            return true;
        }

        protected override bool OnTryLease(int leasePeriodMilliseconds) => true;

        protected override bool OnTryDelete()
        {
            try
            {
                this.mockStorage.Remove(this);
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
#endif
