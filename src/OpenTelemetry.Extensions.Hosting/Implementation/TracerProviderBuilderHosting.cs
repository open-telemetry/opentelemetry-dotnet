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
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    internal sealed class TracerProviderBuilderHosting : TracerProviderBuilderBase
    {
        public TracerProviderBuilderHosting(IServiceCollection services)
            : base(services)
        {
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            Guard.ThrowIfNull(serviceProvider);

            this.ServiceProvider = serviceProvider;
        }

        protected override TracerProvider OnBuild()
        {
            if (this.ServiceProvider == null)
            {
                throw new NotSupportedException("Build cannot be called directly on TracerProviderBuilder instances created through the AddOpenTelemetryTracing extension.");
            }

            return base.OnBuild();
        }
    }
}
