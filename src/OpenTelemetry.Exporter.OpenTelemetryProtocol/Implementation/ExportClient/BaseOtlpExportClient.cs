// <copyright file="BaseOtlpExportClient.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Base class for sending OTLP export requests.</summary>
    internal abstract class BaseOtlpExportClient
    {
        protected BaseOtlpExportClient(OtlpExporterOptions options)
        {
            Guard.Null(options, nameof(options));
            Guard.InvalidTimeout(options.TimeoutMilliseconds, nameof(options.TimeoutMilliseconds));

            this.Options = options;

#if NETCOREAPP3_1
            EnsureUnencryptedSupportIsEnabled(options);
#endif
        }

        internal OtlpExporterOptions Options { get; }

        private static void EnsureUnencryptedSupportIsEnabled(OtlpExporterOptions options)
        {
            if (options.Endpoint.Scheme.Equals("http", StringComparison.InvariantCultureIgnoreCase))
            {
                if (AppContext.TryGetSwitch(
                        "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var unencryptedIsSupported) == false
                    || unencryptedIsSupported == false)
                {
                    throw new InvalidOperationException(
                        "'System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport' must be enabled for using HTTP with .NET 3.1");
                }
            }
        }
    }
}
