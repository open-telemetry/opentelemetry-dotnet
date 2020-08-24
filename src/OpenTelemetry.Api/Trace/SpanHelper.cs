﻿// <copyright file="SpanHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A collection of helper methods to be used when building spans.
    /// </summary>
    public static class SpanHelper
    {
#pragma warning disable CA1805 // Do not initialize unnecessarily
        private static readonly Status DefaultStatus = default;
#pragma warning restore CA1805 // Do not initialize unnecessarily
        private static readonly Dictionary<StatusCanonicalCode, string> StatusCanonicalCodeToStringCache = new Dictionary<StatusCanonicalCode, string>()
        {
            [StatusCanonicalCode.Ok] = StatusCanonicalCode.Ok.ToString(),
            [StatusCanonicalCode.Cancelled] = StatusCanonicalCode.Cancelled.ToString(),
            [StatusCanonicalCode.Unknown] = StatusCanonicalCode.Unknown.ToString(),
            [StatusCanonicalCode.InvalidArgument] = StatusCanonicalCode.InvalidArgument.ToString(),
            [StatusCanonicalCode.DeadlineExceeded] = StatusCanonicalCode.DeadlineExceeded.ToString(),
            [StatusCanonicalCode.NotFound] = StatusCanonicalCode.NotFound.ToString(),
            [StatusCanonicalCode.AlreadyExists] = StatusCanonicalCode.AlreadyExists.ToString(),
            [StatusCanonicalCode.PermissionDenied] = StatusCanonicalCode.PermissionDenied.ToString(),
            [StatusCanonicalCode.ResourceExhausted] = StatusCanonicalCode.ResourceExhausted.ToString(),
            [StatusCanonicalCode.FailedPrecondition] = StatusCanonicalCode.FailedPrecondition.ToString(),
            [StatusCanonicalCode.Aborted] = StatusCanonicalCode.Aborted.ToString(),
            [StatusCanonicalCode.OutOfRange] = StatusCanonicalCode.OutOfRange.ToString(),
            [StatusCanonicalCode.Unimplemented] = StatusCanonicalCode.Unimplemented.ToString(),
            [StatusCanonicalCode.Internal] = StatusCanonicalCode.Internal.ToString(),
            [StatusCanonicalCode.Unavailable] = StatusCanonicalCode.Unavailable.ToString(),
            [StatusCanonicalCode.DataLoss] = StatusCanonicalCode.DataLoss.ToString(),
            [StatusCanonicalCode.Unauthenticated] = StatusCanonicalCode.Unauthenticated.ToString(),
        };

        /// <summary>
        /// Helper method that returns the string version of a <see cref="StatusCanonicalCode"/> using a cache to save on allocations.
        /// </summary>
        /// <param name="statusCanonicalCode"><see cref="StatusCanonicalCode"/>.</param>
        /// <returns>String version of the supplied <see cref="StatusCanonicalCode"/>.</returns>
        public static string GetCachedCanonicalCodeString(StatusCanonicalCode statusCanonicalCode)
        {
            if (!StatusCanonicalCodeToStringCache.TryGetValue(statusCanonicalCode, out string canonicalCode))
            {
                return statusCanonicalCode.ToString();
            }

            return canonicalCode;
        }

        /// <summary>
        /// Helper method that populates span properties from http status code according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="httpStatusCode">Http status code.</param>
        /// <returns>Resolved span <see cref="Status"/> for the Http status code.</returns>
        public static Status ResolveSpanStatusForHttpStatusCode(int httpStatusCode)
        {
            var newStatus = Status.Unknown;

            if (httpStatusCode >= 100 && httpStatusCode <= 399)
            {
                newStatus = Status.Ok;
            }
            else if (httpStatusCode == 400)
            {
                newStatus = Status.InvalidArgument;
            }
            else if (httpStatusCode == 401)
            {
                newStatus = Status.Unauthenticated;
            }
            else if (httpStatusCode == 403)
            {
                newStatus = Status.PermissionDenied;
            }
            else if (httpStatusCode == 404)
            {
                newStatus = Status.NotFound;
            }
            else if (httpStatusCode == 429)
            {
                newStatus = Status.ResourceExhausted;
            }
            else if (httpStatusCode == 501)
            {
                newStatus = Status.Unimplemented;
            }
            else if (httpStatusCode == 503)
            {
                newStatus = Status.Unavailable;
            }
            else if (httpStatusCode == 504)
            {
                newStatus = Status.DeadlineExceeded;
            }

            return newStatus;
        }

        /// <summary>
        /// Helper method that populates span properties from RPC status code according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/rpc.md#status.
        /// </summary>
        /// <param name="statusCode">RPC status code.</param>
        /// <returns>Resolved span <see cref="Status"/> for the Grpc status code.</returns>
        public static Status ResolveSpanStatusForGrpcStatusCode(int statusCode)
        {
            var newStatus = Status.Unknown;

            if (typeof(StatusCanonicalCode).IsEnumDefined(statusCode))
            {
                newStatus = new Status((StatusCanonicalCode)statusCode);
            }

            return newStatus;
        }

        /// <summary>
        /// Helper method that returns Status from <see cref="StatusCanonicalCode"/> to save on allocations.
        /// </summary>
        /// <param name="statusCanonicalCode"><see cref="StatusCanonicalCode"/>.</param>
        /// <returns>Resolved span <see cref="Status"/> for the Canonical status code.</returns>
        public static Status ResolveCanonicalCodeToStatus(string statusCanonicalCode)
        {
            bool success = Enum.TryParse(statusCanonicalCode, out StatusCanonicalCode canonicalCode);

            if (!success)
            {
                return DefaultStatus;
            }

            var status = canonicalCode switch
            {
                StatusCanonicalCode.Cancelled => Status.Cancelled,
                StatusCanonicalCode.Unknown => Status.Unknown,
                StatusCanonicalCode.InvalidArgument => Status.InvalidArgument,
                StatusCanonicalCode.DeadlineExceeded => Status.DeadlineExceeded,
                StatusCanonicalCode.NotFound => Status.NotFound,
                StatusCanonicalCode.AlreadyExists => Status.AlreadyExists,
                StatusCanonicalCode.PermissionDenied => Status.PermissionDenied,
                StatusCanonicalCode.ResourceExhausted => Status.ResourceExhausted,
                StatusCanonicalCode.FailedPrecondition => Status.FailedPrecondition,
                StatusCanonicalCode.Aborted => Status.Aborted,
                StatusCanonicalCode.OutOfRange => Status.OutOfRange,
                StatusCanonicalCode.Unimplemented => Status.Unimplemented,
                StatusCanonicalCode.Internal => Status.Internal,
                StatusCanonicalCode.Unavailable => Status.Unavailable,
                StatusCanonicalCode.DataLoss => Status.DataLoss,
                StatusCanonicalCode.Unauthenticated => Status.Unauthenticated,
                _ => Status.Ok,
            };

            return status;
        }
    }
}
