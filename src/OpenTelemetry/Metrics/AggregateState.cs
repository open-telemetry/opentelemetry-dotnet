// <copyright file="AggregateState.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace OpenTelemetry.Metrics
{
    internal class AggregateState
    {
        internal long Count = 0;
        internal long Sum = 0;

        public virtual void Update(DataPoint? value)
        {
            long val = 0;

            if (value is DataPoint<int> idp)
            {
                val = idp.Value;
            }

            this.Count++;
            this.Sum += val;
        }
    }
}
