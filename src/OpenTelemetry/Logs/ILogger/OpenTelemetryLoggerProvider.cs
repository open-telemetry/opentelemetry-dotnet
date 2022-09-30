// <copyright file="OpenTelemetryLoggerProvider.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> implementation for exporting logs using OpenTelemetry.
    /// </summary>
    [ProviderAlias("OpenTelemetry")]
    public class OpenTelemetryLoggerProvider : BaseProvider, ILoggerProvider, ISupportExternalScope
    {
        internal readonly LoggerProvider Provider;
        private readonly bool ownsProvider;
        private readonly Hashtable loggers = new();
        private bool disposed;

        static OpenTelemetryLoggerProvider()
        {
            // Accessing Sdk class is just to trigger its static ctor,
            // which sets default Propagators and default Activity Id format
            _ = Sdk.SuppressInstrumentation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryLoggerProvider"/> class.
        /// </summary>
        /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
        [Obsolete("Use the Sdk.CreateLoggerProviderBuilder method instead this ctor will be removed in a future version.")]
        public OpenTelemetryLoggerProvider(IOptionsMonitor<OpenTelemetryLoggerOptions> options)
        {
            Guard.ThrowIfNull(options);

            var currentOptions = options.CurrentValue;

            this.SetOptions(currentOptions);

            this.Provider = Sdk
                .CreateLoggerProviderBuilder()
                .ConfigureBuilder((sp, builder) =>
                {
                    if (currentOptions.ResourceBuilder != null)
                    {
                        builder.SetResourceBuilder(currentOptions.ResourceBuilder);
                    }

                    foreach (var processor in currentOptions.Processors)
                    {
                        builder.AddProcessor(processor);
                    }
                })
                .Build();

            this.ownsProvider = true;
        }

        internal OpenTelemetryLoggerProvider(
            OpenTelemetryLoggerOptions options,
            LoggerProvider loggerProvider,
            bool disposeProvider)
        {
            Guard.ThrowIfNull(loggerProvider);

            this.SetOptions(options);

            this.Provider = loggerProvider;
            this.ownsProvider = disposeProvider;
        }

        internal bool IncludeFormattedMessage { get; private set; }

        internal bool IncludeScopes { get; private set; }

        internal bool IncludeState { get; private set; }

        internal bool IncludeTraceState { get; private set; }

        internal bool ParseStateValues { get; private set; }

        internal IExternalScopeProvider? ScopeProvider { get; private set; }

        /// <inheritdoc/>
        void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            this.ScopeProvider = scopeProvider;

            lock (this.loggers)
            {
                foreach (DictionaryEntry entry in this.loggers)
                {
                    if (entry.Value is OpenTelemetryLogger logger)
                    {
                        logger.ScopeProvider = scopeProvider;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName)
        {
            if (this.loggers[categoryName] is not OpenTelemetryLogger logger)
            {
                lock (this.loggers)
                {
                    logger = (this.loggers[categoryName] as OpenTelemetryLogger)!;
                    if (logger == null)
                    {
                        logger = new OpenTelemetryLogger(categoryName, this)
                        {
                            ScopeProvider = this.ScopeProvider,
                        };

                        this.loggers[categoryName] = logger;
                    }
                }
            }

            return logger;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.ownsProvider)
                    {
                        this.Provider.Dispose();
                    }
                }

                this.disposed = true;
            }
        }

        private void SetOptions(OpenTelemetryLoggerOptions options)
        {
            Guard.ThrowIfNull(options);

            this.IncludeFormattedMessage = options.IncludeFormattedMessage;
            this.IncludeScopes = options.IncludeScopes;
            this.IncludeState = options.IncludeState;
            this.IncludeTraceState = options.IncludeTraceState;
            this.ParseStateValues = options.ParseStateValues;
        }
    }
}
