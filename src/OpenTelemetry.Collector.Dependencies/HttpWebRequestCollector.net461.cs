// <copyright file="HttpWebRequestCollector.net461.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Collector.Dependencies.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector.Dependencies
{
    /// <summary>
    /// Dependencies collector.
    /// </summary>
    public class HttpWebRequestCollector : IDisposable
    {
#if NET461
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpWebRequestCollector"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        public HttpWebRequestCollector(Tracer tracer)
            : this(tracer, new HttpClientCollectorOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpWebRequestCollector"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        /// <param name="options">Configuration options for HttpWebRequest collector.</param>
        public HttpWebRequestCollector(Tracer tracer, HttpClientCollectorOptions options)
        {
#if NET461
            GC.KeepAlive(HttpWebRequestDiagnosticSource.Instance);

            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new HttpWebRequestDiagnosticListener(tracer, options), options.EventFilter);
            this.diagnosticSourceSubscriber.Subscribe();
#endif
        }

        /// <inheritdoc/>
        public void Dispose()
        {
#if NET461
            this.diagnosticSourceSubscriber?.Dispose();
#endif
        }
    }
}
