# OpenTelemetry - Jaeger Exporter - Apache Thrift

This folder contains a stripped-down and customized fork of the [ApacheThrift
0.13.0.1](https://www.nuget.org/packages/ApacheThrift/0.13.0.1) library from the
[apache/thrift](https://github.com/apache/thrift/tree/0.13.0) repo. Only the
client bits we need to transmit spans to Jaeger using the compact Thrift
protocol over UDP and binary Thrift over HTTP are included. Further
customizations have been made to improve the performance of our specific use
cases.
