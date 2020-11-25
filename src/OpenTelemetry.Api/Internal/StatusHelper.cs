// <copyright file="StatusHelper.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal
{
    internal static class StatusHelper
    {
        public const string UnsetStatusCodeTagValue = "UNSET";
        public const string OkStatusCodeTagValue = "OK";
        public const string ErrorStatusCodeTagValue = "ERROR";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetTagValueForStatusCode(StatusCode statusCode)
        {
            return statusCode switch
            {
                /*
                 * Note: Order here does matter for perf. Unset is
                 * first because assumption is most spans will be
                 * Unset, then Error. Ok is not set by the SDK.
                 */
                StatusCode.Unset => UnsetStatusCodeTagValue,
                StatusCode.Error => ErrorStatusCodeTagValue,
                StatusCode.Ok => OkStatusCodeTagValue,
                _ => null,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StatusCode? GetStatusCodeForTagValue(string statusCodeTagValue)
        {
            return statusCodeTagValue switch
            {
                /*
                 * Note: Order here does matter for perf. Unset is
                 * first because assumption is most spans will be
                 * Unset, then Error. Ok is not set by the SDK.
                 */
                string _ when UnsetStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => StatusCode.Unset,
                string _ when ErrorStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => StatusCode.Error,
                string _ when OkStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => StatusCode.Ok,
                _ => (StatusCode?)null,
            };
        }
    }
}
