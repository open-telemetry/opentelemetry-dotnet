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
    // 1.11.1 BACKCOMPAT OVERLOAD -- DO NOT TOUCH
    public Tracer GetTracer(
#if NET
        [AllowNull]
#endif
        string name,
        string? version) =>
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
        public readonly KeyValuePair<string, object?>[]? Tags;

        public TracerKey(string? name, string? version, IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            this.Name = name ?? string.Empty;
            this.Version = version;
            this.Tags = this.GetOrderedTags(tags);
        }

        public bool Equals(TracerKey other)
        {
            if (!string.Equals(this.Name, other.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(this.Version, other.Version, StringComparison.Ordinal))
            {
                return false;
            }

            return AreTagsEqual(this.Tags, other.Tags);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (this.Name?.GetHashCode() ?? 0);
                hash = (hash * 31) + (this.Version?.GetHashCode() ?? 0);
                hash = (hash * 31) + GetTagsHashCode(this.Tags);
                return hash;
            }
        }

        private static bool AreTagsEqual(
            KeyValuePair<string, object?>[]? tags1,
            KeyValuePair<string, object?>[]? tags2)
        {
            if (tags1 == null && tags2 == null)
            {
                return true;
            }

            if (tags1 == null || tags2 == null || tags1.Length != tags2.Length)
            {
                return false;
            }

            for (int i = 0; i < tags1.Length; i++)
            {
                var kvp1 = tags1[i];
                var kvp2 = tags2[i];

                if (!string.Equals(kvp1.Key, kvp2.Key, StringComparison.Ordinal))
                {
                    return false;
                }

                // Compare values
                if (kvp1.Value is null)
                {
                    if (kvp2.Value is not null)
                    {
                        return false;
                    }
                }
                else
                {
                    if (!kvp1.Value.Equals(kvp2.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static int GetTagsHashCode(
            IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            if (tags is null)
            {
                return 0;
            }

            var hash = 0;
            unchecked
            {
                foreach (var kvp in tags)
                {
                    hash = (hash * 31) + kvp.Key.GetHashCode();
                    if (kvp.Value != null)
                    {
                        hash = (hash * 31) + kvp.Value.GetHashCode()!;
                    }
                }
            }

            return hash;
        }

        private KeyValuePair<string, object?>[]? GetOrderedTags(
            IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            if (tags is null)
            {
                return null;
            }

            var orderedTagList = new List<KeyValuePair<string, object?>>(tags);
            orderedTagList.Sort((left, right) =>
            {
                // First compare by key
                int keyComparison = string.Compare(left.Key, right.Key, StringComparison.Ordinal);
                if (keyComparison != 0)
                {
                    return keyComparison;
                }

                // If keys are equal, compare by value
                if (left.Value == null && right.Value == null)
                {
                    return 0;
                }

                if (left.Value == null)
                {
                    return -1;
                }

                if (right.Value == null)
                {
                    return 1;
                }

                // Both values are non-null, compare as strings
                return string.Compare(left.Value.ToString(), right.Value.ToString(), StringComparison.Ordinal);
            });
            return orderedTagList.ToArray();
        }
    }
}
