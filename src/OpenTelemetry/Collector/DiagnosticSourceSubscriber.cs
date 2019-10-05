﻿// <copyright file="DiagnosticSourceSubscriber.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using OpenTelemetry.Trace;

    public class DiagnosticSourceSubscriber<TInput> : IDisposable, IObserver<DiagnosticListener>
    {
        private readonly Dictionary<string, Func<ITracerFactory, Func<TInput, ISampler>, ListenerHandler<TInput>>> handlers;
        private readonly ITracerFactory tracerFactory;
        private readonly Func<TInput, ISampler> sampler;
        private ConcurrentDictionary<string, DiagnosticSourceListener<TInput>> subscriptions;
        private long disposed;
        private IDisposable subscription;

        public DiagnosticSourceSubscriber(Dictionary<string, Func<ITracerFactory, Func<TInput, ISampler>, ListenerHandler<TInput>>> handlers, ITracerFactory tracerFactory, Func<TInput, ISampler> sampler)
        {
            this.subscriptions = new ConcurrentDictionary<string, DiagnosticSourceListener<TInput>>();
            this.handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            this.tracerFactory = tracerFactory ?? throw new ArgumentNullException(nameof(tracerFactory));
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
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
            if ((Interlocked.Read(ref this.disposed) == 0) && this.subscriptions != null)
            {
                if (this.handlers.ContainsKey(value.Name))
                {
                    this.subscriptions?.GetOrAdd(value.Name, name =>
                    {
                        var dl = new DiagnosticSourceListener<TInput>(this.handlers[value.Name](this.tracerFactory, this.sampler));
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
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
            {
                return;
            }

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
