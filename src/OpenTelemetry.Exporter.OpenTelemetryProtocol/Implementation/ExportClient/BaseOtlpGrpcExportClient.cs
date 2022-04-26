// <copyright file="BaseOtlpGrpcExportClient.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Internal;
#if NETSTANDARD2_1 || NET6_0_OR_GREATER
using Grpc.Net.Client;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Base class for sending OTLP export request over gRPC.</summary>
    /// <typeparam name="TRequest">Type of export request.</typeparam>
    internal abstract class BaseOtlpGrpcExportClient<TRequest> : IExportClient<TRequest>
    {
        protected BaseOtlpGrpcExportClient(OtlpExporterOptions options)
        {
            Guard.ThrowIfNull(options);
            Guard.ThrowIfInvalidTimeout(options.TimeoutMilliseconds);

            ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options);

            this.Endpoint = new UriBuilder(options.Endpoint).Uri;
            this.Headers = options.GetMetadataFromHeaders();
            this.TimeoutMilliseconds = options.TimeoutMilliseconds;
        }

#if NETSTANDARD2_1 || NET6_0_OR_GREATER
        internal GrpcChannel Channel { get; set; }
#else
        internal Channel Channel { get; set; }
#endif

        internal Uri Endpoint { get; }

        internal Metadata Headers { get; }

        internal int TimeoutMilliseconds { get; }

        /// <inheritdoc/>
        public abstract bool SendExportRequest(TRequest request, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public virtual bool Shutdown(int timeoutMilliseconds)
        {
            if (this.Channel == null)
            {
                return true;
            }

            if (timeoutMilliseconds == -1)
            {
                this.Channel.ShutdownAsync().Wait();
                return true;
            }
            else
            {
                return Task.WaitAny(new Task[] { this.Channel.ShutdownAsync(), Task.Delay(timeoutMilliseconds) }) == 0;
            }
        }
    }
}
