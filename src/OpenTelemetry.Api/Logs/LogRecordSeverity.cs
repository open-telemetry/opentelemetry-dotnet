// <copyright file="LogRecordSeverity.cs" company="OpenTelemetry Authors">
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
/// Describes the severity level of a log record.
/// </summary>
internal enum LogRecordSeverity
{
    /// <summary>Unknown severity (0).</summary>
    Unknown = 0,

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

    /// <summary>Information severity (9).</summary>
    Information = 9,

    /// <summary>Information2 severity (11).</summary>
    Information2 = Information + 1,

    /// <summary>Information3 severity (12).</summary>
    Information3 = Information2 + 1,

    /// <summary>Information4 severity (13).</summary>
    Information4 = Information3 + 1,

    /// <summary>Warning severity (13).</summary>
    Warning = 13,

    /// <summary>Warning2 severity (14).</summary>
    Warning2 = Warning + 1,

    /// <summary>Warning3 severity (15).</summary>
    Warning3 = Warning2 + 1,

    /// <summary>Warning4 severity (16).</summary>
    Warning4 = Warning3 + 1,

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
