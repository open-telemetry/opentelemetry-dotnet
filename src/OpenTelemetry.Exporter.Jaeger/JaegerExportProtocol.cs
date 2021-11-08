// <copyright file="JaegerExportProtocol.cs" company="OpenTelemetry Authors">
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
    /// Defines the exporter protocols supported by the <see cref="JaegerExporter"/>.
    /// </summary>
    public enum JaegerExportProtocol : byte
    {
        /// <summary>
        /// Compact thrift protocol over UDP.
        /// </summary>
        /// <remarks>
        /// Note: Supported by Jaeger Agents only.
        /// </remarks>
        UdpCompactThrift = 0,

        /// <summary>
        /// Binary thrift protocol over HTTP.
        /// </summary>
        /// <remarks>
        /// Note: Supported by Jaeger Collectors only.
        /// </remarks>
        HttpBinaryThrift = 1,
    }
}
