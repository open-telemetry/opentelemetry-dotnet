// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods on Activity.
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// Sets the status of activity execution.
        /// Activity class in .NET does not support 'Status'.
        /// This extension provides a workaround to store Status as special tags with key name of otel.status_code and otel.status_description.
        /// Read more about SetStatus here https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#set-status.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="status">Activity execution status.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static void SetStatus(this Activity activity, Status status)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            activity.SetTag(SpanAttributeConstants.StatusCodeKey, (int)status.StatusCode);
            if (!string.IsNullOrEmpty(status.Description))
            {
                activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, status.Description);
            }
        }

        /// <summary>
        /// Gets the status of activity execution.
        /// Activity class in .NET does not support 'Status'.
        /// This extension provides a workaround to retrieve Status from special tags with key name otel.status_code and otel.status_description.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <returns>Activity execution status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static Status GetStatus(this Activity activity)
        {
            return activity.GetStatusHelper();
        }

        /// <summary>
        /// Record Exception.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="ex">Exception to be recorded.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordException(this Activity activity, Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            var tagsCollection = new ActivityTagsCollection
            {
                { SemanticConventions.AttributeExceptionType, ex.GetType().Name },
                { SemanticConventions.AttributeExceptionStacktrace, ex.ToInvariantString() },
            };

            if (!string.IsNullOrWhiteSpace(ex.Message))
            {
                tagsCollection.Add(SemanticConventions.AttributeExceptionMessage, ex.Message);
            }

            activity?.AddEvent(new ActivityEvent(SemanticConventions.AttributeExceptionEventName, default, tagsCollection));
        }

        private struct ActivityStatusTagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public bool IsValid;

            public StatusCode StatusCode;

            public string StatusDescription;

            public bool ForEach(KeyValuePair<string, object> item)
            {
                switch (item.Key)
                {
                    case SpanAttributeConstants.StatusCodeKey:
                        this.StatusCode = (StatusCode)item.Value;
                        this.IsValid = this.StatusCode == StatusCode.Error || this.StatusCode == StatusCode.Ok || this.StatusCode == StatusCode.Unset;
                        break;
                    case SpanAttributeConstants.StatusDescriptionKey:
                        this.StatusDescription = item.Value as string;
                        break;
                }

                return this.IsValid || this.StatusDescription == null;
            }
        }

        private static class ActivityTagsEnumeratorFactory<TState>
            where TState : struct, IActivityEnumerator<KeyValuePair<string, object>>
        {
            private static readonly object EmptyActivityTagObjects = typeof(Activity).GetField("s_emptyTagObjects", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            private static readonly object EmptyActivityEventTags = typeof(ActivityEvent).GetField("s_emptyTags", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            private static readonly DictionaryEnumerator<string, object, TState>.AllocationFreeForEachDelegate
                ActivityTagObjectsEnumerator = DictionaryEnumerator<string, object, TState>.BuildAllocationFreeForEachDelegate(
                    typeof(Activity).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic).FieldType);

            private static readonly DictionaryEnumerator<string, object, TState>.AllocationFreeForEachDelegate
                ActivityTagsCollectionEnumerator = DictionaryEnumerator<string, object, TState>.BuildAllocationFreeForEachDelegate(typeof(ActivityTagsCollection));

            private static readonly DictionaryEnumerator<string, object, TState>.ForEachDelegate ForEachTagValueCallbackRef = ForEachTagValueCallback;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Enumerate(Activity activity, ref TState state)
            {
                var tagObjects = activity.TagObjects;

                if (ReferenceEquals(tagObjects, EmptyActivityTagObjects))
                {
                    return;
                }

                ActivityTagObjectsEnumerator(
                    tagObjects,
                    ref state,
                    ForEachTagValueCallbackRef);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Enumerate(ActivityLink activityLink, ref TState state)
            {
                var tags = activityLink.Tags;

                if (tags is null)
                {
                    return;
                }

                ActivityTagsCollectionEnumerator(
                    tags,
                    ref state,
                    ForEachTagValueCallbackRef);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Enumerate(ActivityEvent activityEvent, ref TState state)
            {
                var tags = activityEvent.Tags;

                if (ReferenceEquals(tags, EmptyActivityEventTags))
                {
                    return;
                }

                ActivityTagsCollectionEnumerator(
                    tags,
                    ref state,
                    ForEachTagValueCallbackRef);
            }

            private static bool ForEachTagValueCallback(ref TState state, KeyValuePair<string, object> item)
                => state.ForEach(item);
        }
    }
}
