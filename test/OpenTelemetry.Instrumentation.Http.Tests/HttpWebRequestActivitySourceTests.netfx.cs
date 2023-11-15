// <copyright file="HttpWebRequestActivitySourceTests.netfx.cs" company="OpenTelemetry Authors">
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

#if NETFRAMEWORK
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpWebRequestActivitySourceTests : IDisposable
{
    private static bool validateBaggage;
    private readonly IDisposable testServer;
    private readonly string testServerHost;
    private readonly int testServerPort;
    private readonly string netPeerName;
    private readonly int netPeerPort;

    static HttpWebRequestActivitySourceTests()
    {
        HttpClientInstrumentationOptions options = new()
        {
            EnrichWithHttpWebRequest = (activity, httpWebRequest) =>
            {
                VerifyHeaders(httpWebRequest);

                if (validateBaggage)
                {
                    ValidateBaggage(httpWebRequest);
                }
            },
        };

        HttpWebRequestActivitySource.TracingOptions = options;

        _ = Sdk.SuppressInstrumentation;
    }

    public HttpWebRequestActivitySourceTests()
    {
        Assert.Null(Activity.Current);
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = false;

        this.testServer = TestHttpServer.RunServer(
            ctx => ProcessServerRequest(ctx),
            out this.testServerHost,
            out this.testServerPort);

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
    /// A simple test to make sure the Http Diagnostic Source is added into the list of DiagnosticListeners.
    /// </summary>
    [Fact]
    public void TestHttpDiagnosticListenerIsRegistered()
    {
        bool listenerFound = false;
        using ActivityListener activityListener = new ActivityListener
        {
            ShouldListenTo = activitySource =>
            {
                if (activitySource.Name == HttpWebRequestActivitySource.ActivitySourceName)
                {
                    listenerFound = true;
                    return true;
                }

                return false;
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        Assert.True(listenerFound, "The Http Diagnostic Listener didn't get added to the AllListeners list.");
    }

    /// <summary>
    /// A simple test to make sure the Http Diagnostic Source is initialized properly after we subscribed to it, using
    /// the subscribe overload with just the observer argument.
    /// </summary>
    [Fact]
    public async Task TestReflectInitializationViaSubscription()
    {
        using var eventRecords = new ActivitySourceRecorder();

        // Send a random Http request to generate some events
        using (var client = new HttpClient())
        {
            (await client.GetAsync(this.BuildRequestUrl())).Dispose();
        }

        // Just make sure some events are written, to confirm we successfully subscribed to it.
        // We should have exactly one Start and one Stop event
        Assert.Equal(2, eventRecords.Records.Count);
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
                ? await client.GetAsync(url)
                : await client.PostAsync(url, new StringContent("hello world"))).Dispose();
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

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task TestBasicReceiveAndResponseEventsWithoutSampling(string method)
    {
        using var eventRecords = new ActivitySourceRecorder(activitySamplingResult: ActivitySamplingResult.None);

        // Send a random Http request to generate some events
        using (var client = new HttpClient())
        {
            (method == "GET"
                ? await client.GetAsync(this.BuildRequestUrl())
                : await client.PostAsync(this.BuildRequestUrl(), new StringContent("hello world"))).Dispose();
        }

        // There should be no events because we turned off sampling.
        Assert.Empty(eventRecords.Records);
    }

    [Theory]
    [InlineData("GET", 0)]
    [InlineData("GET", 1)]
    [InlineData("GET", 2)]
    [InlineData("GET", 3)]
    [InlineData("POST", 0)]
    [InlineData("POST", 1)]
    [InlineData("POST", 2)]
    [InlineData("POST", 3)]
    public async Task TestBasicReceiveAndResponseWebRequestEvents(string method, int mode)
    {
        string url = this.BuildRequestUrl();

        using var eventRecords = new ActivitySourceRecorder();

        // Send a random Http request to generate some events
        var webRequest = (HttpWebRequest)WebRequest.Create(url);

        if (method == "POST")
        {
            webRequest.Method = method;

            Stream stream = null;
            switch (mode)
            {
                case 0:
                    stream = webRequest.GetRequestStream();
                    break;
                case 1:
                    stream = await webRequest.GetRequestStreamAsync();
                    break;
                case 2:
                    {
                        object state = new object();
                        using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                        IAsyncResult asyncResult = webRequest.BeginGetRequestStream(
                            ar =>
                            {
                                Assert.Equal(state, ar.AsyncState);
                                handle.Set();
                            },
                            state);
                        stream = webRequest.EndGetRequestStream(asyncResult);
                        if (!handle.WaitOne(TimeSpan.FromSeconds(30)))
                        {
                            throw new InvalidOperationException();
                        }

                        handle.Dispose();
                    }

                    break;
                case 3:
                    {
                        using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                        object state = new object();
                        webRequest.BeginGetRequestStream(
                            ar =>
                            {
                                stream = webRequest.EndGetRequestStream(ar);
                                Assert.Equal(state, ar.AsyncState);
                                handle.Set();
                            },
                            state);
                        if (!handle.WaitOne(TimeSpan.FromSeconds(30)))
                        {
                            throw new InvalidOperationException();
                        }

                        handle.Dispose();
                    }

                    break;
                default:
                    throw new NotSupportedException();
            }

            Assert.NotNull(stream);

            using StreamWriter writer = new StreamWriter(stream);

            writer.WriteLine("hello world");
        }

        WebResponse webResponse = null;
        switch (mode)
        {
            case 0:
                webResponse = webRequest.GetResponse();
                break;
            case 1:
                webResponse = await webRequest.GetResponseAsync();
                break;
            case 2:
                {
                    object state = new object();
                    using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                    IAsyncResult asyncResult = webRequest.BeginGetResponse(
                        ar =>
                        {
                            Assert.Equal(state, ar.AsyncState);
                            handle.Set();
                        },
                        state);
                    webResponse = webRequest.EndGetResponse(asyncResult);
                    if (!handle.WaitOne(TimeSpan.FromSeconds(30)))
                    {
                        throw new InvalidOperationException();
                    }

                    handle.Dispose();
                }

                break;
            case 3:
                {
                    using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                    object state = new object();
                    webRequest.BeginGetResponse(
                        ar =>
                        {
                            webResponse = webRequest.EndGetResponse(ar);
                            Assert.Equal(state, ar.AsyncState);
                            handle.Set();
                        },
                        state);
                    if (!handle.WaitOne(TimeSpan.FromSeconds(30)))
                    {
                        throw new InvalidOperationException();
                    }

                    handle.Dispose();
                }

                break;
            default:
                throw new NotSupportedException();
        }

        Assert.NotNull(webResponse);

        using StreamReader reader = new StreamReader(webResponse.GetResponseStream());

        reader.ReadToEnd(); // Make sure response is not disposed.

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

    [Fact]
    public async Task TestTraceStateAndBaggage()
    {
        try
        {
            using var eventRecords = new ActivitySourceRecorder();

            using var parent = new Activity("w3c activity");
            parent.SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());
            parent.TraceStateString = "some=state";
            parent.Start();

            Baggage.SetBaggage("k", "v");

            // Send a random Http request to generate some events
            using (var client = new HttpClient())
            {
                (await client.GetAsync(this.BuildRequestUrl())).Dispose();
            }

            parent.Stop();

            Assert.Equal(2, eventRecords.Records.Count());

            // Check to make sure: The first record must be a request, the next record must be a response.
            _ = AssertFirstEventWasStart(eventRecords);
        }
        finally
        {
            this.CleanUpActivity();
        }
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task DoNotInjectTraceParentWhenPresent(string method)
    {
        try
        {
            using var eventRecords = new ActivitySourceRecorder();

            // Send a random Http request to generate some events
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, this.BuildRequestUrl()))
            {
                request.Headers.Add("traceparent", "00-abcdef0123456789abcdef0123456789-abcdef0123456789-01");

                if (method == "GET")
                {
                    request.Method = HttpMethod.Get;
                }
                else
                {
                    request.Method = HttpMethod.Post;
                    request.Content = new StringContent("hello world");
                }

                (await client.SendAsync(request)).Dispose();
            }

            // No events are sent.
            Assert.Empty(eventRecords.Records);
        }
        finally
        {
            this.CleanUpActivity();
        }
    }

    /// <summary>
    /// Test to make sure we get both request and response events.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task TestResponseWithoutContentEvents(string method)
    {
        string url = this.BuildRequestUrl(queryString: "responseCode=204");

        using var eventRecords = new ActivitySourceRecorder();

        // Send a random Http request to generate some events
        using (var client = new HttpClient())
        {
            using HttpResponseMessage response = method == "GET"
                ? await client.GetAsync(url)
                : await client.PostAsync(url, new StringContent("hello world"));
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

        VerifyActivityStopTags(204, activity);
    }

    /// <summary>
    /// Test that if request is redirected, it gets only one Start and one Stop event.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task TestRedirectedRequest(string method)
    {
        using var eventRecords = new ActivitySourceRecorder();

        using (var client = new HttpClient())
        {
            using HttpResponseMessage response = method == "GET"
                ? await client.GetAsync(this.BuildRequestUrl(queryString: "redirects=10"))
                : await client.PostAsync(this.BuildRequestUrl(queryString: "redirects=10"), new StringContent("hello world"));
        }

        // We should have exactly one Start and one Stop event
        Assert.Equal(2, eventRecords.Records.Count());
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Stop"));
    }

    /// <summary>
    /// Test exception in request processing: exception should have expected type/status and now be swallowed by reflection hook.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task TestRequestWithException(string method)
    {
        string host = Guid.NewGuid().ToString() + ".com";
        string url = method == "GET"
            ? $"http://{host}"
            : $"http://{host}";

        using var eventRecords = new ActivitySourceRecorder();

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
        {
            return method == "GET"
                ? new HttpClient().GetAsync(url)
                : new HttpClient().PostAsync(url, new StringContent("hello world"));
        });

        // check that request failed because of the wrong domain name and not because of reflection
        var webException = (WebException)ex.InnerException;
        Assert.NotNull(webException);
        Assert.True(webException.Status == WebExceptionStatus.NameResolutionFailure);

        // We should have one Start event and one Stop event with an exception.
        Assert.Equal(2, eventRecords.Records.Count());
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Stop"));

        // Check to make sure: The first record must be a request, the next record must be an exception.
        Activity activity = AssertFirstEventWasStart(eventRecords);
        VerifyActivityStartTags(host, null, method, url, activity);

        Assert.True(eventRecords.Records.TryDequeue(out KeyValuePair<string, Activity> exceptionEvent));
        Assert.Equal("Stop", exceptionEvent.Key);

        Assert.True(activity.Status != ActivityStatusCode.Unset);
        Assert.Null(activity.StatusDescription);
    }

    /// <summary>
    /// Test request cancellation: reflection hook does not throw.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task TestCanceledRequest(string method)
    {
        string url = this.BuildRequestUrl();

        CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var eventRecords = new ActivitySourceRecorder(_ => { cts.Cancel(); });

        using (var client = new HttpClient())
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            {
                return method == "GET"
                    ? client.GetAsync(url, cts.Token)
                    : client.PostAsync(url, new StringContent("hello world"), cts.Token);
            });
            Assert.True(ex is TaskCanceledException || ex is WebException);
        }

        // We should have one Start event and one Stop event with an exception.
        Assert.Equal(2, eventRecords.Records.Count());
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Stop"));

        Activity activity = AssertFirstEventWasStart(eventRecords);
        VerifyActivityStartTags(this.netPeerName, this.netPeerPort, method, url, activity);

        Assert.True(eventRecords.Records.TryDequeue(out KeyValuePair<string, Activity> exceptionEvent));
        Assert.Equal("Stop", exceptionEvent.Key);

        Assert.True(exceptionEvent.Value.Status != ActivityStatusCode.Unset);
        Assert.True(exceptionEvent.Value.StatusDescription == null);
    }

    /// <summary>
    /// Test request connection exception: reflection hook does not throw.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task TestSecureTransportFailureRequest(string method)
    {
        string url = "https://expired.badssl.com/";

        using var eventRecords = new ActivitySourceRecorder();

        using (var client = new HttpClient())
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            {
                // https://expired.badssl.com/ has an expired certificae.
                return method == "GET"
                    ? client.GetAsync(url)
                    : client.PostAsync(url, new StringContent("hello world"));
            });
            Assert.True(ex is HttpRequestException);
        }

        // We should have one Start event and one Stop event with an exception.
        Assert.Equal(2, eventRecords.Records.Count());
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Stop"));

        Activity activity = AssertFirstEventWasStart(eventRecords);
        VerifyActivityStartTags("expired.badssl.com", null, method, url, activity);

        Assert.True(eventRecords.Records.TryDequeue(out KeyValuePair<string, Activity> exceptionEvent));
        Assert.Equal("Stop", exceptionEvent.Key);

        Assert.True(exceptionEvent.Value.Status != ActivityStatusCode.Unset);
        Assert.Null(exceptionEvent.Value.StatusDescription);
    }

    /// <summary>
    /// Test request connection retry: reflection hook does not throw.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task TestSecureTransportRetryFailureRequest(string method)
    {
        // This test sends an https request to an endpoint only set up for http.
        // It should retry. What we want to test for is 1 start, 1 exception event even
        // though multiple are actually sent.

        string url = this.BuildRequestUrl(useHttps: true);

        using var eventRecords = new ActivitySourceRecorder();

        using (var client = new HttpClient())
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            {
                return method == "GET"
                    ? client.GetAsync(url)
                    : client.PostAsync(url, new StringContent("hello world"));
            });
            Assert.True(ex is HttpRequestException);
        }

        // We should have one Start event and one Stop event with an exception.
        Assert.Equal(2, eventRecords.Records.Count());
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Stop"));

        Activity activity = AssertFirstEventWasStart(eventRecords);
        VerifyActivityStartTags(this.netPeerName, this.netPeerPort, method, url, activity);

        Assert.True(eventRecords.Records.TryDequeue(out KeyValuePair<string, Activity> exceptionEvent));
        Assert.Equal("Stop", exceptionEvent.Key);

        Assert.True(exceptionEvent.Value.Status != ActivityStatusCode.Unset);
        Assert.Null(exceptionEvent.Value.StatusDescription);
    }

    [Fact]
    public async Task TestInvalidBaggage()
    {
        validateBaggage = true;
        Baggage
            .SetBaggage("key", "value")
            .SetBaggage("bad/key", "value")
            .SetBaggage("goodkey", "bad/value");

        using var eventRecords = new ActivitySourceRecorder();

        using (var client = new HttpClient())
        {
            (await client.GetAsync(this.BuildRequestUrl())).Dispose();
        }

        Assert.Equal(2, eventRecords.Records.Count());
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(1, eventRecords.Records.Count(rec => rec.Key == "Stop"));

        validateBaggage = false;
    }

    /// <summary>
    /// Test to make sure every event record has the right dynamic properties.
    /// </summary>
    [Fact]
    public void TestMultipleConcurrentRequests()
    {
        ServicePointManager.DefaultConnectionLimit = int.MaxValue;
        using var parentActivity = new Activity("parent").Start();
        using var eventRecords = new ActivitySourceRecorder();

        Dictionary<Uri, Tuple<WebRequest, WebResponse>> requestData = new Dictionary<Uri, Tuple<WebRequest, WebResponse>>();
        for (int i = 0; i < 10; i++)
        {
            Uri uriWithRedirect = new Uri(this.BuildRequestUrl(queryString: $"q={i}&redirects=3"));

            requestData[uriWithRedirect] = null;
        }

        // Issue all requests simultaneously
        using var httpClient = new HttpClient();
        Dictionary<Uri, Task<HttpResponseMessage>> tasks = new Dictionary<Uri, Task<HttpResponseMessage>>();

        CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var url in requestData.Keys)
        {
            tasks.Add(url, httpClient.GetAsync(url, cts.Token));
        }

        // wait up to 10 sec for all requests and suppress exceptions
        Task.WhenAll(tasks.Select(t => t.Value).ToArray()).ContinueWith(tt =>
        {
            foreach (var task in tasks)
            {
                task.Value.Result?.Dispose();
            }
        }).Wait();

        // Examine the result. Make sure we got all successful requests.

        // Just make sure some events are written, to confirm we successfully subscribed to it. We should have
        // exactly 1 Start event per request and exactly 1 Stop event per response (if request succeeded)
        var successfulTasks = tasks.Where(t => t.Value.Status == TaskStatus.RanToCompletion);

        Assert.Equal(tasks.Count, eventRecords.Records.Count(rec => rec.Key == "Start"));
        Assert.Equal(successfulTasks.Count(), eventRecords.Records.Count(rec => rec.Key == "Stop"));

        // Check to make sure: We have a WebRequest and a WebResponse for each successful request
        foreach (var pair in eventRecords.Records)
        {
            Activity activity = pair.Value;

            Assert.True(
                pair.Key == "Start" ||
                pair.Key == "Stop",
                "An unexpected event of name " + pair.Key + "was received");
        }
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
        Assert.Equal(statusCode, activity.GetTagValue(SemanticConventions.AttributeHttpStatusCode));
    }

    private static void ActivityEnrichment(Activity activity, string method, object obj)
    {
        switch (method)
        {
            case "OnStartActivity":
                Assert.True(obj is HttpWebRequest);
                VerifyHeaders(obj as HttpWebRequest);

                if (validateBaggage)
                {
                    ValidateBaggage(obj as HttpWebRequest);
                }

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
