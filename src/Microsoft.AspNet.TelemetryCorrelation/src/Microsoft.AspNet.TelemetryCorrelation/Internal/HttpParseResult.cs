// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNet.TelemetryCorrelation
{
    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/HttpParseResult.cs
    internal enum HttpParseResult
    {
        /// <summary>
        /// Parsed succesfully.
        /// </summary>
        Parsed,

        /// <summary>
        /// Was not parsed.
        /// </summary>
        NotParsed,

        /// <summary>
        /// Invalid format.
        /// </summary>
        InvalidFormat,
    }
}