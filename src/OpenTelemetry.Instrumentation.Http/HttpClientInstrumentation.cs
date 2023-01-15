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
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http
{
    /// <summary>
    /// HttpClient instrumentation.
    /// </summary>
    internal sealed class HttpClientInstrumentation : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        private readonly Func<string, object, object, bool> isEnabled = (activityName, obj1, obj2)
            => !activityName.Equals("System.Net.Http.HttpRequestOut");

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientInstrumentation"/> class.
        /// </summary>
        /// <param name="options">Configuration options for HTTP client instrumentation.</param>
        public HttpClientInstrumentation(HttpClientInstrumentationOptions options)
        {
            // For .NET7.0 activity will be created using activitySource.
            // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs
            // However, in case when activity creation returns null (due to sampling)
            // the framework will fall back to creating activity anyways due to active diagnostic source listener
            // To prevent this, isEnabled is implemented which will return false always
            // so that the sampler's decision is respected.
            if (HttpHandlerDiagnosticListener.IsNet7OrGreater)
            {
                this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new HttpHandlerDiagnosticListener(options), this.isEnabled);
            }
            else
            {
                this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new HttpHandlerDiagnosticListener(options), null);
            }

            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber?.Dispose();
        }
    }
}
