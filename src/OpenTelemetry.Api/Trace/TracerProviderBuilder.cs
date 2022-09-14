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
using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Trace
{
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
            where TInstrumentation : class;

        /// <summary>
        /// Adds given activitysource names to the list of subscribed sources.
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

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="IServiceCollection"/> where tracing services are configured.
        /// </summary>
        /// <remarks>
        /// Note: Tracing services are only available during the application
        /// configuration phase.
        /// </remarks>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public virtual TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
            => throw new NotSupportedException();

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="TracerProviderBuilder"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public virtual TracerProviderBuilder ConfigureBuilder(Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            if (this is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure(configure);
            }

            throw new NotSupportedException();
        }
    }
}
