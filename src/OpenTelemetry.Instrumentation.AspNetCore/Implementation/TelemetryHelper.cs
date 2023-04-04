// <copyright file="TelemetryHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal static class TelemetryHelper
{
#pragma warning disable SA1509 // Opening braces should not be preceded by blank line
    // Status Codes listed at http://www.iana.org/assignments/http-status-codes/http-status-codes.xhtml
    private static readonly Dictionary<int, object> BoxedStatusCodes = new()
    {
        { 100, 100 },
        { 101, 101 },
        { 102, 102 },

        { 200, 200 },
        { 201, 201 },
        { 202, 202 },
        { 203, 203 },
        { 204, 204 },
        { 205, 205 },
        { 206, 206 },
        { 207, 207 },
        { 208, 208 },
        { 226, 226 },

        { 300, 300 },
        { 301, 301 },
        { 302, 302 },
        { 303, 303 },
        { 304, 304 },
        { 305, 305 },
        { 306, 306 },
        { 307, 307 },
        { 308, 308 },

        { 400, 400 },
        { 401, 401 },
        { 402, 402 },
        { 403, 403 },
        { 404, 404 },
        { 405, 405 },
        { 406, 406 },
        { 407, 407 },
        { 408, 408 },
        { 409, 409 },
        { 410, 410 },
        { 411, 411 },
        { 412, 412 },
        { 413, 413 },
        { 414, 414 },
        { 415, 415 },
        { 416, 416 },
        { 417, 417 },
        { 418, 418 },
        { 419, 419 },
        { 421, 421 },
        { 422, 422 },
        { 423, 423 },
        { 424, 424 },
        { 426, 426 },
        { 428, 428 },
        { 429, 429 },
        { 431, 431 },
        { 451, 451 },
        { 499, 499 },

        { 500, 500 },
        { 501, 501 },
        { 502, 502 },
        { 503, 503 },
        { 504, 504 },
        { 505, 505 },
        { 506, 506 },
        { 507, 507 },
        { 508, 508 },
        { 510, 510 },
        { 511, 511 },
    };
#pragma warning restore SA1509 // Opening braces should not be preceded by blank line

    public static object GetBoxedStatusCode(int statusCode)
    {
        if (BoxedStatusCodes.TryGetValue(statusCode, out var result))
        {
            return result;
        }

        return statusCode;
    }
}
