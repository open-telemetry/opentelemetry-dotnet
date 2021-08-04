// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNet.TelemetryCorrelation
{
    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/HttpHeaderParser.cs
    internal abstract class HttpHeaderParser<T>
    {
        private bool supportsMultipleValues;

        protected HttpHeaderParser(bool supportsMultipleValues)
        {
            this.supportsMultipleValues = supportsMultipleValues;
        }

        public bool SupportsMultipleValues
        {
            get { return supportsMultipleValues; }
        }

        // If a parser supports multiple values, a call to ParseValue/TryParseValue should return a value for 'index'
        // pointing to the next non-whitespace character after a delimiter. E.g. if called with a start index of 0
        // for string "value , second_value", then after the call completes, 'index' must point to 's', i.e. the first
        // non-whitespace after the separator ','.
        public abstract bool TryParseValue(string value, ref int index, out T parsedValue);
    }
}