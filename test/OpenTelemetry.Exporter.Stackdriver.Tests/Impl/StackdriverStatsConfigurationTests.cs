// <copyright file="StackdriverStatsConfigurationTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Stackriver.Tests
{
    using OpenTelemetry.Exporter.Stackdriver.Implementation;
    using System;
    using Xunit;

    public class StackdriverStatsConfigurationTests
    {
        public StackdriverStatsConfigurationTests()
        {
            // Setting this for unit testing purposes, so we don't need credentials for real Google Cloud Account
            Environment.SetEnvironmentVariable("GOOGLE_PROJECT_ID", "test", EnvironmentVariableTarget.Process);
        }

        [Fact]
        public void StatsConfiguration_ByDefault_MetricNamePrefixEmpty()
        {
            Assert.NotNull(StackdriverStatsConfiguration.Default);
            Assert.Equal(GoogleCloudResourceUtils.GetProjectId(), StackdriverStatsConfiguration.Default.ProjectId);
            Assert.Equal(string.Empty, StackdriverStatsConfiguration.Default.MetricNamePrefix);
        }

        [Fact]
        public void StatsConfiguration_ByDeafult_ProjectIdIsGoogleCloudProjectId()
        {
            Assert.NotNull(StackdriverStatsConfiguration.Default);
            Assert.Equal(GoogleCloudResourceUtils.GetProjectId(), StackdriverStatsConfiguration.Default.ProjectId);
        }

        [Fact]
        public void StatsConfiguration_ByDefault_ExportIntervalMinute()
        {
            Assert.Equal(TimeSpan.FromMinutes(1), StackdriverStatsConfiguration.Default.ExportInterval);
        }

        [Fact]
        public void StatsConfiguration_ByDefault_MonitoredResourceIsGlobal()
        {
            Assert.NotNull(StackdriverStatsConfiguration.Default.MonitoredResource);

            Assert.Equal(Constants.GLOBAL, StackdriverStatsConfiguration.Default.MonitoredResource.Type);

            Assert.NotNull(StackdriverStatsConfiguration.Default.MonitoredResource.Labels);

            Assert.True(StackdriverStatsConfiguration.Default.MonitoredResource.Labels.ContainsKey("project_id"));
            Assert.True(StackdriverStatsConfiguration.Default.MonitoredResource.Labels.ContainsKey(Constants.PROJECT_ID_LABEL_KEY));
            Assert.Equal(
                StackdriverStatsConfiguration.Default.ProjectId,
                StackdriverStatsConfiguration.Default.MonitoredResource.Labels[Constants.PROJECT_ID_LABEL_KEY]);
        }
    }
}