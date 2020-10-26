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

namespace Benchmarks.Helper
{
    internal static class ActivityCreationScenarios
    {
        public static void CreateActivity(ActivitySource source)
        {
            using var activity = source.StartActivity("name");
            activity?.Stop();
        }

        public static void CreateActivityWithKind(ActivitySource source)
        {
            using var activity = source.StartActivity("name", ActivityKind.Client);
            activity?.Stop();
        }

        public static void CreateActivityFromParentContext(ActivitySource source, ActivityContext parentCtx)
        {
            using var activity = source.StartActivity("name", ActivityKind.Internal, parentCtx);
            activity?.Stop();
        }

        public static void CreateActivityFromParentId(ActivitySource source, string parentId)
        {
            using var activity = source.StartActivity("name", ActivityKind.Internal, parentId);
            activity?.Stop();
        }

        public static void CreateActivityWithAttributes(ActivitySource source)
        {
            using var activity = source.StartActivity("name");
            activity?.SetTag("tag1", "string");
            activity?.SetTag("tag2", 1);
            activity?.SetTag("tag3", true);
            activity?.Stop();
        }

        public static void CreateActivityWithAttributesAndCustomProperty(ActivitySource source)
        {
            using var activity = source.StartActivity("name");
            activity?.SetTag("tag1", "string");
            activity?.SetTag("tag2", 1);

            // use custom property instead of tags
            // activity?.SetTag("customPropTag1", "somecustomValue");
            activity?.SetCustomProperty("customPropTag1", "somecustomValue");
            activity?.Stop();
        }
    }
}
