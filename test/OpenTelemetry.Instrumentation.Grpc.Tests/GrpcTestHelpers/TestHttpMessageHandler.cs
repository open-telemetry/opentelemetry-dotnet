// <copyright file="TestHttpMessageHandler.cs" company="OpenTelemetry Authors">
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
