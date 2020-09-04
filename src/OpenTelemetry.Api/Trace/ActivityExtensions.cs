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

        private static readonly Enumerator<IEnumerable<KeyValuePair<string, object>>, KeyValuePair<string, object>, KeyValuePair<string, object>>.AllocationFreeForEachDelegate
            ActivityTagObjectsEnumerator = DictionaryEnumerator<string, object, KeyValuePair<string, object>>.BuildAllocationFreeForEachDelegate(
                typeof(Activity).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic).FieldType);

        private static readonly DictionaryEnumerator<string, object, KeyValuePair<string, object>>.ForEachDelegate GetTagValueCallbackRef = GetTagValueCallback;

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

            var statusCanonicalCode = activity.GetTagValue(SpanAttributeConstants.StatusCodeKey) as string;
            var statusDescription = activity.GetTagValue(SpanAttributeConstants.StatusDescriptionKey) as string;

            var status = SpanHelper.ResolveCanonicalCodeToStatus(statusCanonicalCode);

            if (status.IsValid && !string.IsNullOrEmpty(statusDescription))
            {
                return status.WithDescription(statusDescription);
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
            var tagObjects = activity.TagObjects;
            if (ReferenceEquals(tagObjects, EmptyActivityTagObjects))
            {
                return null;
            }

            KeyValuePair<string, object> state = new KeyValuePair<string, object>(tagName, null);

            ActivityTagObjectsEnumerator(
                tagObjects,
                ref state,
                GetTagValueCallbackRef);

            return state.Value;
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

        private static bool GetTagValueCallback(ref KeyValuePair<string, object> state, KeyValuePair<string, object> item)
        {
            if (item.Key == state.Key)
            {
                state = item;
                return false;
            }

            return true;
        }
    }
}
