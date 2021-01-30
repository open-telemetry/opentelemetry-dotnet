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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

// The Activity class is in the System.Diagnostics namespace.
// These extension methods on Activity are intentionally not placed in the
// same namespace as Activity to prevent name collisions in the future.
// The OpenTelemetry.Trace namespace is used because Activity is analogous
// to Span in OpenTelemetry.
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
        /// Read more about SetStatus here https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="status">Activity execution status.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public static void SetStatus(this Activity activity, Status status)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            activity.SetTag(SpanAttributeConstants.StatusCodeKey, StatusHelper.GetTagValueForStatusCode(status.StatusCode));
            activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, status.Description);
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
            if (!activity.TryGetStatus(out StatusCode statusCode, out string statusDescription))
            {
                return Status.Unset;
            }

            return new Status(statusCode, statusDescription);
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
                { SemanticConventions.AttributeExceptionType, ex.GetType().FullName },
                { SemanticConventions.AttributeExceptionStacktrace, ex.ToInvariantString() },
            };

            if (!string.IsNullOrWhiteSpace(ex.Message))
            {
                tagsCollection.Add(SemanticConventions.AttributeExceptionMessage, ex.Message);
            }

            activity?.AddEvent(new ActivityEvent(SemanticConventions.AttributeExceptionEventName, default, tagsCollection));
        }
    }
}
