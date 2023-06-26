// <copyright file="OpenTelemetryLoggerOptions.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains OpenTelemetry logging options.
/// </summary>
public class OpenTelemetryLoggerOptions
{
    internal readonly List<BaseProcessor<LogRecord>> Processors = new();
    internal ResourceBuilder? ResourceBuilder;

    /// <summary>
    /// Gets or sets a value indicating whether or not formatted log message
    /// should be included on generated <see cref="LogRecord"/>s. Default
    /// value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Note: When set to <see langword="false"/> a formatted log message
    /// will not be included if a message template can be found. If a
    /// message template is not found, a formatted log message is always
    /// included.
    /// </remarks>
    public bool IncludeFormattedMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not log scopes should be
    /// included on generated <see cref="LogRecord"/>s. Default value:
    /// <see langword="false"/>.
    /// </summary>
    public bool IncludeScopes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not log state should be
    /// parsed into <see cref="LogRecord.Attributes"/> on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>As of OpenTelemetry v1.5 state parsing is handled automatically if
    /// the state logged implements <see cref="IReadOnlyList{T}"/> or <see
    /// cref="IEnumerable{T}"/> where <c>T</c> is <c>KeyValuePair&lt;string,
    /// object&gt;</c> than <see cref="LogRecord.Attributes"/> will be set
    /// regardless of the value of <see cref="ParseStateValues"/>.</item>
    /// <item>When <see cref="ParseStateValues"/> is set to <see
    /// langword="true"/> <see cref="LogRecord.State"/> will always be <see
    /// langword="null"/>. When <see cref="ParseStateValues"/> is set to <see
    /// langword="false"/> <see cref="LogRecord.State"/> will always be set to
    /// the logged state to support legacy exporters which access <see
    /// cref="LogRecord.State"/> directly. Exporters should NOT access <see
    /// cref="LogRecord.State"/> directly because is NOT safe and may lead to
    /// exceptions or incorrect data especially when using batching. Exporters
    /// should use <see cref="LogRecord.Attributes"/> to safely access any data
    /// attached to log messages.</item>
    /// </list>
    /// </remarks>
    public bool ParseStateValues { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not attributes specified
    /// via log state should be included on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="true"/>.
    /// </summary>
    internal bool IncludeAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether or not the <see
    /// cref="Activity.TraceStateString"/> for the current <see
    /// cref="Activity"/> should be included on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
    /// </summary>
    internal bool IncludeTraceState { get; set; }

    /// <summary>
    /// Adds processor to the options.
    /// </summary>
    /// <param name="processor">Log processor to add.</param>
    /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
    public OpenTelemetryLoggerOptions AddProcessor(BaseProcessor<LogRecord> processor)
    {
        Guard.ThrowIfNull(processor);

        this.Processors.Add(processor);

        return this;
    }

    /// <summary>
    /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from. Overwrites currently set ResourceBuilder.
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
    /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
    public OpenTelemetryLoggerOptions SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);

        this.ResourceBuilder = resourceBuilder;
        return this;
    }

    internal OpenTelemetryLoggerOptions Copy()
    {
        return new()
        {
            IncludeFormattedMessage = this.IncludeFormattedMessage,
            IncludeScopes = this.IncludeScopes,
            ParseStateValues = this.ParseStateValues,
            IncludeAttributes = this.IncludeAttributes,
            IncludeTraceState = this.IncludeTraceState,
        };
    }
}
