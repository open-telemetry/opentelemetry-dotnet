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
    using OpenTelemetry.Collector.Dependencies.Implementation;
    using OpenTelemetry.Trace;

    /// <summary>
    /// Dependencies collector.
    /// </summary>
    public class DependenciesCollector : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesCollector"/> class.
        /// </summary>
        /// <param name="options">Configuration options for dependencies collector.</param>
        /// <param name="tracerFactory">TracerFactory to create a Tracer to record traced with.</param>
        public DependenciesCollector(DependenciesCollectorOptions options, ITracerFactory tracerFactory)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
                new Dictionary<string, Func<ITracerFactory, ListenerHandler>>()
                {
                    {
                        "HttpHandlerDiagnosticListener", (tf) =>
                        {
                            var tracer = tf.GetTracer("OpenTelemetry.Collector.Dependencies.HttpHandlerDiagnosticListener");
                            return new HttpHandlerDiagnosticListener(tracer);
                        }
                    },
                    {
                        "Azure.Clients", (tf) =>
                        {
                            var tracer = tf.GetTracer("OpenTelemetry.Collector.Dependencies.Azure.Clients");
                            return new AzureSdkDiagnosticListener("Azure.Clients", tracer);
                        }
                    },
                    {
                        "Azure.Pipeline", (tf) =>
                        {
                            var tracer = tf.GetTracer("OpenTelemetry.Collector.Dependencies.Azure.Pipeline");
                            return new AzureSdkDiagnosticListener("Azure.Pipeline", tracer);
                        }
                    },
                },
                tracerFactory,
                options.EventFilter);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
