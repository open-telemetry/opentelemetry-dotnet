// <copyright file="IViewManager.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Stats
{
    using System.Collections.Generic;

    /// <summary>
    /// View manager that holds all configured views.
    /// </summary>
    public interface IViewManager
    {
        /// <summary>
        /// Gets all configured views.
        /// </summary>
        ISet<IView> AllExportedViews { get; }

        /// <summary>
        /// Returns the view with specified view name.
        /// </summary>
        /// <param name="view">View name.</param>
        /// <returns>The view with the specified name.</returns>
        IViewData GetView(IViewName view);

        /// <summary>
        /// Registers a new view to be tracked.
        /// </summary>
        /// <param name="view">View to be registered.</param>
        void RegisterView(IView view);
    }
}
