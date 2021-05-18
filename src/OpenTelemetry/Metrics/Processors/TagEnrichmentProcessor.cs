// <copyright file="TagEnrichmentProcessor.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Example of a MeasurmentProcessor that adds a new attribute to all measurements.
    /// </summary>
    public class TagEnrichmentProcessor : MeasurementProcessor
    {
        private KeyValuePair<string, object> extraAttrib;

        public TagEnrichmentProcessor(string name, string value)
        {
            this.extraAttrib = new KeyValuePair<string, object>(name, value);
        }

        public override void OnEnd(MeasurementItem data)
        {
            var newArray = new KeyValuePair<string, object>[data.Point.Tags.Length + 1];
            data.Point.Tags.CopyTo(newArray, 0);
            newArray[newArray.Length - 1] = this.extraAttrib;

            data.Point = data.Point.NewWithValue();
            data.Point.ResetTags(newArray);
        }
    }
}
