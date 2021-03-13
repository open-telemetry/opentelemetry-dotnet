// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    public static class TracerProviderBuilderExtensions
    {
        public static TracerProviderBuilder AddInstrumentation<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : class
        {
            if (tracerProviderBuilder is TracerProviderBuilderHosting tracerProviderBuilderHosting)
            {
                tracerProviderBuilderHosting.AddInstrumentation<T>();
            }

            return tracerProviderBuilder;
        }

        public static TracerProviderBuilder AddProcessor<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : BaseProcessor<Activity>
        {
            if (tracerProviderBuilder is TracerProviderBuilderHosting tracerProviderBuilderHosting)
            {
                tracerProviderBuilderHosting.AddProcessor<T>();
            }

            return tracerProviderBuilder;
        }

        public static TracerProviderBuilder SetSampler<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : Sampler
        {
            if (tracerProviderBuilder is TracerProviderBuilderHosting tracerProviderBuilderHosting)
            {
                tracerProviderBuilderHosting.SetSampler<T>();
            }

            return tracerProviderBuilder;
        }

        public static TracerProviderBuilder Configure(this TracerProviderBuilder tracerProviderBuilder, Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            if (tracerProviderBuilder is IDeferredTracerBuilder deferredTracerBuilder)
            {
                deferredTracerBuilder.Configure(configure);
            }

            return tracerProviderBuilder;
        }
    }
}
