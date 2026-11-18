// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The declarative-configuration schema's <c>SeverityNumber</c> type, the value type of <c>log_level</c>.
/// Each member is one of the schema's lowercase names (for example <c>debug2</c>) and its value is the
/// severity number the OpenTelemetry logs data model assigns to it.
/// </summary>
internal enum SeverityNumber
{
    /// <summary>The <c>trace</c> severity (1).</summary>
    Trace = 1,

    /// <summary>The <c>trace2</c> severity (2).</summary>
    Trace2 = 2,

    /// <summary>The <c>trace3</c> severity (3).</summary>
    Trace3 = 3,

    /// <summary>The <c>trace4</c> severity (4).</summary>
    Trace4 = 4,

    /// <summary>The <c>debug</c> severity (5).</summary>
    Debug = 5,

    /// <summary>The <c>debug2</c> severity (6).</summary>
    Debug2 = 6,

    /// <summary>The <c>debug3</c> severity (7).</summary>
    Debug3 = 7,

    /// <summary>The <c>debug4</c> severity (8).</summary>
    Debug4 = 8,

    /// <summary>The <c>info</c> severity (9).</summary>
    Info = 9,

    /// <summary>The <c>info2</c> severity (10).</summary>
    Info2 = 10,

    /// <summary>The <c>info3</c> severity (11).</summary>
    Info3 = 11,

    /// <summary>The <c>info4</c> severity (12).</summary>
    Info4 = 12,

    /// <summary>The <c>warn</c> severity (13).</summary>
    Warn = 13,

    /// <summary>The <c>warn2</c> severity (14).</summary>
    Warn2 = 14,

    /// <summary>The <c>warn3</c> severity (15).</summary>
    Warn3 = 15,

    /// <summary>The <c>warn4</c> severity (16).</summary>
    Warn4 = 16,

    /// <summary>The <c>error</c> severity (17).</summary>
    Error = 17,

    /// <summary>The <c>error2</c> severity (18).</summary>
    Error2 = 18,

    /// <summary>The <c>error3</c> severity (19).</summary>
    Error3 = 19,

    /// <summary>The <c>error4</c> severity (20).</summary>
    Error4 = 20,

    /// <summary>The <c>fatal</c> severity (21).</summary>
    Fatal = 21,

    /// <summary>The <c>fatal2</c> severity (22).</summary>
    Fatal2 = 22,

    /// <summary>The <c>fatal3</c> severity (23).</summary>
    Fatal3 = 23,

    /// <summary>The <c>fatal4</c> severity (24).</summary>
    Fatal4 = 24,
}
