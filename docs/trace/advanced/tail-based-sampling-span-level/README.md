# TailBasedSampling at a span level using OpenTelemetry .NET SDK - Example

This is a proof of concept for how we can achieve tail-based sampling at a
span level using the extensibility mechanisms in OpenTelemetry.NET. This
is a way to achieve a combination of head-based sampling (based on
probabilistic sampling) and a way to get all failure spans (non-probabilistic
sampling: e.g., based on failure spans).

You should see the following output on the Console when you run this
application.

```text
Including error span with id 00-404ddff248b8f9a9b21e347d68d2640e-035858bc3c168885-01 and status Error
Activity.TraceId:            404ddff248b8f9a9b21e347d68d2640e
Activity.SpanId:             035858bc3c168885
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.5563112Z
Activity.Duration:           00:00:00.0028144
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-ea861bda268c58d328ab7cbe49851499-daba29055de80a53-00 and status Ok
Dropping span with id 00-7b77ed327ead0a572a15489bb4634e13-d11c04162d37048c-00 and status Ok
Dropping span with id 00-3d68f5b95f188649425c9f98e1b4c488-24cee8bf54a45ce2-00 and status Ok
Dropping span with id 00-4aaaaec409f3b9936e088610507a8744-181b9c6bc5ecdf96-00 and status Ok
Dropping span with id 00-8b5b9df8b1ee260acae5b97ac7fd00cc-bd019eb320019d4f-00 and status Ok
Dropping span with id 00-d85faf450c591dd428799282455e9e38-609ce2f98f13e6a3-00 and status Ok
Dropping span with id 00-7075dd1671056fd0f32b8adebebe9a98-b5f9cdd92c48da7a-00 and status Ok
Dropping span with id 00-710a9c042ad92b9a7451bd2a16a80b18-36c115e8508cc13a-00 and status Ok
Dropping span with id 00-aa1369193141d437708113be3b7a5267-c99a3dc66b7f54c6-00 and status Ok
Dropping span with id 00-ac4ab0200826dadc4525d4bca824f2f1-8283b9092d03b55f-00 and status Ok
Dropping span with id 00-3c2b5f2d3c4d174c68553c1db09c897e-6b2e27536812087e-00 and status Ok
Dropping span with id 00-4cf582ab75290c354e093e41a8de69dc-03cea62bb14d6a68-00 and status Ok
Dropping span with id 00-21f4bfb81394c4af7d039e656c6c1047-9a9fe61152c9ed4d-00 and status Ok
Dropping span with id 00-eedc5a82f68b9296275c8082ebd64ab8-448f367bbc0720a7-00 and status Ok
Including error span with id 00-802dea991247e2d699d943167eb546de-cc120b0bd1741b52-01 and status Error
Activity.TraceId:            802dea991247e2d699d943167eb546de
Activity.SpanId:             cc120b0bd1741b52
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.7021138Z
Activity.Duration:           00:00:00.0000012
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-1f7d3993e4b3f1d7b2e51144796d96f0-37044627a416d927-00 and status Ok
Dropping span with id 00-b1e2ca9a91ef2ce05ce97070e1c991bd-57e320eb4039cdd1-00 and status Ok
Dropping span with id 00-2488e81e56049bce9cf230b31eaa3d76-bba08ec42a2fb279-00 and status Ok
Dropping span with id 00-1402d0d7bae714fd53a3b8a529f88ca0-bd96d9d33fa181bf-00 and status Ok
Dropping span with id 00-e8658be53abd8b5a20e87d0984442bd7-812ed71cfaef4cf3-00 and status Ok
Including error span with id 00-7f11b2538b72223c86d59b0e2862d729-50b25eda420fda64-01 and status Error
Activity.TraceId:            7f11b2538b72223c86d59b0e2862d729
Activity.SpanId:             50b25eda420fda64
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.7041368Z
Activity.Duration:           00:00:00.0000013
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-1fe7e149c0e9c28ed00135a77bd37248-ff00d7374d33488a-00 and status Ok
Dropping span with id 00-0d3399d5cbf87741cfda2090f36f30f0-fd8064def1d5c52c-00 and status Ok
Dropping span with id 00-6770fac020fc66b3b18a876303e980fa-29c8ba6bad22d287-00 and status Ok
Dropping span with id 00-12507e79eac2060164fc9a98bc587efb-1e12be37dcaa80d4-00 and status Ok
Dropping span with id 00-d493549cef3b209a914acbb90090e881-369a73de2ceca24f-00 and status Ok
Including error span with id 00-898839d12f75cad5074c153b4112c165-9efc427231347edb-01 and status Error
Activity.TraceId:            898839d12f75cad5074c153b4112c165
Activity.SpanId:             9efc427231347edb
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.7340078Z
Activity.Duration:           00:00:00.0000026
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-1c4f482ab49f96776bb1e84393f98522-a5521810ec4943c9-00 and status Ok
Dropping span with id 00-5cd7042bc2c8234298f690028932b1f8-dd490430c2937004-00 and status Ok
Dropping span with id 00-3fc032f4085c144a5f0ded6d8f3873c2-f761003fc135f7d8-00 and status Ok
Dropping span with id 00-1b99d91296f09ddf804c020e106f7bce-165394a774803ebb-00 and status Ok
Including error span with id 00-49fc4528291ea45ca5f715a90463d301-398bebd3d5456893-01 and status Error
Activity.TraceId:            49fc4528291ea45ca5f715a90463d301
Activity.SpanId:             398bebd3d5456893
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.7576053Z
Activity.Duration:           00:00:00.0000014
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Including head-sampled span with id 00-0cb8c93c5d6fc38637884bdde35268e7-0bf8ad087721f4c0-01 and status Error
Activity.TraceId:            0cb8c93c5d6fc38637884bdde35268e7
Activity.SpanId:             0bf8ad087721f4c0
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.7766409Z
Activity.Duration:           00:00:00.0000029
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-8810ed43d55cb878e6695e3a6f5734b4-977678a92d0f5b3e-00 and status Ok
Dropping span with id 00-5f8e80f79c78e7d69ff22b64cc92660d-f9e983b360f77b28-00 and status Ok
Dropping span with id 00-3d84c1c1dab3e628e64debe066864136-a0a5c47c2e0f522b-00 and status Ok
Dropping span with id 00-26622865e95aaa9c0901b538f2dee7d6-d7765d3f5a207e4c-00 and status Ok
Dropping span with id 00-9c1d741c7e322061d09bc8b6f6157102-a8698160579c35b3-00 and status Ok
Dropping span with id 00-c02d2bd616dd7f9cb98530984109c4cd-31fe2bafbdf682df-00 and status Ok
Dropping span with id 00-9c65c86e02704d2d052682e42c43060b-6f7631dace1d89c9-00 and status Ok
Dropping span with id 00-996f5952d677d433ac1695f66df4787a-4ff9d1d42843f1c5-00 and status Ok
Dropping span with id 00-a4c5ac589a2d892dff26a5e1752ec0cb-cc99e9f4e863c341-00 and status Ok
Including error span with id 00-1ebb0ae6a78a621adf264f8b1aff571a-ddd7c3a494d8acb4-01 and status Error
Activity.TraceId:            1ebb0ae6a78a621adf264f8b1aff571a
Activity.SpanId:             ddd7c3a494d8acb4
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.8258199Z
Activity.Duration:           00:00:00.0000048
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-bc4bccb79eca649679dd6a1e8a4ab8ab-4586c42c5db4348a-00 and status Ok
Including head-sampled span with id 00-f3c88010615e285c8f3cb3e2bcd70c7f-f9316215f12437c3-01 and status Ok
Activity.TraceId:            f3c88010615e285c8f3cb3e2bcd70c7f
Activity.SpanId:             f9316215f12437c3
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.8519346Z
Activity.Duration:           00:00:00.0000034
Activity.Tags:
    foo: bar
StatusCode: Ok
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-a7ef3fcb54b88bc3a907a7af455213c0-ccf8c8d4b7e540e8-00 and status Ok
Including head-sampled span with id 00-07482e906dc184722f0317682a9af002-50628bdc0d933103-01 and status Ok
Activity.TraceId:            07482e906dc184722f0317682a9af002
Activity.SpanId:             50628bdc0d933103
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.8824318Z
Activity.Duration:           00:00:00.0000030
Activity.Tags:
    foo: bar
StatusCode: Ok
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-0dfa6003eb78f70b7c0f98df6cd346a7-ea4ba454584cd24b-00 and status Ok
Dropping span with id 00-14ae4bc2ad8d7d9e51b4cd1cc34c2574-6811fcaebbfc473b-00 and status Ok
```
