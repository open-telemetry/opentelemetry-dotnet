// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.BlazorWasm.TestApp;

// Simulates a custom async handler that could cause issues with Blazor
// when using HttpClientFactory. See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7708.

internal sealed class AsyncYieldingDelegatingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
