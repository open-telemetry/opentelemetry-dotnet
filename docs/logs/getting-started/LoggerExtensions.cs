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
    // https://docs.microsoft.com/aspnet/core/fundamentals/logging/loggermessage
    private static readonly Action<ILogger, object, Exception> LogExAction = LoggerMessage.Define<object>(
        LogLevel.Information,
        new EventId(1, nameof(LogEx)),
        "LogEx({obj}).");

    public static void LogEx(this ILogger logger, object obj)
    {
        LogExAction(logger, obj, null);
    }
}
