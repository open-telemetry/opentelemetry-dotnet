// <copyright file="ICommonOtlpExporterOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter;

/// <summary>
/// Interface representing the OTLP exporter configuration options common for all signal types (i.e., logs, metrics, and traces).
/// See: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#configuration-options.
/// </summary>
public interface ICommonOtlpExporterOptions
{
    /// <summary>
    /// Gets or sets the target to which the exporter is going to send telemetry.
    /// Must be a valid Uri with scheme (http or https) and host, and
    /// may contain a port and path. The default value is http://localhost:4317.
    /// </summary>
    public Uri Endpoint { get; set; }

    /// <summary>
    /// Gets or sets optional headers for the connection. Refer to the <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables">
    /// specification</a> for information on the expected format for Headers.
    /// </summary>
    public string Headers { get; set; }

    /// <summary>
    /// Gets or sets the max waiting time (in milliseconds) for the backend to process each batch. The default value is 10000.
    /// </summary>
    public int TimeoutMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the the OTLP transport protocol. Supported values: Grpc and HttpProtobuf.
    /// </summary>
    public OtlpExportProtocol Protocol { get; set; }
}
