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

using System.Collections;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

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
    // todo: [Obsolete("Use the Sdk.CreateLoggerProviderBuilder method instead this ctor will be removed in a future version.")]
    public OpenTelemetryLoggerProvider(IOptionsMonitor<OpenTelemetryLoggerOptions> options)
    {
        Guard.ThrowIfNull(options);

        var optionsInstance = options.CurrentValue;

        this.Provider = Sdk
            .CreateLoggerProviderBuilder()
            .ConfigureBuilder((sp, builder) =>
            {
                if (optionsInstance.ResourceBuilder != null)
                {
                    builder.SetResourceBuilder(optionsInstance.ResourceBuilder);
                }

                foreach (var processor in optionsInstance.Processors)
                {
                    builder.AddProcessor(processor);
                }
            })
            .Build();

        this.Options = optionsInstance.Copy();
        this.ownsProvider = true;
    }

    internal OpenTelemetryLoggerProvider(
        LoggerProvider loggerProvider,
        OpenTelemetryLoggerOptions options,
        bool disposeProvider)
    {
        Debug.Assert(loggerProvider != null, "loggerProvider was null");
        Debug.Assert(options != null, "options was null");

        this.Provider = loggerProvider!;
        this.Options = options!.Copy();
        this.ownsProvider = disposeProvider;
    }

    internal OpenTelemetryLoggerOptions Options { get; }

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
        if (this.loggers[categoryName] is not ILogger logger)
        {
            lock (this.loggers)
            {
                logger = (this.loggers[categoryName] as ILogger)!;
                if (logger == null)
                {
                    var loggerProviderSdk = this.Provider as LoggerProviderSdk;
                    if (loggerProviderSdk == null)
                    {
                        logger = NullLogger.Instance;
                    }
                    else
                    {
                        logger = new OpenTelemetryLogger(loggerProviderSdk, this.Options, categoryName)
                        {
                            ScopeProvider = this.ScopeProvider,
                        };
                    }

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
            OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(OpenTelemetryLoggerProvider));
        }

        base.Dispose(disposing);
    }
}
