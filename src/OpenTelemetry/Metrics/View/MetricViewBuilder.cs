// <copyright file="MetricViewBuilder.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    internal class MetricViewBuilder
    {
        internal readonly KeyValuePair<string, object>[] Tags;
        internal readonly ViewSet[] TagSpans;

        private int tagsStartPos = 0;
        private int tagsLen = 0;
        private int spanPos = 0;

        public MetricViewBuilder()
        {
            this.Tags = new KeyValuePair<string, object>[100];
            this.TagSpans = new ViewSet[100];
        }

        public int Count => this.spanPos;

        public void Clear()
        {
            this.tagsStartPos = 0;
            this.tagsLen = 0;
            this.spanPos = 0;
        }

        public ReadOnlySpan<KeyValuePair<string, object>> GetViewAt(int pos, out MetricView view)
        {
            var viewSet = this.TagSpans[pos];

            view = viewSet.View;
            return new ReadOnlySpan<KeyValuePair<string, object>>(this.Tags, viewSet.Pos, viewSet.Len);
        }

        internal void Add(string name, object value)
        {
            this.Tags[this.tagsStartPos + this.tagsLen] = new KeyValuePair<string, object>(name, value);
            this.tagsLen++;
        }

        internal void Commit(MetricView view)
        {
            this.TagSpans[this.spanPos] = new ViewSet(view, this.tagsStartPos, this.tagsLen);

            this.tagsStartPos += this.tagsLen;
            this.tagsLen = 0;

            this.spanPos++;
        }

        internal KeyValuePair<string, object>? FindInCurrentSet(string key)
        {
            for (int i = 0; i < this.tagsLen; i++)
            {
                var kv = this.Tags[this.tagsStartPos + i];
                if (kv.Key == key)
                {
                    return kv;
                }
            }

            return null;
        }

        internal bool ApplyView(MetricView view, Instrument instrument, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            bool match = false;

            if (view.Selector != null)
            {
                if (view.Selector(instrument))
                {
                    match = true;
                }
            }

            if (match)
            {
                // Filter out for included tags.
                foreach (var tag in tags)
                {
                    bool found = false;
                    bool hasRule = false;

                    foreach (var rule in view.ViewRules)
                    {
                        if (rule is IncludeTagRule valid)
                        {
                            hasRule = true;

                            if (valid.ValidFunc(tag.Key))
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found || !hasRule)
                    {
                        this.Add(tag.Key, tag.Value);
                    }
                }

                // Put back any required tags with default values.
                foreach (var rule in view.ViewRules)
                {
                    if (rule is RequireTagRule require)
                    {
                        if (this.FindInCurrentSet(require.Name) == null)
                        {
                            this.Add(require.Name, require.DefaultValue);
                        }
                    }
                }

                this.Commit(view);
            }

            return match;
        }

        internal struct ViewSet
        {
            internal MetricView View;
            internal int Pos;
            internal int Len;

            public ViewSet(MetricView view, int pos, int len)
            {
                this.View = view;
                this.Pos = pos;
                this.Len = len;
            }
        }
    }
}
