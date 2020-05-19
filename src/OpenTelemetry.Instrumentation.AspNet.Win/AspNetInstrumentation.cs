// <copyright file="AspNetInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.AspNet.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Requests instrumentation.
    /// </summary>
    public class AspNetInstrumentation : IDisposable
    {
        internal const string AspNetDiagnosticListenerName = "Microsoft.AspNet.TelemetryCorrelation";

        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetInstrumentation"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        public AspNetInstrumentation(Tracer tracer)
            : this(tracer, new AspNetInstrumentationOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetInstrumentation"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        /// <param name="options">Configuration options for ASP.NET instrumentation.</param>
        public AspNetInstrumentation(Tracer tracer, AspNetInstrumentationOptions options)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
                name => new HttpInListener(name, tracer, options),
                listener => listener.Name == AspNetDiagnosticListenerName,
                null);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
