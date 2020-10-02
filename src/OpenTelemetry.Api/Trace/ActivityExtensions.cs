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
        private static readonly object EmptyActivityTagObjects = typeof(Activity).GetField("s_emptyTagObjects", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static readonly object EmptyActivityLinks = typeof(Activity).GetField("s_emptyLinks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

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

            activity.SetTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
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
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityStatusTagEnumerator state = default;

            ActivityTagObjectsEnumeratorFactory<ActivityStatusTagEnumerator>.Enumerate(activity, ref state);

            var status = SpanHelper.ResolveCanonicalCodeToStatus(state.StatusCode);

            if (status.IsValid && !string.IsNullOrEmpty(state.StatusDescription))
            {
                return status.WithDescription(state.StatusDescription);
            }

            return status;
        }

        /// <summary>
        /// Gets the value of a specific tag on an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="tagName">Case-sensitive tag name to retrieve.</param>
        /// <returns>Tag value or null if a match was not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object GetTagValue(this Activity activity, string tagName)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivitySingleTagEnumerator state = new ActivitySingleTagEnumerator(tagName);

            ActivityTagObjectsEnumeratorFactory<ActivitySingleTagEnumerator>.Enumerate(activity, ref state);

            return state.Value;
        }

        /// <summary>
        /// Enumerates all the key/value pairs on an <see cref="Activity"/> without performing an allocation.
        /// </summary>
        /// <typeparam name="T">The struct <see cref="IActivityEnumerator{T}"/> implementation to use for the enumeration.</typeparam>
        /// <param name="activity">Activity instance.</param>
        /// <param name="tagEnumerator">Tag enumerator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnumerateTagValues<T>(this Activity activity, ref T tagEnumerator)
            where T : struct, IActivityEnumerator<KeyValuePair<string, object>>
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityTagObjectsEnumeratorFactory<T>.Enumerate(activity, ref tagEnumerator);
        }

        /// <summary>
        /// Enumerates all the <see cref="ActivityLink"/>s on an <see cref="Activity"/> without performing an allocation.
        /// </summary>
        /// <typeparam name="T">The struct <see cref="IActivityEnumerator{T}"/> implementation to use for the enumeration.</typeparam>
        /// <param name="activity">Activity instance.</param>
        /// <param name="linkEnumerator">Tag enumerator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnumerateLinks<T>(this Activity activity, ref T linkEnumerator)
            where T : struct, IActivityEnumerator<ActivityLink>
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityLinksEnumeratorFactory<T>.Enumerate(activity, ref linkEnumerator);
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

        private struct ActivitySingleTagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
        {
            private readonly string tagName;

            public ActivitySingleTagEnumerator(string tagName)
            {
                this.tagName = tagName;
                this.Value = null;
            }

            public object Value { get; private set; }

            public bool ForEach(KeyValuePair<string, object> item)
            {
                if (item.Key == this.tagName)
                {
                    this.Value = item.Value;
                    return false;
                }

                return true;
            }
        }

        private struct ActivityStatusTagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public string StatusCode { get; private set; }

            public string StatusDescription { get; private set; }

            public bool ForEach(KeyValuePair<string, object> item)
            {
                switch (item.Key)
                {
                    case SpanAttributeConstants.StatusCodeKey:
                        this.StatusCode = item.Value as string;
                        break;
                    case SpanAttributeConstants.StatusDescriptionKey:
                        this.StatusDescription = item.Value as string;
                        break;
                }

                return this.StatusCode == null || this.StatusDescription == null;
            }
        }

        private static class ActivityTagObjectsEnumeratorFactory<TState>
            where TState : struct, IActivityEnumerator<KeyValuePair<string, object>>
        {
            private static readonly DictionaryEnumerator<string, object, TState>.AllocationFreeForEachDelegate
                ActivityTagObjectsEnumerator = DictionaryEnumerator<string, object, TState>.BuildAllocationFreeForEachDelegate(
                    typeof(Activity).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic).FieldType);

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

            private static bool ForEachTagValueCallback(ref TState state, KeyValuePair<string, object> item)
                => state.ForEach(item);
        }

        private static class ActivityLinksEnumeratorFactory<TState>
            where TState : struct, IActivityEnumerator<ActivityLink>
        {
            private static readonly ListEnumerator<ActivityLink, TState>.AllocationFreeForEachDelegate
                ActivityLinksEnumerator = ListEnumerator<ActivityLink, TState>.BuildAllocationFreeForEachDelegate(
                    typeof(Activity).GetField("_links", BindingFlags.Instance | BindingFlags.NonPublic).FieldType);

            private static readonly ListEnumerator<ActivityLink, TState>.ForEachDelegate ForEachLinkCallbackRef = ForEachLinkCallback;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Enumerate(Activity activity, ref TState state)
            {
                var activityLinks = activity.Links;

                if (ReferenceEquals(activityLinks, EmptyActivityLinks))
                {
                    return;
                }

                ActivityLinksEnumerator(
                    activityLinks,
                    ref state,
                    ForEachLinkCallbackRef);
            }

            private static bool ForEachLinkCallback(ref TState state, ActivityLink item)
                => state.ForEach(item);
        }
    }
}
