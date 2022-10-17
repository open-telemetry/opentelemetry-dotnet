// <copyright file="LoggerOptions.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains options for the <see cref="Logger"/> class.
/// </summary>
public sealed class LoggerOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerOptions"/> class.
    /// </summary>
    public LoggerOptions()
        : this(name: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerOptions"/> class.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    public LoggerOptions(string? name)
        : this(new InstrumentationScope(name))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerOptions"/> class.
    /// </summary>
    /// <param name="instrumentationScope"><see cref="OpenTelemetry.InstrumentationScope"/>.</param>
    public LoggerOptions(InstrumentationScope instrumentationScope)
    {
        Guard.ThrowIfNull(instrumentationScope);

        this.InstrumentationScope = instrumentationScope;
    }

    /// <summary>
    /// Gets the <see cref="OpenTelemetry.InstrumentationScope"/> for log
    /// records emitted by the instrumentation library.
    /// </summary>
    public InstrumentationScope InstrumentationScope { get; }

    /// <summary>
    /// Gets the domain of events emitted by the instrumentation library.
    /// </summary>
    public string? EventDomain
    {
        get
        {
            var attributes = this.InstrumentationScope.AttributeBacking;
            if (attributes != null && attributes.TryGetValue(SemanticConventions.AttributeLogEventDomain, out var eventDomain))
            {
                return eventDomain as string;
            }

            return null;
        }

        init
        {
            var attributes = this.InstrumentationScope.AttributeBacking;

            Dictionary<string, object> newAttributes = new(attributes?.Count + 1 ?? 1);

            if (attributes != null)
            {
                foreach (var kvp in attributes)
                {
                    newAttributes.Add(kvp.Key, kvp.Value);
                }
            }

            if (value != null)
            {
                newAttributes[SemanticConventions.AttributeLogEventDomain] = value;
            }
            else
            {
                newAttributes.Remove(SemanticConventions.AttributeLogEventDomain);
            }

            this.InstrumentationScope.AttributeBacking = newAttributes;
        }
    }

    /// <summary>
    /// Gets a value indicating whether or not trace context should
    /// automatically by injected into log records created by the
    /// instrumentation library.
    /// </summary>
    public bool IncludeTraceContext { get; init; } = true;
}
