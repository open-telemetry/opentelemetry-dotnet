// <copyright file="RequiresDynamicCodeAttribute.cs" company="OpenTelemetry Authors">
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

#nullable enable
#if !NET7_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Indicates that the specified method requires the ability to generate new code at runtime,
    /// for example through <see cref="Reflection"/>.
    /// </summary>
    /// <remarks>
    /// This allows tools to understand which methods are unsafe to call when compiling ahead of time.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequiresDynamicCodeAttribute"/> class
        /// with the specified message.
        /// </summary>
        /// <param name="message">
        /// A message that contains information about the usage of dynamic code.
        /// </param>
        public RequiresDynamicCodeAttribute(string message)
        {
            this.Message = message;
        }

        /// <summary>
        /// Gets a message that contains information about the usage of dynamic code.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets or sets an optional URL that contains more information about the method,
        /// why it requires dynamic code, and what options a consumer has to deal with it.
        /// </summary>
        public string? Url { get; set; }
    }
}
#endif
