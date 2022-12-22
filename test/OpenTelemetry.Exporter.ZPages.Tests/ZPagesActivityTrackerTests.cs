// <copyright file="ZPagesActivityTrackerTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.ZPages.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.ZPages.Tests
{
    public class ZPagesActivityTrackerTests
    {
        [Fact]
        public void CheckingPurge()
        {
            using var activity1 = new Activity("new");
            ZPagesActivityTracker.CurrentHourList.TryAdd("new", new ZPagesActivityAggregate(activity1));
            Assert.NotEmpty(ZPagesActivityTracker.CurrentHourList);
            ZPagesActivityTracker.PurgeCurrentHourData(null, null);
            Assert.Empty(ZPagesActivityTracker.CurrentHourList);

            using var activity2 = new Activity("new");
            ZPagesActivityTracker.CurrentMinuteList.TryAdd("new", new ZPagesActivityAggregate(activity2));
            Assert.NotEmpty(ZPagesActivityTracker.CurrentMinuteList);
            ZPagesActivityTracker.PurgeCurrentMinuteData(null, null);
            Assert.Empty(ZPagesActivityTracker.CurrentMinuteList);

            ZPagesActivityTracker.ProcessingList.Clear();
        }
    }
}
