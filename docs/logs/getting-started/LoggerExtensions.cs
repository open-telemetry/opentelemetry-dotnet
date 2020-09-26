// <copyright file="LoggerExtensions.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

internal static class LoggerExtensions
{
    private static readonly Action<ILogger, object, string, string, int, Exception> LogExInformation = LoggerMessage.Define<object, string, string, int>(
        LogLevel.Information,
        new EventId(1, nameof(LogEx)),
        "LogEx({obj}, {memberName}@{filePath}:{lineNumber}).");

    public static void LogEx(
        this ILogger logger,
        object obj,
        [CallerMemberName] string memberName = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        LogExInformation(logger, obj, memberName, filePath, lineNumber, null);
    }
}
