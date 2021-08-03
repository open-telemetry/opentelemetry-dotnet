// <copyright file="BaseOtlpExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
#if NETSTANDARD2_1
using Grpc.Net.Client;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Implements exporter that exports telemetry objects over OTLP/gRPC.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
    public abstract class BaseOtlpExporter<T> : BaseExporter<T>
        where T : class
    {
        private OtlpResource.Resource processResource;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseOtlpExporter{T}"/> class.
        /// </summary>
        /// <param name="options">The <see cref="OtlpExporterOptions"/> for configuring the exporter.</param>
        protected BaseOtlpExporter(OtlpExporterOptions options)
        {
            this.Options = options ?? throw new ArgumentNullException(nameof(options));
            this.Headers = options.GetMetadataFromHeaders();
            if (this.Options.TimeoutMilliseconds <= 0)
            {
                throw new ArgumentException("Timeout value provided is not a positive number.", nameof(this.Options.TimeoutMilliseconds));
            }
        }

        internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

#if NETSTANDARD2_1
        internal GrpcChannel Channel { get; set; }
#else
        internal Channel Channel { get; set; }
#endif

        internal OtlpExporterOptions Options { get; }

        internal Metadata Headers { get; }

        /// <inheritdoc/>
        protected override bool OnShutdown(int timeoutMilliseconds)
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
