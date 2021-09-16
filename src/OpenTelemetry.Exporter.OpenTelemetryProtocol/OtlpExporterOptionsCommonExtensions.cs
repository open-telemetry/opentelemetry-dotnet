// <copyright file="OtlpExporterOptionsCommonExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    internal static class OtlpExporterOptionsCommonExtensions
    {
        internal static THeaders GetHeaders<THeaders>(this OtlpExporterOptions options, Action<THeaders, string, string> addHeader)
            where THeaders : new()
        {
            var optionHeaders = options.Headers;
            var headers = new THeaders();
            if (!string.IsNullOrEmpty(optionHeaders))
            {
                Array.ForEach(
                    optionHeaders.Split(','),
                    (pair) =>
                    {
                        // Specify the maximum number of substrings to return to 2
                        // This treats everything that follows the first `=` in the string as the value to be added for the metadata key
                        var keyValueData = pair.Split(new char[] { '=' }, 2);
                        if (keyValueData.Length != 2)
                        {
                            throw new ArgumentException("Headers provided in an invalid format.");
                        }

                        var key = keyValueData[0].Trim();
                        var value = keyValueData[1].Trim();
                        addHeader(headers, key, value);
                    });
            }

            return headers;
        }
    }
}
