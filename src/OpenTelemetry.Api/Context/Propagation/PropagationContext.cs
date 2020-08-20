// <copyright file="PropagationContext.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// Stores propagation data.
    /// </summary>
    public readonly struct PropagationContext : IEquatable<PropagationContext>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropagationContext"/> struct.
        /// </summary>
        /// <param name="activityContext">Entries for activity context.</param>
        /// <param name="activityBaggage">Entries for activity baggage.</param>
        public PropagationContext(ActivityContext activityContext, IEnumerable<KeyValuePair<string, string>> activityBaggage)
        {
            this.ActivityContext = activityContext;
            this.ActivityBaggage = activityBaggage;
        }

        /// <summary>
        /// Gets <see cref="ActivityContext"/>.
        /// </summary>
        public ActivityContext ActivityContext { get; }

        /// <summary>
        /// Gets ActivityBaggage.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> ActivityBaggage { get; }

        /// <summary>
        /// Compare two entries of <see cref="PropagationContext"/> for equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator ==(PropagationContext left, PropagationContext right) => left.Equals(right);

        /// <summary>
        /// Compare two entries of <see cref="PropagationContext"/> for not equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator !=(PropagationContext left, PropagationContext right) => !(left == right);

        /// <inheritdoc/>
        public bool Equals(PropagationContext value)
        {
            if (this.ActivityContext != value.ActivityContext
                || this.ActivityBaggage is null != value.ActivityBaggage is null)
            {
                return false;
            }

            if (this.ActivityBaggage is null)
            {
                return true;
            }

            if (this.ActivityBaggage.Count() != value.ActivityBaggage.Count())
            {
                return false;
            }

            var thisEnumerator = this.ActivityBaggage.GetEnumerator();
            var valueEnumerator = value.ActivityBaggage.GetEnumerator();

            while (thisEnumerator.MoveNext() && valueEnumerator.MoveNext())
            {
                if (thisEnumerator.Current.Key != valueEnumerator.Current.Key
                    || thisEnumerator.Current.Value != valueEnumerator.Current.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => (obj is PropagationContext context) && this.Equals(context);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hashCode = 323591981;
            hashCode = (hashCode * -1521134295) + this.ActivityContext.GetHashCode();
            hashCode = (hashCode * -1521134295) + EqualityComparer<IEnumerable<KeyValuePair<string, string>>>.Default.GetHashCode(this.ActivityBaggage);
            return hashCode;
        }
    }
}
