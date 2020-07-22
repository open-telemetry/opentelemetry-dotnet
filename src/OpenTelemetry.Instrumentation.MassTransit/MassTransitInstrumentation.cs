// <copyright file="MassTransitInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.MassTransit.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MassTransit
{
    internal class MassTransitInstrumentation : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="MassTransitInstrumentation"/> class.
        /// </summary>
        /// <param name="activitySource">ActivitySource adapter instance.</param>
        public MassTransitInstrumentation(ActivitySourceAdapter activitySource)
            : this(activitySource, new MassTransitInstrumentationOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MassTransitInstrumentation"/> class.
        /// </summary>
        /// <param name="activitySource">ActivitySource adapter instance.</param>
        /// <param name="options">Instrumentation options.</param>
        public MassTransitInstrumentation(ActivitySourceAdapter activitySource, MassTransitInstrumentationOptions options)
        {
            var diagnosticListener = new MassTransitDiagnosticListener(activitySource, options);
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(diagnosticListener, null);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
