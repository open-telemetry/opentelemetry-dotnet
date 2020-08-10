// <copyright file="ActivityExtensionsTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    public class ActivityExtensionsTest
    {
        private const string ActivitySourceName = "test.status";
        private const string ActivityName = "Test Activity";

        [Fact]
        public void SetStatus()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddActivitySource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Ok);
            activity?.Stop();

            Assert.True(activity.GetStatus().IsOk);
        }

        [Fact]
        public void SetStatusWithDescription()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddActivitySource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.NotFound.WithDescription("Not Found"));
            activity?.Stop();

            var status = activity.GetStatus();
            Assert.Equal(StatusCanonicalCode.NotFound, status.CanonicalCode);
            Assert.Equal("Not Found", status.Description);
        }

        [Fact]
        public void SetCancelledStatus()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddActivitySource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Cancelled);
            activity?.Stop();

            Assert.True(activity.GetStatus().CanonicalCode.Equals(Status.Cancelled.CanonicalCode));
        }

        [Fact]
        public void GetStatusWithNoStatusInActivity()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddActivitySource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity?.Stop();

            Assert.False(activity.GetStatus().IsValid);
        }

        [Fact]
        public void LastSetStatusWins()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddActivitySource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Cancelled);
            activity.SetStatus(Status.Ok);
            activity?.Stop();

            Assert.True(activity.GetStatus().IsOk);
        }

        [Theory]
        [InlineData(ActivityKind.Client)]
        [InlineData(ActivityKind.Consumer)]
        [InlineData(ActivityKind.Internal)]
        [InlineData(ActivityKind.Producer)]
        [InlineData(ActivityKind.Server)]
        public void SetKindSimpleActivity(ActivityKind inputOutput)
        {
            var activity = new Activity("test-activity");
            activity.SetKind(inputOutput);

            Assert.Equal(inputOutput, activity.Kind);
        }
    }
}
