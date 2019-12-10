// <copyright file="TraceExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Google.Protobuf.WellKnownTypes;

    using Grpc.Core;

    using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    using Proto = Opentelemetry.Proto;

    /// <summary>
    /// The trace exporter to otelcol.
    /// </summary>
    // TODO support async exporting and results
    public class TraceExporter : SpanExporter, IDisposable
    {
        private const uint MaxSpanBatchSize = 32;
        private readonly Channel channel;
        private readonly Proto.Agent.Trace.V1.TraceService.TraceServiceClient traceClient;
        private readonly ConcurrentQueue<Span> spans = new ConcurrentQueue<Span>();
        private readonly Proto.Agent.Common.V1.Node node;
        private readonly uint spanBatchSize;
        private CancellationTokenSource cts;
        private Task runTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceExporter"/> class.
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint.</param>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="credentials">The credentials.</param>
        /// <param name="spanBatchSize">Size of the span batch.</param>
        public TraceExporter(string agentEndpoint, string hostName, string serviceName, ChannelCredentials credentials = null, uint spanBatchSize = MaxSpanBatchSize)
        {
            this.channel = new Channel(agentEndpoint, credentials ?? ChannelCredentials.Insecure);
            this.traceClient = new Proto.Agent.Trace.V1.TraceService.TraceServiceClient(this.channel);
            this.spanBatchSize = spanBatchSize;

            this.node = new Proto.Agent.Common.V1.Node
            {
                Identifier = new Proto.Agent.Common.V1.ProcessIdentifier
                {
                    HostName = hostName,
                    Pid = (uint)Process.GetCurrentProcess().Id,
                    StartTimestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                },
                LibraryInfo = new Proto.Agent.Common.V1.LibraryInfo
                {
                    Language = Proto.Agent.Common.V1.LibraryInfo.Types.Language.CSharp,
                    CoreLibraryVersion = GetAssemblyVersion(typeof(Span).Assembly),
                    ExporterVersion = GetAssemblyVersion(typeof(TraceExporter).Assembly),
                },
                ServiceInfo = new Proto.Agent.Common.V1.ServiceInfo
                {
                    Name = serviceName,
                },
            };

            this.Start();
        }

        /// <inheritdoc/>
        public override async Task<ExportResult> ExportAsync(IEnumerable<Span> spanDataList, CancellationToken cancellationToken)
        {
            await Task.Run(
                () =>
                {
                    if (this.cts != null && !this.cts.IsCancellationRequested)
                    {
                        foreach (var spanData in spanDataList)
                        {
                            // TODO back-pressure on the queue
                            this.spans.Enqueue(spanData);
                        }
                    }
                },
                cancellationToken);

            return ExportResult.Success;
        }

        /// <inheritdoc/>
        public override async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            // TODO support cancellation based on cancellationToken
            if (this.cts != null)
            {
                this.cts.Cancel(false);

                // ignore all exceptions
                await this.runTask.ContinueWith(t => { }).ConfigureAwait(false);

                this.cts.Dispose();
                this.cts = null;
                this.runTask = null;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.ShutdownAsync(CancellationToken.None).Wait();
        }

        private static string GetAssemblyVersion(Assembly assembly)
        {
            var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            return fileVersionAttr?.Version ?? "0.0.0";
        }

        private void Start()
        {
            // TODO Config
            // TODO handle connection errors & retries

            this.cts = new CancellationTokenSource();
            this.runTask = this.RunAsync(this.cts.Token);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var duplexCall = this.traceClient.Export();
            try
            {
                var firstRequest = true;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var spanExportRequest = new Proto.Agent.Trace.V1.ExportTraceServiceRequest();
                    if (firstRequest)
                    {
                        spanExportRequest.Node = this.node;
                    }

                    // Spans
                    var hasSpans = false;

                    while (spanExportRequest.Spans.Count < this.spanBatchSize)
                    {
                        if (!this.spans.TryDequeue(out var spanData))
                        {
                            break;
                        }

                        var protoSpan = spanData.ToProtoSpan();
                        if (protoSpan == null)
                        {
                            continue;
                        }

                        spanExportRequest.Spans.Add(protoSpan);
                        hasSpans = true;
                    }

                    if (hasSpans)
                    {
                        await duplexCall.RequestStream.WriteAsync(spanExportRequest).ConfigureAwait(false);
                        firstRequest = false;
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    }
                }

                await duplexCall.RequestStream.CompleteAsync().ConfigureAwait(false);
            }
            catch (RpcException ex)
            {
                ExporterEventSource.Log.FailedToReachCollector(ex);
                throw;
            }
        }
    }
}
