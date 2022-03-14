// <copyright file="ExporterClientValidation.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    internal static class ExporterClientValidation
    {
        internal static void EnsureUnencryptedSupportIsEnabled(OtlpExporterOptions options)
        {
            var version = Environment.Version;

            // This verification is only required for .NET Core 3.x
            if (version.Major != 3)
            {
                return;
            }

            if (options.Endpoint.Scheme.Equals("http", StringComparison.InvariantCultureIgnoreCase))
            {
                if (AppContext.TryGetSwitch(
                        "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var unencryptedIsSupported) == false
                    || unencryptedIsSupported == false)
                {
                    throw new InvalidOperationException(
                        "Calling insecure gRPC services on .NET Core 3.x requires enabling the 'System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport' switch. See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client");
                }
            }
        }
    }
}
