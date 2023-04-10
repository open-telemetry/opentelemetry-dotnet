// <copyright file="HttpClientMetricsInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Runtime.CompilerServices;
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http;

/// <summary>
/// Options for HttpClient metrics instrumentation.
/// </summary>
public class HttpClientMetricsInstrumentationOptions
{
    /// <summary>
    /// Delegate for enrichment of recorded metric with additional tags.
    /// </summary>
    /// <param name="response"><see cref="HttpResponseMessage"/>: the HttpResponseMessage object.</param>
    /// <param name="tags"><see cref="TagList"/>: List of current tags. You can add additional tags to this list. </param>
    public delegate void HttpClientMetricEnrichmentFunc(HttpResponseMessage response, ref TagList tags);

    /// <summary>
    /// Gets or sets a filter function that determines whether or not to
    /// collect telemetry on a per request basis.
    /// </summary>
    /// <remarks>
    /// <para><b>FilterHttpRequestMessage is only executed on .NET and .NET
    /// Core runtimes. <see cref="HttpClient"/> and <see
    /// cref="HttpWebRequest"/> on .NET and .NET Core are both implemented
    /// using <see cref="HttpRequestMessage"/>.</b></para>
    /// Notes:
    /// <list type="bullet">
    /// <item>The return value for the filter function is interpreted as:
    /// <list type="bullet">
    /// <item>If filter returns <see langword="true" />, the request is
    /// collected.</item>
    /// <item>If filter returns <see langword="false" /> or throws an
    /// exception the request is NOT collected.</item>
    /// </list></item>
    /// </list>
    /// </remarks>
    public Func<HttpRequestMessage, bool> FilterHttpRequestMessage { get; set; }

    /// <summary>
    /// Gets or sets an function to enrich a recorded metric with additional custom tags.
    /// </summary>
    public HttpClientMetricEnrichmentFunc Enrich { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool EventFilterHttpRequestMessage(string activityName, object arg1)
    {
        try
        {
            return
                this.FilterHttpRequestMessage == null ||
                !TryParseHttpRequestMessage(activityName, arg1, out HttpRequestMessage requestMessage) ||
                this.FilterHttpRequestMessage(requestMessage);
        }
        catch (Exception ex)
        {
            HttpInstrumentationEventSource.Log.RequestFilterException(ex);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHttpRequestMessage(string activityName, object arg1, out HttpRequestMessage requestMessage)
    {
        return (requestMessage = arg1 as HttpRequestMessage) != null && activityName == "System.Net.Http.HttpRequestOut";
    }
}
