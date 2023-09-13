// <copyright file="LogRecordPoolHelper.cs" company="OpenTelemetry Authors">
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
                // Don't allow the pool to grow unconstained.
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
                // Don't allow the pool to grow unconstained.
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
