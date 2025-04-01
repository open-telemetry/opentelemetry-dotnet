// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;

namespace Examples.Console;

internal class InstrumentationWithActivitySource : IDisposable
{
    private const string RequestPath = "/api/request";
    private readonly SampleServer server = new();
    private readonly SampleClient client = new();

    public void Start(ushort port = 19999)
    {
        var url = $"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}{RequestPath}/";
        this.server.Start(url);
        this.client.Start(url);
    }

    public void Dispose()
    {
        this.client.Dispose();
        this.server.Dispose();
    }

    private class SampleServer : IDisposable
    {
        private readonly HttpListener listener = new();

        public void Start(string url)
        {
            this.listener.Prefixes.Add(url);
            this.listener.Start();

            Task.Run(() =>
            {
                using var source = new ActivitySource("Samples.SampleServer");

                while (this.listener.IsListening)
                {
                    try
                    {
                        var context = this.listener.GetContext();

                        using var activity = source.StartActivity(
                            $"{context.Request.HttpMethod}:{context.Request.Url!.AbsolutePath}",
                            ActivityKind.Server);

                        var headerKeys = context.Request.Headers.AllKeys;
                        foreach (var headerKey in headerKeys)
                        {
                            string? headerValue = context.Request.Headers[headerKey];
                            activity?.SetTag($"http.header.{headerKey}", headerValue);
                        }

                        string requestContent;
                        using (var childSpan = source.StartActivity("ReadStream", ActivityKind.Consumer))
                        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        {
                            requestContent = reader.ReadToEnd();
                            childSpan?.AddEvent(new ActivityEvent("StreamReader.ReadToEnd"));
                        }

                        activity?.SetTag("request.content", requestContent);
                        activity?.SetTag("request.length", requestContent.Length);

                        var echo = Encoding.UTF8.GetBytes("echo: " + requestContent);
                        context.Response.ContentEncoding = Encoding.UTF8;
                        context.Response.ContentLength64 = echo.Length;
                        context.Response.OutputStream.Write(echo, 0, echo.Length);
                        context.Response.Close();
                    }
                    catch (Exception)
                    {
                        // expected when closing the listener.
                    }
                }
            });
        }

        public void Dispose()
        {
            ((IDisposable)this.listener).Dispose();
        }
    }

    private class SampleClient : IDisposable
    {
        private CancellationTokenSource? cts;
        private Task? requestTask;

        public void Start(string url)
        {
            this.cts = new CancellationTokenSource();
            var cancellationToken = this.cts.Token;

            this.requestTask = Task.Run(
                async () =>
                {
                    using var source = new ActivitySource("Samples.SampleClient");
                    using var client = new HttpClient();

                    var count = 1;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);

                        using (var activity = source.StartActivity("POST:" + RequestPath, ActivityKind.Client))
                        {
                            count++;

                            activity?.AddEvent(new ActivityEvent("PostAsync:Started"));
                            using var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                            activity?.AddEvent(new ActivityEvent("PostAsync:Ended"));

                            activity?.SetTag("http.status_code", (int)response.StatusCode);

                            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            activity?.SetTag("response.content", responseContent);
                            activity?.SetTag("response.length", responseContent.Length.ToString(CultureInfo.InvariantCulture));

                            foreach (var header in response.Headers)
                            {
                                if (header.Value is IEnumerable<object> enumerable)
                                {
                                    activity?.SetTag($"http.header.{header.Key}", string.Join(",", enumerable));
                                }
                                else
                                {
                                    activity?.SetTag($"http.header.{header.Key}", header.Value.ToString());
                                }
                            }
                        }

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                    }
                },
                cancellationToken);
        }

        public void Dispose()
        {
            if (this.cts != null)
            {
                this.cts.Cancel();
                this.requestTask!.Wait();
                this.requestTask.Dispose();
                this.cts.Dispose();
            }
        }
    }
}
