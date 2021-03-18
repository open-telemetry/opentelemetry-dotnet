// <copyright file="TracerProviderBuilderHosting.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A <see cref="TracerProviderBuilderBase"/> with support for deferred initialization using <see cref="IServiceProvider"/> for dependency injection.
    /// </summary>
    public class TracerProviderBuilderHosting : TracerProviderBuilderBase, IDeferredTracerBuilder
    {
        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();
        private readonly List<Type> processorTypes = new List<Type>();
        private readonly List<Action<IServiceProvider, TracerProviderBuilder>> configurationActions = new List<Action<IServiceProvider, TracerProviderBuilder>>();
        private Type samplerType;

        internal TracerProviderBuilderHosting(IServiceCollection services)
        {
            this.Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Gets the application services.
        /// </summary>
        public IServiceCollection Services { get; }

        TracerProviderBuilder IDeferredTracerBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            this.configurationActions.Add(configure);
            return this;
        }

        TracerProvider IDeferredTracerBuilder.Build(IServiceProvider serviceProvider)
        {
            foreach (InstrumentationFactory instrumentationFactory in this.instrumentationFactories)
            {
                this.AddInstrumentation(
                    instrumentationFactory.Name,
                    instrumentationFactory.Version,
                    () => serviceProvider.GetRequiredService(instrumentationFactory.Type));
            }

            foreach (Type processorType in this.processorTypes)
            {
                this.AddProcessor((BaseProcessor<Activity>)serviceProvider.GetRequiredService(processorType));
            }

            if (this.samplerType != null)
            {
                this.SetSampler((Sampler)serviceProvider.GetRequiredService(this.samplerType));
            }

            foreach (Action<IServiceProvider, TracerProviderBuilder> configureAction in this.configurationActions)
            {
                configureAction(serviceProvider, this);
            }

            return ((TracerProviderBuilderBase)this).Build();
        }

        internal TracerProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<IServiceProvider, TInstrumentation> instrumentationFactory)
            where TInstrumentation : class
        {
            if (instrumentationFactory == null)
            {
                throw new ArgumentNullException(nameof(instrumentationFactory));
            }

            this.instrumentationFactories.Add(
                new InstrumentationFactory(
                    typeof(TInstrumentation).Name,
                    "semver:" + typeof(TInstrumentation).Assembly.GetName().Version,
                    typeof(TInstrumentation)));

            return this;
        }

        internal TracerProviderBuilder AddProcessor<T>()
            where T : BaseProcessor<Activity>
        {
            this.processorTypes.Add(typeof(T));
            return this;
        }

        internal TracerProviderBuilder SetSampler<T>()
            where T : Sampler
        {
            this.samplerType = typeof(T);
            return this;
        }

        internal readonly struct InstrumentationFactory
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Type Type;

            internal InstrumentationFactory(string name, string version, Type type)
            {
                this.Name = name;
                this.Version = version;
                this.Type = type;
            }
        }
    }
}
