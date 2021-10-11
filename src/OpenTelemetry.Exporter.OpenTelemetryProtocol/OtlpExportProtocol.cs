// <copyright file="OtlpExportProtocol.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Supported by OTLP exporter protocol types according to the specification https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md.
    /// </summary>
    public enum OtlpExportProtocol : byte
    {
        /// <summary>
        /// OTLP over gRPC (corresponds to 'grpc' Protocol configuration option). Used as default.
        /// </summary>
        Grpc = 0,

        /// <summary>
        /// OTLP over HTTP with protobuf payloads (corresponds to 'http/protobuf' Protocol configuration option).
        /// </summary>
        HttpProtobuf = 1,
    }
}
