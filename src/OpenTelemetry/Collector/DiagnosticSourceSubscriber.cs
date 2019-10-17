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
using System;
using System.Diagnostics;
using System.Threading;

namespace OpenTelemetry.Collector
{
    public class DiagnosticSourceSubscriber : IDisposable, IObserver<DiagnosticListener>
    {
        private readonly ListenerHandler handler;
        private readonly Func<string, object, object, bool> filter;
        private long disposed;
        private IDisposable allSourcesSubscription;
        private IDisposable listenerSubscription;

        public DiagnosticSourceSubscriber(
            ListenerHandler handler, 
            Func<string, object, object, bool> filter)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            this.filter = filter;
        }

        public void Subscribe()
        {
            if (this.allSourcesSubscription == null)
            {
                this.allSourcesSubscription = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if ((Interlocked.Read(ref this.disposed) == 0) && 
                this.listenerSubscription == null && 
                this.handler.SourceName == value.Name)
            {
                var listener = new DiagnosticSourceListener(this.handler);
                this.listenerSubscription = this.filter == null ?
                    value.Subscribe(listener) : 
                    value.Subscribe(listener, this.filter);
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

            this.listenerSubscription?.Dispose();
            this.listenerSubscription = null;

            this.allSourcesSubscription?.Dispose();
            this.allSourcesSubscription = null;
        }
    }
}
