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

using System.Runtime.CompilerServices;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal
{
    internal static class StatusHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetStringNameForStatusCode(StatusCode statusCode)
        {
            return statusCode switch
            {
                /*
                 * Note: Order here does matter for perf. Unset is
                 * first because assumption is most spans will be
                 * Unset, then Error. Ok is not set by the SDK.
                 */
                StatusCode.Unset => "Unset",
                StatusCode.Error => "Error",
                StatusCode.Ok => "Ok",
                _ => null,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StatusCode? GetStatusCodeForStringName(string statusCodeName)
        {
            return statusCodeName switch
            {
                /*
                 * Note: Order here does matter for perf. Unset is
                 * first because assumption is most spans will be
                 * Unset, then Error. Ok is not set by the SDK.
                 */
                "Unset" => StatusCode.Unset,
                "Error" => StatusCode.Error,
                "Ok" => StatusCode.Ok,
                _ => (StatusCode?)null,
            };
        }
    }
}
