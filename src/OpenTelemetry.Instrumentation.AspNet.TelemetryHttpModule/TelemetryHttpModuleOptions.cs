// <copyright file="TelemetryHttpModuleOptions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Stores options for the <see cref="TelemetryHttpModule"/>.
    /// </summary>
    public class TelemetryHttpModuleOptions
    {
        private TextMapPropagator textMapPropagator = new TraceContextPropagator();

        internal TelemetryHttpModuleOptions()
        {
        }

        /// <summary>
        /// Gets or sets the <see cref=" Context.Propagation.TextMapPropagator"/> to use to
        /// extract <see cref="PropagationContext"/> from incoming requests.
        /// </summary>
        public TextMapPropagator TextMapPropagator
        {
            get => this.textMapPropagator;
            set
            {
                Guard.ThrowIfNull(value);

                this.textMapPropagator = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback action to be fired when a request is started.
        /// </summary>
        public Action<Activity, HttpContext> OnRequestStartedCallback { get; set; }

        /// <summary>
        /// Gets or sets a callback action to be fired when a request is stopped.
        /// </summary>
        public Action<Activity, HttpContext> OnRequestStoppedCallback { get; set; }

        /// <summary>
        /// Gets or sets a callback action to be fired when an unhandled
        /// exception is thrown processing a request.
        /// </summary>
        public Action<Activity, HttpContext, Exception> OnExceptionCallback { get; set; }
    }
}
