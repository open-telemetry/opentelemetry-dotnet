// <copyright file="OpenTelemetrySerilogSinkOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains options that apply to log messages written through the OpenTelemetry Serilog sink.
/// </summary>
public class OpenTelemetrySerilogSinkOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not rendered log message
    /// should be included on generated <see cref="LogRecord"/>s. Default
    /// value: <see langword="false"/>.
    /// </summary>
    public bool IncludeRenderedMessage { get; set; }
}
