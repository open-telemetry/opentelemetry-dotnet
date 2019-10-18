// <copyright file="OpenTelemetryServicesExtensions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using OpenTelemetry.Hosting.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Configuration;

    /// <summary>
    /// Extension methods for setting up OpenTelemetry services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class OpenTelemetryServicesExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services)
        {
            services.AddOpenTelemetry(builder => { });
            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">The <see cref="TracerBuilder"/> configuration delegate.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Action<TracerBuilder> configure)
        {
            services.AddOpenTelemetry(() => TracerFactory.Create(configure));
            services.AddSingleton<TracerFactory>(s => (TracerFactory)s.GetRequiredService<TracerFactoryBase>());

            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">The <see cref="TracerBuilder"/> configuration delegate.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Action<IServiceProvider, TracerBuilder> configure)
        {
            services.AddOpenTelemetry(s => TracerFactory.Create(builder => configure(s, builder)));
            services.AddSingleton<TracerFactory>(s => (TracerFactory)s.GetRequiredService<TracerFactoryBase>());

            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createFactory">A delegate that provides the factory to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Func<TracerFactoryBase> createFactory)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (createFactory is null)
            {
                throw new ArgumentNullException(nameof(createFactory));
            }

            services.AddSingleton<TracerFactoryBase>(s => createFactory());
            AddOpenTelemetryCore(services);

            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createFactory">A delegate that provides the factory to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Func<IServiceProvider, TracerFactoryBase> createFactory)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (createFactory is null)
            {
                throw new ArgumentNullException(nameof(createFactory));
            }

            services.AddSingleton<TracerFactoryBase>(s => createFactory(s));
            AddOpenTelemetryCore(services);

            return services;
        }

        private static void AddOpenTelemetryCore(IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TelemetryFactoryHostedService>());
        }
    }
}
