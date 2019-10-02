// <copyright file="OpenTelemetryBuilderExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Hosting
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using OpenTelemetry.Hosting.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    public static class OpenTelemetryBuilderExtensions
    {
        public static IOpenTelemetryBuilder AddCollector<TCollector>(this IOpenTelemetryBuilder builder)
            where TCollector : class
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.TryAddSingleton<TCollector>();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CollectorHostingService<TCollector>>());
            return builder;
        }

        public static IOpenTelemetryBuilder AddCollector<TCollector, TCollectorOptions>(this IOpenTelemetryBuilder builder)
            where TCollector : class
            where TCollectorOptions : class
        {
            builder.AddCollector<TCollector>();
            builder.Services.TryAddSingleton<TCollectorOptions>();
            return builder;
        }

        public static IOpenTelemetryBuilder AddCollector<TCollector>(this IOpenTelemetryBuilder builder, object options)
            where TCollector : class
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.AddCollector<TCollector>();
            builder.Services.TryAdd(ServiceDescriptor.Singleton(options.GetType(), options));
            return builder;
        }

        public static IOpenTelemetryBuilder SetSpanExporter<TExporter>(this IOpenTelemetryBuilder builder)
            where TExporter : SpanExporter
        {
            builder.Services.TryAddSingleton<SpanExporter, TExporter>();
            return builder;
        }

        public static IOpenTelemetryBuilder SetSpanExporter<TExporter>(this IOpenTelemetryBuilder builder, object options)
            where TExporter : SpanExporter
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.SetSpanExporter<TExporter>();
            builder.Services.TryAdd(ServiceDescriptor.Singleton(options.GetType(), options));
            return builder;
        }

        public static IOpenTelemetryBuilder SetTracer<TTracer>(this IOpenTelemetryBuilder builder)
            where TTracer : class, ITracer
        {
            builder.Services.TryAddSingleton<ITracer, TTracer>();
            return builder;
        }

        public static IOpenTelemetryBuilder SetTracer<TTracer>(this IOpenTelemetryBuilder builder, object options)
            where TTracer : class, ITracer
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.SetTracer<TTracer>();
            builder.Services.TryAdd(ServiceDescriptor.Singleton(options.GetType(), options));
            return builder;
        }

        public static IOpenTelemetryBuilder SetTracer<TTracer, TTracerOptions>(this IOpenTelemetryBuilder builder)
            where TTracer : class, ITracer
            where TTracerOptions : class
        {
            builder.SetTracer<TTracer>();
            builder.Services.TryAddSingleton<TTracerOptions>();
            return builder;
        }
    }
}
