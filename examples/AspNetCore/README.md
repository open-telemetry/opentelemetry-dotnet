# OpenTelemetry Example Application

This project is an example of ASP.NET Core instrumentation for tracing and
logging.

The following exporters are configured for viewing the traces:

* Console
* Jaeger
* Otlp
* Zipkin

The following exporters are configured for viewing the metrics:

* Console
* Otlp
* Prometheus

## Running the example

Running instances of the following services are required to view the exported
data:

* Jaeger
* OTel Collector
* Prometheus
* Zipkin

These can easily be spun up in docker containers.

The project can be run from this directory as follows:

```shell
dotnet run
```

Instead of running the project and its dependencies individually, if you are
using Docker Desktop, a `docker-compose` file is provided. This makes standing
up the Jaeger, Otlp, Prometheus, and Zipkin dependencies easy, as well as
starting the application.

To run the example using `docker-compose`, run the following from this
directory:

```shell
docker-compose up --build
```

With everything running:

* [Invoke the API](http://localhost:5000/WeatherForecast) to create new data.
* If you have run Jaeger with default settings:
  * View your traces with Jaeger by accessing the local endpoint
  [http://localhost:16686/](http://localhost:16686/).
* If you have run OTel Collector with default settings:
  * View your traces and metrics by checking the logs. If running through
  `docker-compose`, you can execute `docker-compose logs otlp` to view the logs.
* If you have run Prometheus with default settings:
  * View your metrics with Prometheus by accessing the local endpoint
  [http://localhost:9090/graph](http://localhost:9090/graph).
* If you have run Zipkin with default settings:
  * View your traces with Zipkin by accessing the local endpoint
  [http://localhost:9411/zipkin](http://localhost:9411/zipkin).

## References

* [Docker Desktop](https://www.docker.com/products/docker-desktop)
* [Jaeger](https://jaegertracing.io/)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [OTel Collector](https://opentelemetry.io/docs/collector/getting-started/#docker)
* [Prometheus](https://prometheus.io/)
* [Zipkin](https://zipkin.io)
