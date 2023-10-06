// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.Http.Tests;

// Tests for v1.21.0 Semantic Conventions for Http spans
// see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/http/http-spans.md
// These tests emit both the new and older attributes.
// This test class can be deleted when this library is GA.
public class HttpWebRequestActivitySourceTestsDupe : IDisposable
{
    private readonly IDisposable testServer;
    private readonly string testServerHost;
    private readonly int testServerPort;
    private readonly string hostNameAndPort;
    private readonly string netPeerName;
    private readonly int netPeerPort;

    static HttpWebRequestActivitySourceTestsDupe()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http/dup" })
            .Build();

        HttpClientInstrumentationOptions options = new(configuration)
        {
            EnrichWithHttpWebRequest = (activity, httpWebRequest) =>
            {
                VerifyHeaders(httpWebRequest);
            },
        };

        HttpWebRequestActivitySource.Options = options;

        // Need to touch something in HttpWebRequestActivitySource/Sdk to do the static injection.
        GC.KeepAlive(HttpWebRequestActivitySource.Options);
        _ = Sdk.SuppressInstrumentation;
    }

    public HttpWebRequestActivitySourceTestsDupe()
    {
        Assert.Null(Activity.Current);
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = false;

        this.testServer = TestHttpServer.RunServer(
            ctx => ProcessServerRequest(ctx),
            out this.testServerHost,
            out this.testServerPort);

        this.hostNameAndPort = $"{this.testServerHost}:{this.testServerPort}";
        this.netPeerName = this.testServerHost;
        this.netPeerPort = this.testServerPort;

        void ProcessServerRequest(HttpListenerContext context)
        {
            string redirects = context.Request.QueryString["redirects"];
            if (!string.IsNullOrWhiteSpace(redirects) && int.TryParse(redirects, out int parsedRedirects) && parsedRedirects > 0)
            {
                context.Response.Redirect(this.BuildRequestUrl(queryString: $"redirects={--parsedRedirects}"));
                context.Response.OutputStream.Close();
                return;
            }

            string responseContent;
            if (context.Request.QueryString["skipRequestContent"] == null)
            {
                using StreamReader readStream = new StreamReader(context.Request.InputStream);

                responseContent = readStream.ReadToEnd();
            }
            else
            {
                responseContent = $"{{\"Id\":\"{Guid.NewGuid()}\"}}";
            }

            string responseCode = context.Request.QueryString["responseCode"];
            if (!string.IsNullOrWhiteSpace(responseCode))
            {
                context.Response.StatusCode = int.Parse(responseCode);
            }
            else
            {
                context.Response.StatusCode = 200;
            }

            if (context.Response.StatusCode != 204)
            {
                using StreamWriter writeStream = new StreamWriter(context.Response.OutputStream);

                writeStream.Write(responseContent);
            }
            else
            {
                context.Response.OutputStream.Close();
            }
        }
    }

    public void Dispose()
    {
        this.testServer.Dispose();
    }

    /// <summary>
    /// Test to make sure we get both request and response events.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("POST", "skipRequestContent=1")]
    public async Task TestBasicReceiveAndResponseEvents(string method, string queryString = null)
    {
        var url = this.BuildRequestUrl(queryString: queryString);

        using var eventRecords = new ActivitySourceRecorder();

        // Send a random Http request to generate some events
        using (var client = new HttpClient())
        {
            (method == "GET"
                ? await client.GetAsync(url).ConfigureAwait(false)
                : await client.PostAsync(url, new StringContent("hello world")).ConfigureAwait(false)).Dispose();
        }

        // We should have exactly one Start and one Stop event
        Assert.Equal(2, eventRecords.Records.Count);
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Stop"));

        // Check to make sure: The first record must be a request, the next record must be a response.
        Activity activity = AssertFirstEventWasStart(eventRecords);

        VerifyActivityStartTags(this.netPeerName, this.netPeerPort, method, url, activity);

        Assert.True(eventRecords.Records.TryDequeue(out var stopEvent));
        Assert.Equal("Stop", stopEvent.Key);

        VerifyActivityStopTags(200, activity);
    }

    private static Activity AssertFirstEventWasStart(ActivitySourceRecorder eventRecords)
    {
        Assert.True(eventRecords.Records.TryDequeue(out KeyValuePair<string, Activity> startEvent));
        Assert.Equal("Start", startEvent.Key);
        return startEvent.Value;
    }

    private static void VerifyHeaders(HttpWebRequest startRequest)
    {
        var tracestate = startRequest.Headers["tracestate"];
        Assert.Equal("some=state", tracestate);

        var baggage = startRequest.Headers["baggage"];
        Assert.Equal("k=v", baggage);

        var traceparent = startRequest.Headers["traceparent"];
        Assert.NotNull(traceparent);
        Assert.Matches("^[0-9a-f]{2}-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", traceparent);
    }

    private static void VerifyActivityStartTags(string netPeerName, int? netPeerPort, string method, string url, Activity activity)
    {
        // New
        Assert.NotNull(activity.TagObjects);
        Assert.Equal(method, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod));
        if (netPeerPort != null)
        {
            Assert.Equal(netPeerPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        }

        Assert.Equal(netPeerName, activity.GetTagValue(SemanticConventions.AttributeServerAddress));

        Assert.Equal(url, activity.GetTagValue(SemanticConventions.AttributeUrlFull));

        // Old
        Assert.NotNull(activity.TagObjects);
        Assert.Equal(method, activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
        if (netPeerPort != null)
        {
            Assert.Equal(netPeerPort, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
        }

        Assert.Equal(netPeerName, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));

        Assert.Equal(url, activity.GetTagValue(SemanticConventions.AttributeHttpUrl));
    }

    private static void VerifyActivityStopTags(int statusCode, Activity activity)
    {
        // New
        Assert.Equal(statusCode, activity.GetTagValue(SemanticConventions.AttributeHttpResponseStatusCode));

        // Old
        Assert.Equal(statusCode, activity.GetTagValue(SemanticConventions.AttributeHttpStatusCode));
    }

    private static void ActivityEnrichment(Activity activity, string method, object obj)
    {
        switch (method)
        {
            case "OnStartActivity":
                Assert.True(obj is HttpWebRequest);
                VerifyHeaders(obj as HttpWebRequest);
                break;

            case "OnStopActivity":
                Assert.True(obj is HttpWebResponse);
                break;

            case "OnException":
                Assert.True(obj is Exception);
                break;

            default:
                break;
        }
    }

    private static void ValidateBaggage(HttpWebRequest request)
    {
        string[] baggage = request.Headers["baggage"].Split(',');

        Assert.Equal(3, baggage.Length);
        Assert.Contains("key=value", baggage);
        Assert.Contains("bad%2Fkey=value", baggage);
        Assert.Contains("goodkey=bad%2Fvalue", baggage);
    }

    private string BuildRequestUrl(bool useHttps = false, string path = "echo", string queryString = null)
    {
        return $"{(useHttps ? "https" : "http")}://{this.testServerHost}:{this.testServerPort}/{path}{(string.IsNullOrWhiteSpace(queryString) ? string.Empty : $"?{queryString}")}";
    }

    private void CleanUpActivity()
    {
        while (Activity.Current != null)
        {
            Activity.Current.Stop();
        }
    }

    /// <summary>
    /// <see cref="ActivitySourceRecorder"/> is a helper class for recording <see cref="HttpWebRequestActivitySource.ActivitySourceName"/> events.
    /// </summary>
    private class ActivitySourceRecorder : IDisposable
    {
        private readonly Action<KeyValuePair<string, Activity>> onEvent;
        private readonly ActivityListener activityListener;

        public ActivitySourceRecorder(Action<KeyValuePair<string, Activity>> onEvent = null, ActivitySamplingResult activitySamplingResult = ActivitySamplingResult.AllDataAndRecorded)
        {
            this.activityListener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == HttpWebRequestActivitySource.ActivitySourceName,
                ActivityStarted = this.ActivityStarted,
                ActivityStopped = this.ActivityStopped,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => activitySamplingResult,
            };

            ActivitySource.AddActivityListener(this.activityListener);

            this.onEvent = onEvent;
        }

        public ConcurrentQueue<KeyValuePair<string, Activity>> Records { get; } = new ConcurrentQueue<KeyValuePair<string, Activity>>();

        public void Dispose()
        {
            this.activityListener.Dispose();
        }

        public void ActivityStarted(Activity activity) => this.Record("Start", activity);

        public void ActivityStopped(Activity activity) => this.Record("Stop", activity);

        private void Record(string eventName, Activity activity)
        {
            var record = new KeyValuePair<string, Activity>(eventName, activity);

            this.Records.Enqueue(record);
            this.onEvent?.Invoke(record);
        }
    }
}
#endif
