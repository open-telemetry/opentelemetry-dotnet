# OpenTelemetry Example Application

This project is an example of ASP.NET Core instrumentation for tracing and logging.

The following exporters are configured for viewing the traces:
* Console
* Zipkin
* Otlp
* Jaeger

The following exporters are configured for viewing the metrics:
* Console
* Otlp
* Prometheus

## Running the example

Running instances of the following services are required to view exports:
* Zipkin
* OTEL Collector
* Jaeger
* Prometheus

These can easily be spun up in docker containers.

The project can be run from this directory as follows:

```shell
dotnet run
```

Instead of running the project and its dependencies individually, if you are using Docker Desktop,
a `docker-compose` file is provided. This makes standing up the Zipkin, Otlp, Jaeger, and Prometheus dependencies easy, as well as starting the application.

To run the example using `docker-compose`, run the following from this
directory:

```shell
docker-compose up --build
```

With everything running:

* [Invoke the API](http://localhost:5000/WeatherForecast) to create new data.
* If you have run Zipkin with default settings:
  * View your traces with Zipkin [here](http://localhost:9411/zipkin)
* If you have run OTEL Collector with default settings:
  * View your traces and metrics by checking the logs. If running through `docker-compose`, you can execute `docker-compose logs otlp` to view the logs.
* If you have run Jaeger with default settings:
  * View your traces with Jaeger [here](http://localhost:16686/)
* If you have run Prometheus with default settings:
  * View your metrics with Prometheus [here](http://localhost:9090/graph)

## References

* [Docker Desktop](https://www.docker.com/products/docker-desktop)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [Zipkin](https://zipkin.io)
* [OTEL Collector](https://opentelemetry.io/docs/collector/getting-started/#docker)
* [Jaeger](https://jaegertracing.io/)
* [Prometheus](https://prometheus.io/)
