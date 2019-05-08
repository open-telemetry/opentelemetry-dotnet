// <copyright file="IScope.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Common
{
    using System;

    /// <summary>
    /// Scope marker. Used as a syntactic sugar in methods like StartScopedSpan so it can be
    /// wrapped in "using" block with automatic scope completion.
    /// </summary>
    public interface IScope : IDisposable
    {
    }
}
