// <copyright file="MeterProviderBuilderHosting.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// A <see cref="MeterProviderBuilderBase"/> with support for deferred initialization using <see cref="IServiceProvider"/> for dependency injection.
    /// </summary>
    internal sealed class MeterProviderBuilderHosting : MeterProviderBuilderBase, IDeferredMeterProviderBuilder
    {
        private readonly List<Action<IServiceProvider, MeterProviderBuilder>> configurationActions = new List<Action<IServiceProvider, MeterProviderBuilder>>();

        public MeterProviderBuilderHosting(IServiceCollection services)
        {
            Guard.ThrowIfNull(services, nameof(services));

            this.Services = services;
        }

        public IServiceCollection Services { get; }

        public MeterProviderBuilder Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure, nameof(configure));

            this.configurationActions.Add(configure);
            return this;
        }

        public MeterProvider Build(IServiceProvider serviceProvider)
        {
            Guard.ThrowIfNull(serviceProvider, nameof(serviceProvider));

            // Note: Not using a foreach loop because additional actions can be
            // added during each call.
            for (int i = 0; i < this.configurationActions.Count; i++)
            {
                this.configurationActions[i](serviceProvider, this);
            }

            return this.Build();
        }
    }
}
