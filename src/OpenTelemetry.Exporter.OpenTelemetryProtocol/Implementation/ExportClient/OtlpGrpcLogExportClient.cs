// <copyright file="OtlpGrpcLogExportClient.cs" company="OpenTelemetry Authors">
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

using Grpc.Core;
using OpenTelemetry.Proto.Collector.Logs.V1;
using ProtoBuf.Grpc.Client;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Class for sending OTLP Logs export request over gRPC.</summary>
    internal sealed class OtlpGrpcLogExportClient : BaseOtlpGrpcExportClient<ExportLogsServiceRequest>
    {
        private readonly ILogsService logsClient;

        public OtlpGrpcLogExportClient(OtlpExporterOptions options, ILogsService logsServiceClient = null)
            : base(options)
        {
            if (logsServiceClient != null)
            {
                this.logsClient = logsServiceClient;
            }
            else
            {
                this.Channel = options.CreateChannel();
                this.logsClient = this.Channel.CreateGrpcService<ILogsService>();
            }
        }

        /// <inheritdoc/>
        public override bool SendExportRequest(ExportLogsServiceRequest request, CancellationToken cancellationToken = default)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);

            try
            {
                // TODO: Can protogen generate synchronous calls?
                // If not, the exception handling probably needs to be adjusted to handle AggregateException.
                this.logsClient.ExportAsync(request, new ProtoBuf.Grpc.CallContext(new CallOptions(headers: this.Headers, deadline: deadline, cancellationToken: cancellationToken)))
                     .ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (RpcException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

                return false;
            }

            return true;
        }
    }
}
