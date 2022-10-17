// <copyright file="LoggerSdk.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// SDK <see cref="Logger"/> implementation.
/// </summary>
internal sealed class LoggerSdk : Logger
{
    private readonly LoggerProviderSdk loggerProvider;
    private string? eventDomain;

    public LoggerSdk(
        LoggerProviderSdk loggerProvider,
        LoggerOptions options)
        : base(options)
    {
        Guard.ThrowIfNull(loggerProvider);

        this.loggerProvider = loggerProvider;
    }

    /// <inheritdoc />
    public override void EmitEvent(string name, in LogRecordData data, in LogRecordAttributeList attributes = default)
    {
        // Note: This method will throw if event.name or event.domain is missing
        // or null. This was done intentionally see discussion:
        // https://github.com/open-telemetry/opentelemetry-specification/pull/2768#discussion_r972447436

        Guard.ThrowIfNullOrWhitespace(name);

        this.EnsureEventDomain();

        var provider = this.loggerProvider;
        var processor = provider.Processor;
        if (processor != null)
        {
            var pool = provider.LogRecordPool;

            var logRecord = pool.Rent();

            logRecord.Data = data;
            logRecord.ILoggerData = default;

            logRecord.InstrumentationScope = this.Options.InstrumentationScope;

            var exportedAttributes = attributes.Export(ref logRecord.AttributeStorage, additionalCapacity: 2);

            Debug.Assert(exportedAttributes != null, "exportedAttributes was null");

            exportedAttributes!.Add(new KeyValuePair<string, object?>("event.name", name));

            logRecord.Attributes = exportedAttributes;

            processor.OnEnd(logRecord);

            // Attempt to return the LogRecord to the pool. This will no-op
            // if a batch exporter has added a reference.
            pool.Return(logRecord);
        }
    }

    /// <inheritdoc />
    public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes = default)
    {
        var provider = this.loggerProvider;
        var processor = provider.Processor;
        if (processor != null)
        {
            var pool = provider.LogRecordPool;

            var logRecord = pool.Rent();

            logRecord.Data = data;
            logRecord.ILoggerData = default;

            logRecord.InstrumentationScope = this.Options.InstrumentationScope;

            logRecord.Attributes = attributes.Export(ref logRecord.AttributeStorage);

            processor.OnEnd(logRecord);

            // Attempt to return the LogRecord to the pool. This will no-op
            // if a batch exporter has added a reference.
            pool.Return(logRecord);
        }
    }

    private void EnsureEventDomain()
    {
        string? eventDomain = this.eventDomain;

        if (eventDomain == null)
        {
            eventDomain = this.Options.EventDomain;

            if (string.IsNullOrWhiteSpace(eventDomain))
            {
                throw new InvalidOperationException($"Events cannot be emitted through the Logger '{this.Options.InstrumentationScope.Name}' because it does not have a configured EventDomain.");
            }

            this.eventDomain = eventDomain;
        }
    }
}
