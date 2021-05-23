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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Describes a tracer provider builder that supports deferred
    /// initialization using an <see cref="IServiceProvider"/> to perform
    /// dependency injection.
    /// </summary>
    public interface IDeferredTracerProviderBuilder
    {
        /// <summary>
        /// Adds a service registration for the given instance as a <see
        /// cref="ServiceLifetime.Singleton"/>.
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> of the service.</param>
        /// <param name="instance">The instance implementing the service.</param>
        /// <returns>Provided <see cref="IDeferredTracerProviderBuilder"/> for chaining.</returns>
        IDeferredTracerProviderBuilder AddService(Type serviceType, object instance);

        /// <summary>
        /// Adds a service registration using a type.
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> of the service.</param>
        /// <param name="implementationType">The <see cref="Type"/> implementing the service.</param>
        /// <param name="lifetime">The <see cref="ServiceLifetime"/> of created instances.</param>
        /// <returns>Provided <see cref="IDeferredTracerProviderBuilder"/> for chaining.</returns>
        IDeferredTracerProviderBuilder AddService(Type serviceType, Type implementationType, ServiceLifetime lifetime);

        /// <summary>
        /// Adds a service registration using a factory method.
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> of the service.</param>
        /// <param name="factory">The factory used for creating service instances.</param>
        /// <param name="lifetime">The <see cref="ServiceLifetime"/> of created instances.</param>
        /// <returns>Provided <see cref="IDeferredTracerProviderBuilder"/> for chaining.</returns>
        IDeferredTracerProviderBuilder AddService(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime);

        /// <summary>
        /// Register a callback action to configure the <see cref="TracerProviderBuilder"/> during initialization.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        TracerProviderBuilder Configure(Action<IServiceProvider, TracerProviderBuilder> configure);
    }
}
