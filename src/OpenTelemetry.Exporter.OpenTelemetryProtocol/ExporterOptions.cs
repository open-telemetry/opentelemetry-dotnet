// <copyright file="ExporterOptions.cs" company="OpenTelemetry Authors">
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

using Grpc.Core;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Configuration options for the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public class ExporterOptions
    {
        /// <summary>
        /// Gets or sets the target to which the exporter is going to send traces or metrics.
        /// The valid syntax is described at https://github.com/grpc/grpc/blob/master/doc/naming.md.
        /// </summary>
        public string Endpoint { get; set; } = "localhost:55680";

        /// <summary>
        /// Gets or sets the client-side channel credentials. Used for creation of a secure channel.
        /// The default is "insecure". See detais at https://grpc.io/docs/guides/auth/#credential-types.
        /// </summary>
        public ChannelCredentials Credentials { get; set; } = ChannelCredentials.Insecure;
    }
}
