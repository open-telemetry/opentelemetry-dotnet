// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

internal static class LogRecordPoolHelper
{
    public const int DefaultMaxNumberOfAttributes = 64;
    public const int DefaultMaxNumberOfScopes = 16;

    public static void Clear(LogRecord logRecord)
    {
        var attributeStorage = logRecord.AttributeStorage;
        if (attributeStorage != null)
        {
            if (attributeStorage.Count > DefaultMaxNumberOfAttributes)
            {
                // Don't allow the pool to grow unconstrained.
                logRecord.AttributeStorage = null;
            }
            else
            {
                /* List<T>.Clear sets the count/size to 0 but it maintains the
                underlying array (capacity). */
                attributeStorage.Clear();
            }
        }

        var scopeStorage = logRecord.ScopeStorage;
        if (scopeStorage != null)
        {
            if (scopeStorage.Count > DefaultMaxNumberOfScopes)
            {
                // Don't allow the pool to grow unconstrained.
                logRecord.ScopeStorage = null;
            }
            else
            {
                /* List<T>.Clear sets the count/size to 0 but it maintains the
                underlying array (capacity). */
                scopeStorage.Clear();
            }
        }
    }
}
