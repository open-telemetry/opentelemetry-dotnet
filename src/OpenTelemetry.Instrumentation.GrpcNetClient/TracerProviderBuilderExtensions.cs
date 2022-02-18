// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of gRPClient
    /// instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Enables gRPClient Instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <param name="configure">GrpcClient configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddGrpcClientInstrumentation(
            this TracerProviderBuilder builder,
            Action<GrpcClientInstrumentationOptions> configure = null)
        {
            Guard.ThrowIfNull(builder);

            var grpcOptions = new GrpcClientInstrumentationOptions();
            configure?.Invoke(grpcOptions);

            builder.AddInstrumentation(() => new GrpcClientInstrumentation(grpcOptions));
            builder.AddSource(GrpcClientDiagnosticListener.ActivitySourceName);
            builder.AddLegacySource("Grpc.Net.Client.GrpcOut");

            return builder;
        }
    }
}
