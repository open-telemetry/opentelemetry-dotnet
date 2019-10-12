// <copyright file="Constants.cs" company="OpenTelemetry Authors">
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
using System;

namespace OpenTelemetry.Exporter.Stackdriver.Implementation
{
    internal class Constants
    {
        public static readonly string PackagVersionUndefined = "undefined";

        public static readonly string LabelDescription = "OpenTelemetry TagKey";
        public static readonly string OpenTelemetryTask = "OpenTelemetry_task";
        public static readonly string OpenTelemetryTaskDescription = "OpenTelemetry task identifier";

        public static readonly string GcpGkeContainer = "k8s_container";
        public static readonly string GcpGceInstance = "gce_instance";
        public static readonly string AwsEc2Instance = "aws_ec2_instance";
        public static readonly string Global = "global";

        public static readonly string ProjectIdLabelKey = "project_id";
        public static readonly string OpenTelemetryTaskValueDefault = GenerateDefaultTaskValue();

        public static readonly string GceGcpInstanceType = "cloud.google.com/gce/instance";
        public static readonly string GcpInstanceIdKey = "cloud.google.com/gce/instance_id";
        public static readonly string GcpAccountIdKey = "cloud.google.com/gce/project_id";
        public static readonly string GcpZoneKey = "cloud.google.com/gce/zone";

        public static readonly string K8sContainerType = "k8s.io/container";
        public static readonly string K8sClusterNameKey = "k8s.io/cluster/name";
        public static readonly string K8sContainerNameKey = "k8s.io/container/name";
        public static readonly string K8sNamespaceNameKey = "k8s.io/namespace/name";
        public static readonly string K8sPodNameKey = "k8s.io/pod/name";

        private static string GenerateDefaultTaskValue()
        {
            // Something like '<pid>@<hostname>'
            return $"dotnet-{System.Diagnostics.Process.GetCurrentProcess().Id}@{Environment.MachineName}";
        }
    }
}
