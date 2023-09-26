// <copyright file="MeterProviderBuilder.cs" company="OpenTelemetry Authors">
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
/// MeterProviderBuilder base class.
/// </summary>
public abstract class MeterProviderBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    protected MeterProviderBuilder()
    {
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
    /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
    /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public abstract MeterProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation> instrumentationFactory)
        where TInstrumentation : class?;

    /// <summary>
    /// Adds given Meter names to the list of subscribed meters.
    /// </summary>
    /// <param name="names">Meter names.</param>
    /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public abstract MeterProviderBuilder AddMeter(params string[] names);
}
