// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Internal;

internal sealed class InstrumentationScopeLogger : Logger
{
    private const int MaxCacheSize = 1024;

    private static readonly ConcurrentDictionary<(string Name, string? Version, string? SchemaUrl), InstrumentationScopeLogger> Cache = new();

    private static int cacheSize;

    private InstrumentationScopeLogger(string? name, string? version, string? schemaUrl)
        : base(name)
    {
        this.SetInstrumentationScope(version, schemaUrl);
    }

    public static InstrumentationScopeLogger Default { get; } = new(string.Empty, null, null);

    public static InstrumentationScopeLogger GetInstrumentationScopeLogger(LoggerOptions options)
    {
        var name = options.Name is { Length: > 0 } ? options.Name : string.Empty;

        if (name.Length == 0 && options.Version is null && options.SchemaUrl is null)
        {
            return Default;
        }

        var key = (name, options.Version, options.SchemaUrl);

        if (Cache.TryGetValue(key, out var existing))
        {
            return existing;
        }

        if (Volatile.Read(ref cacheSize) >= MaxCacheSize)
        {
            return new(name, options.Version, options.SchemaUrl);
        }

        Interlocked.Increment(ref cacheSize);
        return Cache.GetOrAdd(key, static (o) => new(o.Name, o.Version, o.SchemaUrl));
    }

    public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes)
        => throw new NotSupportedException();
}
