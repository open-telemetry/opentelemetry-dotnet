// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
