// <copyright file="TracingTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Test
{
    using OpenCensus.Trace.Config;
    using OpenCensus.Trace.Export;
    using OpenCensus.Trace.Propagation;
    using Xunit;

    public class TracingTest
    {
        // @Rule public ExpectedException thrown = ExpectedException.none();

        // [Fact]
        // public void loadTraceComponent_UsesProvidedClassLoader()
        //      {
        //          final RuntimeException toThrow = new RuntimeException("UseClassLoader");
        //          thrown.expect(RuntimeException.class);
        //  thrown.expectMessage("UseClassLoader");
        //  Tracing.loadTraceComponent(
        //      new ClassLoader()
        //      {
        //          @Override
        //        public Class<?> loadClass(String name)
        //          {
        //              throw toThrow;
        //          }
        //      });
        // }

        // [Fact]
        //  public void loadTraceComponent_IgnoresMissingClasses()
        //    {
        //        ClassLoader classLoader =
        //            new ClassLoader() {
        //          @Override
        //              public Class<?> loadClass(String name) throws ClassNotFoundException {
        //            throw new ClassNotFoundException();
        //        }
        //    };
        //    assertThat(Tracing.loadTraceComponent(classLoader).getClass().getName())
        //        .isEqualTo("io.opencensus.trace.TraceComponent$NoopTraceComponent");
        // }

        [Fact(Skip = "need to fix the way tracer being instantiated")]
        public void DefaultTracer()
        {
            Assert.Same(Tracer.NoopTracer, Tracing.Tracer);
        }

        [Fact(Skip = "need to fix the way tracer being instantiated")]
        public void DefaultBinaryPropagationHandler()
        {
            Assert.Same(PropagationComponentBase.NoopPropagationComponent, Tracing.PropagationComponent);
        }

        [Fact(Skip = "need to fix the way tracer being instantiated")]
        public void DefaultTraceExporter()
        {
            Assert.Equal(ExportComponentBase.NewNoopExportComponent.GetType(), Tracing.ExportComponent.GetType());
        }

        [Fact(Skip = "need to fix the way tracer being instantiated")]
        public void DefaultTraceConfig()
        {
            Assert.Same(TraceConfigBase.NoopTraceConfig, Tracing.TraceConfig);
        }

        // [Fact]
        // public void ImplementationOfTracer()
        // {
        //    Assert.Type<Tracer>(Tracer()).isInstanceOf(TracerImpl.;
        // }

        // [Fact]
        // public void ImplementationOfBinaryPropagationHandler()
        // {
        //    assertThat(Tracing.getPropagationComponent()).isInstanceOf(PropagationComponent);
        // }

        // [Fact]
        // public void ImplementationOfClock()
        // {
        //    assertThat(Tracing.getClock()).isInstanceOf(MillisClock);
        // }

        // [Fact]
        // public void ImplementationOfTraceExporter()
        // {
        //    assertThat(Tracing.getExportComponent()).isInstanceOf(ExportComponentImpl;
        // }

    }
}
