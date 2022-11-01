// <copyright file="ExemplarFilters.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics;

/// <summary>
/// Provides access to built-in ExemplarFilters.
/// </summary>
internal static class ExemplarFilters
{
    /// <summary>
    /// Gets the ExemplarFilter which never samples any measurements.
    /// </summary>
    public static IExemplarFilter None { get; } = new NoneExemplarFilter();

    /// <summary>
    /// Gets the ExemplarFilter which samples all measurements.
    /// </summary>
    public static IExemplarFilter All { get; } = new AllExemplarFilter();

    /// <summary>
    /// Gets the ExemplarFilter which samples all measurements that are made
    /// inside context of a sampled Activity.
    /// </summary>
    public static IExemplarFilter WithSampledTrace { get; } = new SampledTraceExemplarFilter();
}
