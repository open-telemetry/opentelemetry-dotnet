// <copyright file="IPullMetricExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics;

/// <summary>
/// Describes a type of <see cref="BaseExporter{Metric}"/> which supports <see cref="ExportModes.Pull"/>.
/// </summary>
public interface IPullMetricExporter
{
    /// <summary>
    /// Gets or sets the Collect delegate.
    /// </summary>
    Func<int, bool>? Collect { get; set; }
}
