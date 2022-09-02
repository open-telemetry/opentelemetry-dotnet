// <copyright file="OpenTelemetryLoggingExtensions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Contains extension methods for registering <see cref="OpenTelemetryLoggerProvider"/> into a <see cref="ILoggingBuilder"/> instance.
    /// </summary>
    public static class OpenTelemetryLoggingExtensions
    {
        /// <summary>
        /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks>
        /// Note: This is safe to be called more than once and should be used by
        /// library authors to ensure at least one <see
        /// cref="OpenTelemetryLoggerProvider"/> is registered.
        /// </remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
        public static ILoggingBuilder AddOpenTelemetry(this ILoggingBuilder builder)
            => AddOpenTelemetry(builder, configure: null);

        /// <summary>
        /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks>
        /// Note: This is should only be called once during application
        /// bootstrap for a given <see cref="IServiceCollection"/>. This should
        /// not be used by library authors.
        /// </remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <param name="configure">Optional configuration action.</param>
        /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
        public static ILoggingBuilder AddOpenTelemetry(this ILoggingBuilder builder, Action<OpenTelemetryLoggerOptions>? configure)
        {
            Guard.ThrowIfNull(builder);

            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, OpenTelemetryLoggerProvider>(sp =>
            {
                var registeredBuilders = sp.GetServices<TrackedOpenTelemetryLoggerOptions>();
                if (registeredBuilders.Count() > 1)
                {
                    throw new NotSupportedException("Multiple logger provider builders cannot be registered in the same service collection.");
                }

                var finalOptions = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

                return new OpenTelemetryLoggerProvider(finalOptions, sp, ownsServiceProvider: false);
            }));

            // Note: This will bind logger options element (eg "Logging:OpenTelemetry") to OpenTelemetryLoggerOptions
            LoggerProviderOptions.RegisterProviderOptions<OpenTelemetryLoggerOptions, OpenTelemetryLoggerProvider>(builder.Services);

            if (configure != null)
            {
                /*
                 * We do a two-phase configuration here.
                 *
                 * Step 1: Configure callback is first invoked immediately. This
                 * is to make "Services" available for extension authors to
                 * register additional dependencies into the collection if
                 * needed.
                 */

                var options = new OpenTelemetryLoggerOptions(builder.Services);

                configure(options);

                builder.Services.AddSingleton(new TrackedOpenTelemetryLoggerOptions(options));

                /*
                 * Step 2: When ServiceProvider is built from "Services" and the
                 * LoggerFactory is created then the options pipeline runs and
                 * builds a new OpenTelemetryLoggerOptions from configuration
                 * and callbacks are executed. "Services" can no longer be
                 * modified in this phase because the ServiceProvider is already
                 * complete. We apply the inline options to the final instance
                 * to bridge this gap.
                 */

                builder.Services.Configure<OpenTelemetryLoggerOptions>(finalOptions =>
                {
                    options.ApplyTo(finalOptions);
                });
            }

            return builder;
        }

        /// <summary>
        /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <remarks>
        /// Note: The supplied <see cref="OpenTelemetryLoggerProvider"/> will
        /// automatically be disposed when the <see cref="ILoggerFactory"/>
        /// built from <paramref name="builder"/> is disposed.
        /// </remarks>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <param name="openTelemetryLoggerProvider"><see cref="OpenTelemetryLoggerProvider"/>.</param>
        /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
        public static ILoggingBuilder AddOpenTelemetry(this ILoggingBuilder builder, OpenTelemetryLoggerProvider openTelemetryLoggerProvider)
            => AddOpenTelemetry(builder, openTelemetryLoggerProvider, disposeProvider: true);

        /// <summary>
        /// Adds an OpenTelemetry logger named 'OpenTelemetry' to the <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <param name="openTelemetryLoggerProvider"><see cref="OpenTelemetryLoggerProvider"/>.</param>
        /// <param name="disposeProvider">Controls whether or not the supplied
        /// <paramref name="openTelemetryLoggerProvider"/> will be disposed when
        /// the <see cref="ILoggerFactory"/> is disposed.</param>
        /// <returns>The supplied <see cref="ILoggingBuilder"/> for call chaining.</returns>
        public static ILoggingBuilder AddOpenTelemetry(
            this ILoggingBuilder builder,
            OpenTelemetryLoggerProvider openTelemetryLoggerProvider,
            bool disposeProvider)
        {
            Guard.ThrowIfNull(builder);
            Guard.ThrowIfNull(openTelemetryLoggerProvider);

            // Note: Currently if multiple OpenTelemetryLoggerProvider instances
            // are added to the same ILoggingBuilder everything after the first
            // is silently ignored.

            if (disposeProvider)
            {
                builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, OpenTelemetryLoggerProvider>(sp => openTelemetryLoggerProvider));
            }
            else
            {
                builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider>(openTelemetryLoggerProvider));
            }

            return builder;
        }

        private sealed class TrackedOpenTelemetryLoggerOptions
        {
            public TrackedOpenTelemetryLoggerOptions(OpenTelemetryLoggerOptions options)
            {
                this.Options = options;
            }

            public OpenTelemetryLoggerOptions Options { get; }
        }
    }
}
