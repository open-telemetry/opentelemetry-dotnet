// <copyright file="LogRecordScopeCallback.cs" company="OpenTelemetry Authors">
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

#if NET461 || NETSTANDARD2_0
namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Callback delegate for the <see
    /// cref="LogRecord.ForEachScope{TState}(LogRecordScopeCallback{TState},
    /// TState)"/> method.
    /// </summary>
    /// <typeparam name="TState">State type.</typeparam>
    /// <param name="scope"><see cref="LogRecordScope"/>.</param>
    /// <param name="state">State.</param>
    public delegate void LogRecordScopeCallback<TState>(LogRecordScope scope, TState state);
}
#endif
