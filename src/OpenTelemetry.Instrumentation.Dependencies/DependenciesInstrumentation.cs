// <copyright file="DependenciesInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies
{
    /// <summary>
    /// Instrumentation adaptor that automatically collect calls to Http, SQL, and Azure SDK.
    /// </summary>
    public class DependenciesInstrumentation : IDisposable
    {
        private readonly List<IDisposable> instrumentations = new List<IDisposable>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesInstrumentation"/> class.
        /// </summary>
        /// <param name="tracerFactory">Tracer factory to get a tracer from.</param>
        /// <param name="httpOptions">Http configuration options.</param>
        /// <param name="sqlOptions">Sql configuration options.</param>
        public DependenciesInstrumentation(TracerFactoryBase tracerFactory, HttpClientInstrumentationOptions httpOptions = null, SqlClientInstrumentationOptions sqlOptions = null)
        {
            if (tracerFactory == null)
            {
                throw new ArgumentNullException(nameof(tracerFactory));
            }

            var assemblyVersion = typeof(DependenciesInstrumentation).Assembly.GetName().Version;

            var httpClientListener = new HttpClientInstrumentation(tracerFactory.GetTracer(nameof(HttpClientInstrumentation), "semver:" + assemblyVersion), httpOptions ?? new HttpClientInstrumentationOptions());
            var azureClientsListener = new AzureClientsInstrumentation(tracerFactory.GetTracer(nameof(AzureClientsInstrumentation), "semver:" + assemblyVersion));
            var azurePipelineListener = new AzurePipelineInstrumentation(tracerFactory.GetTracer(nameof(AzurePipelineInstrumentation), "semver:" + assemblyVersion));
            var sqlClientListener = new SqlClientInstrumentation(tracerFactory.GetTracer(nameof(SqlClientInstrumentation), "semver:" + assemblyVersion), sqlOptions ?? new SqlClientInstrumentationOptions());

            this.instrumentations.Add(httpClientListener);
            this.instrumentations.Add(azureClientsListener);
            this.instrumentations.Add(azurePipelineListener);
            this.instrumentations.Add(sqlClientListener);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var instrumentation in this.instrumentations)
            {
                instrumentation.Dispose();
            }
        }
    }
}
