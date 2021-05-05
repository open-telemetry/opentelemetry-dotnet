// <copyright file="MetricProviderBuilderSdk.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    public class MetricProviderBuilderSdk
    {
        private List<Func<Instrument, bool>> includeMeters = new List<Func<Instrument, bool>>();
        private MetricProvider.BuildOptions options = new MetricProvider.BuildOptions();

        public MetricProviderBuilderSdk()
        {
        }

        public MetricProviderBuilderSdk IncludeInstrument(Func<Instrument, bool> meterFunc)
        {
            this.includeMeters.Add(meterFunc);
            return this;
        }

        public MetricProviderBuilderSdk SetObservationPeriod(int periodMilli)
        {
            this.options.ObservationPeriodMilliseconds = periodMilli;
            return this;
        }

        public MetricProviderBuilderSdk Verbose(bool verbose)
        {
            this.options.Verbose = verbose;
            return this;
        }

        public MetricProvider Build()
        {
            this.options.IncludeMeters = this.includeMeters.ToArray();
            return new MetricProvider(this.options);
        }
    }
}
