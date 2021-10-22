
$SCRIPT_DIR=$PSScriptRoot
$ROOT_DIR="${SCRIPT_DIR}/../../"

# freeze the spec & generator tools versions to make SemanticAttributes generation reproducible
$SPEC_VERSION="v1.7.0"
$SCHEMA_URL="https://opentelemetry.io/schemas/$SPEC_VERSION"
$GENERATOR_VERSION="0.8.0"

# install the dotnet-format tool
Write-Host "Restoring dotnet-format tool"
dotnet tool restore

Set-Location $SCRIPT_DIR

Remove-Item -r -fo opentelemetry-specification
mkdir opentelemetry-specification
Set-Location opentelemetry-specification

git init
git remote add origin https://github.com/open-telemetry/opentelemetry-specification.git
git fetch origin $SPEC_VERSION
git reset --hard FETCH_HEAD
Set-Location ${SCRIPT_DIR}

docker run --rm `
  -v ${SCRIPT_DIR}/opentelemetry-specification/semantic_conventions/trace:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}/Trace:/output `
  otel/semconvgen:$GENERATOR_VERSION `
  -f /source code `
  --template /templates/SemanticConventions.cs.j2 `
  --output /output/TraceSemanticConventions.cs `
  -D class=TraceSemanticConventions `
  -D schemaUrl=$SCHEMA_URL `
  -D pkg=OpenTelemetry.Trace

docker run --rm `
  -v ${SCRIPT_DIR}/opentelemetry-specification/semantic_conventions/resource:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}/Resource:/output `
  otel/semconvgen:$GENERATOR_VERSION `
  -f /source code `
  --template /templates/SemanticConventions.cs.j2 `
  --output /output/ResourceSemanticConventions.cs `
  -D class=ResourceSemanticConventions `
  -D schemaUrl=$SCHEMA_URL `
  -D pkg=OpenTelemetry.Resources

Set-Location ${ROOT_DIR}

Write-Host "Running dotnet-format on the generated files"
dotnet format -w -s warn
