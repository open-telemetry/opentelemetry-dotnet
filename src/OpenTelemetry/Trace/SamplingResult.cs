// <copyright file="SamplingResult.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Sampling decision.
    /// </summary>
    public readonly struct SamplingResult : System.IEquatable<SamplingResult>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
        /// </summary>
        /// <param name="decision"> indicates whether an activity object is recorded and sampled.</param>
        public SamplingResult(SamplingDecision decision)
        {
            this.Decision = decision;
            this.Attributes = Enumerable.Empty<KeyValuePair<string, object>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
        /// </summary>
        /// <param name="isSampled"> True if sampled, false otherwise.</param>
        public SamplingResult(bool isSampled)
        {
            this.Decision = isSampled ? SamplingDecision.RecordAndSample : SamplingDecision.Drop;
            this.Attributes = Enumerable.Empty<KeyValuePair<string, object>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
        /// </summary>
        /// <param name="decision">indicates whether an activity object is recorded and sampled.</param>
        /// <param name="attributes">Attributes associated with the sampling decision. Attributes list passed to
        /// this method must be immutable. Mutations of the collection and/or attribute values may lead to unexpected behavior.</param>
        public SamplingResult(SamplingDecision decision, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            this.Decision = decision;

            // Note: Decision object takes ownership of the collection.
            // Current implementation has no means to ensure the collection will not be modified by the caller.
            // If this behavior will be abused we must switch to cloning of the collection.
            this.Attributes = attributes;
        }

        /// <summary>
        /// Gets a value indicating indicates whether an activity object is recorded and sampled.
        /// </summary>
        public SamplingDecision Decision { get; }

        /// <summary>
        /// Gets a map of attributes associated with the sampling decision.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes { get; }

        /// <summary>
        /// Compare two <see cref="SamplingResult"/> for equality.
        /// </summary>
        /// <param name="decision1">First Decision to compare.</param>
        /// <param name="decision2">Second Decision to compare.</param>
        public static bool operator ==(SamplingResult decision1, SamplingResult decision2) => decision1.Equals(decision2);

        /// <summary>
        /// Compare two <see cref="SamplingResult"/> for not equality.
        /// </summary>
        /// <param name="decision1">First Decision to compare.</param>
        /// <param name="decision2">Second Decision to compare.</param>
        public static bool operator !=(SamplingResult decision1, SamplingResult decision2) => !decision1.Equals(decision2);

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is not SamplingResult)
            {
                return false;
            }

            var that = (SamplingResult)obj;
            return this.Decision == that.Decision && this.Attributes == that.Attributes;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var result = 1;
            result = (31 * result) + this.Decision.GetHashCode();
            result = (31 * result) + this.Attributes.GetHashCode();
            return result;
        }

        /// <inheritdoc/>
        public bool Equals(SamplingResult other)
        {
            return this.Decision == other.Decision && this.Attributes.SequenceEqual(other.Attributes);
        }
    }
}
