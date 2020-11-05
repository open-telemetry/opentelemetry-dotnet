// <copyright file="OwinInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.Owin.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Owin
{
    /// <summary>
    /// OWIN Requests instrumentation.
    /// </summary>
    internal class OwinInstrumentation : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwinInstrumentation"/> class.
        /// </summary>
        /// <param name="activitySource">ActivitySource adapter instance.</param>
        /// <param name="options">Configuration options for OWIN instrumentation.</param>
        public OwinInstrumentation(ActivitySourceAdapter activitySource, OwinInstrumentationOptions options)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new HttpInListener("OpenTelemetry.Extensions.Owin", options, activitySource), null);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
