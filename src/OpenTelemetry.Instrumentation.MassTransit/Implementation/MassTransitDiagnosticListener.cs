// <copyright file="MassTransitDiagnosticListener.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MassTransit.Implementation
{
    internal class MassTransitDiagnosticListener : ListenerHandler
    {
        private readonly ActivitySourceAdapter activitySource;

        public MassTransitDiagnosticListener(string name, ActivitySourceAdapter activitySource)
            : base(name)
        {
            this.activitySource = activitySource;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            this.activitySource.Start(activity);
            if (activity.IsAllDataRequested)
            {
                var tags = activity.Tags.ToDictionary(x => x.Key, x => x.Value);

                switch (activity.OperationName)
                {
                    case "MassTransit.Consumer.Consume":
                        activity.DisplayName = $"CONSUME {tags["consumer-type"]}";
                        break;
                    case "MassTransit.Transport.Send":
                        activity.DisplayName = $"SEND {tags["peer.address"]}";
                        break;
                    case "MassTransit.Transport.Receive":
                        activity.DisplayName = $"RECV {tags["peer.address"]}";
                        break;
                }
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            this.activitySource.Stop(activity);
        }
    }
}
