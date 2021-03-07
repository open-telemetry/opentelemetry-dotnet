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
                var exceptionPointers = Marshal.PtrToStructure<EXCEPTION_POINTERS>(pointers);
                var exceptionRecord = Marshal.PtrToStructure<EXCEPTION_RECORD>(exceptionPointers.ExceptionRecord);
                activity.SetTag("exceptionRecord.ExceptionCode", exceptionRecord.ExceptionCode);
                activity.SetTag("exceptionRecord.ExceptionFlags", exceptionRecord.ExceptionFlags);

                switch (exceptionRecord.ExceptionCode)
                {
                case EXCEPTION_CODE.EXCEPTION_COMPLUS:
                    if (exceptionRecord.NumberParameters != 5 /* INSTANCE_TAGGED_SEH_PARAM_ARRAY_SIZE */)
                    {
                        break;
                    }

                    activity.SetTag("exceptionRecord.NumberParameters", exceptionRecord.NumberParameters);
                    activity.SetTag("exceptionRecord.ExceptionInformation", exceptionRecord.ExceptionInformation);
                    var hresult = (HRESULT)((ulong)exceptionRecord.ExceptionInformation[0] % 0x100000000UL);
                    activity.SetTag("exceptionRecord.ExceptionInformation.HResult", hresult);

                    // exceptionRecord.ExceptionInformation[1] == 0
                    // exceptionRecord.ExceptionInformation[2] == 0
                    // exceptionRecord.ExceptionInformation[3] == 0

                    var pClrModuleBase = exceptionRecord.ExceptionInformation[4];
                    activity.SetTag("exceptionRecord.ExceptionInformation.ClrModuleBase", pClrModuleBase);

                    break;
                default:
                    break;
                }

                activity.SetStatus(Status.Error);
            }
        }

