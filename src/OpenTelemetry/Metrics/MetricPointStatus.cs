// <copyright file="MetricPointStatus.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal enum MetricPointStatus
    {
        /// <summary>
        /// This status is applied to <see cref="MetricPoint"/>s with status <see cref="CollectPending"/> after a Collect.
        /// If an update occurs, status will be moved to <see cref="CollectPending"/>.
        /// </summary>
        NoCollectPending,

        /// <summary>
        /// The <see cref="MetricPoint"/> has been updated since the previous Collect cycle.
        /// Collect will move it to <see cref="NoCollectPending"/>.
        /// </summary>
        CollectPending,
    }
}
