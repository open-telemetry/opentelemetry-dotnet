# OpenTelemetry HTTP Server Demo

## Scenario

The _OpenTelemetry HTTP Server_ demo shows how a library developer X could use
metrics API to instrument a library, and how the application developer Y can
configure the library to use OpenTelemetry SDK in a final application. X and Y
are working for different companies and they don't communicate. The demo has two
parts - the library `io.opentelemetry.example.httpserver.instrumented` (owned by
X) and the server app `io.opentelemetry.example.httpserver.application` (owned
by Y):

* How developer X could instrument the library code in a vendor agnostic way
  * Performance is critical for X
  * X doesn't know which time series and which dimension will Y pick
  * X doesn't know the aggregation time window, nor the final destination of the
    metrics
* How developer Y could configure the SDK and exporter
  * How should Y hook up the metrics SDK with the library
  * How should Y configure the time window(s) and destination(s)
  * How should Y pick the time series and the dimensions

### Library Requirements

The library developer (developer X) will expose the following metrics out of
box:

#### Process CPU Usage

| Host Name | Process ID | CPU% [0.0, 100.0] |
| --------- | ---------- | ----------------- |
| MachineA  | 1234       | 15.3              |

#### System CPU Usage

| Host Name | CPU% [0, 100] |
| --------- | ------------- |
| MachineA  | 30            |

#### Server Room Temperature

| Host Name | Temperature (F) |
| --------- | --------------- |
| MachineA  | 65.3            |

#### HTTP Server Duration

| Host Name | Process ID | HTTP Method | HTTP Host | HTTP Status Code | HTTP Flavor | Peer IP   | Peer Port | Host IP   | Host Port | Duration (ms) |
| --------- | ---------- | ----------- | --------- | ---------------- | ----------- | --------- | --------- | --------- | --------- | ------------- |
| MachineA  | 1234       | GET         | otel.org  | 200              | 1.1         | 127.0.0.1 | 51327     | 127.0.0.1 | 80        | 8.5           |
| MachineA  | 1234       | POST        | otel.org  | 304              | 1.1         | 127.0.0.1 | 51328     | 127.0.0.1 | 80        | 100.0         |

### Application Requirements

The application owner (developer Y) would only want the following metrics:

* [System CPU Usage](#system-cpu-usage) reported every 5 seconds
* [Server Room Temperature](#server-room-temperature) reported every minute
* [HTTP Server Duration](#http-server-duration), reported every 5 seconds, with
  a subset of the dimensions:
  * Host Name
  * HTTP Method
  * HTTP Host
  * HTTP Status Code
  * 90%, 95%, 99% and 99.9% latency
* HTTP request counters, reported every 5 seconds:
  * Total number of received HTTP requests
  * Total number of finished HTTP requests
  * Number of ongoing HTTP requests (concurrent HTTP requests)

  | Host Name | Process ID | HTTP Host | Received Requests | Finished Requests | Concurrent Requests |
  | --------- | ---------- | --------- | ----------------- | ----------------- | ------------------- |
  | MachineA  | 1234       | otel.org  | 630               | 601               | 29                  |
  | MachineA  | 5678       | otel.org  | 1005              | 1001              | 4                   |
* Exception samples (examplar) - in case HTTP 5xx happened, developer Y would
  want to see a sample request with all the dimensions (IP, Port, etc.)

## How to run
