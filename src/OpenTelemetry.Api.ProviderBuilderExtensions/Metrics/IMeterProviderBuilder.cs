// <copyright file="IMeterProviderBuilder.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Describes a <see cref="MeterProviderBuilder"/> backed by an <see cref="IServiceCollection"/>.
/// </summary>
// Note: This API may be made public if there is a need for it.
internal interface IMeterProviderBuilder : IDeferredMeterProviderBuilder
{
    /// <summary>
    /// Gets the <see cref="MeterProvider"/> being constructed by the builder.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="Provider"/> should return <see langword="null"/> until
    /// construction has started and the <see cref="IServiceCollection"/> has
    /// closed.
    /// </remarks>
    MeterProvider? Provider { get; }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="IServiceCollection"/> where metric services are configured.
    /// </summary>
    /// <remarks>
    /// Note: Metric services are only available during the application
    /// configuration phase. This method should throw a <see
    /// cref="NotSupportedException"/> if services are configured after the
    /// application <see cref="IServiceProvider"/> has been created.
    /// </remarks>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure);
}
