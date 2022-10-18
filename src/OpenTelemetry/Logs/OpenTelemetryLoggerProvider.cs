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
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> implementation for exporting logs using OpenTelemetry.
    /// </summary>
    [ProviderAlias("OpenTelemetry")]
    public class OpenTelemetryLoggerProvider : BaseProvider, ILoggerProvider, ISupportExternalScope
    {
        internal readonly bool IncludeScopes;
        internal readonly bool IncludeFormattedMessage;
        internal readonly bool ParseStateValues;
        internal BaseProcessor<LogRecord>? Processor;
        internal Resource Resource;
        private readonly Hashtable loggers = new();
        private ILogRecordPool? threadStaticPool = LogRecordThreadStaticPool.Instance;
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
        public OpenTelemetryLoggerProvider(IOptionsMonitor<OpenTelemetryLoggerOptions> options)
            : this(options?.CurrentValue ?? throw new ArgumentNullException(nameof(options)))
        {
        }

        internal OpenTelemetryLoggerProvider()
            : this(new OpenTelemetryLoggerOptions())
        {
        }

        internal OpenTelemetryLoggerProvider(Action<OpenTelemetryLoggerOptions> configure)
            : this(BuildOptions(configure))
        {
        }

        internal OpenTelemetryLoggerProvider(OpenTelemetryLoggerOptions options)
        {
            OpenTelemetrySdkEventSource.Log.OpenTelemetryLoggerProviderEvent("Building OpenTelemetryLoggerProvider.");

            Guard.ThrowIfNull(options);

            this.IncludeScopes = options.IncludeScopes;
            this.IncludeFormattedMessage = options.IncludeFormattedMessage;
            this.ParseStateValues = options.ParseStateValues;

            this.Resource = options.ResourceBuilder.Build();

            foreach (var processor in options.Processors)
            {
                this.AddProcessor(processor);
            }

            OpenTelemetrySdkEventSource.Log.OpenTelemetryLoggerProviderEvent("OpenTelemetryLoggerProvider built successfully.");
        }

        internal IExternalScopeProvider? ScopeProvider { get; private set; }

        internal ILogRecordPool LogRecordPool => this.threadStaticPool ?? LogRecordSharedPool.Current;

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

        /// <summary>
        /// Flushes all the processors registered under <see
        /// cref="OpenTelemetryLoggerProvider"/>, blocks the current thread
        /// until flush completed, shutdown signaled or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when force flush succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety.
        /// </remarks>
        internal bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
        {
            OpenTelemetrySdkEventSource.Log.OpenTelemetryLoggerProviderForceFlushInvoked(timeoutMilliseconds);
            return this.Processor?.ForceFlush(timeoutMilliseconds) ?? true;
        }

        /// <summary>
        /// Add a processor to the <see cref="OpenTelemetryLoggerProvider"/>.
        /// </summary>
        /// <remarks>
        /// Note: The supplied <paramref name="processor"/> will be
        /// automatically disposed when then the <see
        /// cref="OpenTelemetryLoggerProvider"/> is disposed.
        /// </remarks>
        /// <param name="processor">Log processor to add.</param>
        /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        internal OpenTelemetryLoggerProvider AddProcessor(BaseProcessor<LogRecord> processor)
        {
            OpenTelemetrySdkEventSource.Log.OpenTelemetryLoggerProviderEvent("Started adding processor.");

            Guard.ThrowIfNull(processor);

            processor.SetParentProvider(this);

            StringBuilder processorAdded = new StringBuilder();

            if (this.threadStaticPool != null && this.ContainsBatchProcessor(processor))
            {
                OpenTelemetrySdkEventSource.Log.OpenTelemetryLoggerProviderEvent("Using shared thread pool.");

                this.threadStaticPool = null;
            }

            if (this.Processor == null)
            {
                processorAdded.Append("Setting processor to ");
                processorAdded.Append(processor);

                this.Processor = processor;
            }
            else if (this.Processor is CompositeProcessor<LogRecord> compositeProcessor)
            {
                processorAdded.Append("Adding processor ");
                processorAdded.Append(processor);
                processorAdded.Append(" to composite processor");

                compositeProcessor.AddProcessor(processor);
            }
            else
            {
                processorAdded.Append("Creating new composite processor with processor ");
                processorAdded.Append(this.Processor);
                processorAdded.Append(" and adding new processor ");
                processorAdded.Append(processor);

                var newCompositeProcessor = new CompositeProcessor<LogRecord>(new[]
                {
                    this.Processor,
                });
                newCompositeProcessor.SetParentProvider(this);
                newCompositeProcessor.AddProcessor(processor);
                this.Processor = newCompositeProcessor;
            }

            OpenTelemetrySdkEventSource.Log.OpenTelemetryLoggerProviderEvent($"Completed adding processor = \"{processorAdded}\".");

            return this;
        }

        internal bool ContainsBatchProcessor(BaseProcessor<LogRecord> processor)
        {
            if (processor is BatchExportProcessor<LogRecord>)
            {
                return true;
            }
            else if (processor is CompositeProcessor<LogRecord> compositeProcessor)
            {
                var current = compositeProcessor.Head;
                while (current != null)
                {
                    if (this.ContainsBatchProcessor(current.Value))
                    {
                        return true;
                    }

                    current = current.Next;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // Wait for up to 5 seconds grace period
                    this.Processor?.Shutdown(5000);
                    this.Processor?.Dispose();
                }

                this.disposed = true;
                OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(OpenTelemetryLoggerProvider));
            }

            base.Dispose(disposing);
        }

        private static OpenTelemetryLoggerOptions BuildOptions(Action<OpenTelemetryLoggerOptions> configure)
        {
            var options = new OpenTelemetryLoggerOptions();
            configure?.Invoke(options);
            return options;
        }
    }
}
