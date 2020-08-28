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
using System.Diagnostics;

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
        /// <param name="activityContext"><see cref="System.Diagnostics.ActivityContext"/>.</param>
        /// <param name="baggage"><see cref="Baggage"/>.</param>
        public PropagationContext(ActivityContext activityContext, Baggage baggage)
        {
            this.ActivityContext = activityContext;
            this.Baggage = baggage;
        }

        /// <summary>
        /// Gets <see cref="System.Diagnostics.ActivityContext"/>.
        /// </summary>
        public ActivityContext ActivityContext { get; }

        /// <summary>
        /// Gets <see cref="Baggage"/>.
        /// </summary>
        public Baggage Baggage { get; }

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
            return this.ActivityContext == value.ActivityContext
                && this.Baggage == value.Baggage;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => (obj is PropagationContext context) && this.Equals(context);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hashCode = 323591981;
            hashCode = (hashCode * -1521134295) + this.ActivityContext.GetHashCode();
            hashCode = (hashCode * -1521134295) + this.Baggage.GetHashCode();
            return hashCode;
        }
    }
}
