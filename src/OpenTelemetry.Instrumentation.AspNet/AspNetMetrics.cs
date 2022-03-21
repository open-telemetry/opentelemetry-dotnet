// <copyright file="AspNetMetrics.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Instrumentation.AspNet.Implementation;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Asp.Net Requests instrumentation.
    /// </summary>
    internal class AspNetMetrics : IDisposable
    {
        internal static readonly AssemblyName AssemblyName = typeof(HttpInMetricsListener).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

        private readonly Meter meter;

        private readonly HttpInMetricsListener httpInMetricsListener;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetMetrics"/> class.
        /// </summary>
        public AspNetMetrics()
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
            this.httpInMetricsListener = new HttpInMetricsListener(this.meter);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.meter?.Dispose();
            this.httpInMetricsListener?.Dispose();
        }
    }
}
