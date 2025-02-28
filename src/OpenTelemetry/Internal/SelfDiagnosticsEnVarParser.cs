// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal;

internal sealed class SelfDiagnosticsEnVarParser
{
    internal EventLevel GetLogLevel()
    {
        EventLevel logLevel = EventLevel.Informational;
        var logLevelString = Environment.GetEnvironmentVariable("LogLevel");

        if (!string.IsNullOrEmpty(logLevelString))
        {
            if (Enum.TryParse(logLevelString, out logLevel))
            {
                return logLevel;
            }
        }

        return logLevel;
    }
}
