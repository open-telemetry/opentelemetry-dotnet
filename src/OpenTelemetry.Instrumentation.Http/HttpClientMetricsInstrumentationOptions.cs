using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http;

/// <summary>
/// Options for HttpClient metrics instrumentation.
/// </summary>
public class HttpClientMetricsInstrumentationOptions
{
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
    /// Gets or sets an action to enrich an <see cref="Activity"/> with <see cref="HttpResponseMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>EnrichWithHttpResponseMessage is only executed on .NET and .NET
    /// Core runtimes. <see cref="HttpClient"/> and <see
    /// cref="HttpWebRequest"/> on .NET and .NET Core are both implemented
    /// using <see cref="HttpRequestMessage"/>.</b></para>
    /// </remarks>
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
