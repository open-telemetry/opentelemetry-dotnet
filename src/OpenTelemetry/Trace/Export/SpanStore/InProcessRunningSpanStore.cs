// <copyright file="InProcessRunningSpanStore.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System.Collections.Generic;
    using OpenTelemetry.Utils;

    /// <inheritdoc/>
    public sealed class InProcessRunningSpanStore : RunningSpanStoreBase
    {
        private readonly ConcurrentIntrusiveList<Span> runningSpans;

        /// <summary>
        /// Constructs a new <see cref="InProcessRunningSpanStore"/>.
        /// </summary>
        public InProcessRunningSpanStore()
        {
            this.runningSpans = new ConcurrentIntrusiveList<Span>();
        }

        /// <inheritdoc/>
        public override IRunningSpanStoreSummary Summary
        {
            get
            {
                IEnumerable<Span> allRunningSpans = this.runningSpans.Copy();
                var numSpansPerName = new Dictionary<string, int>();
                foreach (var span in allRunningSpans)
                {
                    numSpansPerName.TryGetValue(span.Name, out var prevValue);
                    numSpansPerName[span.Name] = prevValue + 1;
                }

                var perSpanNameSummary = new Dictionary<string, IRunningPerSpanNameSummary>();
                foreach (var it in numSpansPerName)
                {
                    var numRunningSpans = it.Value;
                    var runningPerSpanNameSummary = RunningPerSpanNameSummary.Create(numRunningSpans);
                    perSpanNameSummary[it.Key] = runningPerSpanNameSummary;
                }

                var summary = RunningSpanStoreSummary.Create(perSpanNameSummary);
                return summary;
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<SpanData> GetRunningSpans(IRunningSpanStoreFilter filter)
        {
            IReadOnlyCollection<Span> allRunningSpans = this.runningSpans.Copy();
            var maxSpansToReturn = filter.MaxSpansToReturn == 0 ? allRunningSpans.Count : filter.MaxSpansToReturn;
            var ret = new List<SpanData>(maxSpansToReturn);
            foreach (var span in allRunningSpans)
            {
                if (ret.Count == maxSpansToReturn)
                {
                    break;
                }

                if (span.Name.Equals(filter.SpanName))
                {
                    ret.Add(span.ToSpanData());
                }
            }

            return ret;
        }

        /// <inheritdoc/>
        public override void OnEnd(ISpan span)
        {
            if (span is Span spanBase)
            {
                this.runningSpans.RemoveElement(spanBase);
            }
        }

        /// <inheritdoc/>
        public override void OnStart(ISpan span)
        {
            if (span is Span spanBase)
            {
                this.runningSpans.AddElement(spanBase);
            }
        }
    }
}
