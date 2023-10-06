// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

/// <summary>
/// A collection of helper methods to be used when building Http activities.
/// </summary>
internal static class HttpTagHelper
{
    /// <summary>
    /// Gets the OpenTelemetry standard version tag value for a span based on its protocol/>.
    /// </summary>
    /// <param name="protocol">.</param>
    /// <returns>Span flavor value.</returns>
    public static string GetFlavorTagValueFromProtocol(string protocol)
    {
        switch (protocol)
        {
            case "HTTP/2":
                return "2.0";

            case "HTTP/3":
                return "3.0";

            case "HTTP/1.1":
                return "1.1";

            default:
                return protocol;
        }
    }
}
