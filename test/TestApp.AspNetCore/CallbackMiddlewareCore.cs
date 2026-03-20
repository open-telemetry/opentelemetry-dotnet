// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

internal sealed class CallbackMiddlewareCore
{
    public Task<bool> ProcessAsync(HttpContext context)
    {
        System.Diagnostics.Debug.Assert(context != null, "HttpContext is null.");
        return Task.FromResult(true);
    }
}
