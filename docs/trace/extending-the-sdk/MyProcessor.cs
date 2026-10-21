// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;

internal sealed class MyProcessor : BaseProcessor<Activity>
{
    private readonly string name;

    public MyProcessor(string name = "MyProcessor")
    {
        this.name = name;
    }

    public override void OnStart(Activity data)
    {
        Console.WriteLine($"{this.name}.OnStart({data.DisplayName})");
    }

    public override void OnEnd(Activity data)
    {
        Console.WriteLine($"{this.name}.OnEnd({data.DisplayName})");
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        Console.WriteLine($"{this.name}.OnForceFlush({timeoutMilliseconds})");
        return true;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Console.WriteLine($"{this.name}.OnShutdown({timeoutMilliseconds})");
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Console.WriteLine($"{this.name}.Dispose({disposing})");
    }
}
