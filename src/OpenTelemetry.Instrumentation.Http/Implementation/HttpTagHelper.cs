// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    /// <returns>Span uri value.</returns>
    public static string GetUriTagValueFromRequestUri(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            return uri.OriginalString;
        }

        return string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.PathAndQuery, uri.Fragment);
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