// SA1201: A enum should not follow a method
// SA1602: Enumeration items should be documented
#pragma warning disable SA1201, SA1602

        internal enum HRESULT : int
        {
            S_OK = unchecked((int)0x00000000),
            S_FALSE = unchecked((int)0x1),
            COR_E_ABANDONEDMUTEX = unchecked((int)0x8013152D),
            COR_E_AMBIGUOUSIMPLEMENTATION = unchecked((int)0x8013106A),
            COR_E_AMBIGUOUSMATCH = unchecked((int)0x8000211D),
            COR_E_APPDOMAINUNLOADED = unchecked((int)0x80131014),
            COR_E_APPLICATION = unchecked((int)0x80131600),
            COR_E_ARGUMENT = unchecked((int)0x80070057),
            COR_E_ARGUMENTOUTOFRANGE = unchecked((int)0x80131502),
            COR_E_ARITHMETIC = unchecked((int)0x80070216),
            COR_E_ARRAYTYPEMISMATCH = unchecked((int)0x80131503),
            COR_E_BADEXEFORMAT = unchecked((int)0x800700C1),
            COR_E_BADIMAGEFORMAT = unchecked((int)0x8007000B),
            COR_E_CANNOTUNLOADAPPDOMAIN = unchecked((int)0x80131015),
            COR_E_CODECONTRACTFAILED = unchecked((int)0x80131542),
            COR_E_CONTEXTMARSHAL = unchecked((int)0x80131504),
            COR_E_CUSTOMATTRIBUTEFORMAT = unchecked((int)0x80131605),
            COR_E_DATAMISALIGNED = unchecked((int)0x80131541),
            COR_E_DIRECTORYNOTFOUND = unchecked((int)0x80070003),
            COR_E_DIVIDEBYZERO = unchecked((int)0x80020012),
            COR_E_DLLNOTFOUND = unchecked((int)0x80131524),
            COR_E_DUPLICATEWAITOBJECT = unchecked((int)0x80131529),
            COR_E_ENDOFSTREAM = unchecked((int)0x80070026),
            COR_E_ENTRYPOINTNOTFOUND = unchecked((int)0x80131523),
            COR_E_EXCEPTION = unchecked((int)0x80131500),
            COR_E_EXECUTIONENGINE = unchecked((int)0x80131506),
            COR_E_FIELDACCESS = unchecked((int)0x80131507),
            COR_E_FILELOAD = unchecked((int)0x80131621),
            COR_E_FILENOTFOUND = unchecked((int)0x80070002),
            COR_E_FORMAT = unchecked((int)0x80131537),
            COR_E_INDEXOUTOFRANGE = unchecked((int)0x80131508),
            COR_E_INSUFFICIENTEXECUTIONSTACK = unchecked((int)0x80131578),
            COR_E_INSUFFICIENTMEMORY = unchecked((int)0x8013153D),
            COR_E_INVALIDCAST = unchecked((int)0x80004002),
            COR_E_INVALIDCOMOBJECT = unchecked((int)0x80131527),
            COR_E_INVALIDFILTERCRITERIA = unchecked((int)0x80131601),
            COR_E_INVALIDOLEVARIANTTYPE = unchecked((int)0x80131531),
            COR_E_INVALIDOPERATION = unchecked((int)0x80131509),
            COR_E_INVALIDPROGRAM = unchecked((int)0x8013153A),
            COR_E_IO = unchecked((int)0x80131620),
            COR_E_KEYNOTFOUND = unchecked((int)0x80131577),
            COR_E_MARSHALDIRECTIVE = unchecked((int)0x80131535),
            COR_E_MEMBERACCESS = unchecked((int)0x8013151A),
            COR_E_METHODACCESS = unchecked((int)0x80131510),
            COR_E_MISSINGFIELD = unchecked((int)0x80131511),
            COR_E_MISSINGMANIFESTRESOURCE = unchecked((int)0x80131532),
            COR_E_MISSINGMEMBER = unchecked((int)0x80131512),
            COR_E_MISSINGMETHOD = unchecked((int)0x80131513),
            COR_E_MISSINGSATELLITEASSEMBLY = unchecked((int)0x80131536),
            COR_E_MULTICASTNOTSUPPORTED = unchecked((int)0x80131514),
            COR_E_NOTFINITENUMBER = unchecked((int)0x80131528),
            COR_E_NOTSUPPORTED = unchecked((int)0x80131515),
            COR_E_OBJECTDISPOSED = unchecked((int)0x80131622),
            COR_E_OPERATIONCANCELED = unchecked((int)0x8013153B),
            COR_E_OUTOFMEMORY = unchecked((int)0x8007000E),
            COR_E_OVERFLOW = unchecked((int)0x80131516),
            COR_E_PATHTOOLONG = unchecked((int)0x800700CE),
            COR_E_PLATFORMNOTSUPPORTED = unchecked((int)0x80131539),
            COR_E_RANK = unchecked((int)0x80131517),
            COR_E_REFLECTIONTYPELOAD = unchecked((int)0x80131602),
            COR_E_RUNTIMEWRAPPED = unchecked((int)0x8013153E),
            COR_E_SAFEARRAYRANKMISMATCH = unchecked((int)0x80131538),
            COR_E_SAFEARRAYTYPEMISMATCH = unchecked((int)0x80131533),
            COR_E_SECURITY = unchecked((int)0x8013150A),
            COR_E_SERIALIZATION = unchecked((int)0x8013150C),
            COR_E_STACKOVERFLOW = unchecked((int)0x800703E9),
            COR_E_SYNCHRONIZATIONLOCK = unchecked((int)0x80131518),
            COR_E_SYSTEM = unchecked((int)0x80131501),
            COR_E_TARGET = unchecked((int)0x80131603),
            COR_E_TARGETINVOCATION = unchecked((int)0x80131604),
            COR_E_TARGETPARAMCOUNT = unchecked((int)0x8002000E),
            COR_E_THREADABORTED = unchecked((int)0x80131530),
            COR_E_THREADINTERRUPTED = unchecked((int)0x80131519),
            COR_E_THREADSTART = unchecked((int)0x80131525),
            COR_E_THREADSTATE = unchecked((int)0x80131520),
            COR_E_TIMEOUT = unchecked((int)0x80131505),
            COR_E_TYPEACCESS = unchecked((int)0x80131543),
            COR_E_TYPEINITIALIZATION = unchecked((int)0x80131534),
            COR_E_TYPELOAD = unchecked((int)0x80131522),
            COR_E_TYPEUNLOADED = unchecked((int)0x80131013),
            COR_E_UNAUTHORIZEDACCESS = unchecked((int)0x80070005),
            COR_E_VERIFICATION = unchecked((int)0x8013150D),
            COR_E_WAITHANDLECANNOTBEOPENED = unchecked((int)0x8013152C),
            CO_E_NOTINITIALIZED = unchecked((int)0x800401F0),
            DISP_E_OVERFLOW = unchecked((int)0x8002000A),
            E_BOUNDS = unchecked((int)0x8000000B),
            E_CHANGED_STATE = unchecked((int)0x8000000C),
            E_FILENOTFOUND = unchecked((int)0x80070002),
            E_FAIL = unchecked((int)0x80004005),
            E_HANDLE = unchecked((int)0x80070006),
            E_INVALIDARG = unchecked((int)0x80070057),
            E_NOTIMPL = unchecked((int)0x80004001),
            E_POINTER = unchecked((int)0x80004003),
            ERROR_MRM_MAP_NOT_FOUND = unchecked((int)0x80073B1F),
            ERROR_TIMEOUT = unchecked((int)0x800705B4),
            RO_E_CLOSED = unchecked((int)0x80000013),
            RPC_E_CHANGED_MODE = unchecked((int)0x80010106),
            TYPE_E_TYPEMISMATCH = unchecked((int)0x80028CA0),
        }

        [Flags]
        internal enum EXCEPTION_FLAGS : uint
        {
            EXCEPTION_NONCONTINUABLE = 0x1,
            EXCEPTION_UNWINDING = 0x2,
            EXCEPTION_EXIT_UNWIND = 0x4, // Exit unwind is in progress (not used by PAL SEH)
            EXCEPTION_NESTED_CALL = 0x10, // Nested exception handler call
            EXCEPTION_TARGET_UNWIND = 0x20, // Target unwind in progress
            EXCEPTION_COLLIDED_UNWIND = 0x40, // Collided exception handler call
            EXCEPTION_SKIP_VEH = 0x200,
        }

        internal enum EXCEPTION_CODE : uint
        {
            EXCEPTION_ACCESS_VIOLATION = 0xC0000005,
            EXCEPTION_MSVC = 0xE06d7363, // a.k.a. Error "msc" in ASCII
            EXCEPTION_COMPLUS = 0xE0434352, // a.k.a. Error "CCR" in ASCII
            EXCEPTION_HIJACK = 0xE0434f4e, // a.k.a. Error "COM"+1 (ComPlus) in ASCII
        }

        /* <winnt.h>
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

        /* <winnt.h>
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
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15 /* EXCEPTION_MAXIMUM_PARAMETERS */)]
            public UIntPtr[] ExceptionInformation;
        }

// SA1201: A enum should not follow a method
// SA1602: Enumeration items should be documented
#pragma warning restore SA1201, SA1602
    }
}
