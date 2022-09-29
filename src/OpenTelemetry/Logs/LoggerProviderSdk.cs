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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

using CallbackHelper = OpenTelemetry.ProviderBuilderServiceCollectionCallbackHelper<
    OpenTelemetry.Logs.LoggerProviderBuilderSdk,
    OpenTelemetry.Logs.LoggerProviderSdk,
    OpenTelemetry.Logs.LoggerProviderBuilderState>;

namespace OpenTelemetry.Logs;

/// <summary>
/// SDK <see cref="LoggerProvider"/> implementation.
/// </summary>
internal sealed class LoggerProviderSdk : LoggerProvider
{
    private readonly ServiceProvider? ownedServiceProvider;
    private readonly List<object> instrumentations = new();
    private ILogRecordPool? threadStaticPool = LogRecordThreadStaticPool.Instance;
    private bool disposed;

    public LoggerProviderSdk(
        IServiceProvider serviceProvider,
        bool ownsServiceProvider)
    {
        OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent("Building LoggerProviderSdk.");

        if (ownsServiceProvider)
        {
            this.ownedServiceProvider = serviceProvider as ServiceProvider;

            Debug.Assert(this.ownedServiceProvider != null, "ownedServiceProvider was null");
        }

        var state = new LoggerProviderBuilderState(serviceProvider, this);

        CallbackHelper.InvokeRegisteredConfigureStateCallbacks(
            serviceProvider,
            state);

        foreach (var processor in state.Processors)
        {
            this.AddProcessor(processor);
        }

        foreach (var instrumentation in state.Instrumentation)
        {
            this.instrumentations.Add(instrumentation.Instance);
        }

        this.Resource = (state.ResourceBuilder ?? ResourceBuilder.CreateDefault()).Build();

        OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent("LoggerProviderSdk built successfully.");
    }

    public Resource Resource { get; }

    public List<object> Instrumentations => this.instrumentations;

    public BaseProcessor<LogRecord>? Processor { get; private set; }

    public ILogRecordPool LogRecordPool => this.threadStaticPool ?? LogRecordSharedPool.Current;

    public void AddProcessor(BaseProcessor<LogRecord> processor)
    {
        OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent("Started adding processor.");

        Guard.ThrowIfNull(processor);

        processor.SetParentProvider(this);

        StringBuilder processorAdded = new StringBuilder();

        if (this.threadStaticPool != null && this.ContainsBatchProcessor(processor))
        {
            OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent("Using shared thread pool.");

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

        OpenTelemetrySdkEventSource.Log.LoggerProviderSdkEvent($"Completed adding processor = \"{processorAdded}\".");
    }

    public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
        => this.Processor?.ForceFlush(timeoutMilliseconds) ?? true;

    public bool Shutdown(int timeoutMilliseconds)
        => this.Processor?.Shutdown(timeoutMilliseconds) ?? true;

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
    public override Logger GetLogger(LoggerOptions options)
    {
        return new LoggerSdk(this, options);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
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

                this.ownedServiceProvider?.Dispose();
            }

            this.disposed = true;
            OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(LoggerProviderSdk));
        }

        base.Dispose(disposing);
    }
}
