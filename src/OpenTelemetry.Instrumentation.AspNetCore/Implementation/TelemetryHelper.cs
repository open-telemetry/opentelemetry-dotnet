// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

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

    public static object GetBoxedStatusCode(int statusCode)
    {
        if (statusCode >= 100 && statusCode < 600)
        {
            return BoxedStatusCodes[statusCode - 100];
        }

        return statusCode;
    }
}