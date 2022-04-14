// <copyright file="EventSourceBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Benchmarks
{
    public class EventSourceBenchmarks
    {
        [Benchmark]
        public void EventWithIdAllocation()
        {
            Activity activity = new Activity("TestActivity");
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
            activity.Stop();

            OpenTelemetrySdkEventSource.Log.ActivityStarted(activity.OperationName, activity.Id);
        }

        [Benchmark]
        public void EventWithCheck()
        {
            Activity activity = new Activity("TestActivity");
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
            activity.Stop();

            OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);
        }
    }
}
