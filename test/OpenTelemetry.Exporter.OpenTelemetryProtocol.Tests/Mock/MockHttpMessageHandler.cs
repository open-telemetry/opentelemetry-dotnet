// <copyright file="MockHttpMessageHandler.cs" company="OpenTelemetry Authors">
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

using System.Net.Http;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Mock;

internal class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage HttpRequestMessage { get; private set; }

    public byte[] HttpRequestContent { get; private set; }

    public virtual HttpResponseMessage MockSend(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.HttpRequestMessage = request;
        this.HttpRequestContent = request.Content.ReadAsByteArrayAsync().Result;
        return new HttpResponseMessage();
    }

#if NET6_0_OR_GREATER
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return this.MockSend(request, cancellationToken);
    }
#endif

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.MockSend(request, cancellationToken));
    }
}
