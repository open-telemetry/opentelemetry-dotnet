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

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryLoggerProvider"/> class.
        /// </summary>
        /// <param name="configure"><see cref="OpenTelemetryLoggerOptions"/> configuration callback.</param>
        public OpenTelemetryLoggerProvider(Action<OpenTelemetryLoggerOptions> configure)
            : this(BuildOptions(configure ?? throw new ArgumentNullException(nameof(configure))))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryLoggerProvider"/> class.
        /// </summary>
        public OpenTelemetryLoggerProvider()
            : this(BuildOptions(configure: null))
        {
        }

        internal OpenTelemetryLoggerProvider(OpenTelemetryLoggerOptions options)
        {
            Guard.ThrowIfNull(options);

            this.IncludeScopes = options.IncludeScopes;
            this.IncludeFormattedMessage = options.IncludeFormattedMessage;
            this.ParseStateValues = options.ParseStateValues;

            this.Resource = options.ResourceBuilder.Build();

            foreach (var processor in options.Processors)
            {
                this.AddProcessor(processor);
            }
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
        public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
        {
            return this.Processor?.ForceFlush(timeoutMilliseconds) ?? true;
        }

        /// <summary>
        /// Create a <see cref="LogEmitter"/>.
        /// </summary>
        /// <returns><see cref="LogEmitter"/>.</returns>
        internal LogEmitter CreateEmitter() => new(this);

        internal OpenTelemetryLoggerProvider AddProcessor(BaseProcessor<LogRecord> processor)
        {
            Guard.ThrowIfNull(processor);

            processor.SetParentProvider(this);

            if (this.threadStaticPool != null && this.ContainsBatchProcessor(processor))
            {
                this.threadStaticPool = null;
            }

            if (this.Processor == null)
            {
                this.Processor = processor;
            }
            else if (this.Processor is CompositeProcessor<LogRecord> compositeProcessor)
            {
                compositeProcessor.AddProcessor(processor);
            }
            else
            {
                var newCompositeProcessor = new CompositeProcessor<LogRecord>(new[]
                {
                    this.Processor,
                });
                newCompositeProcessor.SetParentProvider(this);
                newCompositeProcessor.AddProcessor(processor);
                this.Processor = newCompositeProcessor;
            }

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

        private static OpenTelemetryLoggerOptions BuildOptions(Action<OpenTelemetryLoggerOptions>? configure)
        {
            OpenTelemetryLoggerOptions options = new();
            configure?.Invoke(options);
            return options;
        }
    }
}
