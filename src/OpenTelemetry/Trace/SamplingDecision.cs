// <copyright file="SamplingDecision.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Enumeration to define sampling decision.
    /// </summary>
    public enum SamplingDecision
    {
        /// <summary>
        /// The activity object needs to be created. It will have Name, Source, Id and Baggage.
        /// Other properties will be ignored.
        /// </summary>
        Drop,

        /// <summary>
        /// The activity object should be populated with all the propagation info and also all other
        /// properties such as Links, Tags, and Events. Activity.IsAllDataRequested will return true.
        /// </summary>
        RecordOnly,

        /// <summary>
        /// The activity object should be populated with all the propagation info and also all other
        /// properties such as Links, Tags, and Events.
        /// Both Activity.IsAllDataRequested and Activity.IsRecorded will return true.
        /// </summary>
        RecordAndSample,
    }
}
