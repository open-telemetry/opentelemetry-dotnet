// <copyright file="OpenTelemetryServicesExtensions.cs" company="OpenTelemetry Authors">
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

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using OpenTelemetry.Hosting;
    using OpenTelemetry.Hosting.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;

    public static class OpenTelemetryServicesExtensions
    {
        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services)
        {
            return AddOpenTelemetry(services, builder => { });
        }

        public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Action<IOpenTelemetryBuilder> configure)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            configure(new OpenTelemetryBuilder(services));

            services.TryAddSingleton<SpanExporter>(new NoopSpanExporter());
            services.TryAddSingleton<ISampler>(Samplers.AlwaysSample);
            services.TryAddSingleton<SpanProcessor, BatchingSpanProcessor>();
            services.TryAddSingleton<ITracer>(NoopTracer.Instance);

            return services;
        }

        /// <inheritdoc />
        internal sealed class NoopSpanExporter : SpanExporter
        {
            /// <inheritdoc />
            public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
            {
                return Task.FromResult(ExportResult.Success);
            }

            /// <inheritdoc />
            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
