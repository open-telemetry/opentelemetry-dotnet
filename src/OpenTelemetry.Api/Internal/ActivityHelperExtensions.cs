// <copyright file="ActivityHelperExtensions.cs" company="OpenTelemetry Authors">
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
    internal static class ActivityHelperExtensions
    {
        /// <summary>
        /// Gets the status of activity execution.
        /// Activity class in .NET does not support 'Status'.
        /// This extension provides a workaround to retrieve Status from special tags with key name otel.status_code and otel.status_description.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="statusCode"><see cref="StatusCode"/>.</param>
        /// <param name="statusDescription">Status description.</param>
        /// <returns><see langword="true"/> if <see cref="Status"/> was found on the supplied Activity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static bool TryGetStatus(this Activity activity, out StatusCode statusCode, out string statusDescription)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityStatusTagEnumerator state = default;

            ActivityTagsEnumeratorFactory<ActivityStatusTagEnumerator>.Enumerate(activity, ref state);

            if (!state.StatusCode.HasValue)
            {
                statusCode = default;
                statusDescription = null;
                return false;
            }

            statusCode = state.StatusCode.Value;
            statusDescription = state.StatusDescription;
            return true;
        }

        /// <summary>
        /// Gets the value of a specific tag on an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="tagName">Case-sensitive tag name to retrieve.</param>
        /// <returns>Tag value or null if a match was not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static object GetTagValue(this Activity activity, string tagName)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivitySingleTagEnumerator state = new ActivitySingleTagEnumerator(tagName);

            ActivityTagsEnumeratorFactory<ActivitySingleTagEnumerator>.Enumerate(activity, ref state);

            return state.Value;
        }

        /// <summary>
        /// Checks if the user provided tag name is the first tag of the <see cref="Activity"/> and retrieves the tag value.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="tagName">Tag name.</param>
        /// <param name="tagValue">Tag value.</param>
        /// <returns><see langword="true"/> if the first tag of the supplied Activity matches the user provide tag name.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryCheckFirstTag(this Activity activity, string tagName, out object tagValue)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityFirstTagEnumerator state = new ActivityFirstTagEnumerator(tagName);

            ActivityTagsEnumeratorFactory<ActivityFirstTagEnumerator>.Enumerate(activity, ref state);

            if (state.Value == null)
            {
                tagValue = null;
                return false;
            }

            tagValue = state.Value;
            return true;
        }

        /// <summary>
        /// Enumerates all the key/value pairs on an <see cref="Activity"/> without performing an allocation.
        /// </summary>
        /// <typeparam name="T">The struct <see cref="IActivityEnumerator{T}"/> implementation to use for the enumeration.</typeparam>
        /// <param name="activity">Activity instance.</param>
        /// <param name="tagEnumerator">Tag enumerator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static void EnumerateTags<T>(this Activity activity, ref T tagEnumerator)
            where T : struct, IActivityEnumerator<KeyValuePair<string, object>>
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityTagsEnumeratorFactory<T>.Enumerate(activity, ref tagEnumerator);
        }

        /// <summary>
        /// Enumerates all the <see cref="ActivityLink"/>s on an <see cref="Activity"/> without performing an allocation.
        /// </summary>
        /// <typeparam name="T">The struct <see cref="IActivityEnumerator{T}"/> implementation to use for the enumeration.</typeparam>
        /// <param name="activity">Activity instance.</param>
        /// <param name="linkEnumerator">Link enumerator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static void EnumerateLinks<T>(this Activity activity, ref T linkEnumerator)
            where T : struct, IActivityEnumerator<ActivityLink>
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityLinksEnumeratorFactory<T>.Enumerate(activity, ref linkEnumerator);
        }

        /// <summary>
        /// Enumerates all the key/value pairs on an <see cref="ActivityLink"/> without performing an allocation.
        /// </summary>
        /// <typeparam name="T">The struct <see cref="IActivityEnumerator{T}"/> implementation to use for the enumeration.</typeparam>
        /// <param name="activityLink">ActivityLink instance.</param>
        /// <param name="tagEnumerator">Tag enumerator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static void EnumerateTags<T>(this ActivityLink activityLink, ref T tagEnumerator)
            where T : struct, IActivityEnumerator<KeyValuePair<string, object>>
        {
            ActivityTagsEnumeratorFactory<T>.Enumerate(activityLink, ref tagEnumerator);
        }

        /// <summary>
        /// Enumerates all the <see cref="ActivityEvent"/>s on an <see cref="Activity"/> without performing an allocation.
        /// </summary>
        /// <typeparam name="T">The struct <see cref="IActivityEnumerator{T}"/> implementation to use for the enumeration.</typeparam>
        /// <param name="activity">Activity instance.</param>
        /// <param name="eventEnumerator">Event enumerator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static void EnumerateEvents<T>(this Activity activity, ref T eventEnumerator)
            where T : struct, IActivityEnumerator<ActivityEvent>
        {
            Debug.Assert(activity != null, "Activity should not be null");

            ActivityEventsEnumeratorFactory<T>.Enumerate(activity, ref eventEnumerator);
        }

        /// <summary>
        /// Enumerates all the key/value pairs on an <see cref="ActivityEvent"/> without performing an allocation.
        /// </summary>
        /// <typeparam name="T">The struct <see cref="IActivityEnumerator{T}"/> implementation to use for the enumeration.</typeparam>
        /// <param name="activityEvent">ActivityEvent instance.</param>
        /// <param name="tagEnumerator">Tag enumerator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static void EnumerateTags<T>(this ActivityEvent activityEvent, ref T tagEnumerator)
            where T : struct, IActivityEnumerator<KeyValuePair<string, object>>
        {
            ActivityTagsEnumeratorFactory<T>.Enumerate(activityEvent, ref tagEnumerator);
        }

        private struct ActivitySingleTagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public object Value;

            private readonly string tagName;

            public ActivitySingleTagEnumerator(string tagName)
            {
                this.tagName = tagName;
                this.Value = null;
            }

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
            public StatusCode? StatusCode;

            public string StatusDescription;

            public bool ForEach(KeyValuePair<string, object> item)
            {
                switch (item.Key)
                {
                    case SpanAttributeConstants.StatusCodeKey:
                        this.StatusCode = StatusHelper.GetStatusCodeForTagValue(item.Value as string);
                        break;
                    case SpanAttributeConstants.StatusDescriptionKey:
                        this.StatusDescription = item.Value as string;
                        break;
                }

                return !this.StatusCode.HasValue || this.StatusDescription == null;
            }
        }

        private struct ActivityFirstTagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public object Value;

            private readonly string tagName;

            public ActivityFirstTagEnumerator(string tagName)
            {
                this.tagName = tagName;
                this.Value = null;
            }

            public bool ForEach(KeyValuePair<string, object> item)
            {
                if (item.Key == this.tagName)
                {
                    this.Value = item.Value;
                }

                return false;
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

        private static class ActivityLinksEnumeratorFactory<TState>
            where TState : struct, IActivityEnumerator<ActivityLink>
        {
            private static readonly object EmptyActivityLinks = typeof(Activity).GetField("s_emptyLinks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

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

        private static class ActivityEventsEnumeratorFactory<TState>
            where TState : struct, IActivityEnumerator<ActivityEvent>
        {
            private static readonly object EmptyActivityEvents = typeof(Activity).GetField("s_emptyEvents", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            private static readonly ListEnumerator<ActivityEvent, TState>.AllocationFreeForEachDelegate
                ActivityEventsEnumerator = ListEnumerator<ActivityEvent, TState>.BuildAllocationFreeForEachDelegate(
                    typeof(Activity).GetField("_events", BindingFlags.Instance | BindingFlags.NonPublic).FieldType);

            private static readonly ListEnumerator<ActivityEvent, TState>.ForEachDelegate ForEachEventCallbackRef = ForEachEventCallback;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Enumerate(Activity activity, ref TState state)
            {
                var activityEvents = activity.Events;

                if (ReferenceEquals(activityEvents, EmptyActivityEvents))
                {
                    return;
                }

                ActivityEventsEnumerator(
                    activityEvents,
                    ref state,
                    ForEachEventCallbackRef);
            }

            private static bool ForEachEventCallback(ref TState state, ActivityEvent item)
                => state.ForEach(item);
        }
    }
}
