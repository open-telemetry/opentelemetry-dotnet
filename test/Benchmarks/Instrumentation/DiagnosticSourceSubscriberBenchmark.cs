// <copyright file="DiagnosticSourceSubscriberBenchmark.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Instrumentation;

namespace Benchmarks.Instrumentation;

[InProcess]
public class DiagnosticSourceSubscriberBenchmark
{
    [Params(1, 2)]
    public int SubscriberCount;

    [Params(false, true)]
    public bool UseIsEnabledFilter;

    private const string SourceName = "MySource";

    private readonly DiagnosticListener listener = new(SourceName);
    private readonly List<DiagnosticSourceSubscriber> subscribers = new();
    private readonly Func<string, object, object, bool> isEnabledFilter = (name, arg1, arg2) => ((EventPayload)arg1).Data == "Data";

    [GlobalSetup]
    public void GlobalSetup()
    {
        for (var i = 0; i < this.SubscriberCount; ++i)
        {
            var subscriber = new DiagnosticSourceSubscriber(
                new TestListener(),
                this.UseIsEnabledFilter ? this.isEnabledFilter : null,
                logUnknownException: null);

            this.subscribers.Add(subscriber);
            subscriber.Subscribe();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        foreach (var subscriber in this.subscribers)
        {
            subscriber.Dispose();
        }
    }

    [Benchmark]
    public void WriteDiagnosticSourceEvent()
    {
        var payload = new EventPayload("Data");
        this.listener.Write("SomeEvent", payload);
    }

    private struct EventPayload
    {
        public EventPayload(string data)
        {
            this.Data = data;
        }

        public string Data { get; }
    }

    private class TestListener : ListenerHandler
    {
        public TestListener()
            : base(DiagnosticSourceSubscriberBenchmark.SourceName)
        {
        }

        public override bool SupportsNullActivity => true;
    }
}
