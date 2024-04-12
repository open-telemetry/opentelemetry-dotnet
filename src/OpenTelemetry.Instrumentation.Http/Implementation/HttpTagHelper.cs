// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

/// <summary>
/// A collection of helper methods to be used when building Http activities.
/// </summary>
internal static class HttpTagHelper
{
    /// <summary>
    /// Gets the OpenTelemetry standard uri tag value for a span based on its request <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri"><see cref="Uri"/>.</param>
    /// <param name="disableQueryRedaction">Indicates whether query parameter should be redacted or not.</param>
    /// <returns>Span uri value.</returns>
    public static string GetUriTagValueFromRequestUri(Uri uri, bool disableQueryRedaction)
    {
        if (string.IsNullOrEmpty(uri.UserInfo) && disableQueryRedaction)
        {
            return uri.OriginalString;
        }

        var query = disableQueryRedaction ? uri.Query : RedactionHelper.GetRedactedQueryString(uri.Query);

        return string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.AbsolutePath, query, uri.Fragment);
    }

    public static string GetProtocolVersionString(Version httpVersion) => (httpVersion.Major, httpVersion.Minor) switch
    {
        (1, 0) => "1.0",
        (1, 1) => "1.1",
        (2, 0) => "2",
        (3, 0) => "3",
        _ => httpVersion.ToString(),
    };
}
