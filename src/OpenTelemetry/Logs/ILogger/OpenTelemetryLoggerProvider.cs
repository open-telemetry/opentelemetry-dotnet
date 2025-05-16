// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    private readonly Hashtable loggers = [];
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

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        var optionsInstance = options.CurrentValue;
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1

        this.Provider = Sdk
            .CreateLoggerProviderBuilder()
            .ConfigureBuilder((sp, builder) =>
            {
                if (optionsInstance.ResourceBuilder != null)
                {
                    builder.SetResourceBuilder(optionsInstance.ResourceBuilder);
                }

                foreach (var processorFactory in optionsInstance.ProcessorFactories)
                {
                    builder.AddProcessor(processorFactory);
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
#pragma warning disable CA1033 // Interface methods should be callable by child types
    void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider scopeProvider)
#pragma warning restore CA1033 // Interface methods should be callable by child types
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
        // Lock-free reading leveraging Hashtable's thread safety feature.
        // https://learn.microsoft.com/dotnet/api/system.collections.hashtable#thread-safety

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
