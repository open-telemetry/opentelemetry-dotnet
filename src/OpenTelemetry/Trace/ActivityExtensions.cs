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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods on Activity.
    /// </summary>
    public static class ActivityExtensions
    {
        internal const string ResourcePropertyName = "OTel.Resource";

        /// <summary>
        /// Gets the Resource associated with the Activity.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <returns>The resource.</returns>
        public static Resource GetResource(this Activity activity)
        {
            if (activity?.GetCustomProperty(ResourcePropertyName) is Resource res)
            {
                return res;
            }
            else
            {
                return Resource.Empty;
            }
        }

        /// <summary>
        /// Sets the Resource associated with the Activity..
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="resource">Resource to set to.</param>
        internal static void SetResource(this Activity activity, Resource resource)
        {
            activity.SetCustomProperty(ResourcePropertyName, resource);
        }
    }
}
