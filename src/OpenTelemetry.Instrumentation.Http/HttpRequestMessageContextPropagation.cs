// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace OpenTelemetry.Instrumentation.Http;

internal static class HttpRequestMessageContextPropagation
{
    internal static Func<HttpRequestMessage, string, IEnumerable<string>> HeaderValuesGetter => (request, name) =>
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            return values;
        }

        return null;
    };

    internal static Action<HttpRequestMessage, string, string> HeaderValueSetter => (request, name, value) =>
    {
        request.Headers.Remove(name);
        request.Headers.Add(name, value);
    };
}
