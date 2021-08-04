// <copyright file="GenericHeaderParser.cs" company="OpenTelemetry Authors">
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