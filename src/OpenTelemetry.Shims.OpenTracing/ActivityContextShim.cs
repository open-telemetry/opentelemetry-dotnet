// <copyright file="ActivityContextShim.cs" company="OpenTelemetry Authors">
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
using global::OpenTracing;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing
{
    public sealed class ActivityContextShim : ISpanContext
    {
        public ActivityContextShim(in ActivityContext activityContext)
        {
            if (!activityContext.IsValid())
            {
                throw new ArgumentException(nameof(activityContext));
            }

            this.Context = activityContext;
        }

        public ActivityContext Context { get; private set; }

        /// <inheritdoc/>
        public string TraceId => this.Context.TraceId.ToString();

        /// <inheritdoc/>
        public string SpanId => this.Context.SpanId.ToString();

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return Activity.Current.Baggage;
        }
    }
}
