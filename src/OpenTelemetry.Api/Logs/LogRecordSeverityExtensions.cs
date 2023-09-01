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

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Contains extension methods for the <see cref="LogRecordSeverity"/> enum.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
public
#else
/// <summary>
/// Contains extension methods for the <see cref="LogRecordSeverity"/> enum.
/// </summary>
internal
#endif
    static class LogRecordSeverityExtensions
{
    internal const string UnspecifiedShortName = "UNSPECIFIED";

    internal const string TraceShortName = "TRACE";
    internal const string Trace2ShortName = TraceShortName + "2";
    internal const string Trace3ShortName = TraceShortName + "3";
    internal const string Trace4ShortName = TraceShortName + "4";

    internal const string DebugShortName = "DEBUG";
    internal const string Debug2ShortName = DebugShortName + "2";
    internal const string Debug3ShortName = DebugShortName + "3";
    internal const string Debug4ShortName = DebugShortName + "4";

    internal const string InfoShortName = "INFO";
    internal const string Info2ShortName = InfoShortName + "2";
    internal const string Info3ShortName = InfoShortName + "3";
    internal const string Info4ShortName = InfoShortName + "4";

    internal const string WarnShortName = "WARN";
    internal const string Warn2ShortName = WarnShortName + "2";
    internal const string Warn3ShortName = WarnShortName + "3";
    internal const string Warn4ShortName = WarnShortName + "4";

    internal const string ErrorShortName = "ERROR";
    internal const string Error2ShortName = ErrorShortName + "2";
    internal const string Error3ShortName = ErrorShortName + "3";
    internal const string Error4ShortName = ErrorShortName + "4";

    internal const string FatalShortName = "FATAL";
    internal const string Fatal2ShortName = FatalShortName + "2";
    internal const string Fatal3ShortName = FatalShortName + "3";
    internal const string Fatal4ShortName = FatalShortName + "4";

    private static readonly string[] LogRecordSeverityShortNames = new string[]
    {
        UnspecifiedShortName,

        TraceShortName,
        Trace2ShortName,
        Trace3ShortName,
        Trace4ShortName,

        DebugShortName,
        Debug2ShortName,
        Debug3ShortName,
        Debug4ShortName,

        InfoShortName,
        Info2ShortName,
        Info3ShortName,
        Info4ShortName,

        WarnShortName,
        Warn2ShortName,
        Warn3ShortName,
        Warn4ShortName,

        ErrorShortName,
        Error2ShortName,
        Error3ShortName,
        Error4ShortName,

        FatalShortName,
        Fatal2ShortName,
        Fatal3ShortName,
        Fatal4ShortName,
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
