// <copyright file="ActivityInstrumentationHelperTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Instrumentation.Tests
{
    public class ActivityInstrumentationHelperTest
    {
        [Theory]
        [InlineData("TestActivitySource", null)]
        [InlineData("TestActivitySource", "1.0.0")]
        public void SetActivitySource(string name, string version)
        {
            using var activity = new Activity("Test");
            using var activitySource = new ActivitySource(name, version);

            activity.Start();
            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, activitySource);
            Assert.Equal(activitySource.Name, activity.Source.Name);
            Assert.Equal(activitySource.Version, activity.Source.Version);
            activity.Stop();
        }

        [Theory]
        [InlineData(ActivityKind.Client)]
        [InlineData(ActivityKind.Consumer)]
        [InlineData(ActivityKind.Internal)]
        [InlineData(ActivityKind.Producer)]
        [InlineData(ActivityKind.Server)]
        public void SetActivityKind(ActivityKind activityKind)
        {
            using var activity = new Activity("Test");
            activity.Start();
            ActivityInstrumentationHelper.SetKindProperty(activity, activityKind);
            Assert.Equal(activityKind, activity.Kind);
        }
    }
}
