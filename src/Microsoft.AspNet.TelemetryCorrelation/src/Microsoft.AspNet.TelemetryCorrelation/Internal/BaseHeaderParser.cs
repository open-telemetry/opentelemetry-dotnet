﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNet.TelemetryCorrelation
{
    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/BaseHeaderParser.cs
    internal abstract class BaseHeaderParser<T> : HttpHeaderParser<T>
    {
        protected BaseHeaderParser(bool supportsMultipleValues)
            : base(supportsMultipleValues)
        {
        }

        public sealed override bool TryParseValue(string value, ref int index, out T parsedValue)
        {
            parsedValue = default(T);

            // If multiple values are supported (i.e. list of values), then accept an empty string: The header may
            // be added multiple times to the request/response message. E.g.
            //  Accept: text/xml; q=1
            //  Accept:
            //  Accept: text/plain; q=0.2
            if (string.IsNullOrEmpty(value) || (index == value.Length))
            {
                return SupportsMultipleValues;
            }

            var separatorFound = false;
            var current = HeaderUtilities.GetNextNonEmptyOrWhitespaceIndex(value, index, SupportsMultipleValues, out separatorFound);

            if (separatorFound && !SupportsMultipleValues)
            {
                return false; // leading separators not allowed if we don't support multiple values.
            }

            if (current == value.Length)
            {
                if (SupportsMultipleValues)
                {
                    index = current;
                }

                return SupportsMultipleValues;
            }

            T result;
            var length = GetParsedValueLength(value, current, out result);

            if (length == 0)
            {
                return false;
            }

            current = current + length;
            current = HeaderUtilities.GetNextNonEmptyOrWhitespaceIndex(value, current, SupportsMultipleValues, out separatorFound);

            // If we support multiple values and we've not reached the end of the string, then we must have a separator.
            if ((separatorFound && !SupportsMultipleValues) || (!separatorFound && (current < value.Length)))
            {
                return false;
            }

            index = current;
            parsedValue = result;
            return true;
        }

        protected abstract int GetParsedValueLength(string value, int startIndex, out T parsedValue);
    }
}