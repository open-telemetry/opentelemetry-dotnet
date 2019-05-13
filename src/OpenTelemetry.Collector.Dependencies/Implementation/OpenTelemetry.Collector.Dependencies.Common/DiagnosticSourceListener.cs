// <copyright file="DiagnosticSourceListener.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using System.Diagnostics;

    internal class DiagnosticSourceListener : IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly string sourceName;
        private readonly ListenerHandler handler;

        public DiagnosticSourceListener(string sourceName, ListenerHandler handler)
        {
            this.sourceName = sourceName;
            this.handler = handler;
        }

        public IDisposable Subscription { get; set; }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (Activity.Current == null)
            {
                Debug.WriteLine("Activity is null " + value.Key);
                return;
            }

            try
            {
                if (value.Key.EndsWith("Start"))
                {
                    this.handler.OnStartActivity(Activity.Current, value.Value);
                }
                else if (value.Key.EndsWith("Stop"))
                {
                    this.handler.OnStopActivity(Activity.Current, value.Value);
                }
                else if (value.Key.EndsWith("Exception"))
                {
                    this.handler.OnException(Activity.Current, value.Value);
                }
                else
                {
                    this.handler.OnCustom(value.Key, Activity.Current, value.Value);
                }
            }
            catch (Exception e)
            {
                // Debug.WriteLine(e);
                // TODO: make sure to output the handler name as part of error message
            }
        }

        public void Dispose()
        {
            this.Subscription?.Dispose();
        }
    }
}