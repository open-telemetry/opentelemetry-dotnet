// <copyright file="AzureSdkCollector.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collectors.Azure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Context;
    using OpenTelemetry.Trace;

    public class AzureSdkCollector : IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>
    {
        private readonly ConcurrentDictionary<Activity, IScope> scopes = new ConcurrentDictionary<Activity, IScope>(new ActivityReferenceEqualityComparer());

        private readonly ITracer tracer;

        private readonly ISampler sampler;

        private List<IDisposable> subscriptions = new List<IDisposable>();

        public AzureSdkCollector(ITracer tracer, ISampler sampler)
        {
            this.tracer = tracer;
            this.sampler = sampler;

            this.subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        public void Dispose()
        {
            lock (this.subscriptions)
            {
                foreach (var subscription in this.subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key.EndsWith("Start"))
            {
                this.OnStartActivity(Activity.Current, value.Value);
            }
            else if (value.Key.EndsWith("Stop"))
            {
                // Current.Parent is used because OT wraps additional Activity over
                this.OnStopActivity(Activity.Current.Parent, value.Value);
            }
            else if (value.Key.EndsWith("Exception"))
            {
                // Current.Parent is used because OT wraps additional Activity over
                this.OnException(Activity.Current.Parent, value.Value);
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name.StartsWith("Azure"))
            {
                lock (this.subscriptions)
                {
                    this.subscriptions.Add(value.Subscribe(this));
                }
            }
        }

        private void OnStartActivity(Activity current, object valueValue)
        {
            var operationName = current.OperationName;
            foreach (var keyValuePair in current.Tags)
            {
                if (keyValuePair.Key == "http.url")
                {
                    var indexOfQuery = keyValuePair.Value.IndexOf('?');
                    if (indexOfQuery == -1)
                    {
                        indexOfQuery = keyValuePair.Value.Length;
                    }

                    operationName = keyValuePair.Value.Substring(0, indexOfQuery);
                }
            }

            var span = this.tracer.SpanBuilder(operationName)
                .SetSampler(this.sampler)
                .StartSpan();

            this.scopes.TryAdd(current, this.tracer.WithSpan(span));
        }

        private void OnStopActivity(Activity current, object valueValue)
        {
            var span = this.tracer.CurrentSpan;
            foreach (var keyValuePair in current.Tags)
            {
                span.SetAttribute(keyValuePair.Key, keyValuePair.Value);
            }

            this.scopes.TryRemove(current, out var scope);

            scope?.Dispose();
        }

        private void OnException(Activity current, object valueValue)
        {
            var span = this.tracer.CurrentSpan;
            foreach (var keyValuePair in current.Tags)
            {
                span.SetAttribute(keyValuePair.Key, keyValuePair.Value);
            }

            span.Status = Status.Unknown;

            this.scopes.TryRemove(current, out var scope);
            scope?.Dispose();
        }

        public class ActivityReferenceEqualityComparer : EqualityComparer<Activity>
        {
            public override bool Equals(Activity x, Activity y)
            {
                return ReferenceEquals(x, y);
            }

            public override int GetHashCode(Activity obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
