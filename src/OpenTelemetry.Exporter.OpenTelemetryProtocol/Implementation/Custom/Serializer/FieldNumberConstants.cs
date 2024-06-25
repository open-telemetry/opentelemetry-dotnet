// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.Serializer;

internal class FieldNumberConstants
{
    // Resource spans
#pragma warning disable SA1310 // Field names should not contain underscore
    internal const int ResourceSpans_resource = 1;
    internal const int ResourceSpans_scope_spans = 2;
    internal const int ResourceSpans_schema_url = 3;

    // Resource
    internal const int Resource_attributes = 1;

    // ScopeSpans
    internal const int ScopeSpans_scope = 1;
    internal const int ScopeSpans_span = 2;
    internal const int ScopeSpans_shema_url = 3;

    // Span
    internal const int Span_trace_id = 1;
    internal const int Span_span_id = 2;
    internal const int Span_trace_state = 3;
    internal const int Span_parent_span_id = 4;
    internal const int Span_name = 5;
    internal const int Span_kind = 6;
    internal const int Span_start_time_unix_nano = 7;
    internal const int Span_end_time_unix_nano = 8;
    internal const int Span_attributes = 9;
    internal const int Span_dropped_attributes_count = 10;
    internal const int Span_events = 11;
    internal const int Span_dropped_events_count = 12;
    internal const int Span_links = 13;
    internal const int Span_dropped_links_count = 14;
    internal const int Span_status = 15;
    internal const int Span_flags = 16;

    // SpanKind
    internal const int SpanKind_internal = 2;
    internal const int SpanKind_server = 3;
    internal const int SpanKind_client = 4;
    internal const int SpanKind_producer = 5;
    internal const int SpanKind_consumer = 6;

    // Events
    internal const int Event_time_unix_nano = 1;
    internal const int Event_name = 2;
    internal const int Event_attributes = 3;
    internal const int Event_dropped_attributes_count = 4;

    // Links
    internal const int Link_trace_id = 1;
    internal const int Link_span_id = 2;
    internal const int Link_trace_state = 3;
    internal const int Link_attributes = 4;
    internal const int Link_dropped_attributes_count = 5;
    internal const int Link_flags = 6;

    // Status
    internal const int Status_message = 2;
    internal const int Status_code = 3;

    // StatusCode
    internal const int StatusCode_unset = 0;
    internal const int StatusCode_ok = 1;
    internal const int StatusCode_error = 2;

    // InstrumentationScope
    internal const int InstrumentationScope_name = 1;
    internal const int InstrumentationScope_version = 2;

    // KeyValue
    internal const int KeyValue_key = 1;
    internal const int KeyValue_value = 2;

    // AnyValue
    internal const int AnyValue_string_value = 1;
    internal const int AnyValue_bool_value = 2;
    internal const int AnyValue_int_value = 3;
    internal const int AnyValue_double_value = 4;
    internal const int AnyValue_array_value = 5;
    internal const int AnyValue_kvlist_value = 6;
    internal const int AnyValue_bytes_value = 7;

    internal const int ArrayValue_Value = 1;
#pragma warning restore SA1310 // Field names should not contain underscore
}

