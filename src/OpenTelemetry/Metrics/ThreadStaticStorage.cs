// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal sealed class ThreadStaticStorage
{
    internal const int MaxTagCacheSize = 8;

    // Tag sets longer than this are not cached, so a one-off measurement with a very
    // large tag set cannot leave a large array rooted for the lifetime of the thread.
    internal const int MaxLargeTagCacheSize = 64;

    // Direct-mapped cache of tag key string reference -> the string's hash code.
    internal const int KeyHashCacheSize = 64;

    internal readonly KeyHashCacheEntry[] KeyHashCache = new KeyHashCacheEntry[KeyHashCacheSize];

    [ThreadStatic]
    private static ThreadStaticStorage? storage;

    private readonly TagStorage[] primaryTagStorage = new TagStorage[MaxTagCacheSize];
    private readonly TagStorage[] secondaryTagStorage = new TagStorage[MaxTagCacheSize];

    // Grow-on-demand buffers for measurements carrying between MaxTagCacheSize
    // and MaxLargeTagCacheSize tags. Without these, every such measurement
    // allocates a new array (twice, for the filtered paths which also trim).
    private KeyValuePair<string, object?>[]? largePrimaryTagStorage;
    private KeyValuePair<string, object?>[]? largeTrimmedTagStorage;
    private KeyValuePair<string, object?>[]? largeSecondaryTagStorage;

    private ThreadStaticStorage()
    {
        for (var i = 0; i < MaxTagCacheSize; i++)
        {
            this.primaryTagStorage[i] = new TagStorage(i + 1);
            this.secondaryTagStorage[i] = new TagStorage(i + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ThreadStaticStorage GetStorage()
        => storage ??= new ThreadStaticStorage();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SplitToKeysAndValues(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int tagLength,
        out KeyValuePair<string, object?>[] tagKeysAndValues)
    {
        Guard.ThrowIfZero(tagLength, $"There must be at least one tag to use {nameof(ThreadStaticStorage)}");

        tagKeysAndValues = tagLength <= MaxTagCacheSize
            ? this.primaryTagStorage[tagLength - 1].TagKeysAndValues
            : Rent(ref this.largePrimaryTagStorage, tagLength);

        tags.CopyTo(tagKeysAndValues);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SplitToKeysAndValues(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int tagLength,
#if NET
        FrozenSet<string> tagKeysInteresting,
#else
        HashSet<string> tagKeysInteresting,
#endif
        out KeyValuePair<string, object?>[]? tagKeysAndValues,
        out int actualLength)
    {
        // We do not know ahead the actual length, so start with max possible length.
        var maxLength = Math.Min(tagKeysInteresting.Count, tagLength);
        tagKeysAndValues = maxLength == 0
            ? null
            : maxLength <= MaxTagCacheSize
                ? this.primaryTagStorage[maxLength - 1].TagKeysAndValues
                : Rent(ref this.largePrimaryTagStorage, maxLength);

        actualLength = 0;
        for (var n = 0; n < tagLength; n++)
        {
            // Copy only interesting tags, and keep count.
            if (tagKeysInteresting.Contains(tags[n].Key))
            {
                tagKeysAndValues![actualLength] = tags[n];
                actualLength++;
            }
        }

        // If the actual length was equal to max, great!
        // else, we need to pick the array of the actual length,
        // and copy tags into it.
        // This optimizes the common scenario:
        // User is interested only in TagA and TagB
        // and incoming measurement has TagA and TagB and many more.
        // In this case, the actual length would be same as max length,
        // and the following copy is avoided.
        if (actualLength < maxLength)
        {
            if (actualLength == 0)
            {
                tagKeysAndValues = null;
                return;
            }

            Debug.Assert(tagKeysAndValues != null, "tagKeysAndValues was null");

            if (actualLength <= MaxTagCacheSize)
            {
                var tmpTagKeysAndValues = this.primaryTagStorage[actualLength - 1].TagKeysAndValues;

                Array.Copy(tagKeysAndValues, 0, tmpTagKeysAndValues, 0, actualLength);

                tagKeysAndValues = tmpTagKeysAndValues;
            }
            else
            {
                var tmpTagKeysAndValues = Rent(ref this.largeTrimmedTagStorage, actualLength);

                Array.Copy(tagKeysAndValues, 0, tmpTagKeysAndValues, 0, actualLength);

                tagKeysAndValues = tmpTagKeysAndValues;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SplitToKeysAndValuesExclude(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int tagLength,
#if NET
        FrozenSet<string> excludedTagKeys,
#else
        HashSet<string> excludedTagKeys,
#endif
        out KeyValuePair<string, object?>[]? tagKeysAndValues,
        out int actualLength)
    {
        // Worst case: no tags excluded, all survive.
        var maxLength = tagLength;
        tagKeysAndValues = maxLength == 0
            ? null
            : maxLength <= MaxTagCacheSize
                ? this.primaryTagStorage[maxLength - 1].TagKeysAndValues
                : Rent(ref this.largePrimaryTagStorage, maxLength);

        actualLength = 0;
        for (var n = 0; n < tagLength; n++)
        {
            var tag = tags[n];
            if (!excludedTagKeys.Contains(tag.Key))
            {
                tagKeysAndValues![actualLength] = tag;
                actualLength++;
            }
        }

        if (actualLength < maxLength)
        {
            if (actualLength == 0)
            {
                tagKeysAndValues = null;
                return;
            }

            Debug.Assert(tagKeysAndValues != null, "tagKeysAndValues was null");

            if (actualLength <= MaxTagCacheSize)
            {
                var tmpTagKeysAndValues = this.primaryTagStorage[actualLength - 1].TagKeysAndValues;

                Array.Copy(tagKeysAndValues, 0, tmpTagKeysAndValues, 0, actualLength);

                tagKeysAndValues = tmpTagKeysAndValues;
            }
            else
            {
                var tmpTagKeysAndValues = Rent(ref this.largeTrimmedTagStorage, actualLength);

                Array.Copy(tagKeysAndValues, 0, tmpTagKeysAndValues, 0, actualLength);

                tagKeysAndValues = tmpTagKeysAndValues;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CloneKeysAndValues(
        KeyValuePair<string, object?>[] inputTagKeysAndValues,
        int tagLength,
        out KeyValuePair<string, object?>[] clonedTagKeysAndValues)
    {
        Guard.ThrowIfZero(tagLength, $"There must be at least one tag to use {nameof(ThreadStaticStorage)}", $"{nameof(tagLength)}");

        clonedTagKeysAndValues = tagLength <= MaxTagCacheSize
            ? this.secondaryTagStorage[tagLength - 1].TagKeysAndValues
            : Rent(ref this.largeSecondaryTagStorage, tagLength);

        Array.Copy(inputTagKeysAndValues, 0, clonedTagKeysAndValues, 0, tagLength);
    }

    // These buffers are handed to Tags, which hashes and compares the whole
    // array, so a buffer must be exactly `length` long rather than merely large
    // enough. A stream whose tag count varies within the cacheable range
    // therefore re-allocates when the count changes; a stream with a stable tag
    // count (the normal case) allocates once per thread.
    private static KeyValuePair<string, object?>[] Rent(
        ref KeyValuePair<string, object?>[]? cache,
        int length)
    {
        if (length > MaxLargeTagCacheSize)
        {
            return new KeyValuePair<string, object?>[length];
        }

        var buffer = cache;
        if (buffer == null || buffer.Length != length)
        {
            buffer = cache = new KeyValuePair<string, object?>[length];
        }

        return buffer;
    }

    internal struct KeyHashCacheEntry
    {
        public string? Key;
        public int Hash;
    }

    internal sealed class TagStorage
    {
        // Used to split into Key sequence, Value sequence.
        internal readonly KeyValuePair<string, object?>[] TagKeysAndValues;

        internal TagStorage(int n)
        {
            this.TagKeysAndValues = new KeyValuePair<string, object?>[n];
        }
    }
}
