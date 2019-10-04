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

namespace OpenTelemetry.Collector
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    public class DiagnosticSourceSubscriber : IDisposable, IObserver<DiagnosticListener>
    {
        private readonly ListenerHandler handler;
        private readonly Func<string, object, object, bool> filter;
        private DiagnosticSourceListener listener;
        private long disposed;
        private IDisposable subscription;

        public DiagnosticSourceSubscriber(ListenerHandler handler, Func<string, object, object, bool> filter)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            this.filter = filter;
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
            if ((Interlocked.Read(ref this.disposed) == 0) && this.listener == null)
            {
                if (this.handler.SourceName == value.Name)
                {
                    this.listener = new DiagnosticSourceListener(this.handler);
                    this.listener.Subscription = this.filter == null ?
                        value.Subscribe(this.listener) : 
                        value.Subscribe(this.listener, this.filter);
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

            this.listener?.Dispose();
            this.listener = null;

            this.subscription?.Dispose();
            this.subscription = null;
        }
    }
}
