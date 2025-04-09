// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

internal sealed class ActivityMiddleware
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
}
