// <copyright file="NameValueHeaderValue.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/NameValueHeaderValue.cs
    internal class NameValueHeaderValue
    {
        private static readonly HttpHeaderParser<NameValueHeaderValue> SingleValueParser
            = new GenericHeaderParser<NameValueHeaderValue>(false, GetNameValueLength);

        private string name;
        private string value;

        private NameValueHeaderValue()
        {
            // Used by the parser to create a new instance of this type.
        }

        public string Name
        {
            get { return this.name; }
        }

        public string Value
        {
            get { return this.value; }
        }

        public static bool TryParse(string input, out NameValueHeaderValue parsedValue)
        {
            var index = 0;
            return SingleValueParser.TryParseValue(input, ref index, out parsedValue);
        }

        internal static int GetValueLength(string input, int startIndex)
        {
            if (startIndex >= input.Length)
            {
                return 0;
            }

            var valueLength = HttpRuleParser.GetTokenLength(input, startIndex);

            if (valueLength == 0)
            {
                // A value can either be a token or a quoted string. Check if it is a quoted string.
                if (HttpRuleParser.GetQuotedStringLength(input, startIndex, out valueLength) != HttpParseResult.Parsed)
                {
                    // We have an invalid value. Reset the name and return.
                    return 0;
                }
            }

            return valueLength;
        }

        private static int GetNameValueLength(string input, int startIndex, out NameValueHeaderValue parsedValue)
        {
            parsedValue = null;

            if (string.IsNullOrEmpty(input) || (startIndex >= input.Length))
            {
                return 0;
            }

            // Parse the name, i.e. <name> in name/value string "<name>=<value>". Caller must remove
            // leading whitespaces.
            var nameLength = HttpRuleParser.GetTokenLength(input, startIndex);

            if (nameLength == 0)
            {
                return 0;
            }

            var name = input.Substring(startIndex, nameLength);
            var current = startIndex + nameLength;
            current += HttpRuleParser.GetWhitespaceLength(input, current);

            // Parse the separator between name and value
            if ((current == input.Length) || (input[current] != '='))
            {
                // We only have a name and that's OK. Return.
                parsedValue = new NameValueHeaderValue
                {
                    name = name,
                };
                current += HttpRuleParser.GetWhitespaceLength(input, current); // skip whitespaces
                return current - startIndex;
            }

            current++; // skip delimiter.
            current += HttpRuleParser.GetWhitespaceLength(input, current);

            // Parse the value, i.e. <value> in name/value string "<name>=<value>"
            int valueLength = GetValueLength(input, current);

            // Value after the '=' may be empty
            // Use parameterless ctor to avoid double-parsing of name and value, i.e. skip public ctor validation.
            parsedValue = new NameValueHeaderValue
            {
                name = name,
                value = input.Substring(current, valueLength),
            };
            current += valueLength;
            current += HttpRuleParser.GetWhitespaceLength(input, current); // skip whitespaces
            return current - startIndex;
        }
    }
}
