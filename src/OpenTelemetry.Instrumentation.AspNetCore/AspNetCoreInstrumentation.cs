// <copyright file="AspNetCoreInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;

namespace OpenTelemetry.Instrumentation.AspNetCore
{
    /// <summary>
    /// Asp.Net Core Requests instrumentation.
    /// </summary>
    internal class AspNetCoreInstrumentation : IDisposable
    {
        internal const string OnStartEvent = "start";
        internal const string OnStopEvent = "stop";
        internal const string OnMvcBeforeAction = "error";
        internal const string OnUnhandledHostingExceptionEvent = "exception";
        internal const string OnUnHandledDiagnosticsExceptionEvent = "exception1";

        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        public AspNetCoreInstrumentation(HttpInListener httpInListener)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(httpInListener, null);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
