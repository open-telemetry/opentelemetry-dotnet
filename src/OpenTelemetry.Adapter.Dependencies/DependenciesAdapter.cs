// <copyright file="DependenciesAdapter.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Adapter.Dependencies
{
    /// <summary>
    /// Instrumentation adaptor that automatically collect calls to http and Azure SDK.
    /// </summary>
    public class DependenciesAdapter : IDisposable
    {
        private readonly List<IDisposable> adapters = new List<IDisposable>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesAdapter"/> class.
        /// </summary>
        /// <param name="options">Configuraiton options.</param>
        /// <param name="tracerFactory">Tracer factory to get a tracer from.</param>
        public DependenciesAdapter(HttpClientAdapterOptions options, TracerFactoryBase tracerFactory)
        {
            var assemblyVersion = typeof(DependenciesAdapter).Assembly.GetName().Version;
            var httpClientListener = new HttpClientAdapter(tracerFactory.GetTracer(nameof(HttpClientAdapter), "semver:" + assemblyVersion), options);
            var azureClientsListener = new AzureClientsAdapter(tracerFactory.GetTracer(nameof(AzureClientsAdapter), "semver:" + assemblyVersion));
            var azurePipelineListener = new AzurePipelineAdapter(tracerFactory.GetTracer(nameof(AzurePipelineAdapter), "semver:" + assemblyVersion));

            this.adapters.Add(httpClientListener);
            this.adapters.Add(azureClientsListener);
            this.adapters.Add(azurePipelineListener);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var adapter in this.adapters)
            {
                adapter.Dispose();
            }
        }
    }
}
