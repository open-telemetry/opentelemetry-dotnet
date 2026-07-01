// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/// <summary>
/// Defines field number constants for fields defined in
/// <see href="https://github.com/open-telemetry/opentelemetry-proto/blob/v1.2.0/opentelemetry/proto/common/v1/common.proto"/>.
/// </summary>
internal static class ProtobufOtlpCommonFieldNumberConstants
{
    // InstrumentationScope
    internal const int InstrumentationScope_Name = 1;
    internal const int InstrumentationScope_Version = 2;
    internal const int InstrumentationScope_Attributes = 3;
    internal const int InstrumentationScope_Dropped_Attributes_Count = 4;

    // KeyValue
    internal const int KeyValue_Key = 1;
    internal const int KeyValue_Value = 2;

    // AnyValue
    internal const int AnyValue_String_Value = 1;
    internal const int AnyValue_Bool_Value = 2;
    internal const int AnyValue_Int_Value = 3;
    internal const int AnyValue_Double_Value = 4;
    internal const int AnyValue_Array_Value = 5;
    internal const int AnyValue_Kvlist_Value = 6;
    internal const int AnyValue_Bytes_Value = 7;

    internal const int ArrayValue_Value = 1;

    internal const int KeyValueList_Values = 1;
}
