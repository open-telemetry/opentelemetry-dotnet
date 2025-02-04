// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace OpenTelemetry.Trace;

/// <summary>
/// TracerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Tracer"/>.
/// </summary>
public class TracerProvider : BaseProvider
{
    internal ConcurrentDictionary<TracerKey, Tracer>? Tracers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerProvider"/> class.
    /// </summary>
    protected TracerProvider()
    {
    }

    /// <summary>
    /// Gets the default <see cref="TracerProvider"/>.
    /// </summary>
    public static TracerProvider Default { get; } = new TracerProvider();

    /// <summary>
    /// Gets a tracer with given name and version.
    /// </summary>
    /// <param name="name">Name identifying the instrumentation library.</param>
    /// <param name="version">Version of the instrumentation library.</param>
    /// <returns>Tracer instance.</returns>
    public Tracer GetTracer(
#if NET
        [AllowNull]
#endif
        string name,
        string? version = null)
    {
        var tracers = this.Tracers;
        if (tracers == null)
        {
            // Note: Returns a no-op Tracer once dispose has been called.
            return new(activitySource: null);
        }

        var key = new TracerKey(name, version);

        if (!tracers.TryGetValue(key, out var tracer))
        {
            lock (tracers)
            {
                if (this.Tracers == null)
                {
                    // Note: We check here for a race with Dispose and return a
                    // no-op Tracer in that case.
                    return new(activitySource: null);
                }

                tracer = new(new(key.Name, key.Version));
#if DEBUG
                bool result = tracers.TryAdd(key, tracer);
                System.Diagnostics.Debug.Assert(result, "Write into tracers cache failed");
#else
                tracers.TryAdd(key, tracer);
#endif
            }
        }

        return tracer;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var tracers = Interlocked.Exchange(ref this.Tracers, null);
            if (tracers != null)
            {
                lock (tracers)
                {
                    foreach (var kvp in tracers)
                    {
                        var tracer = kvp.Value;
                        var activitySource = tracer.ActivitySource;
                        tracer.ActivitySource = null;
                        activitySource?.Dispose();
                    }

                    tracers.Clear();
                }
            }
        }

        base.Dispose(disposing);
    }

    internal readonly record struct TracerKey
    {
        public readonly string Name;
        public readonly string? Version;

        public TracerKey(string? name, string? version)
        {
            this.Name = name ?? string.Empty;
            this.Version = version;
        }
    }
}
