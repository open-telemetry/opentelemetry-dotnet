// <copyright file="StackdriverStatsExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Stackdriver.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Api;
    using Google.Api.Gax;
    using Google.Api.Gax.Grpc;
    using Google.Apis.Auth.OAuth2;
    using Google.Cloud.Monitoring.V3;
    using Grpc.Auth;
    using Grpc.Core;
    using OpenTelemetry.Exporter.Stackdriver.Utils;
    using OpenTelemetry.Stats;

    internal class StackdriverStatsExporter
    {
        private readonly MonitoredResource monitoredResource;
        private readonly IViewManager viewManager;
        private readonly Dictionary<IViewName, IView> registeredViews = new Dictionary<IViewName, IView>();

        private readonly Dictionary<IView, MetricDescriptor> metricDescriptors = new Dictionary<IView, MetricDescriptor>(new ViewNameComparer());
        private readonly ProjectName project;
        private readonly GoogleCredential credential;
        private MetricServiceClient metricServiceClient;
        private CancellationTokenSource tokenSource;

#pragma warning disable SA1203 // Sensible grouping is more important than ordering by accessability
#pragma warning disable SA1214 // Readonly fields should appear before non-readonly fields
        private const int MaxBatchExportSize = 200;
        private static readonly string DefaultDisplayNamePrefix = "OpenTelemetry/";
        private static readonly string CustomMetricsDomain = "custom.googleapis.com/";
        private static readonly string CustomOpenTelemetryDomain = CustomMetricsDomain + "OpenTelemetry/";

        private static readonly string UserAgentKey = "user-agent";
        private static readonly string UserAgent;

        private readonly string domain;
        private readonly string displayNamePrefix;

        private bool isStarted;

                              /// <summary>
                              /// Interval between two subsequent stats collection operations.
                              /// </summary>
        private readonly TimeSpan collectionInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Interval within which the cancellation should be honored
        /// from the point it was requested.
        /// </summary>
        private readonly TimeSpan cancellationInterval = TimeSpan.FromSeconds(3);

        private readonly object locker = new object();
#pragma warning restore SA1203 // Constants should appear before fields
#pragma warning restore SA1214 // Sensible grouping is more important than ordering by accessability

        static StackdriverStatsExporter()
        {
            try
            {
                var assemblyPackageVersion = typeof(StackdriverStatsExporter).GetTypeInfo().Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
                UserAgent = $"OpenTelemetry-dotnet/{assemblyPackageVersion}";
            }
            catch (Exception)
            {
                UserAgent = $"OpenTelemetry-dotnet/{Constants.PackagVersionUndefined}";
            }
        }

        public StackdriverStatsExporter(
           IViewManager viewManager,
           StackdriverStatsConfiguration configuration)
        {
            GaxPreconditions.CheckNotNull(configuration, "configuration");
            GaxPreconditions.CheckNotNull(configuration.MonitoredResource, "configuration.MonitoredResource");
            GaxPreconditions.CheckNotNull(configuration.GoogleCredential, "configuration.GoogleCredential");
            GaxPreconditions.CheckArgument(
                configuration.ExportInterval != TimeSpan.Zero,
                paramName: "configuration.ExportInterval", 
                message: "Export interval can't be zero. Typically it's 1 minute");

            this.viewManager = viewManager;
            this.monitoredResource = configuration.MonitoredResource;
            this.collectionInterval = configuration.ExportInterval;
            this.project = new ProjectName(configuration.ProjectId);
            this.credential = configuration.GoogleCredential;

            this.domain = GetDomain(configuration.MetricNamePrefix);
            this.displayNamePrefix = this.GetDisplayNamePrefix(configuration.MetricNamePrefix);
        }

        public void Start()
        {
            lock (this.locker)
            {
                if (!this.isStarted)
                {
                    this.tokenSource = new CancellationTokenSource();
                    this.metricServiceClient = CreateMetricServiceClient(this.credential, this.tokenSource);

                    Task.Factory.StartNew(this.DoWork, this.tokenSource.Token);

                    this.isStarted = true;
                }
            }
        }

        public void Stop()
        {
            lock (this.locker)
            {
                if (!this.isStarted)
                {
                    return;
                }

                this.tokenSource.Cancel();
            }
        }

        private static MetricServiceClient CreateMetricServiceClient(GoogleCredential credential, CancellationTokenSource tokenSource)
        {
            // Make sure to add OpenTelemetry header to every outgoing call to Stackdriver APIs
            Action<Metadata> addOpenTelemetryHeader = m => m.Add(UserAgentKey, UserAgent);
            var callSettings = new CallSettings(
                cancellationToken: tokenSource.Token,
                credentials: null,
                timing: null,
                headerMutation: addOpenTelemetryHeader,
                writeOptions: WriteOptions.Default,
                propagationToken: null);

            var channel = new Channel(
                MetricServiceClient.DefaultEndpoint.ToString(),
                credential.ToChannelCredentials());

            var metricServiceSettings = new MetricServiceSettings()
            {
                CallSettings = callSettings,
            };

            return MetricServiceClient.Create(channel, settings: metricServiceSettings);
        }

        private static string GetDomain(string metricNamePrefix)
        {
            string domain;
            if (string.IsNullOrEmpty(metricNamePrefix))
            {
                domain = CustomOpenTelemetryDomain;
            }
            else
            {
                if (!metricNamePrefix.EndsWith("/"))
                {
                    domain = metricNamePrefix + '/';
                }
                else
                {
                    domain = metricNamePrefix;
                }
            }

            return domain;
        }

        private static string GenerateMetricDescriptorTypeName(IViewName viewName, string domain)
        {
            return domain + viewName.AsString;
        }

        /// <summary>
        /// Periodic operation happening on a dedicated thread that is 
        /// capturing the metrics collected within a collection interval.
        /// </summary>
        private void DoWork()
        {
            try
            {
                var sleepTime = this.collectionInterval;
                var stopWatch = new Stopwatch();

                while (!this.tokenSource.IsCancellationRequested)
                {
                    // Calculate the duration of collection iteration
                    stopWatch.Start();

                    // Collect metrics
                    this.Export();

                    stopWatch.Stop();

                    // Adjust the wait time - reduce export operation duration
                    sleepTime = this.collectionInterval.Subtract(stopWatch.Elapsed);
                    sleepTime = sleepTime.Duration();

                    // If the cancellation was requested, we should honor
                    // that within the cancellation interval, so we wait in 
                    // intervals of <cancellationInterval>
                    while (sleepTime > this.cancellationInterval && !this.tokenSource.IsCancellationRequested)
                    {
                        Thread.Sleep(this.cancellationInterval);
                        sleepTime = sleepTime.Subtract(this.cancellationInterval);
                    }

                    Thread.Sleep(sleepTime);
                }
            }
            catch (Exception ex)
            {
                ExporterStackdriverEventSource.Log.UnknownProblemInWorkerThreadError(ex);
            }
        }

        private bool RegisterView(IView view)
        {
            IView existing = null;
            if (this.registeredViews.TryGetValue(view.Name, out existing))
            {
                // Ignore views that are already registered.
                return existing.Equals(view);
            }

            this.registeredViews.Add(view.Name, view);

            var metricDescriptorTypeName = GenerateMetricDescriptorTypeName(view.Name, this.domain);

            // TODO - zeltser: don't need to create MetricDescriptor for RpcViewConstants once we defined
            // canonical metrics. Registration is required only for custom view definitions. Canonical
            // views should be pre-registered.
            var metricDescriptor = MetricsConversions.CreateMetricDescriptor(
                metricDescriptorTypeName,
                view,
                this.project,
                this.domain,
                this.displayNamePrefix);

            if (metricDescriptor == null)
            {
                // Don't register interval views in this version.
                return false;
            }

            // Cache metric descriptor and ensure it exists in Stackdriver
            if (!this.metricDescriptors.ContainsKey(view))
            {
                this.metricDescriptors.Add(view, metricDescriptor);
            }

            return this.EnsureMetricDescriptorExists(metricDescriptor);
        }

        private bool EnsureMetricDescriptorExists(MetricDescriptor metricDescriptor)
        {
            try
            {
                var request = new CreateMetricDescriptorRequest();
                request.ProjectName = this.project;
                request.MetricDescriptor = metricDescriptor;
                this.metricServiceClient.CreateMetricDescriptor(request);
            }
            catch (RpcException e)
            {
                // Metric already exists
                if (e.StatusCode == StatusCode.AlreadyExists)
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        private void Export()
        {
            var viewDataList = new List<IViewData>();
            foreach (var view in this.viewManager.AllExportedViews)
            {
                if (this.RegisterView(view))
                {
                    var data = this.viewManager.GetView(view.Name);
                    viewDataList.Add(data);
                }
            }

            // Add time series from all the views that need exporting
            var timeSeriesList = new List<TimeSeries>();
            foreach (var viewData in viewDataList)
            {
                var metricDescriptor = this.metricDescriptors[viewData.View];
                var timeSeries = MetricsConversions.CreateTimeSeriesList(viewData, this.monitoredResource, metricDescriptor, this.domain);
                timeSeriesList.AddRange(timeSeries);
            }

            // Perform the operation in batches of MAX_BATCH_EXPORT_SIZE
            foreach (var batchedTimeSeries in timeSeriesList.Partition(MaxBatchExportSize))
            {
                var request = new CreateTimeSeriesRequest();
                request.ProjectName = this.project;
                request.TimeSeries.AddRange(batchedTimeSeries);

                try
                {
                    this.metricServiceClient.CreateTimeSeries(request);
                }
                catch (RpcException e)
                {
                    ExporterStackdriverEventSource.Log.UnknownProblemWhileCreatingStackdriverTimeSeriesError(e);
                }
            }
        }

        private string GetDisplayNamePrefix(string metricNamePrefix)
        {
            if (metricNamePrefix == null)
            {
                return DefaultDisplayNamePrefix;
            }
            else
            {
                if (!metricNamePrefix.EndsWith("/") && !string.IsNullOrEmpty(metricNamePrefix))
                {
                    metricNamePrefix += '/';
                }

                return metricNamePrefix;
            }
        }

        /// <summary>
        /// Comparison between two OpenTelemetry Views.
        /// </summary>
        private class ViewNameComparer : IEqualityComparer<IView>
        {
            public bool Equals(IView x, IView y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                else if (x == null || y == null)
                {
                    return false;
                }
                else if (x.Name.AsString.Equals(y.Name.AsString))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public int GetHashCode(IView obj)
            {
                return obj.Name.GetHashCode();
            }
        }
    }
}
