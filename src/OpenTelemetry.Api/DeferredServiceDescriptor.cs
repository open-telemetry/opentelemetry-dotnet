// <copyright file="DeferredServiceDescriptor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    /// <summary>
    /// Describes a service with its service type, implementation, and lifetime.
    /// </summary>
    public sealed class DeferredServiceDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeferredServiceDescriptor"/>
        /// class with the specified instance as a singleton.
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> of the service.</param>
        /// <param name="instance">The instance implementing the service.</param>
        public DeferredServiceDescriptor(Type serviceType, object instance)
            : this(serviceType, DeferredServiceLifetime.Singleton)
        {
            this.ImplementationInstance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeferredServiceDescriptor"/>
        /// class with the specified <paramref name="implementationType"/>.
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> of the service.</param>
        /// <param name="implementationType">The <see cref="Type"/> implementing the service.</param>
        /// <param name="lifetime">The <see cref="DeferredServiceLifetime"/> of created instances.</param>
        public DeferredServiceDescriptor(Type serviceType, Type implementationType, DeferredServiceLifetime lifetime)
            : this(serviceType, lifetime)
        {
            this.ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeferredServiceDescriptor"/>
        /// class with the specified <paramref name="factory"/>.
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> of the service.</param>
        /// <param name="factory">The factory used for creating service instances.</param>
        /// <param name="lifetime">The <see cref="DeferredServiceLifetime"/> of created instances.</param>
        public DeferredServiceDescriptor(Type serviceType, Func<IServiceProvider, object> factory, DeferredServiceLifetime lifetime)
            : this(serviceType, lifetime)
        {
            this.ImplementationFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        private DeferredServiceDescriptor(Type serviceType, DeferredServiceLifetime serviceLifetime)
        {
            this.ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            this.Lifetime = serviceLifetime;
        }

        /// <summary>
        /// Gets the type of service.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Gets the <see cref="DeferredServiceLifetime"/> for the service.
        /// </summary>
        public DeferredServiceLifetime Lifetime { get; }

        /// <summary>
        /// Gets the type used to implement the service.
        /// </summary>
        public Type ImplementationType { get; }

        /// <summary>
        /// Gets the instance used for the service.
        /// </summary>
        public object ImplementationInstance { get; }

        /// <summary>
        /// Gets the factory used to create the service implementation.
        /// </summary>
        public Func<IServiceProvider, object> ImplementationFactory { get; }
    }
}
