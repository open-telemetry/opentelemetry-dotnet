// <copyright file="MetricView.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    internal struct MetricView
    {
        internal readonly string Name;
        internal readonly string Description;
        internal readonly Func<Instrument, bool> Selector;
        internal readonly IViewRule[] ViewRules;
        internal readonly Func<Instrument, IAggregator[]> Aggregators;

        public MetricView(string name, string description, Func<Instrument, bool> selector, Func<Instrument, IAggregator[]> aggregators, IViewRule[] rules)
        {
            this.Name = name;
            this.Description = description;
            this.Selector = selector;
            this.ViewRules = rules;
            this.Aggregators = aggregators;
        }
    }
}
