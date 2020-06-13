// <copyright file="ActivityCreationScenarios.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace Benchmarks.Tracing
{
    internal class ActivityCreationScenarios
    {
        public static Activity CreateActivity(ActivitySource source)
        {
            var activity = source.StartActivity("name");
            activity?.Stop();
            return activity;
        }

        public static Activity CreateActivityFromParentContext(ActivitySource source, ActivityContext parentCtx)
        {
            var activity = source.StartActivity("name", ActivityKind.Internal, parentCtx);
            activity?.Stop();
            return activity;
        }

        public static Activity CreateActivityFromParentId(ActivitySource source, string parentId)
        {
            var activity = source.StartActivity("name", ActivityKind.Internal, parentId);
            activity?.Stop();
            return activity;
        }

        public static Activity CreateActivityWithAttributes(ActivitySource source)
        {
            var activity = source.StartActivity("name");
            activity?.AddTag("tag1", "value1");
            activity?.AddTag("tag2", "value2");
            activity?.AddTag("customPropTag1", "somecustomValue");
            activity?.Stop();
            return activity;
        }

        public static Activity CreateActivityWithAttributesAndCustomProperty(ActivitySource source)
        {
            var activity = source.StartActivity("name");
            activity?.AddTag("tag1", "value1");
            activity?.AddTag("tag2", "value2");

            // use custom property instead of tags
            // activity?.AddTag("customPropTag1", "somecustomValue");
            activity?.SetCustomProperty("customPropTag1", "somecustomValue");
            activity?.Stop();
            return activity;
        }
    }
}
