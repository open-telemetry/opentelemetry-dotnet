// <copyright file="DependenciesCollector.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.Dependencies
{
    /// <summary>
    /// Instrumentation adaptor that automatically collect calls to Http, SQL, and Azure SDK.
    /// </summary>
    public class DependenciesCollector : IDisposable
    {
        private readonly List<IDisposable> collectors = new List<IDisposable>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesCollector"/> class.
        /// </summary>
        /// <param name="tracerFactory">Tracer factory to get a tracer from.</param>
        /// <param name="httpOptions">Http configuration options.</param>
        /// <param name="sqlOptions">Sql configuration options.</param>
        public DependenciesCollector(TracerFactoryBase tracerFactory, HttpClientCollectorOptions httpOptions = null, SqlClientCollectorOptions sqlOptions = null)
        {
            if (tracerFactory == null)
            {
                throw new ArgumentNullException(nameof(tracerFactory));
            }

            var assemblyVersion = typeof(DependenciesCollector).Assembly.GetName().Version;

            var httpClientListener = new HttpClientCollector(tracerFactory.GetTracer(nameof(HttpClientCollector), "semver:" + assemblyVersion), httpOptions ?? new HttpClientCollectorOptions());
            var httpWebRequestCollector = new HttpWebRequestCollector(tracerFactory.GetTracer(nameof(HttpWebRequestCollector), "semver:" + assemblyVersion), httpOptions ?? new HttpClientCollectorOptions());
            var azureClientsListener = new AzureClientsCollector(tracerFactory.GetTracer(nameof(AzureClientsCollector), "semver:" + assemblyVersion));
            var azurePipelineListener = new AzurePipelineCollector(tracerFactory.GetTracer(nameof(AzurePipelineCollector), "semver:" + assemblyVersion));
            var sqlClientListener = new SqlClientCollector(tracerFactory.GetTracer(nameof(AzurePipelineCollector), "semver:" + assemblyVersion), sqlOptions ?? new SqlClientCollectorOptions());

            this.collectors.Add(httpClientListener);
            this.collectors.Add(httpWebRequestCollector);
            this.collectors.Add(azureClientsListener);
            this.collectors.Add(azurePipelineListener);
            this.collectors.Add(sqlClientListener);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var collector in this.collectors)
            {
                collector.Dispose();
            }
        }
    }
}
