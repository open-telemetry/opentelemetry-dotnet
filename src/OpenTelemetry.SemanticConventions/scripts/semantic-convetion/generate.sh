#!/bin/bash

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="${SCRIPT_DIR}/../../"

# freeze the spec & generator tools versions to make SemanticAttributes generation reproducible
SPEC_VERSION=v1.7.0
SCHEMA_URL=https://opentelemetry.io/schemas/$SPEC_VERSION
GENERATOR_VERSION=0.8.0

# install the dotnet-format tool
echo "Restoring dotnet-format tool"
dotnet tool restore

cd ${SCRIPT_DIR}

rm -rf opentelemetry-specification || true
mkdir opentelemetry-specification
cd opentelemetry-specification

git init
git remote add origin https://github.com/open-telemetry/opentelemetry-specification.git
git fetch origin "$SPEC_VERSION"
git reset --hard FETCH_HEAD
cd ${SCRIPT_DIR}

docker run --rm \
  -v ${SCRIPT_DIR}/opentelemetry-specification/semantic_conventions/trace:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/Trace:/output \
  otel/semconvgen:$GENERATOR_VERSION \
  -f /source code \
  --template /templates/SemanticConventions.cs.j2 \
  --output /output/TraceSemanticConventions.cs \
  -Dclass=TraceSemanticConventions \
  -DschemaUrl=$SCHEMA_URL \
  -Dpkg=OpenTelemetry.Trace

docker run --rm \
  -v ${SCRIPT_DIR}/opentelemetry-specification/semantic_conventions/resource:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/Resource:/output \
  otel/semconvgen:$GENERATOR_VERSION \
  -f /source code \
  --template /templates/SemanticConventions.cs.j2 \
  --output /output/ResourceSemanticConventions.cs \
  -Dclass=ResourceSemanticConventions \
  -DschemaUrl=$SCHEMA_URL \
  -Dpkg=OpenTelemetry.Resources
  
cd ${ROOT_DIR}

echo "Running dotnet-format on the generated files"
dotnet format -a warn
dotnet format -w -s warn
