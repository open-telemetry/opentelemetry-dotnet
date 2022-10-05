// <copyright file="OpenTelemetrySerilogEnricherOptions.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Serilog.Events;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains options that control the behavior of the OpenTelemetry Serilog enricher.
/// </summary>
public class OpenTelemetrySerilogEnricherOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not the <see
    /// cref="Activity.TraceStateString"/> for the current <see
    /// cref="Activity"/> should be included on <see cref="LogEvent"/>s as the
    /// "TraceState" property. Default value: <see langword="false"/>.
    /// </summary>
    public bool IncludeTraceState { get; set; }
}

