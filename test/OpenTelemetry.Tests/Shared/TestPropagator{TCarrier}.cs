// <copyright file="TestPropagator{TCarrier}.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;

namespace OpenTelemetry.Context.Propagation.Tests
{
    internal sealed class TestPropagator<TCarrier> : TextMapPropagator
    {
        private static readonly ExtractDelegate NoopExtract = (ref PropagationContext context, TCarrier carrier, Func<TCarrier, string, IEnumerable<string>> getter) => { };
        private static readonly InjectDelegate NoopInject = (in PropagationContext context, TCarrier carrier, Action<TCarrier, string, string> setter) => { };

        private readonly ExtractDelegate extractDelegate;
        private readonly InjectDelegate injectDelegate;

        public TestPropagator(ExtractDelegate extractDelegate = null, InjectDelegate injectDelegate = null)
        {
            this.extractDelegate = extractDelegate ?? NoopExtract;
            this.injectDelegate = injectDelegate ?? NoopInject;
        }

        public delegate void ExtractDelegate(ref PropagationContext context, TCarrier carrier, Func<TCarrier, string, IEnumerable<string>> getter);

        public delegate void InjectDelegate(in PropagationContext context, TCarrier carrier, Action<TCarrier, string, string> setter);

        public override ISet<string> Fields => null;

        public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            this.Extract(ref context, carrier, getter);
            return context;
        }

        public override void Extract<T>(ref PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (carrier is TCarrier typedCarrier)
            {
                this.extractDelegate(ref context, typedCarrier, (Func<TCarrier, string, IEnumerable<string>>)(object)getter);
            }
        }

        public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            this.Inject(in context, carrier, setter);
        }

        public override void Inject<T>(in PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            if (carrier is TCarrier typedCarrier)
            {
                this.injectDelegate(in context, typedCarrier, (Action<TCarrier, string, string>)(object)setter);
            }
        }
    }
}
