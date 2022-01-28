// <copyright file="MeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds instrumentation to the provider.
        /// </summary>
        /// <typeparam name="T">Instrumentation type.</typeparam>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddInstrumentation<T>(this MeterProviderBuilder meterProviderBuilder)
            where T : class
        {
            if (meterProviderBuilder is MeterProviderBuilderHosting meterProviderBuilderHosting)
            {
                meterProviderBuilderHosting.Configure((sp, builder) => builder
                    .AddInstrumentation(() => sp.GetRequiredService<T>()));
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Adds a reader to the provider.
        /// </summary>
        /// <typeparam name="T">Reader type.</typeparam>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddReader<T>(this MeterProviderBuilder meterProviderBuilder)
            where T : MetricReader
        {
            if (meterProviderBuilder is MeterProviderBuilderHosting meterProviderBuilderHosting)
            {
                meterProviderBuilderHosting.Configure((sp, builder) => builder
                    .AddReader(sp.GetRequiredService<T>()));
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="MeterProviderBuilder"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder Configure(this MeterProviderBuilder meterProviderBuilder, Action<IServiceProvider, MeterProviderBuilder> configure)
        {
            if (meterProviderBuilder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                deferredMeterProviderBuilder.Configure(configure);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Gets the application <see cref="IServiceCollection"/> attached to
        /// the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns><see cref="IServiceCollection"/> or <see langword="null"/>
        /// if services are unavailable.</returns>
        public static IServiceCollection GetServices(this MeterProviderBuilder meterProviderBuilder)
        {
            if (meterProviderBuilder is MeterProviderBuilderHosting meterProviderBuilderHosting)
            {
                return meterProviderBuilderHosting.Services;
            }

            return null;
        }

        /// <summary>
        /// Run the configured actions to initialize the <see cref="MeterProvider"/>.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProvider Build(this MeterProviderBuilder meterProviderBuilder, IServiceProvider serviceProvider)
        {
            if (meterProviderBuilder is MeterProviderBuilderHosting meterProviderBuilderHosting)
            {
                return meterProviderBuilderHosting.Build(serviceProvider);
            }

            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                return meterProviderBuilderBase.Build();
            }

            return null;
        }
    }
}
