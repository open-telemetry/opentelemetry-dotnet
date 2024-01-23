// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
                response = await base.SendAsync(request, cancellationToken);
            }
            catch
            {
            }
        }

        return response;
    }
}
