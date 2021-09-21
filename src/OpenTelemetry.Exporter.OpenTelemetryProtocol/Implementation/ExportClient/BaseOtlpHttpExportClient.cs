// <copyright file="BaseOtlpHttpExportClient.cs" company="OpenTelemetry Authors">
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
using System.Threading;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Base class for sending OTLP export request over HTTP.</summary>
    /// <typeparam name="TRequest">Type of export request.</typeparam>
    internal abstract class BaseOtlpHttpExportClient<TRequest> : IExportClient<TRequest>
    {
        protected BaseOtlpHttpExportClient(OtlpExporterOptions options, IHttpHandler httpHandler = null)
        {
            this.Options = options ?? throw new ArgumentNullException(nameof(options));

            if (this.Options.TimeoutMilliseconds <= 0)
            {
                throw new ArgumentException("Timeout value provided is not a positive number.", nameof(this.Options.TimeoutMilliseconds));
            }

            this.Headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));

            this.HttpHandler = httpHandler ?? new HttpHandler(TimeSpan.FromMilliseconds(this.Options.TimeoutMilliseconds));
        }

        internal OtlpExporterOptions Options { get; }

        internal IHttpHandler HttpHandler { get; }

        internal IReadOnlyDictionary<string, string> Headers { get; }

        /// <inheritdoc/>
        public abstract bool SendExportRequest(TRequest request, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public virtual bool CancelExportRequest(int timeoutMilliseconds)
        {
            try
            {
                this.HttpHandler.CancelPendingRequests();
                return true;
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
                return false;
            }
        }
    }
}
