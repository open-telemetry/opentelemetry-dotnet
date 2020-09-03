﻿// <copyright file="ActivityExtensionsTest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class ActivityExtensionsTest
    {
        private const string ActivitySourceName = "test.status";
        private const string ActivityName = "Test Activity";

        [Fact]
        public void SetStatus()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
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
                .AddSource(ActivitySourceName)
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
                .AddSource(ActivitySourceName)
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
                .AddSource(ActivitySourceName)
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
                .AddSource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Cancelled);
            activity.SetStatus(Status.Ok);
            activity?.Stop();

            Assert.True(activity.GetStatus().IsOk);
        }

        [Fact]
        public void CheckRecordException()
        {
            var message = "message";
            var exception = new ArgumentNullException(message, new Exception(message));
            var activity = new Activity("test-activity");
            activity.RecordException(exception);

            var @event = activity.Events.FirstOrDefault(e => e.Name == SemanticConventions.AttributeExceptionEventName);
            Assert.Equal(message, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
            Assert.Equal(exception.GetType().Name, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
        }

        [Fact]
        public void GetTagValueEmpty()
        {
            Activity activity = new Activity("Test");

            Assert.Null(activity.GetTagValue("Tag1"));
        }

        [Fact]
        public void GetTagValue()
        {
            Activity activity = new Activity("Test");
            activity.AddTag("Tag1", "Value1");

            Assert.Equal("Value1", activity.GetTagValue("Tag1"));
            Assert.Null(activity.GetTagValue("tag1"));
            Assert.Null(activity.GetTagValue("Tag2"));
        }
    }
}
