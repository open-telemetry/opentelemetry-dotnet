// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Runtime.InteropServices;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public class PrometheusHttpListenerAcceptFailureTests
{
    [Fact]
    public async Task ScrapesAreStillServedAfterHttpListenerRepeatedlyFailsToGetRequest()
    {
        Assert.SkipWhen(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "HTTP.sys does not fail the pending GetContextAsync() operation when requests do not pass authentication.");

        const int Clients = 32;

        // Every rejected request that is queued behind the held one blocks a thread pool thread while
        // it waits for the listener's lock, which is held for as long as the first one is held. Make
        // sure the pool can still run this test's own continuations, and does not wait for the held
        // request to time out, however few processors there are.
        PrometheusHttpListenerTests.EnsureThreadPoolWorkerThreadsAvailable(minimumWorkerThreads: Clients * 2);

        var timeout = TimeSpan.FromSeconds(10);

        using var meter = new Meter(nameof(PrometheusHttpListenerAcceptFailureTests));

        // The failure is logged by the thread that accepts requests, not this one
        using var eventListener = new AcceptRequestFailedListener();

        using var requestHeld = new ManualResetEventSlim();
        using var allowRequestToBeRejected = new ManualResetEventSlim();

        var holdNextRequest = 0;

        using var context = PrometheusHttpListenerTests.CreateMeterProvider(meter, configureListener: (options) =>
        {
            // The listener itself answers requests without credentials with 401,
            // they are not handed to the exporter
            options.ConfigureHttpListener = (_, listener) => listener.AuthenticationSchemeSelectorDelegate = (_) =>
            {
                if (Interlocked.Exchange(ref holdNextRequest, 0) == 1)
                {
                    requestHeld.Set();
                    allowRequestToBeRejected.Wait(timeout);
                }

                return AuthenticationSchemes.Basic;
            };

            return options.Port;
        });

        meter.CreateCounter<long>("counter_long").Add(1);

        var requestUri = new Uri("metrics", UriKind.Relative);
        var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("user:password"));

        using var httpClient = new HttpClient { BaseAddress = context.BaseAddress, Timeout = timeout };

        using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
        {
            request.Headers.Authorization = new("Basic", credentials);

            using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // On the managed (non-Windows) HttpListener implementation, the pending GetContextAsync()
        // operation fails when more than 20 requests that are already waiting are rejected one
        // after the other. To get there, hold the rejection of a first request, which is sent on
        // the connection of the scrape above so that it is not read by the thread that accepts
        // connections, and let more requests queue up behind it.
        //
        // The accept loop only forgets its earlier failures once it is handed a request, which none
        // of the rejected ones are, so repeating this without a scrape in between makes it fail
        // repeatedly and exercises the back-off between attempts. A request is then handed to the
        // operation that failed, so only some of the rounds fail the pending operation.
        const int Rounds = 5;
        const int MinimumFailures = 2;

        for (var round = 0; round < Rounds; round++)
        {
            requestHeld.Reset();
            allowRequestToBeRejected.Reset();
            Volatile.Write(ref holdNextRequest, 1);

            var heldRequest = httpClient.GetAsync(requestUri, TestContext.Current.CancellationToken);

            var clients = new List<System.Net.Sockets.TcpClient>();

            try
            {
                Assert.True(requestHeld.Wait(timeout, TestContext.Current.CancellationToken));

                var unauthenticated = System.Text.Encoding.ASCII.GetBytes(
                    $"GET /metrics HTTP/1.1\r\nHost: localhost:{context.Port}\r\nConnection: close\r\n\r\n");

                for (var i = 0; i < Clients; i++)
                {
                    var client = new System.Net.Sockets.TcpClient();
                    clients.Add(client);

#if NET
                    await client.ConnectAsync(context.BaseAddress.Host, context.Port, TestContext.Current.CancellationToken);
                    await client.GetStream().WriteAsync(unauthenticated, TestContext.Current.CancellationToken);
#else
                    await client.ConnectAsync(context.BaseAddress.Host, context.Port);
                    await client.GetStream().WriteAsync(unauthenticated, 0, unauthenticated.Length, TestContext.Current.CancellationToken);
#endif
                }

                // Give the listener time to read the requests
                await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

                allowRequestToBeRejected.Set();

                using (var response = await heldRequest)
                {
                    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                }

                foreach (var client in clients)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                    cts.CancelAfter(timeout);

                    var statusLine = await ReadStatusLineAsync(client.GetStream(), cts.Token);

                    Assert.StartsWith("HTTP/1.1 401", statusLine, StringComparison.Ordinal);
                }
            }
            finally
            {
                allowRequestToBeRejected.Set();

                foreach (var client in clients)
                {
                    client.Dispose();
                }
            }
        }

        string? content = null;

        // The listener can still hand the first request that follows to the operation which
        // failed, and that request is then never answered, so allow for further attempts.
        for (var attempt = 0; attempt < 5 && string.IsNullOrEmpty(content); attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new("Basic", credentials);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            try
            {
                using var response = await httpClient.SendAsync(request, cts.Token);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

#if NET
                content = await response.Content.ReadAsStringAsync(cts.Token);
#else
                content = await response.Content.ReadAsStringAsync();
#endif
            }
            catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
            {
                // Not answered
            }
        }

        Assert.NotNull(content);
        Assert.Contains("counter_long", content, StringComparison.Ordinal);

        // Guard against the scenario no longer being reproduced, in which case the
        // scrapes above would succeed whether or not failures are handled
        Assert.True(
            eventListener.Count >= MinimumFailures,
            $"HttpListener failed to get a request {eventListener.Count} time(s) instead of at least {MinimumFailures}, so the test did not exercise repeated failures.");
    }

    private static async Task<string> ReadStatusLineAsync(System.Net.Sockets.NetworkStream stream, CancellationToken cancellationToken)
    {
        // A read may return only part of the response, so keep reading until the status line is complete
        var buffer = new byte[512];
        var length = 0;

        while (length < buffer.Length)
        {
#if NET
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
#else
            var read = await stream.ReadAsync(buffer, length, buffer.Length - length, cancellationToken);
#endif

            if (read == 0)
            {
                break;
            }

            length += read;

            if (Array.IndexOf(buffer, (byte)'\n', 0, length) >= 0)
            {
                break;
            }
        }

        return System.Text.Encoding.ASCII.GetString(buffer, 0, length);
    }

    private sealed class AcceptRequestFailedListener : EventListener
    {
        private int count;

        public int Count => Volatile.Read(ref this.count);

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "OpenTelemetry-Exporter-Prometheus")
            {
                this.EnableEvents(eventSource, EventLevel.Error);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == 17)
            {
                Interlocked.Increment(ref this.count);
            }
        }
    }
}
