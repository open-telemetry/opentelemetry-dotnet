// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

internal sealed class CallbackMiddlewareCore
{
    public Task<bool> ProcessAsync(HttpContext context)
    {
        return Task.FromResult(true);
    }
}
