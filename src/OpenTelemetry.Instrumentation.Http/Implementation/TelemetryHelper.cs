// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

internal static class TelemetryHelper
{
    public static readonly object[] BoxedStatusCodes;

    static TelemetryHelper()
    {
        BoxedStatusCodes = new object[500];
        for (int i = 0, c = 100; i < BoxedStatusCodes.Length; i++, c++)
        {
            BoxedStatusCodes[i] = c;
        }
    }

    public static object GetBoxedStatusCode(HttpStatusCode statusCode)
    {
        int intStatusCode = (int)statusCode;
        if (intStatusCode >= 100 && intStatusCode < 600)
        {
            return BoxedStatusCodes[intStatusCode - 100];
        }

        return intStatusCode;
    }
}
