// <copyright file="ThreadStaticStorage.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal sealed class ThreadStaticStorage
{
    internal const int MaxTagCacheSize = 8;

    [ThreadStatic]
    private static ThreadStaticStorage? storage;

    private readonly TagStorage[] primaryTagStorage = new TagStorage[MaxTagCacheSize];
    private readonly TagStorage[] secondaryTagStorage = new TagStorage[MaxTagCacheSize];

    private ThreadStaticStorage()
    {
        for (int i = 0; i < MaxTagCacheSize; i++)
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

        if (tagLength <= MaxTagCacheSize)
        {
            tagKeysAndValues = this.primaryTagStorage[tagLength - 1].TagKeysAndValues;
        }
        else
        {
            tagKeysAndValues = new KeyValuePair<string, object?>[tagLength];
        }

        tags.CopyTo(tagKeysAndValues);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SplitToKeysAndValues(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int tagLength,
        HashSet<string> tagKeysInteresting,
        out KeyValuePair<string, object?>[]? tagKeysAndValues,
        out int actualLength)
    {
        // We do not know ahead the actual length, so start with max possible length.
        var maxLength = Math.Min(tagKeysInteresting.Count, tagLength);
        if (maxLength == 0)
        {
            tagKeysAndValues = null;
        }
        else if (maxLength <= MaxTagCacheSize)
        {
            tagKeysAndValues = this.primaryTagStorage[maxLength - 1].TagKeysAndValues;
        }
        else
        {
            tagKeysAndValues = new KeyValuePair<string, object?>[maxLength];
        }

        actualLength = 0;
        for (var n = 0; n < tagLength; n++)
        {
            // Copy only interesting tags, and keep count.
            if (tagKeysInteresting.Contains(tags[n].Key))
            {
                Debug.Assert(tagKeysAndValues != null, "tagKeysAndValues was null");

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
                var tmpTagKeysAndValues = new KeyValuePair<string, object?>[actualLength];

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

        if (tagLength <= MaxTagCacheSize)
        {
            clonedTagKeysAndValues = this.secondaryTagStorage[tagLength - 1].TagKeysAndValues;
        }
        else
        {
            clonedTagKeysAndValues = new KeyValuePair<string, object?>[tagLength];
        }

        Array.Copy(inputTagKeysAndValues, 0, clonedTagKeysAndValues, 0, tagLength);
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
