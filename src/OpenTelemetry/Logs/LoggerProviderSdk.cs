// <copyright file="LoggerProviderSdk.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs;

/// <summary>
/// SDK <see cref="LoggerProvider"/> implementation.
/// </summary>
internal sealed class LoggerProviderSdk : LoggerProvider
{
    internal readonly IServiceProvider ServiceProvider;
    internal readonly IDisposable? OwnedServiceProvider;
    internal bool Disposed;
    internal int ShutdownCount;

    private readonly List<object> instrumentations = new();
    private ILogRecordPool? threadStaticPool = LogRecordThreadStaticPool.Instance;

    public LoggerProviderSdk(
        IServiceProvider serviceProvider,
        bool ownsServiceProvider)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");

        var state = serviceProvider!.GetRequiredService<LoggerProviderBuilderSdk>();
        state.RegisterProvider(this);

        this.ServiceProvider = serviceProvider!;

        if (ownsServiceProvider)
        {
            this.OwnedServiceProvider = serviceProvider as IDisposable;
            Debug.Assert(this.OwnedServiceProvider != null, "ownedServiceProvider was null");
        }

        OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent("Building LoggerProvider.");

        var configureProviderBuilders = serviceProvider!.GetServices<IConfigureLoggerProviderBuilder>();
        foreach (var configureProviderBuilder in configureProviderBuilders)
        {
            configureProviderBuilder.ConfigureBuilder(serviceProvider!, state);
        }

        var resourceBuilder = state.ResourceBuilder ?? ResourceBuilder.CreateDefault();
        resourceBuilder.ServiceProvider = serviceProvider;
        this.Resource = resourceBuilder.Build();

        foreach (var processor in state.Processors)
        {
            this.AddProcessor(processor);
        }

        StringBuilder instrumentationFactoriesAdded = new StringBuilder();

        foreach (var instrumentation in state.Instrumentation)
        {
            this.instrumentations.Add(instrumentation.Instance);
            instrumentationFactoriesAdded.Append(instrumentation.Name);
            instrumentationFactoriesAdded.Append(';');
        }

        if (instrumentationFactoriesAdded.Length != 0)
        {
            instrumentationFactoriesAdded.Remove(instrumentationFactoriesAdded.Length - 1, 1);
            OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent($"Instrumentations added = \"{instrumentationFactoriesAdded}\".");
        }

        OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent("LoggerProviderSdk built successfully.");
    }

    public Resource Resource { get; }

    public List<object> Instrumentations => this.instrumentations;

    public BaseProcessor<LogRecord>? Processor { get; private set; }

    public ILogRecordPool LogRecordPool => this.threadStaticPool ?? LogRecordSharedPool.Current;

    public void AddProcessor(BaseProcessor<LogRecord> processor)
    {
        Guard.ThrowIfNull(processor);

        processor.SetParentProvider(this);

        if (this.threadStaticPool != null && this.ContainsBatchProcessor(processor))
        {
            OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent("Using shared thread pool.");

            this.threadStaticPool = null;
        }

        StringBuilder processorAdded = new StringBuilder();

        if (this.Processor == null)
        {
            processorAdded.Append("Setting processor to '");
            processorAdded.Append(processor);
            processorAdded.Append('\'');

            this.Processor = processor;
        }
        else if (this.Processor is CompositeProcessor<LogRecord> compositeProcessor)
        {
            processorAdded.Append("Adding processor '");
            processorAdded.Append(processor);
            processorAdded.Append("' to composite processor");

            compositeProcessor.AddProcessor(processor);
        }
        else
        {
            processorAdded.Append("Creating new composite processor and adding new processor '");
            processorAdded.Append(processor);
            processorAdded.Append('\'');

            var newCompositeProcessor = new CompositeProcessor<LogRecord>(new[]
            {
                this.Processor,
            });
            newCompositeProcessor.SetParentProvider(this);
            newCompositeProcessor.AddProcessor(processor);
            this.Processor = newCompositeProcessor;
        }

        OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent($"Completed adding processor = \"{processorAdded}\".");
    }

    public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
    {
        try
        {
            return this.Processor?.ForceFlush(timeoutMilliseconds) ?? true;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.LoggerProviderException(nameof(this.ForceFlush), ex);
            return false;
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        if (Interlocked.Increment(ref this.ShutdownCount) > 1)
        {
            return false; // shutdown already called
        }

        try
        {
            return this.Processor?.Shutdown(timeoutMilliseconds) ?? true;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.LoggerProviderException(nameof(this.Shutdown), ex);
            return false;
        }
    }

    public bool ContainsBatchProcessor(BaseProcessor<LogRecord> processor)
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

    /// <inheritdoc />
    protected override bool TryCreateLogger(
        string? name,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        out Logger? logger)
    {
        logger = new LoggerSdk(this, name);
        return true;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.Disposed)
        {
            if (disposing)
            {
                if (this.instrumentations != null)
                {
                    foreach (var item in this.instrumentations)
                    {
                        (item as IDisposable)?.Dispose();
                    }

                    this.instrumentations.Clear();
                }

                // Wait for up to 5 seconds grace period
                this.Processor?.Shutdown(5000);
                this.Processor?.Dispose();

                this.OwnedServiceProvider?.Dispose();
            }

            this.Disposed = true;
            OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(LoggerProviderSdk));
        }

        base.Dispose(disposing);
    }
}
