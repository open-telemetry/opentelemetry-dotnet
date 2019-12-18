// <copyright file="ListenerHandler.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector
{
    public abstract class ListenerHandler
    {
        protected readonly Tracer Tracer;

        public ListenerHandler(string sourceName, Tracer tracer)
        {
            this.SourceName = sourceName;
            this.Tracer = tracer;
        }

        public string SourceName { get; }

        public abstract void OnStartActivity(Activity activity, object payload);

        public virtual void OnStopActivity(Activity activity, object payload)
        {
            var span = this.Tracer.CurrentSpan;

            if (span == null || span == BlankSpan.Instance)
            {
                CollectorEventSource.Log.NullOrBlankSpan("ListenerHandler.OnStopActivity");
                return;
            }

            foreach (var tag in activity.Tags)
            {
                span.SetAttribute(tag.Key, tag.Value);
            }
        }

        public virtual void OnException(Activity activity, object payload)
        {
            var span = this.Tracer.CurrentSpan;

            // TODO: gather exception information
        }

        public virtual void OnCustom(string name, Activity activity, object payload)
        {
            // if custom handler needs to react on other events - this method should be overridden
        }
    }
}
