# Changelog

## Unreleased

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

* `AddRedisInstrumentation` extension will now resolve `IConnectionMultiplexer`
  & `StackExchangeRedisCallsInstrumentationOptions` through DI when
  OpenTelemetry.Extensions.Hosting is in use.
  ([#2110](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2110))

## 1.0.0-rc5

Released 2021-Jun-09

## 1.0.0-rc4

Released 2021-Apr-23

* Activities are now created with the `db.system` attribute set for usage
  during sampling. ([#1984](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1984))

## 1.0.0-rc3

Released 2021-Mar-19

## 1.0.0-rc2

Released 2021-Jan-29

## 1.0.0-rc1.1

Released 2020-Nov-17

## 0.8.0-beta.1

Released 2020-Nov-5

## 0.7.0-beta.1

Released 2020-Oct-16

* Span Status is populated as per new spec
  ([#1313](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1313))

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
