// <copyright file="JvmMetrics.cs" company="OpenTelemetry Authors">
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

// <auto-generated> This file has been auto generated from buildscripts/semantic-conventions/templates/Metrics.cs.j2</auto-generated>

using System.Diagnostics.Metrics;

namespace OpenTelemetry.SemanticConventions.v1_23_1_Experimental
{
  /// <summary>
  /// Describes semantic conventions for metrics in the <c>jvm</c> namespace.
  /// </summary>
  public static class JvmMetrics {
      /// <summary>
      /// Creates <c>jvm.buffer.count</c> instrument.
      /// Number of buffers in the pool.
      /// </summary>
      public static UpDownCounter<long> CreateJvmBufferCount(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.buffer.count", "{buffer}", "Number of buffers in the pool.");
      }

      /// <summary>
      /// Creates <c>jvm.buffer.memory.limit</c> instrument.
      /// Measure of total memory capacity of buffers.
      /// </summary>
      public static UpDownCounter<long> CreateJvmBufferMemoryLimit(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.buffer.memory.limit", "By", "Measure of total memory capacity of buffers.");
      }

      /// <summary>
      /// Creates <c>jvm.buffer.memory.usage</c> instrument.
      /// Measure of memory used by buffers.
      /// </summary>
      public static UpDownCounter<long> CreateJvmBufferMemoryUsage(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.buffer.memory.usage", "By", "Measure of memory used by buffers.");
      }

      /// <summary>
      /// Creates <c>jvm.class.count</c> instrument.
      /// Number of classes currently loaded.
      /// </summary>
      public static UpDownCounter<long> CreateJvmClassCount(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.class.count", "{class}", "Number of classes currently loaded.");
      }

      /// <summary>
      /// Creates <c>jvm.class.loaded</c> instrument.
      /// Number of classes loaded since JVM start.
      /// </summary>
      public static Counter<long> CreateJvmClassLoaded(Meter meter)
      {
          return meter.CreateCounter<long>("jvm.class.loaded", "{class}", "Number of classes loaded since JVM start.");
      }

      /// <summary>
      /// Creates <c>jvm.class.unloaded</c> instrument.
      /// Number of classes unloaded since JVM start.
      /// </summary>
      public static Counter<long> CreateJvmClassUnloaded(Meter meter)
      {
          return meter.CreateCounter<long>("jvm.class.unloaded", "{class}", "Number of classes unloaded since JVM start.");
      }

      /// <summary>
      /// Creates <c>jvm.cpu.count</c> instrument.
      /// Number of processors available to the Java virtual machine.
      /// </summary>
      public static UpDownCounter<long> CreateJvmCpuCount(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.cpu.count", "{cpu}", "Number of processors available to the Java virtual machine.");
      }

      /// <summary>
      /// Creates <c>jvm.cpu.recent_utilization</c> instrument.
      /// Recent CPU utilization for the process as reported by the JVM.
      /// </summary>
      /// <remarks>
      /// The value range is [0.0,1.0]. This utilization is not defined as being for the specific interval since last measurement (unlike <c>system.cpu.utilization</c>). <a href="https://docs.oracle.com/en/java/javase/17/docs/api/jdk.management/com/sun/management/OperatingSystemMXBean.html#getProcessCpuLoad()">Reference</a>.
      /// </remarks>
      public static ObservableGauge<double> CreateJvmCpuRecentUtilization(Meter meter, Func<Measurement<double>> observe)
      {
          return meter.CreateObservableGauge<double>("jvm.cpu.recent_utilization", observe, "1", "Recent CPU utilization for the process as reported by the JVM.");
      }

      /// <summary>
      /// Creates <c>jvm.cpu.time</c> instrument.
      /// CPU time used by the process as reported by the JVM.
      /// </summary>
      public static Counter<long> CreateJvmCpuTime(Meter meter)
      {
          return meter.CreateCounter<long>("jvm.cpu.time", "s", "CPU time used by the process as reported by the JVM.");
      }

