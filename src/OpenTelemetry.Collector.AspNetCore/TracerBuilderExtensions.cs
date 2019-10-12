// <copyright file="TracerBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Collector.AspNetCore
{
    public static class TracerBuilderExtensions
    {
        public static TracerBuilder AddRequestCollector(this TracerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddCollector(t => new AspNetCoreCollector(t));
        }

        public static TracerBuilder AddRequestCollector(this TracerBuilder builder, Action<AspNetCoreCollectorOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new AspNetCoreCollectorOptions();
            configure(options);

            return builder.AddCollector(t => new AspNetCoreCollector(t, options));
        }
    }
}
