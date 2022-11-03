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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    internal sealed class ThreadStaticStorage
    {
        internal const int MaxTagCacheSize = 8;

        [ThreadStatic]
        private static ThreadStaticStorage storage;

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
        {
            if (storage == null)
            {
                storage = new ThreadStaticStorage();
            }

            return storage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SplitToKeysAndValues(ReadOnlySpan<KeyValuePair<string, object>> tags, int tagLength, out string[] tagKeys, out object[] tagValues)
        {
            Guard.ThrowIfZero(tagLength, $"There must be at least one tag to use {nameof(ThreadStaticStorage)}");

            if (tagLength <= MaxTagCacheSize)
            {
                tagKeys = this.primaryTagStorage[tagLength - 1].TagKeys;
                tagValues = this.primaryTagStorage[tagLength - 1].TagValues;
            }
            else
            {
                tagKeys = new string[tagLength];
                tagValues = new object[tagLength];
            }

            for (var n = 0; n < tagLength; n++)
            {
                tagKeys[n] = tags[n].Key;
                tagValues[n] = tags[n].Value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SplitToKeysAndValues(ReadOnlySpan<KeyValuePair<string, object>> tags, int tagLength, HashSet<string> tagKeysInteresting, out string[] tagKeys, out object[] tagValues, out int actualLength)
        {
            // Iterate over tags to find the exact length.
            int i = 0;
            for (var n = 0; n < tagLength; n++)
            {
                if (tagKeysInteresting.Contains(tags[n].Key))
                {
                    i++;
                }
            }

            actualLength = i;

            if (actualLength == 0)
            {
                tagKeys = null;
                tagValues = null;
            }
            else if (actualLength <= MaxTagCacheSize)
            {
                tagKeys = this.primaryTagStorage[actualLength - 1].TagKeys;
                tagValues = this.primaryTagStorage[actualLength - 1].TagValues;
            }
            else
            {
                tagKeys = new string[actualLength];
                tagValues = new object[actualLength];
            }

            // Iterate again (!) to assign the actual value.
            // TODO: The dual iteration over tags might be
            // avoidable if we change the tagKey and tagObject
            // to be a different type (eg: List).
            // It might lead to some wasted memory.
            // Also, it requires changes to the Dictionary
            // used for lookup.
            // The TODO here is to make that change
            // separately, after benchmarking.
            i = 0;
            for (var n = 0; n < tagLength; n++)
            {
                var tag = tags[n];
                if (tagKeysInteresting.Contains(tag.Key))
                {
                    tagKeys[i] = tag.Key;
                    tagValues[i] = tag.Value;
                    i++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CloneKeysAndValues(string[] inputTagKeys, object[] inputTagValues, int tagLength, out string[] clonedTagKeys, out object[] clonedTagValues)
        {
            Guard.ThrowIfZero(tagLength, $"There must be at least one tag to use {nameof(ThreadStaticStorage)}", $"{nameof(tagLength)}");

            if (tagLength <= MaxTagCacheSize)
            {
                clonedTagKeys = this.secondaryTagStorage[tagLength - 1].TagKeys;
                clonedTagValues = this.secondaryTagStorage[tagLength - 1].TagValues;
            }
            else
            {
                clonedTagKeys = new string[tagLength];
                clonedTagValues = new object[tagLength];
            }

            for (int i = 0; i < tagLength; i++)
            {
                clonedTagKeys[i] = inputTagKeys[i];
                clonedTagValues[i] = inputTagValues[i];
            }
        }

        internal sealed class TagStorage
        {
            // Used to split into Key sequence, Value sequence.
            internal readonly string[] TagKeys;
            internal readonly object[] TagValues;

            internal TagStorage(int n)
            {
                this.TagKeys = new string[n];
                this.TagValues = new object[n];
            }
        }
    }
}
