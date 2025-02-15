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
    /// <returns>Tracer instance.</returns>
    public Tracer GetTracer(
#if NET
        [AllowNull]
#endif
        string name) =>
        this.GetTracer(name, string.Empty, null);

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
        string? version = "") =>
        this.GetTracer(name, version, null);

    /// <summary>
    /// Gets a tracer with given name, version and tags.
    /// </summary>
    /// <param name="name">Name identifying the instrumentation library.</param>
    /// <param name="version">Version of the instrumentation library.</param>
    /// <param name="tags">Tags associated with the tracer.</param>
    /// <returns>Tracer instance.</returns>
    public Tracer GetTracer(
#if NET
        [AllowNull]
#endif
        string name,
        string? version = null,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        var tracers = this.Tracers;
        if (tracers == null)
        {
            // Note: Returns a no-op Tracer once dispose has been called.
            return new(activitySource: null);
        }

        var key = new TracerKey(name, version, tags);

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

                tracer = new(new(key.Name, key.Version, key.Tags));
                bool result = tracers.TryAdd(key, tracer);
#if DEBUG
                System.Diagnostics.Debug.Assert(result, "Write into tracers cache failed");
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
            var tracers = Interlocked.CompareExchange(ref this.Tracers, null, this.Tracers);
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
        public readonly IEnumerable<KeyValuePair<string, object?>>? Tags;

        public TracerKey(string? name, string? version, IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            this.Name = name ?? string.Empty;
            this.Version = version;
            this.Tags = GetOrderedTags(tags);
        }

        private IEnumerable<KeyValuePair<string, object>>? GetOrderedTags(
            IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            if (tags is null)
            {
                return null;
            }

            var orderedTagList = new List<KeyValuePair<string, object?>>(tags);
            orderedTagList.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));
            return orderedTagList.AsReadOnly();
        }
    }
}
