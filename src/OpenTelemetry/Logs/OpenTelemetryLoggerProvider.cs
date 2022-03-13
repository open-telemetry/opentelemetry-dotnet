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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs
{
    [ProviderAlias("OpenTelemetry")]
    public class OpenTelemetryLoggerProvider : BaseProvider, ILoggerProvider, ISupportExternalScope
    {
        internal readonly OpenTelemetryLoggerOptions Options;
        internal Resource Resource;
        private readonly Hashtable loggers = new(StringComparer.OrdinalIgnoreCase);
        private LinkedListNode head;
        private LinkedListNode tail;
        private bool disposed;
        private IExternalScopeProvider scopeProvider;

        static OpenTelemetryLoggerProvider()
        {
            // Accessing Sdk class is just to trigger its static ctor,
            // which sets default Propagators and default Activity Id format
            _ = Sdk.SuppressInstrumentation;
        }

        public OpenTelemetryLoggerProvider(IOptionsMonitor<OpenTelemetryLoggerOptions> options)
            : this(options?.CurrentValue)
        {
        }

        internal OpenTelemetryLoggerProvider(OpenTelemetryLoggerOptions options)
        {
            Guard.ThrowIfNull(options);

            this.Options = options;
            this.Resource = options.ResourceBuilder.Build();

            foreach (var processor in options.Processors)
            {
                if (processor is BaseProcessor<LogRecord> legacyProcessor)
                {
                    this.AddProcessor(legacyProcessor);
                }
                else if (processor is IInlineLogProcessor inlineLogProcessor)
                {
                    this.AddProcessor(inlineLogProcessor);
                }
                else
                {
                    // TODO: Log event, invalid processor type.
                }
            }
        }

        internal IInlineLogProcessor Processor => this.head?.Value;

        void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            this.scopeProvider = scopeProvider;

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

        public ILogger CreateLogger(string categoryName)
        {
            if (this.loggers[categoryName] is not OpenTelemetryLogger logger)
            {
                lock (this.loggers)
                {
                    logger = this.loggers[categoryName] as OpenTelemetryLogger;
                    if (logger == null)
                    {
                        logger = new OpenTelemetryLogger(categoryName, this)
                        {
                            ScopeProvider = this.scopeProvider,
                        };

                        this.loggers[categoryName] = logger;
                    }
                }
            }

            return logger;
        }

        internal OpenTelemetryLoggerProvider AddProcessor(BaseProcessor<LogRecord> processor)
        {
            Guard.ThrowIfNull(processor);

            return this.AddProcessor(new InlineLogProcessor<LogRecord>(BuildLegacyLogRecord, processor));
        }

        internal OpenTelemetryLoggerProvider AddProcessor(IInlineLogProcessor processor)
        {
            Guard.ThrowIfNull(processor);

            processor.SetParentProvider(this);

            var node = new LinkedListNode(processor);
            if (this.head == null)
            {
                this.head = node;
                this.tail = node;
            }
            else
            {
                this.tail.Next = node;
                this.tail = node;
            }

            return this;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // Wait for up to 5 seconds grace period
                    this.ShutdownProcessors(5000);
                    this.DisposeProcessors();
                }

                this.disposed = true;
                OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(OpenTelemetryLoggerProvider));
            }

            base.Dispose(disposing);
        }

        private static LogRecord BuildLegacyLogRecord(
            in ActivityContext activityContext,
            string categoryName,
            DateTime timestamp,
            LogLevel logLevel,
            EventId eventId,
            object state,
            IReadOnlyList<KeyValuePair<string, object>> parsedState,
            IExternalScopeProvider scopeProvider,
            Exception exception,
            string formattedLogMessage)
        {
            return new LogRecord(
                in activityContext,
                scopeProvider,
                timestamp,
                categoryName,
                logLevel,
                eventId,
                formattedLogMessage,
                state,
                exception,
                parsedState);
        }

        private void ShutdownProcessors(int timeoutMilliseconds)
        {
            var sw = timeoutMilliseconds == Timeout.Infinite
                ? null
                : Stopwatch.StartNew();

            for (var cur = this.head; cur != null; cur = cur.Next)
            {
                if (sw == null)
                {
                    cur.Value.Shutdown(Timeout.Infinite);
                }
                else
                {
                    var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                    // notify all the processors, even if we run overtime
                    cur.Value.Shutdown((int)Math.Max(timeout, 0));
                }
            }
        }

        private void DisposeProcessors()
        {
            for (var cur = this.head; cur != null; cur = cur.Next)
            {
                try
                {
                    cur.Value?.Dispose();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                }
            }
        }

        private class LinkedListNode
        {
            public readonly IInlineLogProcessor Value;

            public LinkedListNode(IInlineLogProcessor value)
            {
                this.Value = value;
            }

            public LinkedListNode Next { get; set; }
        }
    }
}
