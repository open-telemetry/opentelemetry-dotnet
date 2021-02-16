// <copyright file="ActivityEventAttachingLogProcessorOptions.cs" company="OpenTelemetry Authors">
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
using System;
using System.Diagnostics;

namespace OpenTelemetry.Logs
{
    public class ActivityEventAttachingLogProcessorOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not log scopes should be included on generated <see cref="ActivityEvent"/>s. Default value: False.
        /// </summary>
        public bool IncludeScopes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not log state should be included on generated <see cref="ActivityEvent"/>s. Default value: False.
        /// </summary>
        public bool IncludeState { get; set; }

        /// <summary>
        /// Gets or sets the callback action used to convert log state to tags.
        /// </summary>
        public Action<ActivityTagsCollection, object> StateConverter { get; set; } = DefaultLogStateConverter.ConvertState;

        /// <summary>
        /// Gets or sets the callback action used to convert log scopes to tags.
        /// </summary>
        public Action<ActivityTagsCollection, int, object> ScopeConverter { get; set; } = DefaultLogStateConverter.ConvertScope;
    }
}
#endif
