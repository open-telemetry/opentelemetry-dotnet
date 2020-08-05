# Changelog

## Unreleased

* Renaming `BroadcastActivityProcessor` to `FanOutActivityProcessor` (#1015)
* Introduce `SuppressInstrumentationScope` API (#988).
* `ActivityProcessor` implements `IDisposable`.
  * When `Dispose` occurs, it calls `ShutdownAsync`.
  * If you want a custom behavior for dispose, you will have to override the
    `Dispose(bool disposing)`.
* `BatchingActivityProcessor`/`SimpleActivityProcessor` is disposable and it
  disposes the containing exporter.
* `BroadcastActivityProcessor`is disposable and it disposes the processors.
* Samplers now get the actual TraceId of the Activity to be created.
* Default Sampler changed from AlwaysOn to ParentOrElse(AlwaysOn) to match the
  spec.

## 0.3.0-beta

Released 2020-07-23

* Initial release
