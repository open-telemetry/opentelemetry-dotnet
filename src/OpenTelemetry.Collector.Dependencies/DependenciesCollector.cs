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
    using System.Net.Http;
    using OpenTelemetry.Collector.Dependencies.Implementation;
    using OpenTelemetry.Trace;

    /// <summary>
    /// Dependencies collector.
    /// </summary>
    public class DependenciesCollector : IDisposable
    {
        private readonly DiagnosticSourceSubscriber<HttpRequestMessage> diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesCollector"/> class.
        /// </summary>
        /// <param name="options">Configuration options for dependencies collector.</param>
        /// <param name="tracer">Tracer to record traced with.</param>
        /// <param name="sampler">Sampler to use to sample dependnecy calls.</param>
        public DependenciesCollector(DependenciesCollectorOptions options, ITracer tracer, ISampler sampler)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber<HttpRequestMessage>(
                new Dictionary<string, Func<ITracer, Func<HttpRequestMessage, ISampler>, ListenerHandler<HttpRequestMessage>>>()
                {
                    { "HttpHandlerDiagnosticListener", (t, s) => new HttpHandlerDiagnosticListener(t, s) },
                    { "Azure.Clients", (t, s) => new AzureSdkDiagnosticListener("Azure.Clients", t, sampler) },
                    { "Azure.Pipeline", (t, s) => new AzureSdkDiagnosticListener("Azure.Pipeline", t, sampler) },
                },
                tracer,
                x =>
                {
                    ISampler s = null;
                    try
                    {
                        s = options.CustomSampler(x);
                    }
                    catch (Exception e)
                    {
                        s = null;
                        CollectorEventSource.Log.ExceptionInCustomSampler(e);
                    }

                    return s ?? sampler;
                    });
            this.diagnosticSourceSubscriber.Subscribe();
        }

        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
