// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Contracts;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    // Adoption of the code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/HeaderUtilities.cs
    internal static class HeaderUtilities
    {
        internal static int GetNextNonEmptyOrWhitespaceIndex(
            string input,
            int startIndex,
            bool skipEmptyValues,
            out bool separatorFound)
        {
            Contract.Requires(input != null);
            Contract.Requires(startIndex <= input.Length); // it's OK if index == value.Length.

            separatorFound = false;
            var current = startIndex + HttpRuleParser.GetWhitespaceLength(input, startIndex);

            if ((current == input.Length) || (input[current] != ','))
            {
                return current;
            }

            // If we have a separator, skip the separator and all following whitespaces. If we support
            // empty values, continue until the current character is neither a separator nor a whitespace.
            separatorFound = true;
            current++; // skip delimiter.
            current = current + HttpRuleParser.GetWhitespaceLength(input, current);

            if (skipEmptyValues)
            {
                while ((current < input.Length) && (input[current] == ','))
                {
                    current++; // skip delimiter.
                    current = current + HttpRuleParser.GetWhitespaceLength(input, current);
                }
            }

            return current;
        }
    }
}