// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// A collection of helper methods to be used when building spans.
/// </summary>
internal static class SpanHelper
{
    /// <summary>
    /// Helper method that populates span properties from http status code according
    /// to https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md#status.
    /// </summary>
    /// <param name="kind">The span kind.</param>
    /// <param name="httpStatusCode">Http status code.</param>
    /// <returns>Resolved span <see cref="Status"/> for the Http status code.</returns>
    public static ActivityStatusCode ResolveSpanStatusForHttpStatusCode(ActivityKind kind, int httpStatusCode)
    {
        var upperBound = kind == ActivityKind.Client ? 399 : 499;
        if (httpStatusCode >= 100 && httpStatusCode <= upperBound)
        {
            return ActivityStatusCode.Unset;
        }

        return ActivityStatusCode.Error;
    }
}
