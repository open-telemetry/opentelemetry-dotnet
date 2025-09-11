// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Describes the severity level of a log record.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
[Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
/// <summary>
/// Describes the severity level of a log record.
/// </summary>
internal
#endif
    enum LogRecordSeverity
{
    /// <summary>Unspecified severity (0).</summary>
    Unspecified = 0,

    /// <summary>Trace severity (1).</summary>
    Trace = 1,

    /// <summary>Trace1 severity (2).</summary>
    Trace2 = Trace + 1,

    /// <summary>Trace3 severity (3).</summary>
    Trace3 = Trace2 + 1,

    /// <summary>Trace4 severity (4).</summary>
    Trace4 = Trace3 + 1,

    /// <summary>Debug severity (5).</summary>
    Debug = 5,

    /// <summary>Debug2 severity (6).</summary>
    Debug2 = Debug + 1,

    /// <summary>Debug3 severity (7).</summary>
    Debug3 = Debug2 + 1,

    /// <summary>Debug4 severity (8).</summary>
    Debug4 = Debug3 + 1,

    /// <summary>Info severity (9).</summary>
    Info = 9,

    /// <summary>Info2 severity (11).</summary>
    Info2 = Info + 1,

    /// <summary>Info3 severity (12).</summary>
    Info3 = Info2 + 1,

    /// <summary>Info4 severity (13).</summary>
    Info4 = Info3 + 1,

    /// <summary>Warn severity (13).</summary>
    Warn = 13,

    /// <summary>Warn2 severity (14).</summary>
    Warn2 = Warn + 1,

    /// <summary>Warn3 severity (15).</summary>
    Warn3 = Warn2 + 1,

    /// <summary>Warn severity (16).</summary>
    Warn4 = Warn3 + 1,

    /// <summary>Error severity (17).</summary>
    Error = 17,

    /// <summary>Error2 severity (18).</summary>
    Error2 = Error + 1,

    /// <summary>Error3 severity (19).</summary>
    Error3 = Error2 + 1,

    /// <summary>Error4 severity (20).</summary>
    Error4 = Error3 + 1,

    /// <summary>Fatal severity (21).</summary>
    Fatal = 21,

    /// <summary>Fatal2 severity (22).</summary>
    Fatal2 = Fatal + 1,

    /// <summary>Fatal3 severity (23).</summary>
    Fatal3 = Fatal2 + 1,

    /// <summary>Fatal4 severity (24).</summary>
    Fatal4 = Fatal3 + 1,
}
