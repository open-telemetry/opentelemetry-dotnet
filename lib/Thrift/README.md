# Thrift Protocol .Net Implementation

This is the .net implementation of the Apache Thrift protocol. This code was forked from the  [Jaeger Tracing C# Client Repository](https://github.com/jaegertracing/jaeger-client-csharp).

Path: src/Thrift

commitID: 0794ea71cb6e58f7bf0f0ef2c0c8ceceb1d8b6d9

The following changes were made to this fork:

* ConfigureAwait(false) added to async calls to prevent deadlocks.
* THttpClientTransport uses WebRequestHandler() in .NET Framework 4.6 builds.