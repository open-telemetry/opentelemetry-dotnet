// <copyright file="OpenTelemetryLogger.cs" company="OpenTelemetry Authors">
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

#if NET461 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    internal class OpenTelemetryLogger : ILogger
    {
        private readonly string categoryName;
        private readonly OpenTelemetryLoggerProvider provider;

        internal OpenTelemetryLogger(string categoryName, OpenTelemetryLoggerProvider provider)
        {
            this.categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        internal IExternalScopeProvider ScopeProvider { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!this.IsEnabled(logLevel) || Sdk.SuppressInstrumentation)
            {
                return;
            }

            var processor = this.provider.Processor;
            if (processor != null)
            {
                var options = this.provider.Options;

                var record = new LogRecord(
                    options.IncludeScopes ? this.ScopeProvider : null,
                    DateTime.UtcNow,
                    this.categoryName,
                    logLevel,
                    eventId,
                    options.IncludeMessage ? formatter(state, exception) : null,
                    options.ParseStateValues ? null : (object)state,
                    exception,
                    options.ParseStateValues ? this.ParseState(state) : null);

                processor.OnEnd(record);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public IDisposable BeginScope<TState>(TState state) => this.ScopeProvider?.Push(state) ?? null;

        private IReadOnlyList<KeyValuePair<string, object>> ParseState<TState>(TState state)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object>>)
            {
                return StateValueListParser<TState>.ParseStateFunc(state);
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> stateValues)
            {
                return new List<KeyValuePair<string, object>>(stateValues);
            }
            else
            {
                return new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>(string.Empty, state),
                };
            }
        }

        private static class StateValueListParser<TState>
        {
            static StateValueListParser()
            {
                /* The code below is building a dynamic method to do this...

                     int count = state.Count;

                     List<KeyValuePair<string, object>> stateValues = new List<KeyValuePair<string, object>>(count);

                     for (int i = 0; i < count; i++)
                     {
                         stateValues.Add(state[i]);
                     }

                     return stateValues;

                  ...so we don't box structs or allocate an enumerator. */

                var stateType = typeof(TState);
                var listType = typeof(List<KeyValuePair<string, object>>);
                var readOnlyListType = typeof(IReadOnlyList<KeyValuePair<string, object>>);

                var dynamicMethod = new DynamicMethod(
                    nameof(StateValueListParser<TState>),
                    readOnlyListType,
                    new[] { stateType },
                    typeof(StateValueListParser<TState>).Module,
                    skipVisibility: true);

                var generator = dynamicMethod.GetILGenerator();

                var testLabel = generator.DefineLabel();
                var writeItemLabel = generator.DefineLabel();

                generator.DeclareLocal(typeof(int)); // count
                generator.DeclareLocal(listType); // list
                generator.DeclareLocal(typeof(int)); // i

                if (stateType.IsValueType)
                {
                    generator.Emit(OpCodes.Ldarga_S, 0); // state
                    generator.Emit(OpCodes.Call, stateType.GetProperty("Count").GetMethod);
                }
                else
                {
                    generator.Emit(OpCodes.Ldarg_0); // state
                    generator.Emit(OpCodes.Callvirt, typeof(IReadOnlyCollection<KeyValuePair<string, object>>).GetProperty("Count").GetMethod);
                }

                generator.Emit(OpCodes.Stloc_0); // count = state.Count
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Newobj, listType.GetConstructor(new Type[] { typeof(int) }));
                generator.Emit(OpCodes.Stloc_1); // list = new List(count)

                generator.Emit(OpCodes.Ldc_I4_0);
                generator.Emit(OpCodes.Stloc_2); // i = 0

                generator.Emit(OpCodes.Br_S, testLabel);
                generator.MarkLabel(writeItemLabel);
                generator.Emit(OpCodes.Ldloc_1); // list
                if (stateType.IsValueType)
                {
                    generator.Emit(OpCodes.Ldarga_S, 0); // state
                }
                else
                {
                    generator.Emit(OpCodes.Ldarg_0); // state
                }

                generator.Emit(OpCodes.Ldloc_2); // i
                if (stateType.IsValueType)
                {
                    generator.Emit(OpCodes.Call, stateType.GetProperty("Item").GetMethod);
                }
                else
                {
                    generator.Emit(OpCodes.Callvirt, readOnlyListType.GetProperty("Item").GetMethod);
                }

                generator.Emit(OpCodes.Callvirt, listType.GetMethod("Add", new Type[] { typeof(KeyValuePair<string, object>) })); // list.Add(state[i])

                generator.Emit(OpCodes.Ldloc_2); // i
                generator.Emit(OpCodes.Ldc_I4_1);
                generator.Emit(OpCodes.Add); // i++
                generator.Emit(OpCodes.Stloc_2);

                generator.MarkLabel(testLabel);
                generator.Emit(OpCodes.Ldloc_2); // i
                generator.Emit(OpCodes.Ldloc_0); // count
                generator.Emit(OpCodes.Blt_S, writeItemLabel);

                generator.Emit(OpCodes.Ldloc_1); // list
                generator.Emit(OpCodes.Ret);

                ParseStateFunc = (Func<TState, IReadOnlyList<KeyValuePair<string, object>>>)dynamicMethod.CreateDelegate(typeof(Func<TState, IReadOnlyList<KeyValuePair<string, object>>>));
            }

            public static Func<TState, IReadOnlyList<KeyValuePair<string, object>>> ParseStateFunc { get; }
        }
    }
}
#endif
