// <copyright file="MeasureToViewMap.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using OpenTelemetry.Tags;

    internal sealed class MeasureToViewMap
    {
        private readonly object lck = new object();
        private readonly IDictionary<string, ICollection<MutableViewData>> mutableMap = new Dictionary<string, ICollection<MutableViewData>>();

        private readonly IDictionary<IViewName, IView> registeredViews = new Dictionary<IViewName, IView>();

        // TODO(songya): consider adding a Measure.Name class
        private readonly IDictionary<string, IMeasure> registeredMeasures = new Dictionary<string, IMeasure>();

        // Cached set of exported views. It must be set to null whenever a view is registered or
        // unregistered.
        private volatile ISet<IView> exportedViews;

        /// <summary>
        /// Gets a {@link ViewData} corresponding to the given {@link View.Name}.
        /// </summary>
        internal ISet<IView> ExportedViews
        {
            get
            {
                ISet<IView> views = this.exportedViews;
                if (views == null)
                {
                    lock (this.lck)
                    {
                        this.exportedViews = views = FilterExportedViews(this.registeredViews.Values);
                    }
                }

                return views;
            }
        }

        internal IViewData GetView(IViewName viewName, StatsCollectionState state)
        {
            lock (this.lck)
            {
                MutableViewData view = this.GetMutableViewData(viewName);
                return view?.ToViewData(DateTimeOffset.Now, state);
            }
        }

        /** Enable stats collection for the given {@link View}. */
        internal void RegisterView(IView view)
        {
            lock (this.lck)
            {
                this.exportedViews = null;
                this.registeredViews.TryGetValue(view.Name, out IView existing);
                if (existing != null)
                {
                    if (existing.Equals(view))
                    {
                        // Ignore views that are already registered.
                        return;
                    }
                    else
                    {
                        throw new ArgumentException("A different view with the same name is already registered: " + existing);
                    }
                }

                IMeasure measure = view.Measure;
                this.registeredMeasures.TryGetValue(measure.Name, out IMeasure registeredMeasure);
                if (registeredMeasure != null && !registeredMeasure.Equals(measure))
                {
                    throw new ArgumentException("A different measure with the same name is already registered: " + registeredMeasure);
                }

                this.registeredViews.Add(view.Name, view);
                if (registeredMeasure == null)
                {
                    this.registeredMeasures.Add(measure.Name, measure);
                }

                this.AddMutableViewData(view.Measure.Name, MutableViewData.Create(view, DateTimeOffset.Now));
            }
        }

        // Records stats with a set of tags.
        internal void Record(ITagContext tags, IEnumerable<IMeasurement> stats, DateTimeOffset timestamp)
        {
            lock (this.lck)
            {
                foreach (var measurement in stats)
                {
                    IMeasure measure = measurement.Measure;
                    this.registeredMeasures.TryGetValue(measure.Name, out IMeasure value);
                    if (!measure.Equals(value))
                    {
                        // unregistered measures will be ignored.
                        continue;
                    }

                    var views = this.mutableMap[measure.Name];
                    foreach (MutableViewData view in views)
                    {
                        measurement.Match<object>(
                            (arg) =>
                            {
                                view.Record(tags, arg.Value, timestamp);
                                return null;
                            },
                            (arg) =>
                            {
                                view.Record(tags, arg.Value, timestamp);
                                return null;
                            },
                            (arg) =>
                            {
                                throw new ArgumentException();
                            });
                    }
                }
            }
        }

        // Clear stats for all the current MutableViewData
        internal void ClearStats()
        {
            lock (this.lck)
            {
                foreach (var entry in this.mutableMap)
                {
                    foreach (MutableViewData mutableViewData in entry.Value)
                    {
                        mutableViewData.ClearStats();
                    }
                }
            }
        }

        // Resume stats collection for all MutableViewData.
        internal void ResumeStatsCollection(DateTimeOffset now)
        {
            lock (this.lck)
            {
                foreach (var entry in this.mutableMap)
                {
                    foreach (MutableViewData mutableViewData in entry.Value)
                    {
                        mutableViewData.ResumeStatsCollection(now);
                    }
                }
            }
        }

        // Returns the subset of the given views that should be exported.
        private static ISet<IView> FilterExportedViews(ICollection<IView> allViews)
        {
            return ImmutableHashSet.CreateRange(allViews);
        }

        private void AddMutableViewData(string name, MutableViewData mutableViewData)
        {
            if (this.mutableMap.ContainsKey(name))
            {
                this.mutableMap[name].Add(mutableViewData);
            }
            else
            {
                this.mutableMap.Add(name, new List<MutableViewData>() { mutableViewData });
            }
        }

        private MutableViewData GetMutableViewData(IViewName viewName)
        {
            lock (this.lck)
            {
                this.registeredViews.TryGetValue(viewName, out IView view);
                if (view == null)
                {
                    return null;
                }

                this.mutableMap.TryGetValue(view.Measure.Name, out ICollection<MutableViewData> views);
                if (views != null)
                {
                    foreach (MutableViewData viewData in views)
                    {
                        if (viewData.View.Name.Equals(viewName))
                        {
                            return viewData;
                        }
                    }
                }

                throw new InvalidOperationException(
                    "Internal error: Not recording stats for view: \""
                        + viewName
                        + "\" registeredViews="
                        + this.registeredViews
                        + ", mutableMap="
                        + this.mutableMap);
            }
        }
    }
}
