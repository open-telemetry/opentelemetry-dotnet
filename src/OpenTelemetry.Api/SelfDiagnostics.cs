// <copyright file="SelfDiagnostics.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry
{
    /// <summary>
    /// SelfDiagnostics holds a Logger which can generate internal logs via EventSource for OpenTelemetry self diagnostics.
    /// </summary>
    public class SelfDiagnostics
    {
        /// <summary>
        /// Logger for generating internal logs via EventSource for OpenTelemetry self diagnostics.
        /// </summary>
        public static SelfDiagnostics Logger = new SelfDiagnostics();

        private SelfDiagnostics()
        {
        }

        /// <summary>
        /// Formats and generates a log message at the Critical level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogCritical(Type type, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogCritical(type, null, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Critical level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogCritical(Type type, Exception exception, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogCritical(type, exception, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Error level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogError(Type type, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogError(type, null, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Error level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogError(Type type, Exception exception, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogError(type, exception, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Warning level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogWarning(Type type, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogWarning(type, null, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Warning level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogWarning(Type type, Exception exception, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogWarning(type, exception, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Information level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogInformation(Type type, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogInformation(type, null, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Information level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogInformation(Type type, Exception exception, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogInformation(type, exception, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Verbose level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogVerbose(Type type, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogVerbose(type, null, message, args);
        }

        /// <summary>
        /// Formats and generates a log message at the Verbose level.
        /// </summary>
        /// <param name="type">The type of the class where the log origins from.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogVerbose(Type type, Exception exception, string message, params object[] args)
        {
            OpenTelemetryApiEventSource.Log.LogVerbose(type, exception, message, args);
        }
    }
}
