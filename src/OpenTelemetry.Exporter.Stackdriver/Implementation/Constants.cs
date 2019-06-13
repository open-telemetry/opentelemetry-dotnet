// <copyright file="ApplicationInsightsExporter.cs" company="OpenTelemetry Authors">
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

    internal class Constants
    {
        public static readonly string PACKAGE_VERSION_UNDEFINED = "undefined";

        public static readonly string LABEL_DESCRIPTION = "OpenTelemetry TagKey";
        public static readonly string OpenTelemetry_TASK = "OpenTelemetry_task";
        public static readonly string OpenTelemetry_TASK_DESCRIPTION = "OpenTelemetry task identifier";

        public static readonly string GCP_GKE_CONTAINER = "k8s_container";
        public static readonly string GCP_GCE_INSTANCE = "gce_instance";
        public static readonly string AWS_EC2_INSTANCE = "aws_ec2_instance";
        public static readonly string GLOBAL = "global";

        public static readonly string PROJECT_ID_LABEL_KEY = "project_id";
        public static readonly string OpenTelemetry_TASK_VALUE_DEFAULT = GenerateDefaultTaskValue();

        public static readonly string GCP_GCE_INSTANCE_TYPE = "cloud.google.com/gce/instance";
        public static readonly string GCP_INSTANCE_ID_KEY = "cloud.google.com/gce/instance_id";
        public static readonly string GCP_ACCOUNT_ID_KEY = "cloud.google.com/gce/project_id";
        public static readonly string GCP_ZONE_KEY = "cloud.google.com/gce/zone";

        public static readonly string K8S_CONTAINER_TYPE = "k8s.io/container";
        public static readonly string K8S_CLUSTER_NAME_KEY = "k8s.io/cluster/name";
        public static readonly string K8S_CONTAINER_NAME_KEY = "k8s.io/container/name";
        public static readonly string K8S_NAMESPACE_NAME_KEY = "k8s.io/namespace/name";
        public static readonly string K8S_POD_NAME_KEY = "k8s.io/pod/name";

        private static string GenerateDefaultTaskValue()
        {
            // Something like '<pid>@<hostname>'
            return $"dotnet-{System.Diagnostics.Process.GetCurrentProcess().Id}@{Environment.MachineName}";
        }
    }
}