// <copyright file="CorrelationContext.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Correlation context.
    /// </summary>
    public readonly struct CorrelationContext : IEquatable<CorrelationContext>
    {
        internal static readonly CorrelationContext Empty = new CorrelationContext(null);
        internal static readonly IEnumerable<KeyValuePair<string, string>> EmptyBaggage = new KeyValuePair<string, string>[0];
        private readonly Activity activity;

        internal CorrelationContext(in Activity activity)
        {
            this.activity = activity;
        }

        /// <summary>
        /// Gets the current <see cref="CorrelationContext"/>.
        /// </summary>
        public static CorrelationContext Current
        {
            get
            {
                Activity activity = Activity.Current;
                return activity == null
                    ? Empty
                    : new CorrelationContext(activity);
            }
        }

        /// <summary>
        /// Gets the correlation values.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Correlations => this.activity?.Baggage ?? EmptyBaggage;

        /// <summary>
        /// Compare two entries of <see cref="CorrelationContext"/> for equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator ==(CorrelationContext left, CorrelationContext right) => left.Equals(right);

        /// <summary>
        /// Compare two entries of <see cref="CorrelationContext"/> for equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator !=(CorrelationContext left, CorrelationContext right) => !(left == right);

        /// <summary>
        /// Retrieves a correlation item.
        /// </summary>
        /// <param name="key">Correlation item key.</param>
        /// <returns>Retrieved correlation value or <see langword="null"/> if no match was found.</returns>
        public string GetCorrelation(string key)
            => this.activity?.GetBaggageItem(key);

        /// <summary>
        /// Adds a correlation item.
        /// </summary>
        /// <param name="key">Correlation item key.</param>
        /// <param name="value">Correlation item value.</param>
        /// <returns>The <see cref="CorrelationContext"/> instance for chaining.</returns>
        public CorrelationContext AddCorrelation(string key, string value)
        {
            this.activity?.AddBaggage(key, value);

            return this;
        }

        /// <summary>
        /// Adds correlation items.
        /// </summary>
        /// <param name="correlations">Correlation items.</param>
        /// <returns>The <see cref="CorrelationContext"/> instance for chaining.</returns>
        public CorrelationContext AddCorrelation(IEnumerable<KeyValuePair<string, string>> correlations)
        {
            if (correlations != null)
            {
                foreach (KeyValuePair<string, string> correlation in correlations)
                {
                    this.activity?.AddBaggage(correlation.Key, correlation.Value);
                }
            }

            return this;
        }

        /// <inheritdoc/>
        public bool Equals(CorrelationContext other)
        {
            var thisCorrelations = this.Correlations;
            var otherCorrelations = other.Correlations;

            if (thisCorrelations.Count() != otherCorrelations.Count())
            {
                return false;
            }

            var thisEnumerator = thisCorrelations.GetEnumerator();
            var otherEnumerator = otherCorrelations.GetEnumerator();

            while (thisEnumerator.MoveNext() && otherEnumerator.MoveNext())
            {
                if (thisEnumerator.Current.Key != otherEnumerator.Current.Key
                    || thisEnumerator.Current.Value != otherEnumerator.Current.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CorrelationContext context && this.Equals(context);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.Correlations.GetHashCode();
        }
    }
}
