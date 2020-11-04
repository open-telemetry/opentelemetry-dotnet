// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods on Activity.
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// Gets the Resource associated with the Activity.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <returns>The resource.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Resource GetResource(this Activity activity)
        {
            return activity?.GetTagValue(Resource.ResourceTagName) is Resource res
                ? res
                : Resource.Empty;
        }

        /// <summary>
        /// Sets the Resource associated with the Activity.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="resource">Resource to set to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetResource(this Activity activity, Resource resource)
        {
            activity.SetTag(Resource.ResourceTagName, resource);
        }
    }
}
