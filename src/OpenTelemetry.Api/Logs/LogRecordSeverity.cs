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
    /// <summary>Trace severity.</summary>
    Trace,

    /// <summary>Debug severity.</summary>
    Debug,

    /// <summary>Information severity.</summary>
    Information,

    /// <summary>Warning severity.</summary>
    Warning,

    /// <summary>Error severity.</summary>
    Error,

    /// <summary>Fatal severity.</summary>
    Fatal,
}
