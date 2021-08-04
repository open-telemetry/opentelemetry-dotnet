// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/GenericHeaderParser.cs
    internal sealed class GenericHeaderParser<T> : BaseHeaderParser<T>
    {
        private GetParsedValueLengthDelegate getParsedValueLength;

        internal GenericHeaderParser(bool supportsMultipleValues, GetParsedValueLengthDelegate getParsedValueLength)
            : base(supportsMultipleValues)
        {
            if (getParsedValueLength == null)
            {
                throw new ArgumentNullException(nameof(getParsedValueLength));
            }

            this.getParsedValueLength = getParsedValueLength;
        }

        internal delegate int GetParsedValueLengthDelegate(string value, int startIndex, out T parsedValue);

        protected override int GetParsedValueLength(string value, int startIndex, out T parsedValue)
        {
            return getParsedValueLength(value, startIndex, out parsedValue);
        }
    }
}