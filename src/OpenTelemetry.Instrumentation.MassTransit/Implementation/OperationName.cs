// <copyright file="OperationName.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.MassTransit.Implementation
{
    /// <summary>
    /// MassTransit diagnostic source operation name constants.
    /// </summary>
    public static class OperationName
    {
        /// <summary>
        /// MassTransit Transport category constants.
        /// </summary>
        public static class Transport
        {
            /// <summary>
            /// MassTransit send diagnostic source operation name.
            /// </summary>
            public const string Send = "MassTransit.Transport.Send";

            /// <summary>
            /// MassTransit receive diagnostic source operation name.
            /// </summary>
            public const string Receive = "MassTransit.Transport.Receive";
        }

        /// <summary>
        /// MassTransit Consumer category constants.
        /// </summary>
        public static class Consumer
        {
            /// <summary>
            /// MassTransit consume diagnostic source operation name.
            /// </summary>
            public const string Consume = "MassTransit.Consumer.Consume";

            /// <summary>
            /// MassTransit handle diagnostic source operation name.
            /// </summary>
            public const string Handle = "MassTransit.Consumer.Handle";
        }
    }
}
