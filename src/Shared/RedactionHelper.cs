// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Text;

namespace OpenTelemetry.Internal;

internal class RedactionHelper
{
    private static readonly string RedactedText = "Redacted";

    public static string? GetRedactedQueryString(string query)
    {
        int length = query.Length;
        int index = 0;

        // Preallocate some size to avoid re-sizing multiple times.
        // Since the size will increase, allocating twice as much.
        // TODO: Check to see if perf can be improved here.
        StringBuilder queryBuilder = new(2 * length);
        while (index < query.Length)
        {
            // Check if the character is = for redacting value.
            if (query[index] == '=')
            {
                // Append =
                queryBuilder.Append('=');
                index++;

                // Append redactedText in place of original value.
                queryBuilder.Append(RedactedText);

                // Move until end of this key/value pair.
                while (index < length && query[index] != '&')
                {
                    index++;
                }

                // End of key/value.
                if (index < length && query[index] == '&')
                {
                    queryBuilder.Append(query[index]);
                }
            }
            else
            {
                // Keep adding to the result
                queryBuilder.Append(query[index]);
            }

            index++;
        }

        return queryBuilder.ToString();
    }
}
