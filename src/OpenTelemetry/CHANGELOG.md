# Changelog

## Unreleased

* `ActivityProcessor` implements IDisposable.
  * When `Dispose` occurs, it calls `ShutdownAsync`.
* `BatchingActivityProcessor`/`SimpleActivityProcessor` is disposable and it
  disposes the containing exporter.
* `BroadcastActivityProcessor`is disposable and it disposes the processors.

## 0.3.0-beta

Released 2020-07-23

* Initial release
