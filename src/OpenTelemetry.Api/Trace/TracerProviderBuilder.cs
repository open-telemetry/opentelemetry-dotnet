// <copyright file="TracerProviderBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace;

/// <summary>
/// TracerProviderBuilder base class.
/// </summary>
public abstract class TracerProviderBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TracerProviderBuilder"/> class.
    /// </summary>
    protected TracerProviderBuilder()
    {
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
    /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation> instrumentationFactory)
        where TInstrumentation : class?;

    /// <summary>
    /// Adds the given <see cref="ActivitySource"/> names to the list of subscribed sources.
    /// </summary>
    /// <param name="names">Activity source names.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddSource(params string[] names);

    /// <summary>
    /// Adds a listener for <see cref="Activity"/> objects created with the given operation name to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// This is provided to capture legacy <see cref="Activity"/> objects created without using the <see cref="ActivitySource"/> API.
    /// </remarks>
    /// <param name="operationName">Operation name of the <see cref="Activity"/> objects to capture.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddLegacySource(string operationName);
}
