// <copyright file="LogRecordSeverityExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods for the <see cref="LogRecordSeverity"/> enum.
/// </summary>
internal static class LogRecordSeverityExtensions
{
    private static readonly string[] LogRecordSeverityShortNames = new string[]
    {
        "UNKNOWN",

        "TRACE",
        "TRACE2",
        "TRACE3",
        "TRACE4",

        "DEBUG",
        "DEBUG2",
        "DEBUG3",
        "DEBUG4",

        "INFO",
        "INFO2",
        "INFO3",
        "INFO4",

        "WARN",
        "WARN2",
        "WARN3",
        "WARN4",

        "ERROR",
        "ERROR2",
        "ERROR3",
        "ERROR4",

        "FATAL",
        "FATAL2",
        "FATAL3",
        "FATAL4",
    };

    /// <summary>
    /// Returns the OpenTelemetry Specification short name for the <see
    /// cref="LogRecordSeverity"/> suitable for display.
    /// </summary>
    /// <remarks>
    /// See: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/data-model.md#displaying-severity"/>.
    /// </remarks>
    /// <param name="logRecordSeverity"><see cref="LogRecordSeverity"/>.</param>
    /// <returns>OpenTelemetry Specification short name for the supplied <see
    /// cref="LogRecordSeverity"/>.</returns>
    public static string ToShortName(this LogRecordSeverity logRecordSeverity)
    {
        int severityLevel = (int)logRecordSeverity;

        if (severityLevel < 0 || severityLevel > 24)
        {
            severityLevel = 0;
        }

        return LogRecordSeverityShortNames[severityLevel];
    }
}
