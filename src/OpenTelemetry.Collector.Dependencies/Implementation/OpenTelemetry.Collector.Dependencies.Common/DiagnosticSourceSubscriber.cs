// <copyright file="DiagnosticSourceSubscriber.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Collector.Dependencies.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using OpenTelemetry.Trace;

    internal class DiagnosticSourceSubscriber : IDisposable, IObserver<DiagnosticListener>
    {
        private readonly Dictionary<string, Func<ITracer, Func<HttpRequestMessage, ISampler>, ListenerHandler>> handlers;
        private readonly ITracer tracer;
        private readonly Func<HttpRequestMessage, ISampler> sampler;
        private ConcurrentDictionary<string, DiagnosticSourceListener> subscriptions;
        private bool disposing;
        private IDisposable subscription;

        public DiagnosticSourceSubscriber(Dictionary<string, Func<ITracer, Func<HttpRequestMessage, ISampler>, ListenerHandler>> handlers, ITracer tracer, Func<HttpRequestMessage, ISampler> sampler)
        {
            this.subscriptions = new ConcurrentDictionary<string, DiagnosticSourceListener>();
            this.handlers = handlers;
            this.tracer = tracer;
            this.sampler = sampler;
        }

        public void Subscribe()
        {
            if (this.subscription == null)
            {
                this.subscription = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if (!Volatile.Read(ref this.disposing) && this.subscriptions != null)
            {
                if (this.handlers.ContainsKey(value.Name))
                {
                    this.subscriptions.GetOrAdd(value.Name, name =>
                    {
                        var dl = new DiagnosticSourceListener(value.Name, this.handlers[value.Name](this.tracer, this.sampler));
                        dl.Subscription = value.Subscribe(dl);
                        return dl;
                    });
                }
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            Volatile.Write(ref this.disposing, true);

            var subsCopy = this.subscriptions;
            this.subscriptions = null;

            var keys = subsCopy.Keys;
            foreach (var key in keys)
            {
                if (subsCopy.TryRemove(key, out var sub))
                {
                    sub?.Dispose();
                }
            }

            this.subscription?.Dispose();
            this.subscription = null;
        }
    }
}
