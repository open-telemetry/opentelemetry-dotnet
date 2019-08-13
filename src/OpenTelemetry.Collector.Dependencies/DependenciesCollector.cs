﻿// <copyright file="DependenciesCollector.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Collector.Dependencies.Common;
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
        /// <param name="tracerFactory">TracerFactory for creating named Tracers.</param>
        /// <param name="sampler">Sampler to use to sample dependency calls.</param>
        public DependenciesCollector(DependenciesCollectorOptions options, ITracerFactory tracerFactory, ISampler sampler)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
                new Dictionary<string, Func<ITracerFactory, Func<HttpRequestMessage, ISampler>, ListenerHandler>>()
                {
                    { "HttpHandlerDiagnosticListener", (tf, s) => new HttpHandlerDiagnosticListener(tf, s) },
                    { "Azure.Clients", (tf, s) => new AzureSdkDiagnosticListener("Azure.Clients", tf, sampler) },
                    { "Azure.Pipeline", (tf, s) => new AzureSdkDiagnosticListener("Azure.Pipeline", tf, sampler) },
                },
                tracerFactory,
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
                        DependenciesCollectorEventSource.Log.ExceptionInCustomSampler(e);
                    }

                    return s ?? sampler;
                    });
            this.diagnosticSourceSubscriber.Subscribe();
        }

        public void Dispose()
        {
            this.diagnosticSourceSubscriber.Dispose();
        }
    }
}
