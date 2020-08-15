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
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

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
        public static Status GetStatus(this Activity activity)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            var statusCanonicalCode = activity.Tags.FirstOrDefault(k => k.Key == SpanAttributeConstants.StatusCodeKey).Value;
            var statusDescription = activity.Tags.FirstOrDefault(d => d.Key == SpanAttributeConstants.StatusDescriptionKey).Value;

            var status = SpanHelper.ResolveCanonicalCodeToStatus(statusCanonicalCode);

            if (status.IsValid && !string.IsNullOrEmpty(statusDescription))
            {
                return status.WithDescription(statusDescription);
            }

            return status;
        }

        /// <summary>
        /// Sets the kind of activity execution.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="kind">Activity execution kind.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetKind(this Activity activity, ActivityKind kind)
        {
            Debug.Assert(activity != null, "Activity should not be null");
            SetKindProperty(activity, kind);
        }

#pragma warning disable SA1201 // Elements should appear in the correct order
        private static readonly Action<Activity, ActivityKind> SetKindProperty = CreateActivityKindSetter();
#pragma warning restore SA1201 // Elements should appear in the correct order

        private static Action<Activity, ActivityKind> CreateActivityKindSetter()
        {
            ParameterExpression instance = Expression.Parameter(typeof(Activity), "instance");
            ParameterExpression propertyValue = Expression.Parameter(typeof(ActivityKind), "propertyValue");
            var body = Expression.Assign(Expression.Property(instance, "Kind"), propertyValue);
            return Expression.Lambda<Action<Activity, ActivityKind>>(body, instance, propertyValue).Compile();
        }
    }
}
