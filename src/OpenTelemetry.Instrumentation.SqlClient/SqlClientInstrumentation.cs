// <copyright file="SqlClientInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.SqlClient.Implementation;

namespace OpenTelemetry.Instrumentation.SqlClient
{
    /// <summary>
    /// SqlClient instrumentation.
    /// </summary>
    internal class SqlClientInstrumentation : IDisposable
    {
        internal const string SqlClientDiagnosticListenerName = "SqlClientDiagnosticListener";

        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;
#if NETFRAMEWORK
        private readonly SqlEventSourceListener sqlEventSourceListener;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlClientInstrumentation"/> class.
        /// </summary>
        /// <param name="options">Configuration options for sql instrumentation.</param>
        public SqlClientInstrumentation(SqlClientInstrumentationOptions options = null)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
               name => new SqlClientDiagnosticListener(name, options),
               listener => listener.Name == SqlClientDiagnosticListenerName,
               null);
            this.diagnosticSourceSubscriber.Subscribe();

#if NETFRAMEWORK
            this.sqlEventSourceListener = new SqlEventSourceListener(options);
#endif
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
#if NETFRAMEWORK
            this.sqlEventSourceListener?.Dispose();
#endif
        }
    }
}
