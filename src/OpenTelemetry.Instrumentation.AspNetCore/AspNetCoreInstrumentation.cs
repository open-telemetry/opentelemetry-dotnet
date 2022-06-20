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
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        private readonly Func<string, object, object, bool> isEnabled = (activityName, obj1, obj2) =>
        {
            if (activityName.Equals("Microsoft.AspNetCore.Hosting.HttpRequestIn"))
            {
                return false;
            }

            return true;
        };

        public AspNetCoreInstrumentation(HttpInListener httpInListener)
        {
            // For .NET7.0 activity will be created using activitySource.
            // https://github.com/dotnet/aspnetcore/blob/main/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L327
            // However, in case when activity creation returns null (due to sampling)
            // the framework will fall back to creating activity anyways due to active diagnostic source listener
            // To prevent this, isEnabled is implemented which will return false always
            // so that the sampler's decision is respected.
            if (HttpInListener.IsNet7OrGreater)
            {
                this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(httpInListener, this.isEnabled);
            }
            else
            {
                this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(httpInListener, null);
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
