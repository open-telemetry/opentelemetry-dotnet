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

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Represents a callback action to process <see cref="LogRecordScope"/>s
    /// attached to a log message.
    /// </summary>
    /// <typeparam name="TState">Type of state passed to the callback.</typeparam>
    /// <param name="scope">The <see cref="LogRecordScope"/> being processed.</param>
    /// <param name="state">State value.</param>
    public delegate void LogRecordScopeCallback<TState>(LogRecordScope scope, TState state);
}
