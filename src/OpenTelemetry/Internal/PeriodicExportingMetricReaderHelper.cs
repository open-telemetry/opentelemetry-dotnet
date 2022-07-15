// <copyright file="PeriodicExportingMetricReaderHelper.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal static class PeriodicExportingMetricReaderHelper
{
    internal const string OTelMetricExportIntervalEnvVarKey = "OTEL_METRIC_EXPORT_INTERVAL";
    internal const int DefaultExportIntervalMilliseconds = 60000;
    internal const string OTelMetricExportTimeoutEnvVarKey = "OTEL_METRIC_EXPORT_TIMEOUT";
    internal const int DefaultExportTimeoutMilliseconds = 30000;

    internal static PeriodicExportingMetricReader CreatePeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        MetricReaderOptions options,
        int defaultExportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int defaultExportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
    {
        var exportInterval = GetValue(
            options.PeriodicExportingMetricReaderOptions?.ExportIntervalMilliseconds,
            OTelMetricExportIntervalEnvVarKey,
            defaultExportIntervalMilliseconds);

        var exportTimeout = GetValue(
            options.PeriodicExportingMetricReaderOptions?.ExportTimeoutMilliseconds,
            OTelMetricExportTimeoutEnvVarKey,
            defaultExportTimeoutMilliseconds);

        var metricReader = new PeriodicExportingMetricReader(exporter, exportInterval, exportTimeout)
        {
            TemporalityPreference = options.TemporalityPreference,
        };

        return metricReader;
    }

    private static int GetValue(int? optionsValue, string envVarKey, int defaultValue)
    {
        if (optionsValue.HasValue)
        {
            return optionsValue.Value;
        }

        if (EnvironmentVariableHelper.LoadNumeric(envVarKey, out var envVarValue))
        {
            return envVarValue;
        }

        return defaultValue;
    }
}
