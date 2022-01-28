// <copyright file="AspNetInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.AspNet.Implementation;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Asp.Net Requests instrumentation.
    /// </summary>
    internal sealed class AspNetInstrumentation : IDisposable
    {
        private readonly HttpInListener httpInListener;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetInstrumentation"/> class.
        /// </summary>
        /// <param name="options">Configuration options for ASP.NET instrumentation.</param>
        public AspNetInstrumentation(AspNetInstrumentationOptions options)
        {
            this.httpInListener = new HttpInListener(options);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.httpInListener?.Dispose();
        }
    }
}
