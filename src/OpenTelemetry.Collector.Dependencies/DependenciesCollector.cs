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

namespace OpenTelemetry.Collector.Dependencies
{
    using System;
    using System.Collections.Generic;
    using OpenTelemetry.Trace;

    public class DependenciesCollector : IDisposable
    {
        private readonly List<IDisposable> collectors = new List<IDisposable>();

        public DependenciesCollector(HttpClientCollectorOptions options, TracerFactoryBase tracerFactory)
        {
            var assemblyVersion = typeof(DependenciesCollector).Assembly.GetName().Version;
            var httpClientListener = new HttpClientCollector(tracerFactory.GetTracer(nameof(HttpClientCollector), "semver:" + assemblyVersion), options);
            var azureClientsListener = new AzureClientsCollector(tracerFactory.GetTracer(nameof(AzureClientsCollector), "semver:" + assemblyVersion));
            var azurePipelineListener = new AzurePipelineCollector(tracerFactory.GetTracer(nameof(AzurePipelineCollector), "semver:" + assemblyVersion));

            this.collectors.Add(httpClientListener);
            this.collectors.Add(azureClientsListener);
            this.collectors.Add(azurePipelineListener);
        }

        public void Dispose()
        {
            foreach (var collector in this.collectors)
            {
                collector.Dispose();
            }
        }
    }
}
