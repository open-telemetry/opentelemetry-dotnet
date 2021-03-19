// <copyright file="IDeferredTracerProviderBuilder.cs" company="OpenTelemetry Authors">
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

using System;
#if NET461_OR_GREATER || NETSTANDARD2_0
using Microsoft.Extensions.DependencyInjection;
#endif

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Describes a tracer provider builder that supports deferred
    /// initialization using an <see cref="IServiceProvider"/> to perform
    /// dependency injection.
    /// </summary>
    public interface IDeferredTracerProviderBuilder
    {
#if NET461_OR_GREATER || NETSTANDARD2_0
        IServiceCollection Services { get; }
#endif

        /// <summary>
        /// Register a callback action to configure the <see cref="TracerProviderBuilder"/> during initialization.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        TracerProviderBuilder Configure(Action<IServiceProvider, TracerProviderBuilder> configure);
    }
}
