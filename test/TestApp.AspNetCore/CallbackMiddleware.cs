// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

internal sealed class CallbackMiddleware
{
    private readonly CallbackMiddlewareCore core;
    private readonly RequestDelegate next;

    public CallbackMiddleware(RequestDelegate next, CallbackMiddlewareCore core)
    {
        this.next = next;
        this.core = core;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (this.core == null || await this.core.ProcessAsync(context).ConfigureAwait(true))
        {
            await this.next(context).ConfigureAwait(true);
        }
    }
}
