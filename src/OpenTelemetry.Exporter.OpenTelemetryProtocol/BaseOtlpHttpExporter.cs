// <copyright file="BaseOtlpHttpExporter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Implements exporter that exports telemetry objects over OTLP/HTTP.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
    public abstract class BaseOtlpHttpExporter<T> : BaseExporter<T>
        where T : class
    {
        private OtlpResource.Resource processResource;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseOtlpHttpExporter{T}"/> class.
        /// </summary>
        /// <param name="options">The <see cref="OtlpExporterOptions"/> for configuring the exporter.</param>
        /// <param name="httpHandler">The <see cref="IHttpHandler"/> used http requests.</param>
        protected BaseOtlpHttpExporter(OtlpExporterOptions options, IHttpHandler httpHandler = null)
        {
            this.Options = options ?? throw new ArgumentNullException(nameof(options));
            this.Headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));
            if (this.Options.TimeoutMilliseconds <= 0)
            {
                throw new ArgumentException("Timeout value provided is not a positive number.", nameof(this.Options.TimeoutMilliseconds));
            }

            this.HttpHandler = httpHandler ?? new HttpHandler(TimeSpan.FromMilliseconds(this.Options.TimeoutMilliseconds));
        }

        internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

        internal OtlpExporterOptions Options { get; }

        internal IReadOnlyDictionary<string, string> Headers { get; }

        internal IHttpHandler HttpHandler { get; }

        /// <inheritdoc/>
        protected override bool OnShutdown(int timeoutMilliseconds)
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
