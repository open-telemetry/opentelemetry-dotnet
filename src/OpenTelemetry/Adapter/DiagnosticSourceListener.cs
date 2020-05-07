// <copyright file="DiagnosticSourceListener.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Adapter
{
    internal class DiagnosticSourceListener : IObserver<KeyValuePair<string, object>>
    {
        private readonly ListenerHandler handler;

        public DiagnosticSourceListener(ListenerHandler handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

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
                AdapterEventSource.Log.NullActivity(value.Key);
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
            catch (Exception ex)
            {
                AdapterEventSource.Log.UnknownErrorProcessingEvent(this.handler?.SourceName, value.Key, ex);
            }
        }
    }
}
