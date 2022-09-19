// <copyright file="InstrumentationScope.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry;

internal sealed class InstrumentationScope
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentationScope"/> class.
    /// </summary>
    public InstrumentationScope()
        : this(name: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentationScope"/> class.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    public InstrumentationScope(string? name)
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
    public string? Version { get; init; }

    /// <summary>
    /// Gets the schema url of the instrumentation library.
    /// </summary>
    public string? SchemaUrl { get; init; }

    /// <summary>
    /// Gets the attributes which should be associated with log records created
    /// by the instrumentation library.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Attributes { get; init; }
}
