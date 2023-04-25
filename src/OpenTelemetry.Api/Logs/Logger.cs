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

namespace OpenTelemetry.Logs;

/// <summary>
/// Logger is the class responsible for creating log records.
/// </summary>
internal abstract class Logger
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    protected Logger(string? name)
    {
        this.Name = string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : name!;
    }

    /// <summary>
    /// Gets the name identifying the instrumentation library.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the version of the instrumentation library.
    /// </summary>
    public string? Version { get; private set; }

    /// <summary>
    /// Gets the attributes which should be associated with log records created
    /// by the instrumentation library.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Attributes { get; private set; }

    /// <summary>
    /// Emit a log.
    /// </summary>
    /// <param name="data"><see cref="LogRecordData"/>.</param>
    /// <param name="attributes"><see cref="LogRecordAttributeList"/>.</param>
    public abstract void EmitLog(
        in LogRecordData data,
        in LogRecordAttributeList attributes = default);

    internal void SetInstrumentationScope(
        string? version,
        IReadOnlyDictionary<string, object>? attributes)
    {
        this.Version = version;
        this.Attributes = attributes;
    }
}
