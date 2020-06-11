// <copyright file="HttpClientInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.Dependencies.Implementation;

namespace OpenTelemetry.Instrumentation.Dependencies
{
    /// <summary>
    /// Dependencies instrumentation.
    /// </summary>
    public class HttpClientInstrumentation : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientInstrumentation"/> class.
        /// </summary>
        public HttpClientInstrumentation()
            : this(new HttpClientInstrumentationOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientInstrumentation"/> class.
        /// </summary>
        /// <param name="options">Configuration options for dependencies instrumentation.</param>
        public HttpClientInstrumentation(HttpClientInstrumentationOptions options)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new HttpHandlerDiagnosticListener(options), options.EventFilter);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
