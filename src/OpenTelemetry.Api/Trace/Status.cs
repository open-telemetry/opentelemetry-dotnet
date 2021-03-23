// <copyright file="Status.cs" company="OpenTelemetry Authors">
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
    /// Span execution status.
    /// </summary>
    public readonly struct Status : System.IEquatable<Status>
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        public static readonly Status Ok = new Status(StatusCode.Ok);

        /// <summary>
        /// The default status.
        /// </summary>
        public static readonly Status Unset = new Status(StatusCode.Unset);

        /// <summary>
        /// The operation contains an error.
        /// </summary>
        public static readonly Status Error = new Status(StatusCode.Error);

        internal Status(StatusCode statusCode, string description = null)
        {
            this.StatusCode = statusCode;
            this.Description = description;
        }

        /// <summary>
        /// Gets the canonical code from this status.
        /// </summary>
        public StatusCode StatusCode { get; }

        /// <summary>
        /// Gets the status description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Compare two <see cref="Status"/> for equality.
        /// </summary>
        /// <param name="status1">First Status to compare.</param>
        /// <param name="status2">Second Status to compare.</param>
        public static bool operator ==(Status status1, Status status2) => status1.Equals(status2);

        /// <summary>
        /// Compare two <see cref="Status"/> for not equality.
        /// </summary>
        /// <param name="status1">First Status to compare.</param>
        /// <param name="status2">Second Status to compare.</param>
        public static bool operator !=(Status status1, Status status2) => !status1.Equals(status2);

        /// <summary>
        /// Returns a new instance of a status with the description populated.
        /// </summary>
        /// <remarks>
        /// Note: Status Description is only valid for <see
        /// cref="StatusCode.Error"/> Status and will be ignored for all other
        /// <see cref="Trace.StatusCode"/> values. See the <a
        /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status">Status
        /// API</a> for details.
        /// </remarks>
        /// <param name="description">Description of the status.</param>
        /// <returns>New instance of the status class with the description populated.</returns>
        public Status WithDescription(string description)
        {
            if (this.StatusCode != StatusCode.Error || this.Description == description)
            {
                return this;
            }

            return new Status(this.StatusCode, description);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is Status))
            {
                return false;
            }

            var that = (Status)obj;
            return this.StatusCode == that.StatusCode && this.Description == that.Description;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var result = 1;
            result = (31 * result) + this.StatusCode.GetHashCode();
            result = (31 * result) + (this.Description?.GetHashCode() ?? 0);
            return result;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return nameof(Status)
                + "{"
                + nameof(this.StatusCode) + "=" + this.StatusCode + ", "
                + nameof(this.Description) + "=" + this.Description
                + "}";
        }

        /// <inheritdoc/>
        public bool Equals(Status other)
        {
            return this.StatusCode == other.StatusCode && this.Description == other.Description;
        }
    }
}
