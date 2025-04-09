// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

public class ActivityMiddleware
{
    private readonly ActivityMiddlewareCore core;
    private readonly RequestDelegate next;

    public ActivityMiddleware(RequestDelegate next, ActivityMiddlewareCore core)
    {
        this.next = next;
        this.core = core;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (this.core != null)
        {
            this.core.PreProcess(context);
        }

        await this.next(context).ConfigureAwait(true);

        if (this.core != null)
        {
            this.core.PostProcess(context);
        }
    }

    public class ActivityMiddlewareCore
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
