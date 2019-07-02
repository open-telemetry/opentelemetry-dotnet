// <copyright file="CurrentSpanUtils.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using OpenTelemetry.Trace.Internal;

namespace OpenTelemetry.Trace
{
    using OpenTelemetry.Context;

    internal class CurrentSpanUtils
    {
        private readonly ConditionalWeakTable<Activity, ISpan> activitySpanTable = new ConditionalWeakTable<Activity, ISpan>();

        public ISpan CurrentSpan
        {
            get
            {
                var currentActivity = Activity.Current;
                if (currentActivity == null)
                {
                    return BlankSpan.Instance;
                }

                if (this.activitySpanTable.TryGetValue(currentActivity, out var currentSpan))
                {
                    return currentSpan;
                }

                return BlankSpan.Instance;
            }
        }

        private void SetSpan(Activity activity, ISpan span)
        {
            if (activity == null)
            {
                // log error
                return;
            }

            if (this.activitySpanTable.TryGetValue(activity, out _))
            {
                // log warning
            }

            this.activitySpanTable.Add(activity, span);
        }

        public IScope WithSpan(ISpan span, bool endSpan)
        {
            return new ScopeInSpan(span, endSpan, this);
        }

        private sealed class ScopeInSpan : IScope
        {
            private readonly ISpan span;
            private readonly bool endSpan;
            private readonly CurrentSpanUtils currentUtils;

            public ScopeInSpan(ISpan span, bool endSpan, CurrentSpanUtils currentUtils)
            {
                this.span = span;
                this.endSpan = endSpan;
                this.currentUtils = currentUtils;
                this.currentUtils.SetSpan(Activity.Current, span);
            }

            public void Dispose()
            {
                var current = this.currentUtils.CurrentSpan;
                current?.Activity?.Stop();

                if (this.endSpan)
                {
                    this.span.End();
                }
            }
        }
    }
}
