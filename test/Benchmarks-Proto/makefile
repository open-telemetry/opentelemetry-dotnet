all: extract040 extract050 extract060 extract070 extract080

extract080:
	rm -r -f zip
	unzip -x protos/opentelemetry-proto-0.8.0.zip -d zip
	rm -r -f opentelemetry080
	mv zip/opentelemetry-proto-0.8.0/opentelemetry opentelemetry080
	rm -r -f zip
	find opentelemetry080 | grep \\.proto | xargs sed -i 's/opentelemetry/opentelemetry080/g'
	sed -i 's/string equals/string equalszz/g' opentelemetry080/proto/metrics/experimental/metrics_config_service.proto

extract070:
	rm -r -f zip
	unzip -x protos/opentelemetry-proto-0.7.0.zip -d zip
	rm -r -f opentelemetry070
	mv zip/opentelemetry-proto-0.7.0/opentelemetry opentelemetry070
	rm -r -f zip
	find opentelemetry070 | grep \\.proto | xargs sed -i 's/opentelemetry/opentelemetry070/g'
	sed -i 's/string equals/string equalszz/g' opentelemetry070/proto/metrics/experimental/configservice.proto

extract060:
	rm -r -f zip
	unzip -x protos/opentelemetry-proto-0.6.0.zip -d zip
	rm -r -f opentelemetry060
	mv zip/opentelemetry-proto-0.6.0/opentelemetry opentelemetry060
	rm -r -f zip
	find opentelemetry060 | grep \\.proto | xargs sed -i 's/opentelemetry/opentelemetry060/g'
	sed -i 's/string equals/string equalszz/g' opentelemetry060/proto/metrics/experimental/configservice.proto

extract050:
	rm -r -f zip
	unzip -x protos/opentelemetry-proto-0.5.0.zip -d zip
	rm -r -f opentelemetry050
	mv zip/opentelemetry-proto-0.5.0/opentelemetry opentelemetry050
	rm -r -f zip
	find opentelemetry050 | grep \\.proto | xargs sed -i 's/opentelemetry/opentelemetry050/g'
	sed -i 's/string equals/string equalszz/g' opentelemetry050/proto/metrics/experimental/configservice.proto

extract040:
	rm -r -f zip
	unzip -x protos/opentelemetry-proto-0.4.0.zip -d zip
	rm -r -f opentelemetry040
	mv zip/opentelemetry-proto-0.4.0/opentelemetry opentelemetry040
	rm -r -f zip
	find opentelemetry040 | grep \\.proto | xargs sed -i 's/opentelemetry/opentelemetry040/g'

clean:
	rm -r -f zip
	rm -r -f opentelemetry040
	rm -r -f opentelemetry050
	rm -r -f opentelemetry060
	rm -r -f opentelemetry070
	rm -r -f opentelemetry080