      /// <summary>
      /// Creates <c>jvm.gc.duration</c> instrument.
      /// Duration of JVM garbage collection actions.
      /// </summary>
      public static Histogram<double> CreateJvmGcDuration(Meter meter)
      {
          return meter.CreateHistogram<double>("jvm.gc.duration", "s", "Duration of JVM garbage collection actions.");
      }

      /// <summary>
      /// Creates <c>jvm.memory.committed</c> instrument.
      /// Measure of memory committed.
      /// </summary>
      public static UpDownCounter<long> CreateJvmMemoryCommitted(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.memory.committed", "By", "Measure of memory committed.");
      }

      /// <summary>
      /// Creates <c>jvm.memory.init</c> instrument.
      /// Measure of initial memory requested.
      /// </summary>
      public static UpDownCounter<long> CreateJvmMemoryInit(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.memory.init", "By", "Measure of initial memory requested.");
      }

      /// <summary>
      /// Creates <c>jvm.memory.limit</c> instrument.
      /// Measure of max obtainable memory.
      /// </summary>
      public static UpDownCounter<long> CreateJvmMemoryLimit(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.memory.limit", "By", "Measure of max obtainable memory.");
      }

      /// <summary>
      /// Creates <c>jvm.memory.usage</c> instrument.
      /// Measure of memory used.
      /// </summary>
      public static UpDownCounter<long> CreateJvmMemoryUsage(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.memory.usage", "By", "Measure of memory used.");
      }

      /// <summary>
      /// Creates <c>jvm.memory.usage_after_last_gc</c> instrument.
      /// Measure of memory used, as measured after the most recent garbage collection event on this pool.
      /// </summary>
      public static UpDownCounter<long> CreateJvmMemoryUsageAfterLastGc(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.memory.usage_after_last_gc", "By", "Measure of memory used, as measured after the most recent garbage collection event on this pool.");
      }

      /// <summary>
      /// Creates <c>jvm.system.cpu.load_1m</c> instrument.
      /// Average CPU load of the whole system for the last minute as reported by the JVM.
      /// </summary>
      /// <remarks>
      /// The value range is [0,n], where n is the number of CPU cores - or a negative number if the value is not available. This utilization is not defined as being for the specific interval since last measurement (unlike <c>system.cpu.utilization</c>). <a href="https://docs.oracle.com/en/java/javase/17/docs/api/java.management/java/lang/management/OperatingSystemMXBean.html#getSystemLoadAverage()">Reference</a>.
      /// </remarks>
      public static ObservableGauge<double> CreateJvmSystemCpuLoad1m(Meter meter, Func<Measurement<double>> observe)
      {
          return meter.CreateObservableGauge<double>("jvm.system.cpu.load_1m", observe, "{run_queue_item}", "Average CPU load of the whole system for the last minute as reported by the JVM.");
      }

      /// <summary>
      /// Creates <c>jvm.system.cpu.utilization</c> instrument.
      /// Recent CPU utilization for the whole system as reported by the JVM.
      /// </summary>
      /// <remarks>
      /// The value range is [0.0,1.0]. This utilization is not defined as being for the specific interval since last measurement (unlike <c>system.cpu.utilization</c>). <a href="https://docs.oracle.com/en/java/javase/17/docs/api/jdk.management/com/sun/management/OperatingSystemMXBean.html#getCpuLoad()">Reference</a>.
      /// </remarks>
      public static ObservableGauge<double> CreateJvmSystemCpuUtilization(Meter meter, Func<Measurement<double>> observe)
      {
          return meter.CreateObservableGauge<double>("jvm.system.cpu.utilization", observe, "1", "Recent CPU utilization for the whole system as reported by the JVM.");
      }

      /// <summary>
      /// Creates <c>jvm.thread.count</c> instrument.
      /// Number of executing platform threads.
      /// </summary>
      public static UpDownCounter<long> CreateJvmThreadCount(Meter meter)
      {
          return meter.CreateUpDownCounter<long>("jvm.thread.count", "{thread}", "Number of executing platform threads.");
      }
  }
}
