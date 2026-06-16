// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
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
    private readonly Lock syncObject = new();
    private readonly bool ownsProvider;
    private readonly Hashtable loggers = [];
    private Func<LoggerProvider>? loggerProviderFactory;
    private LoggerProvider? provider;
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

        this.provider = Sdk
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
        Func<LoggerProvider> loggerProviderFactory,
        OpenTelemetryLoggerOptions options,
        bool disposeProvider)
    {
        Guard.ThrowIfNull(loggerProviderFactory);

        this.loggerProviderFactory = loggerProviderFactory;
        this.Options = options.Copy();
        this.ownsProvider = disposeProvider;
    }

    internal LoggerProvider Provider
    {
        get
        {
            // Volatile.Read/Write are used to make the lock-free fast path
            // correct on weaker memory models, guaranteeing the provider
            // is fully constructed before it is observed by another thread.
            var provider = Volatile.Read(ref this.provider);
            if (provider != null)
            {
                return provider;
            }

            lock (this.syncObject)
            {
                provider = this.provider;
                if (provider == null)
                {
                    // If the factory throws (e.g. an invalid configuration)
                    // it is intentionally left in place so the next access
                    // retries instead of caching the failure.
                    provider = this.loggerProviderFactory!();
                    Volatile.Write(ref this.provider, provider);
                    this.loggerProviderFactory = null;
                }

                return provider;
            }
        }
    }

    internal OpenTelemetryLoggerOptions Options { get; }

    internal IExternalScopeProvider? ScopeProvider { get; private set; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
    /// <inheritdoc/>
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
                    logger = this.Provider is not LoggerProviderSdk loggerProviderSdk
                        ? NullLogger.Instance
                        : new OpenTelemetryLogger(loggerProviderSdk, this.Options, categoryName)
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
                    this.provider?.Dispose();
                }
            }

            this.disposed = true;
            OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(OpenTelemetryLoggerProvider));
        }

        base.Dispose(disposing);
    }
}
