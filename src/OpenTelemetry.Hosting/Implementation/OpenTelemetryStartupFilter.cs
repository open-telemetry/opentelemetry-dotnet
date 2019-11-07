// <copyright file="OpenTelemetryStartupFilter.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Implementation
{
    internal class OpenTelemetryStartupFilter : IStartupFilter
    {
        private readonly ILogger<OpenTelemetryStartupFilter> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryStartupFilter"/> class.
        /// </summary>
        /// <param name="logger">Instance of ILogger.</param>
        public OpenTelemetryStartupFilter(ILogger<OpenTelemetryStartupFilter> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                try
                {
                    // Attempting to resolve TracerFactory triggers configuration of all monitoring
                    var tc = app.ApplicationServices.GetService<TracerFactory>();
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(0, ex, "Failed to resolve TracerFactory.");
                }

                // Invoking next builder is not wrapped in try catch to ensure any exceptions gets propogated up.
                next(app);
            };
        }
    }
}
