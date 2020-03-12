// <copyright file="SqlClientCollector.cs" company="OpenTelemetry Authors">
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
    /// SqlClient collector.
    /// </summary>
    public class SqlClientCollector : IDisposable
    {
        internal const string SqlClientDiagnosticListenerName = "SqlClientDiagnosticListener";

        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlClientCollector"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        public SqlClientCollector(Tracer tracer)
            : this(tracer, new SqlClientCollectorOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlClientCollector"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        /// <param name="options">Configuration options for sql collector.</param>
        public SqlClientCollector(Tracer tracer, SqlClientCollectorOptions options)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
               name => new SqlClientDiagnosticListener(name, tracer, options),
               listener => listener.Name == SqlClientDiagnosticListenerName,
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
