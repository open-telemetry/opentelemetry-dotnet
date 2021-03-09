// <copyright file="TracerProviderBuilderHosting.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Trace
{
    internal class TracerProviderBuilderHosting : TracerProviderBuilderSdk, IResolvingTracerProviderBuilder
    {
        private readonly IServiceProvider serviceProvider;

        public TracerProviderBuilderHosting(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public TracerProviderBuilder AddProcessor<T>()
            where T : BaseProcessor<Activity>
        {
            return this.AddProcessor(this.serviceProvider.GetRequiredService<T>());
        }

        public TracerProviderBuilder SetSampler<T>()
            where T : Sampler
        {
            return this.SetSampler(this.serviceProvider.GetRequiredService<T>());
        }

        public T ResolveService<T>()
        {
            return this.serviceProvider.GetRequiredService<T>();
        }

        public T ResolveOptions<T>()
            where T : class, new()
        {
            return this.serviceProvider.GetRequiredService<IOptions<T>>().Value;
        }
    }
}
