// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

public class CallbackMiddleware
{
    private readonly CallbackMiddlewareImpl impl;
    private readonly RequestDelegate next;

    public CallbackMiddleware(RequestDelegate next, CallbackMiddlewareImpl impl)
    {
        this.next = next;
        this.impl = impl;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (this.impl == null || await this.impl.ProcessAsync(context).ConfigureAwait(true))
        {
            await this.next(context).ConfigureAwait(true);
        }
    }

    public class CallbackMiddlewareImpl
    {
        public virtual Task<bool> ProcessAsync(HttpContext context)
        {
            return Task.FromResult(true);
        }
    }
}
