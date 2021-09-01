// <copyright file="HttpClientMetrics.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http
{
    /// <summary>
    /// HttpClient instrumentation.
    /// </summary>
    internal class HttpClientMetrics : IDisposable
    {
        internal static readonly AssemblyName AssemblyName = typeof(HttpClientMetrics).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;
        private readonly Meter meter;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientMetrics"/> class.
        /// </summary>
        public HttpClientMetrics()
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new HttpHandlerMetricsDiagnosticListener("HttpHandlerDiagnosticListener", this.meter), null);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
            this.meter?.Dispose();
        }
    }
}
