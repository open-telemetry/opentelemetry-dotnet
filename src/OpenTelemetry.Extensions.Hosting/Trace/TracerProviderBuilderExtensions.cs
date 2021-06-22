// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Contains extension methods for the <see cref="TracerProviderBuilder"/> class.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Adds instrumentation to the provider.
        /// </summary>
        /// <typeparam name="T">Instrumentation type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddInstrumentation<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : class
        {
            if (tracerProviderBuilder is TracerProviderBuilderHosting tracerProviderBuilderHosting)
            {
                tracerProviderBuilderHosting.Configure((sp, builder) => builder
                    .AddInstrumentation(() => sp.GetRequiredService<T>()));
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Adds a processor to the provider.
        /// </summary>
        /// <typeparam name="T">Processor type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddProcessor<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : BaseProcessor<Activity>
        {
            if (tracerProviderBuilder is TracerProviderBuilderHosting tracerProviderBuilderHosting)
            {
                tracerProviderBuilderHosting.Configure((sp, builder) => builder
                    .AddProcessor(sp.GetRequiredService<T>()));
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Sets the sampler on the provider.
        /// </summary>
        /// <typeparam name="T">Sampler type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetSampler<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : Sampler
        {
            if (tracerProviderBuilder is TracerProviderBuilderHosting tracerProviderBuilderHosting)
            {
                tracerProviderBuilderHosting.Configure((sp, builder) => builder
                    .SetSampler(sp.GetRequiredService<T>()));
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="TracerProviderBuilder"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder Configure(this TracerProviderBuilder tracerProviderBuilder, Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            if (tracerProviderBuilder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                deferredTracerProviderBuilder.Configure(configure);
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Run the configured actions to initialize the <see cref="TracerProvider"/>.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <returns><see cref="TracerProvider"/>.</returns>
        public static TracerProvider Build(this TracerProviderBuilder tracerProviderBuilder, IServiceProvider serviceProvider)
        {
            if (tracerProviderBuilder is TracerProviderBuilderHosting tracerProviderBuilderHosting)
            {
                return tracerProviderBuilderHosting.Build(serviceProvider);
            }

            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                return tracerProviderBuilderBase.Build();
            }

            return null;
        }
    }
}
