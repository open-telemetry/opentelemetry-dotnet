// <copyright file="IDeferredTracerBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Describes a tracer builder tha support deferred initialization using a <see cref="IServiceProvider"/> to perform dependency injection.
    /// </summary>
    public interface IDeferredTracerBuilder
    {
        /// <summary>
        /// Register a callback action to configure the <see cref="TracerProviderBuilder"/> during initialization.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        TracerProviderBuilder Configure(Action<IServiceProvider, TracerProviderBuilder> configure);

        /// <summary>
        /// Run the configured actions to initialize the <see cref="TracerProvider"/>.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <returns><see cref="TracerProvider"/>.</returns>
        TracerProvider Build(IServiceProvider serviceProvider);
    }
}
