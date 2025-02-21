// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal;

internal sealed class SelfDiagnosticsEnvarParser
{
    //public bool TryGetConfigurationFromEnVar(
    //    out string? outputTarget, // default writing to Console
    //    out EventLevel logLevel)
    //{
    //    outputTarget = null;

    //    logLevel = EventLevel.LogAlways;
    //    if (Environment.GetEnvironmentVariable("EnableSelfDiagnostics") != null)
    //    {
    //        outputTarget = Environment.GetEnvironmentVariable("outputTarget");
    //        var logLevelStringFromEnVar = Environment.GetEnvironmentVariable("selfDiagnosticsLogLevel");
    //        Enum.TryParse(logLevelStringFromEnVar, out logLevel);

    //        return true;
    //    }
    //}
}
