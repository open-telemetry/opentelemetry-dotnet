// <copyright file="OpenTelemetryBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.MassTransit;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Extension methods to simplify registering of dependency instrumentation.
    /// </summary>
    public static class OpenTelemetryBuilderExtensions
    {
        /// <summary>
        /// Enables the outgoing requests automatic data collection for MassTransit.
        /// </summary>
        /// <param name="builder"><see cref="OpenTelemetryBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="OpenTelemetryBuilder"/> to chain the calls.</returns>
        public static OpenTelemetryBuilder AddMassTransitInstrumentation(
            this OpenTelemetryBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddInstrumentation(activitySource => new MassTransitInstrumentation(activitySource));
        }
    }
}
