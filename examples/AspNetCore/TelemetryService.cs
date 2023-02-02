// <copyright file="TelemetryService.cs" company="OpenTelemetry Authors">
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

namespace Examples.AspNetCore;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public class TelemetryService : ITelemetryService
{
    private const string InstrumentationScopeName = "Examples.AspNetCore";

    public TelemetryService(string version)
    {
        this.ActivitySource = new ActivitySource(InstrumentationScopeName, version);
        this.Meter = new Meter(InstrumentationScopeName, version);
        this.FreezingDaysCounter = this.Meter.CreateCounter<int>("weather.days.freezing", "The number of days where the temperature is below freezing");
    }

    public ActivitySource ActivitySource { get; }

    public Meter Meter { get; }

    public Counter<int> FreezingDaysCounter { get; }
}
