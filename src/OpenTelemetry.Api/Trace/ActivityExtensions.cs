// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

// The Activity class is in the System.Diagnostics namespace.
// These extension methods on Activity are intentionally not placed in the
// same namespace as Activity to prevent name collisions in the future.
// The OpenTelemetry.Trace namespace is used because Activity is analogous
// to Span in OpenTelemetry.
namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods on Activity.
/// </summary>
public static class ActivityExtensions
{
    /// <summary>
    /// Sets the status of activity execution.
    /// </summary>
    /// <remarks>
    /// This method is obsolete.
    /// Use the `Activity.SetStatus(ActivityStatusCode)` method
    /// from `System.Diagnostics.DiagnosticSource` for setting the status of an `Activity`.
    /// For more details see: <see href="https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api#setting-status" />.
    /// </remarks>
    /// <param name="activity">Activity instance.</param>
    /// <param name="status">Activity execution status.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Obsolete("Use Activity.SetStatus(ActivityStatusCode) instead.")]
    public static void SetStatus(this Activity activity, Status status)
    {
        if (activity != null)
        {
            activity.SetTag(SpanAttributeConstants.StatusCodeKey, StatusHelper.GetTagValueForStatusCode(status.StatusCode));
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
        if (activity == null
            || !activity.TryGetStatus(out var statusCode, out var statusDescription))
        {
            return Status.Unset;
        }

        return new Status(statusCode, statusDescription);
    }

    /// <summary>
    /// Adds an <see cref="ActivityEvent"/>  containing information from the specified exception.
    /// </summary>
    /// <param name="activity">Activity instance.</param>
    /// <param name="ex">Exception to be recorded.</param>
    /// <remarks> The exception is recorded as per <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/exceptions.md">specification</a>.
    /// "exception.stacktrace" is represented using the value of <a href="https://learn.microsoft.com/dotnet/api/system.exception.tostring">Exception.ToString</a>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordException(this Activity activity, Exception? ex)
        => RecordException(activity, ex, default);

    /// <summary>
    /// Adds an <see cref="ActivityEvent"/> containing information from the specified exception and additional tags.
    /// </summary>
    /// <param name="activity">Activity instance.</param>
    /// <param name="ex">Exception to be recorded.</param>
    /// <param name="tags">Additional tags to record on the event.</param>
    /// <remarks> The exception is recorded as per <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/exceptions.md">specification</a>.
    /// "exception.stacktrace" is represented using the value of <a href="https://learn.microsoft.com/dotnet/api/system.exception.tostring">Exception.ToString</a>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordException(this Activity activity, Exception? ex, in TagList tags)
    {
        if (ex == null || activity == null)
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

        foreach (var tag in tags)
        {
            tagsCollection[tag.Key] = tag.Value;
        }

        activity.AddEvent(new ActivityEvent(SemanticConventions.AttributeExceptionEventName, default, tagsCollection));
    }
}
