// <copyright file="Logger.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Logger is the class responsible for creating log records.
/// </summary>
public abstract class Logger
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    /// <param name="options"><see cref="LoggerOptions"/>.</param>
    protected Logger(LoggerOptions options)
    {
        Guard.ThrowIfNull(options);

        this.Options = options;
    }

    /// <summary>
    /// Gets the options associated with the logger.
    /// </summary>
    public LoggerOptions Options { get; }

    /// <summary>
    /// Emit an event.
    /// </summary>
    /// <param name="name">Event name.</param>
    /// <param name="data"><see cref="LogRecordData"/>.</param>
    /// <param name="attributes"><see cref="LogRecordAttributeList"/>.</param>
    public abstract void EmitEvent(
        string name,
        in LogRecordData data,
        in LogRecordAttributeList attributes = default);

    /// <summary>
    /// Emit a log.
    /// </summary>
    /// <param name="data"><see cref="LogRecordData"/>.</param>
    /// <param name="attributes"><see cref="LogRecordAttributeList"/>.</param>
    public abstract void EmitLog(
        in LogRecordData data,
        in LogRecordAttributeList attributes = default);
}
