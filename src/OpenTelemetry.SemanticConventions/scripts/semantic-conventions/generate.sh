#!/bin/bash

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="${SCRIPT_DIR}/../../"

# freeze the spec & generator tools versions to make SemanticAttributes generation reproducible
SPEC_VERSION=v1.23.1
SPEC_VERSION_ESCAPED=v1_23_1_Experimental
SCHEMA_URL=https://opentelemetry.io/schemas/$SPEC_VERSION
GENERATOR_VERSION=0.23.0

cd ${SCRIPT_DIR}

rm -rf semantic-conventions || true
mkdir semantic-conventions
cd semantic-conventions

git init
git remote add origin https://github.com/open-telemetry/semantic-conventions.git
git fetch origin "$SPEC_VERSION"
git reset --hard FETCH_HEAD
cd ${SCRIPT_DIR}

# stable attributes
docker run --rm \
  -v ${SCRIPT_DIR}/semantic_conventions/model:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/SemanticConventions:/output \
  otel/semconvgen:$GENERATOR_VERSION \
  --yaml-root /source code \
  --template /templates/Attributes.cs.j2 \
  --output /output/Attributes.cs \
  --trim-whitespace \
  --file-per-group root_namespace \
  -Dfilter=is_stable \
  -Dpkg=OpenTelemetry.SemanticConventions

#experimental attributes
docker run --rm \
  -v ${SCRIPT_DIR}/semantic_conventions/model:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/SemanticConventions/${SPEC_VERSION_ESCAPED}:/output \
  otel/semconvgen:$GENERATOR_VERSION \
  --yaml-root /source code \
  --template /templates/Attributes.cs.j2 \
  --output /output/Attributes.cs \
  --trim-whitespace \
  --file-per-group root_namespace \
  -Dfilter=is_experimental \
  -Dpkg=OpenTelemetry.SemanticConventions.${SPEC_VERSION_ESCAPED}

#experimental metrics
docker run --rm \
  -v ${SCRIPT_DIR}/semantic-conventions/model:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/SemanticConventions/${SPEC_VERSION_ESCAPED}:/output \
  semconvgen:$GENERATOR_VERSION \
  --yaml-root /source code \
  --template /templates/Metrics.cs.j2 \
  --output /output/Metrics.cs \
  --trim-whitespace \
  --file-per-group root_namespace \
  -D filter=is_experimental \
  -D pkg=OpenTelemetry.SemanticConventions.${SPEC_VERSION_ESCAPED}