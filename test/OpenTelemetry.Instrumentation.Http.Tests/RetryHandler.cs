// <copyright file="RetryHandler.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests;

public class RetryHandler : DelegatingHandler
{
    private readonly int maxRetries;

    public RetryHandler(HttpMessageHandler innerHandler, int maxRetries)
        : base(innerHandler)
    {
        this.maxRetries = maxRetries;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = null;
        for (int i = 0; i < this.maxRetries; i++)
        {
            response?.Dispose();

            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        return response;
    }
}
