// <copyright file="AzureClientsAdapter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Adapter.Dependencies
{
    /// <summary>
    /// Dependencies adapter.
    /// </summary>
    public class AzureClientsAdapter : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureClientsAdapter"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        public AzureClientsAdapter(Tracer tracer)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
                name => new AzureSdkDiagnosticListener(name, tracer),
                listener => listener.Name.StartsWith("Azure."),
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
