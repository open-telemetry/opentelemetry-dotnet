﻿// <copyright file="Status.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
    public readonly struct Status
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        public static readonly Status Ok = new Status(StatusCanonicalCode.Ok);

        /// <summary>
        /// The operation was cancelled (typically by the caller).
        /// </summary>
        public static readonly Status Cancelled = new Status(StatusCanonicalCode.Cancelled);

        /// <summary>
        /// Unknown error. An example of where this error may be returned is if a Status value received
        /// from another address space belongs to an error-space that is not known in this address space.
        /// Also errors raised by APIs that do not return enough error information may be converted to
        /// this error.
        /// </summary>
        public static readonly Status Unknown = new Status(StatusCanonicalCode.Unknown);

        /// <summary>
        /// Client specified an invalid argument. Note that this differs from FAILED_PRECONDITION.
        /// INVALID_ARGUMENT indicates arguments that are problematic regardless of the state of the
        /// system (e.g., a malformed file name).
        /// </summary>
        public static readonly Status InvalidArgument = new Status(StatusCanonicalCode.InvalidArgument);

        /// <summary>
        /// Deadline expired before operation could complete. For operations that change the state of the
        /// system, this error may be returned even if the operation has completed successfully. For
        /// example, a successful response from a server could have been delayed long enough for the
        /// deadline to expire.
        /// </summary>
        public static readonly Status DeadlineExceeded = new Status(StatusCanonicalCode.DeadlineExceeded);

        /// <summary>
        /// Some requested entity (e.g., file or directory) was not found.
        /// </summary>
        public static readonly Status NotFound = new Status(StatusCanonicalCode.NotFound);

        /// <summary>
        /// Some entity that we attempted to create (e.g., file or directory) already exists.
        /// </summary>
        public static readonly Status AlreadyExists = new Status(StatusCanonicalCode.AlreadyExists);

        /// <summary>
        /// The caller does not have permission to execute the specified operation. PERMISSION_DENIED
        /// must not be used for rejections caused by exhausting some resource (use RESOURCE_EXHAUSTED
        /// instead for those errors). PERMISSION_DENIED must not be used if the caller cannot be
        /// identified (use UNAUTHENTICATED instead for those errors).
        /// </summary>
        public static readonly Status PermissionDenied = new Status(StatusCanonicalCode.PermissionDenied);

        /// <summary>
        /// The request does not have valid authentication credentials for the operation.
        /// </summary>
        public static readonly Status Unauthenticated = new Status(StatusCanonicalCode.Unauthenticated);

        /// <summary>
        /// Some resource has been exhausted, perhaps a per-user quota, or perhaps the entire file system
        /// is out of space.
        /// </summary>
        public static readonly Status ResourceExhausted = new Status(StatusCanonicalCode.ResourceExhausted);

        /// <summary>
        /// Operation was rejected because the system is not in a state required for the operation's
        /// execution. For example, directory to be deleted may be non-empty, an rmdir operation is
        /// applied to a non-directory, etc.
        /// A litmus test that may help a service implementor in deciding between FAILED_PRECONDITION,
        /// ABORTED, and UNAVAILABLE: (a) Use UNAVAILABLE if the client can retry just the failing call.
        /// (b) Use ABORTED if the client should retry at a higher-level (e.g., restarting a
        /// read-modify-write sequence). (c) Use FAILED_PRECONDITION if the client should not retry until
        /// the system state has been explicitly fixed. E.g., if an "rmdir" fails because the directory
        /// is non-empty, FAILED_PRECONDITION should be returned since the client should not retry unless
        /// they have first fixed up the directory by deleting files from it.
        /// </summary>
        public static readonly Status FailedPrecondition = new Status(StatusCanonicalCode.FailedPrecondition);

        /// <summary>
        /// The operation was aborted, typically due to a concurrency issue like sequencer check
        /// failures, transaction aborts, etc.
        /// </summary>
        public static readonly Status Aborted = new Status(StatusCanonicalCode.Aborted);

        /// <summary>
        /// Operation was attempted past the valid range. E.g., seeking or reading past end of file.
        ///
        /// Unlike INVALID_ARGUMENT, this error indicates a problem that may be fixed if the system
        /// state changes. For example, a 32-bit file system will generate INVALID_ARGUMENT if asked to
        /// read at an offset that is not in the range [0,2^32-1], but it will generate OUT_OF_RANGE if
        /// asked to read from an offset past the current file size.
        ///
        /// There is a fair bit of overlap between FAILED_PRECONDITION and OUT_OF_RANGE. We recommend
        /// using OUT_OF_RANGE (the more specific error) when it applies so that callers who are
        /// iterating through a space can easily look for an OUT_OF_RANGE error to detect when they are
        /// done.
        /// </summary>
        public static readonly Status OutOfRange = new Status(StatusCanonicalCode.OutOfRange);

        /// <summary>
        /// Operation is not implemented or not supported/enabled in this service.
        /// </summary>
        public static readonly Status Unimplemented = new Status(StatusCanonicalCode.Unimplemented);

        /// <summary>
        /// Internal errors. Means some invariants expected by underlying system has been broken. If you
        /// see one of these errors, something is very broken.
        /// </summary>
        public static readonly Status Internal = new Status(StatusCanonicalCode.Internal);

        /// <summary>
        /// The service is currently unavailable. This is a most likely a transient condition and may be
        /// corrected by retrying with a backoff.
        ///
        /// See litmus test above for deciding between FAILED_PRECONDITION, ABORTED, and UNAVAILABLE.
        /// </summary>
        public static readonly Status Unavailable = new Status(StatusCanonicalCode.Unavailable);

        /// <summary>
        /// Unrecoverable data loss or corruption.
        /// </summary>
        public static readonly Status DataLoss = new Status(StatusCanonicalCode.DataLoss);

        internal Status(StatusCanonicalCode statusCanonicalCode, string description = null)
        {
            this.CanonicalCode = statusCanonicalCode;
            this.Description = description;
            this.IsValid = true;
        }

        /// <summary>
        /// Gets a value indicating whether this is a valid-to-use status.
        /// Only status instances created with a CanonicalCode (optional Description) are considered valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the canonical code from this status.
        /// </summary>
        public StatusCanonicalCode CanonicalCode { get; }

        /// <summary>
        /// Gets the status description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets a value indicating whether span completed sucessfully.
        /// </summary>
        public bool IsOk => this.CanonicalCode == StatusCanonicalCode.Ok;

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
        /// <param name="description">Description of the status.</param>
        /// <returns>New instance of the status class with the description populated.</returns>
        public Status WithDescription(string description)
        {
            if (this.Description == description)
            {
                return this;
            }

            return new Status(this.CanonicalCode, description);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is Status))
            {
                return false;
            }

            var that = (Status)obj;
            return this.IsValid == that.IsValid && this.CanonicalCode == that.CanonicalCode && this.Description == that.Description;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var result = 1;
            result = (31 * result) + this.CanonicalCode.GetHashCode();
            result = (31 * result) + this.Description.GetHashCode();
            return result;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return nameof(Status)
                + "{"
                + nameof(this.CanonicalCode) + "=" + this.CanonicalCode + ", "
                + nameof(this.Description) + "=" + this.Description
                + "}";
        }
    }
}
