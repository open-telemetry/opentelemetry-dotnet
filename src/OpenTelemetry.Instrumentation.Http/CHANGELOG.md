# Changelog

## Unreleased

* Removed `AddHttpInstrumentation` and `AddHttpWebRequestInstrumentation` (.NET
  Framework) `TracerProviderBuilderExtensions`. `AddHttpClientInstrumentation`
  will now register `HttpClient` instrumentation on .NET Core and `HttpClient` +
  `HttpWebRequest` instrumentation on .NET Framework.

## 0.3.0-beta

Released 2020-07-23

* Initial release
