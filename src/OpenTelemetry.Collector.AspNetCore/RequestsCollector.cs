﻿// <copyright file="RequestsCollector.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.AspNetCore
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.AspNetCore.Http;
    using OpenTelemetry.Collector.AspNetCore.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Utils;

    /// <summary>
    /// Requests collector.
    /// </summary>
    public class RequestsCollector : IDisposable
    {
        private readonly DiagnosticSourceSubscriber<HttpRequest> diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestsCollector"/> class.
        /// </summary>
        /// <param name="options">Configuration options for dependencies collector.</param>
        /// <param name="tracerFactory">TracerFactory which creates the Tracer to record traced with.</param>
        /// <param name="sampler">Sampler to use to sample dependency calls.</param>
        public RequestsCollector(RequestsCollectorOptions options, ITracerFactory tracerFactory, ISampler sampler)
        {
            const string name = "Microsoft.AspNetCore";
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber<HttpRequest>(
                new Dictionary<string, Func<ITracerFactory, Func<HttpRequest, ISampler>, ListenerHandler<HttpRequest>>>()
                {
                    {
                        name, (t, s) =>
                        {
                            var version = typeof(RequestDelegate).Assembly.GetName().Version;
                            var tracer = tracerFactory.GetTracer(typeof(RequestsCollector).Namespace, $"semver:{version.Major}.{version.Minor}.{version.Revision}");
                            return new HttpInListener(name, tracer, s);
                        }
                    },
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
