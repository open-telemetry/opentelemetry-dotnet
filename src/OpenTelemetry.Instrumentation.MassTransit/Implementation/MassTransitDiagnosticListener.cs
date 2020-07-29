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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MassTransit.Implementation
{
    internal class MassTransitDiagnosticListener : ListenerHandler
    {
        private readonly ActivitySourceAdapter activitySource;
        private readonly MassTransitInstrumentationOptions options;

        public MassTransitDiagnosticListener(ActivitySourceAdapter activitySource, MassTransitInstrumentationOptions options)
            : base("MassTransit")
        {
            this.activitySource = activitySource;
            this.options = options;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            if (!this.options.TracedOperations.Contains(activity.OperationName))
            {
                return;
            }

            activity.SetKind(this.GetActivityKind(activity));
            activity.DisplayName = this.GetDisplayName(activity);

            this.activitySource.Start(activity);
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (!this.options.TracedOperations.Contains(activity.OperationName))
            {
                return;
            }

            this.activitySource.Stop(activity);
        }

        private string GetDisplayName(Activity activity)
        {
            switch (activity.OperationName)
            {
                case OperationName.Transport.Send:
                    return DisplayNameHelper.GetSendOperationDisplayName(this.GetTag(activity.Tags, TagName.PeerAddress));
                case OperationName.Transport.Receive:
                    return DisplayNameHelper.GetReceiveOperationDisplayName(this.GetTag(activity.Tags, TagName.PeerAddress));
                case OperationName.Consumer.Consume:
                    return DisplayNameHelper.GetConsumeOperationDisplayName(this.GetTag(activity.Tags, TagName.ConsumerType));
                case OperationName.Consumer.Handle:
                    return DisplayNameHelper.GetHandleOperationDisplayName(this.GetTag(activity.Tags, TagName.PeerAddress));
                default:
                    return activity.DisplayName;
            }
        }

        private ActivityKind GetActivityKind(Activity activity)
        {
            switch (activity.OperationName)
            {
                case OperationName.Transport.Send:
                    return ActivityKind.Client;
                case OperationName.Transport.Receive:
                    return ActivityKind.Internal;
                case OperationName.Consumer.Consume:
                    return ActivityKind.Consumer;
                case OperationName.Consumer.Handle:
                    return ActivityKind.Consumer;
                default:
                    return activity.Kind;
            }
        }

        private string GetTag(IEnumerable<KeyValuePair<string, string>> tags, string tagName)
        {
            var tag = tags.SingleOrDefault(kv => kv.Key == tagName);
            return tag.Value;
        }
    }
}
