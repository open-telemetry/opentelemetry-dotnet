// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Internal;

internal sealed class InstrumentationScopeLogger : Logger
{
    private static readonly ConcurrentDictionary<(string Name, string? Version, string? SchemaUrl), InstrumentationScopeLogger> Cache = new();

    private InstrumentationScopeLogger(string? name, string? version, string? schemaUrl)
        : base(name)
    {
        this.SetInstrumentationScope(version, schemaUrl);
    }

    public static InstrumentationScopeLogger Default { get; } = new(string.Empty, null, null);

    public static InstrumentationScopeLogger GetInstrumentationScopeLogger(LoggerOptions options)
        => options.Name is not { Length: > 0 }
            ? Default
            : Cache.GetOrAdd(
                (options.Name, options.Version, options.SchemaUrl),
                static (o) => new(o.Name, o.Version, o.SchemaUrl));

    public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes)
        => throw new NotSupportedException();
}
