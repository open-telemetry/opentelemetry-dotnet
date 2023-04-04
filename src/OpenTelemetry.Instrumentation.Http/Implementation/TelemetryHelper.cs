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

using System.Net;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

internal static class TelemetryHelper
{
#pragma warning disable SA1509 // Opening braces should not be preceded by blank line
    // Status Codes listed at http://www.iana.org/assignments/http-status-codes/http-status-codes.xhtml
    private static readonly Dictionary<HttpStatusCode, object> BoxedStatusCodes = new()
    {
        { HttpStatusCode.Continue, 100 },
        { HttpStatusCode.SwitchingProtocols, 101 },
        /*{ 102, 102 },*/

        { HttpStatusCode.OK, 200 },
        { HttpStatusCode.Created, 201 },
        { HttpStatusCode.Accepted, 202 },
        { HttpStatusCode.NonAuthoritativeInformation, 203 },
        { HttpStatusCode.NoContent, 204 },
        { HttpStatusCode.ResetContent, 205 },
        { HttpStatusCode.PartialContent, 206 },
        /*{ 207, 207 },
        { 208, 208 },
        { 226, 226 },*/

        { HttpStatusCode.MultipleChoices, 300 },
        /* { HttpStatusCode.Ambiguous, 300 }, */
        { HttpStatusCode.MovedPermanently, 301 },
        /* { HttpStatusCode.Moved, 301 }, */
        { HttpStatusCode.Found, 302 },
        /* { HttpStatusCode.Redirect, 302 }, */
        { HttpStatusCode.SeeOther, 303 },
        /* { HttpStatusCode.RedirectMethod, 303 }, */
        { HttpStatusCode.NotModified, 304 },
        { HttpStatusCode.UseProxy, 305 },
        { HttpStatusCode.Unused, 306 },
        { HttpStatusCode.TemporaryRedirect, 307 },
        /* { HttpStatusCode.RedirectKeepVerb, 307 }, */
        /*{ 308, 308 },*/

        { HttpStatusCode.BadRequest, 400 },
        { HttpStatusCode.Unauthorized, 401 },
        { HttpStatusCode.PaymentRequired, 402 },
        { HttpStatusCode.Forbidden, 403 },
        { HttpStatusCode.NotFound, 404 },
        { HttpStatusCode.MethodNotAllowed, 405 },
        { HttpStatusCode.NotAcceptable, 406 },
        { HttpStatusCode.ProxyAuthenticationRequired, 407 },
        { HttpStatusCode.RequestTimeout, 408 },
        { HttpStatusCode.Conflict, 409 },
        { HttpStatusCode.Gone, 410 },
        { HttpStatusCode.LengthRequired, 411 },
        { HttpStatusCode.PreconditionFailed, 412 },
        { HttpStatusCode.RequestEntityTooLarge, 413 },
        { HttpStatusCode.RequestUriTooLong, 414 },
        { HttpStatusCode.UnsupportedMediaType, 415 },
        { HttpStatusCode.RequestedRangeNotSatisfiable, 416 },
        { HttpStatusCode.ExpectationFailed, 417 },
        /*{ 418, 418 },
        { 419, 419 },
        { 421, 421 },
        { 422, 422 },
        { 423, 423 },
        { 424, 424 },*/
        { HttpStatusCode.UpgradeRequired, 426 },
        /*{ 428, 428 },
        { 429, 429 },
        { 431, 431 },
        { 451, 451 },
        { 499, 499 },*/

        { HttpStatusCode.InternalServerError, 500 },
        { HttpStatusCode.NotImplemented, 501 },
        { HttpStatusCode.BadGateway, 502 },
        { HttpStatusCode.ServiceUnavailable, 503 },
        { HttpStatusCode.GatewayTimeout, 504 },
        { HttpStatusCode.HttpVersionNotSupported, 505 },
        /*{ 506, 506 },
        { 507, 507 },
        { 508, 508 },
        { 510, 510 },
        { 511, 511 },*/
    };
#pragma warning restore SA1509 // Opening braces should not be preceded by blank line

    public static object GetBoxedStatusCode(HttpStatusCode statusCode)
    {
        if (BoxedStatusCodes.TryGetValue(statusCode, out var result))
        {
            return result;
        }

        return (int)statusCode;
    }
}
