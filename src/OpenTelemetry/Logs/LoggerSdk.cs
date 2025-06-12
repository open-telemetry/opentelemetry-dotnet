// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// SDK <see cref="Logger"/> implementation.
/// </summary>
internal sealed class LoggerSdk : Logger
{
    private readonly LoggerProviderSdk loggerProvider;

    public LoggerSdk(
        LoggerProviderSdk loggerProvider,
        string? name)
        : base(name)
    {
        Guard.ThrowIfNull(loggerProvider);

        this.loggerProvider = loggerProvider;
    }

    /// <inheritdoc />
    public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes)
    {
        var provider = this.loggerProvider;
        var processor = provider.Processor;
        if (processor != null)
        {
            var pool = provider.LogRecordPool;

            var logRecord = pool.Rent();

            logRecord.Data = data;
            logRecord.ILoggerData = default;
            logRecord.ILoggerData.EventId = new EventId(default, data.EventName);

            logRecord.Logger = this;

            logRecord.AttributeData = attributes.Export(ref logRecord.AttributeStorage);

            processor.OnEnd(logRecord);

            // Attempt to return the LogRecord to the pool. This will no-op
            // if a batch exporter has added a reference.
            pool.Return(logRecord);
        }
    }
}
