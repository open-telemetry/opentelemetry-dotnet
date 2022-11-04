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
            // We do not know ahead the actual length, so start with max possible length.
            var maxLength = Math.Min(tagKeysInteresting.Count, tagLength);
            if (maxLength == 0)
            {
                tagKeys = null;
                tagValues = null;
            }
            else if (maxLength <= MaxTagCacheSize)
            {
                tagKeys = this.primaryTagStorage[maxLength - 1].TagKeys;
                tagValues = this.primaryTagStorage[maxLength - 1].TagValues;
            }
            else
            {
                tagKeys = new string[maxLength];
                tagValues = new object[maxLength];
            }

            actualLength = 0;
            for (var n = 0; n < tagLength; n++)
            {
                // Copy only interesting tags, and keep count.
                var tag = tags[n];
                if (tagKeysInteresting.Contains(tag.Key))
                {
                    tagKeys[actualLength] = tag.Key;
                    tagValues[actualLength] = tag.Value;
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
                    tagKeys = null;
                    tagValues = null;
                    return;
                }
                else if (actualLength <= MaxTagCacheSize)
                {
                    var tmpKeys = this.primaryTagStorage[actualLength - 1].TagKeys;
                    var tmpValues = this.primaryTagStorage[actualLength - 1].TagValues;
                    for (var n = 0; n < actualLength; n++)
                    {
                        tmpKeys[n] = tagKeys[n];
                        tmpValues[n] = tagValues[n];
                    }

                    tagKeys = tmpKeys;
                    tagValues = tmpValues;
                }
                else
                {
                    var tmpKeys = new string[actualLength];
                    var tmpValues = new object[actualLength];

                    for (var n = 0; n < actualLength; n++)
                    {
                        tmpKeys[n] = tagKeys[n];
                        tmpValues[n] = tagValues[n];
                    }

                    tagKeys = tmpKeys;
                    tagValues = tmpValues;
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
