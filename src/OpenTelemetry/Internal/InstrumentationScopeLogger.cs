// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Internal;

internal sealed class InstrumentationScopeLogger : Logger
{
    private static readonly ConcurrentDictionary<string, InstrumentationScopeLogger> Cache = new();

    private InstrumentationScopeLogger(string name)
        : base(name)
    {
    }

    public static InstrumentationScopeLogger Default { get; } = new(string.Empty);

    public static InstrumentationScopeLogger GetInstrumentationScopeLoggerForName(string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? Default
            : Cache.GetOrAdd(name, static n => new(n));
    }

    public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes)
        => throw new NotSupportedException();
}
