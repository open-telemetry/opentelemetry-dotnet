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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs
{
    [ProviderAlias("OpenTelemetry")]
    public class OpenTelemetryLoggerProvider : BaseProvider, ILoggerProvider, ISupportExternalScope
    {
        internal readonly OpenTelemetryLoggerOptions Options;
        internal BaseProcessor<LogRecord> Processor;
        internal Resource Resource;
        private readonly Hashtable loggers = new Hashtable();
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
            this.Options = options ?? throw new ArgumentNullException(nameof(options));

            this.Resource = options.ResourceBuilder.Build();

            foreach (var processor in options.Processors)
            {
                this.AddProcessor(processor);
            }
        }

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
            if (!(this.loggers[categoryName] is OpenTelemetryLogger logger))
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
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            processor.SetParentProvider(this);

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
                this.Processor = new CompositeProcessor<LogRecord>(new[]
                {
                    this.Processor,
                    processor,
                });
            }

            return this;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                // Wait for up to 5 seconds grace period
                this.Processor?.Shutdown(5000);
                this.Processor?.Dispose();
            }

            this.disposed = true;
        }
    }
}
