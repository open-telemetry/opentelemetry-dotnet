// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines field number constants for fields defined in
/// <see href="https://github.com/open-telemetry/opentelemetry-proto/blob/v1.2.0/opentelemetry/proto/trace/v1/trace.proto"/>.
/// </summary>
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "These names describe the nested names and properties in the .Proto file.")]
internal static class ProtobufOtlpTraceFieldNumberConstants
{
    // Traces data
    internal const int TracesData_Resource_Spans = 1;

    // Resource spans
    internal const int ResourceSpans_Resource = 1;
    internal const int ResourceSpans_Scope_Spans = 2;
    internal const int ResourceSpans_Schema_Url = 3;

    // Resource
    internal const int Resource_Attributes = 1;

    // ScopeSpans
    internal const int ScopeSpans_Scope = 1;
    internal const int ScopeSpans_Span = 2;
    internal const int ScopeSpans_Schema_Url = 3;

    // Span
    internal const int Span_Trace_Id = 1;
    internal const int Span_Span_Id = 2;
    internal const int Span_Trace_State = 3;
    internal const int Span_Parent_Span_Id = 4;
    internal const int Span_Name = 5;
    internal const int Span_Kind = 6;
    internal const int Span_Start_Time_Unix_Nano = 7;
    internal const int Span_End_Time_Unix_Nano = 8;
    internal const int Span_Attributes = 9;
    internal const int Span_Dropped_Attributes_Count = 10;
    internal const int Span_Events = 11;
    internal const int Span_Dropped_Events_Count = 12;
    internal const int Span_Links = 13;
    internal const int Span_Dropped_Links_Count = 14;
    internal const int Span_Status = 15;
    internal const int Span_Flags = 16;

    // SpanKind
    internal const int SpanKind_Internal = 2;
    internal const int SpanKind_Server = 3;
    internal const int SpanKind_Client = 4;
    internal const int SpanKind_Producer = 5;
    internal const int SpanKind_Consumer = 6;

    // Events
    internal const int Event_Time_Unix_Nano = 1;
    internal const int Event_Name = 2;
    internal const int Event_Attributes = 3;
    internal const int Event_Dropped_Attributes_Count = 4;

    // Links
    internal const int Link_Trace_Id = 1;
    internal const int Link_Span_Id = 2;
    internal const int Link_Trace_State = 3;
    internal const int Link_Attributes = 4;
    internal const int Link_Dropped_Attributes_Count = 5;
    internal const int Link_Flags = 6;

    // Status
    internal const int Status_Message = 2;
    internal const int Status_Code = 3;

    // StatusCode
    internal const int StatusCode_Unset = 0;
    internal const int StatusCode_Ok = 1;
    internal const int StatusCode_Error = 2;
}

