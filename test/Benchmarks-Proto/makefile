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

strip:
	cp opentelemetry040/proto/metrics/v1/metrics.proto protov040.txt
	sed -i 's://.*::g' protov040.txt
	sed -i 's:import.*::g' protov040.txt
	sed -i 's:option.*::g' protov040.txt
	sed -i 's:^[ ]*$$::g' protov040.txt
	sed -i '/^$$/d' protov040.txt

	cp opentelemetry050/proto/metrics/v1/metrics.proto protov050.txt
	sed -i 's://.*::g' protov050.txt
	sed -i 's:import.*::g' protov050.txt
	sed -i 's:option.*::g' protov050.txt
	sed -i 's:^[ ]*$$::g' protov050.txt
	sed -i '/^$$/d' protov050.txt

	cp opentelemetry060/proto/metrics/v1/metrics.proto protov060.txt
	sed -i 's://.*::g' protov060.txt
	sed -i 's:import.*::g' protov060.txt
	sed -i 's:option.*::g' protov060.txt
	sed -i 's:^[ ]*$$::g' protov060.txt
	sed -i '/^$$/d' protov060.txt

	cp opentelemetry070/proto/metrics/v1/metrics.proto protov070.txt
	sed -i 's://.*::g' protov070.txt
	sed -i 's:import.*::g' protov070.txt
	sed -i 's:option.*::g' protov070.txt
	sed -i 's:^[ ]*$$::g' protov070.txt
	sed -i '/^$$/d' protov070.txt

	cp opentelemetry080/proto/metrics/v1/metrics.proto protov080.txt
	sed -i 's://.*::g' protov080.txt
	sed -i 's:import.*::g' protov080.txt
	sed -i 's:option.*::g' protov080.txt
	sed -i 's:^[ ]*$$::g' protov080.txt
	sed -i '/^$$/d' protov080.txt
