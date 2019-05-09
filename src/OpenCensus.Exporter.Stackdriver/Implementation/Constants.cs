// <copyright file="ApplicationInsightsExporter.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Exporter.Stackdriver.Implementation
{
    using System;

    internal class Constants
    {
        public const string PACKAGE_VERSION_UNDEFINED = "undefined";

        public const string LABEL_DESCRIPTION = "OpenCensus TagKey";
        public const string OPENCENSUS_TASK = "opencensus_task";
        public const string OPENCENSUS_TASK_DESCRIPTION = "Opencensus task identifier";

        public const string GCP_GKE_CONTAINER = "k8s_container";
        public const string GCP_GCE_INSTANCE = "gce_instance";
        public const string AWS_EC2_INSTANCE = "aws_ec2_instance";
        public const string GLOBAL = "global";

        public const string PROJECT_ID_LABEL_KEY = "project_id";
        public static readonly string OPENCENSUS_TASK_VALUE_DEFAULT = GenerateDefaultTaskValue();

        public const string GCP_GCE_INSTANCE_TYPE = "cloud.google.com/gce/instance";
        public const string GCP_INSTANCE_ID_KEY = "cloud.google.com/gce/instance_id";
        public const string GCP_ACCOUNT_ID_KEY = "cloud.google.com/gce/project_id";
        public const string GCP_ZONE_KEY = "cloud.google.com/gce/zone";

        public const string K8S_CONTAINER_TYPE = "k8s.io/container";
        public const string K8S_CLUSTER_NAME_KEY = "k8s.io/cluster/name";
        public const string K8S_CONTAINER_NAME_KEY = "k8s.io/container/name";
        public const string K8S_NAMESPACE_NAME_KEY = "k8s.io/namespace/name";
        public const string K8S_POD_NAME_KEY = "k8s.io/pod/name";

        private static string GenerateDefaultTaskValue()
        {
            // Something like '<pid>@<hostname>'
            return $"dotnet-{System.Diagnostics.Process.GetCurrentProcess().Id}@{Environment.MachineName}";
        }
    }
}