// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

public class ActivityMiddleware
{
    private readonly ActivityMiddlewareImpl impl;
    private readonly RequestDelegate next;

    public ActivityMiddleware(RequestDelegate next, ActivityMiddlewareImpl impl)
    {
        this.next = next;
        this.impl = impl;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (this.impl != null)
        {
            this.impl.PreProcess(context);
        }

        await this.next(context).ConfigureAwait(true);

        if (this.impl != null)
        {
            this.impl.PostProcess(context);
        }
    }

    public class ActivityMiddlewareImpl
    {
        public virtual void PreProcess(HttpContext context)
        {
            // Do nothing
        }

        public virtual void PostProcess(HttpContext context)
        {
            // Do nothing
        }
    }
}
