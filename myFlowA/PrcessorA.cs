// <copyright file="MyProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Logs;

internal class MyProcessor : BaseProcessor<LogRecord>
{
    private readonly string name;

    public MyProcessor(string name = "MyProcessor")
    {
        this.name = name;
    }

    public override void OnStart(LogRecord logRecord)
    {
        Console.WriteLine($"{this.name}.OnStart({logRecord})");
    }

    public override void OnEnd(LogRecord logRecord)
    {
        Console.WriteLine("hello!");
        Console.WriteLine($"{this.name}.OnEnd({logRecord})");
    }
}
