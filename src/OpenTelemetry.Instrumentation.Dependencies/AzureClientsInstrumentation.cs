// <copyright file="AzureClientsInstrumentation.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Dependencies
{
    /// <summary>
    /// AzureClients instrumentation.
    /// TODO: Azure specific listeners would be moved out of this repo.
    /// I believe this was initially put here for quick validation.
    /// There were no unit tests covering this feature, so
    /// cannot validate after Span is replaced with Activity.
    /// </summary>
    public class AzureClientsInstrumentation : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureClientsInstrumentation"/> class.
        /// </summary>
        public AzureClientsInstrumentation()
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
                name => new AzureSdkDiagnosticListener(name),
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
