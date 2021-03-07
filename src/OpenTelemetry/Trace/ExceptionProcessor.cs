// <copyright file="ExceptionProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTelemetry.Trace
{
    internal class ExceptionProcessor : BaseProcessor<Activity>
    {
        private const string ExceptionPointersKey = "otel.exception_pointers";

        private readonly Func<IntPtr> fnGetExceptionPointers;

        public ExceptionProcessor()
        {
            try
            {
                var flags = BindingFlags.Static | BindingFlags.Public;
                var method = typeof(Marshal).GetMethod("GetExceptionPointers", flags, null, new Type[] { }, null);
                var lambda = Expression.Lambda<Func<IntPtr>>(Expression.Call(method));
                this.fnGetExceptionPointers = lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new NotSupportedException("System.Runtime.InteropServices.Marshal.GetExceptionPointers is not supported.", ex);
            }
        }

        /// <summary>TBD.</summary>
        [Flags]
        internal enum EXCEPTION_FLAGS : uint
        {
            /// <summary>TBD.</summary>
            EXCEPTION_NONCONTINUABLE = 0x1,

            /// <summary>TBD.</summary>
            EXCEPTION_UNWINDING = 0x2,

            /// <summary>Exit unwind is in progress (not used by PAL SEH).</summary>
            EXCEPTION_EXIT_UNWIND = 0x4,

            /// <summary>Nested exception handler call.</summary>
            EXCEPTION_NESTED_CALL = 0x10,

            /// <summary>Target unwind in progress.</summary>
            EXCEPTION_TARGET_UNWIND = 0x20,

            /// <summary>Collided exception handler call.</summary>
            EXCEPTION_COLLIDED_UNWIND = 0x40,

            /// <summary>TBD.</summary>
            EXCEPTION_SKIP_VEH = 0x200,
        }

        /// <summary>TBD.</summary>
        internal enum EXCEPTION_CODE : uint
        {
            /// <summary>TBD.</summary>
            EXCEPTION_ACCESS_VIOLATION = 0xC0000005,

            /// <summary>a.k.a. Error "msc" in ASCII.</summary>
            EXCEPTION_MSVC = 0xE06d7363,

            /// <summary>a.k.a. Error "CCR" in ASCII.</summary>
            EXCEPTION_COMPLUS = 0xE0434352,

            /// <summary>a.k.a. Error "COM"+1 (ComPlus) in ASCII.</summary>
            EXCEPTION_HIJACK = 0xE0434f4e,
        }

        /// <inheritdoc />
        public override void OnStart(Activity activity)
        {
            var pointers = this.fnGetExceptionPointers();

            if (pointers != IntPtr.Zero)
            {
                activity.SetTag(ExceptionPointersKey, pointers);
            }
        }

        /// <inheritdoc />
        public override void OnEnd(Activity activity)
        {
            var pointers = this.fnGetExceptionPointers();

            if (pointers == IntPtr.Zero)
            {
                return;
            }

            var snapshot = activity.GetTagValue(ExceptionPointersKey) as IntPtr?;

            if (snapshot != null)
            {
                activity.SetTag(ExceptionPointersKey, null);
            }

            if (snapshot != pointers)
            {
                var exceptionPointers = this.PtrToStructure<EXCEPTION_POINTERS>(pointers);
                activity.SetTag("exceptionPointers.ContextRecord", exceptionPointers.ContextRecord);
                var exceptionRecord = this.PtrToStructure<EXCEPTION_RECORD>(exceptionPointers.ExceptionRecord);
                activity.SetTag("exceptionRecord.ExceptionCode", exceptionRecord.ExceptionCode);
                activity.SetTag("exceptionRecord.ExceptionFlags", exceptionRecord.ExceptionFlags);
                activity.SetStatus(Status.Error);
            }
        }

        internal T PtrToStructure<T>(IntPtr p)
        {
            return (T)Marshal.PtrToStructure(p, typeof(T));
        }

        /*
            typedef struct _EXCEPTION_POINTERS {
            PEXCEPTION_RECORD ExceptionRecord;
            PCONTEXT          ContextRecord;
            } EXCEPTION_POINTERS, *PEXCEPTION_POINTERS;
        */
        [StructLayout(LayoutKind.Sequential)]
        internal struct EXCEPTION_POINTERS
        {
            public IntPtr ExceptionRecord;
            public IntPtr ContextRecord;
        }

        /*
            typedef struct _EXCEPTION_RECORD {
            DWORD                    ExceptionCode;
            DWORD                    ExceptionFlags;
            struct _EXCEPTION_RECORD *ExceptionRecord;
            PVOID                    ExceptionAddress;
            DWORD                    NumberParameters;
            ULONG_PTR                ExceptionInformation[EXCEPTION_MAXIMUM_PARAMETERS];
            } EXCEPTION_RECORD;
        */
        [StructLayout(LayoutKind.Sequential)]
        internal struct EXCEPTION_RECORD
        {
            public EXCEPTION_CODE ExceptionCode;
            public EXCEPTION_FLAGS ExceptionFlags;
            public IntPtr ExceptionRecord;
            public IntPtr ExceptionAddress;
            public uint NumberParameters;
            public IntPtr ExceptionInformation;
        }
    }
}
