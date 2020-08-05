// <copyright file="MyActivityProcessor.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;

internal class MyActivityProcessor : ActivityProcessor
{
    private readonly string name;

    public MyActivityProcessor(string name)
    {
        this.name = name;
    }

    public override string ToString()
    {
        return $"{this.GetType()}({this.name})";
    }

    public override void OnStart(Activity activity)
    {
        Console.WriteLine($"{this}.OnStart");
    }

    public override void OnEnd(Activity activity)
    {
        Console.WriteLine($"{this}.OnEnd");
    }

    public override Task ForceFlushAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{this}.ForceFlushAsync");
        return Task.CompletedTask;
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{this}.ShutdownAsync");
        return Task.CompletedTask;
    }
}
