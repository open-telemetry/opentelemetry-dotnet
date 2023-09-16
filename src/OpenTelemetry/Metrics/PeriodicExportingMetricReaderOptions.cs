// <copyright file="PeriodicExportingMetricReaderOptions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains periodic metric reader options.
/// </summary>
/// <remarks>
/// Note: OTEL_METRIC_EXPORT_INTERVAL and OTEL_METRIC_EXPORT_TIMEOUT environment
/// variables are parsed during object construction.
/// </remarks>
public class PeriodicExportingMetricReaderOptions
{
    internal const string OTelMetricExportIntervalEnvVarKey = "OTEL_METRIC_EXPORT_INTERVAL";
    internal const string OTelMetricExportTimeoutEnvVarKey = "OTEL_METRIC_EXPORT_TIMEOUT";

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReaderOptions"/> class.
    /// </summary>
    public PeriodicExportingMetricReaderOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal PeriodicExportingMetricReaderOptions(IConfiguration configuration)
    {
        if (configuration.TryGetIntValue(OTelMetricExportIntervalEnvVarKey, out var interval))
        {
            this.ExportIntervalMilliseconds = interval;
        }

        if (configuration.TryGetIntValue(OTelMetricExportTimeoutEnvVarKey, out var timeout))
        {
            this.ExportTimeoutMilliseconds = timeout;
        }
    }

    /// <summary>
    /// Gets or sets the metric export interval in milliseconds.
    /// If not set, the default value depends on the type of metric exporter
    /// associated with the metric reader.
    /// </summary>
    public int? ExportIntervalMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the metric export timeout in milliseconds.
    /// If not set, the default value depends on the type of metric exporter
    /// associated with the metric reader.
    /// </summary>
    public int? ExportTimeoutMilliseconds { get; set; }
}
