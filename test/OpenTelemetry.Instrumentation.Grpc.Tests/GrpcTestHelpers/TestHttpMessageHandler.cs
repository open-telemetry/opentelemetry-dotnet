// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers;

public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync;

    public TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        this.sendAsync = sendAsync;
    }

    public static TestHttpMessageHandler Create(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
    {
        var tcs = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        return new TestHttpMessageHandler(async (request, cancellationToken) =>
        {
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

            var result = await Task.WhenAny(sendAsync(request), tcs.Task).ConfigureAwait(false);
            return await result.ConfigureAwait(false);
        });
    }

    public static TestHttpMessageHandler Create(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        return new TestHttpMessageHandler(sendAsync);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return this.sendAsync(request, cancellationToken);
    }
}
