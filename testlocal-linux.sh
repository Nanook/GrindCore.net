#!/bin/bash
set -e

## Publish the test project for linux-x64, Debug configuration, .NET 10.0, output to output/linux-x64
#dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r linux-x64 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/linux-x64 --self-contained false
#
## Copy native libraries to the output directory for easier loading
#cp -a ./output/linux-x64/runtimes/linux-x64/native/* ./output/linux-x64/
#
## Run the test executable
#chmod +x ./output/linux-x64/GrindCore.Tests.Runtime
#./output/linux-x64/GrindCore.Tests.Runtime

# Configurable via environment (sensible defaults)
TARGET_FRAMEWORK=${TARGET_FRAMEWORK:-net10.0}
RUNTIME=${RUNTIME:-linux-x64}
CONFIG=${CONFIG:-Release}
PROJECT_PATH=${PROJECT_PATH:-Tests/GrindCore.Tests/GrindCore.Tests.csproj}
DOCKER_IMAGE=${DOCKER_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}

# Get the absolute path of the current directory to mount in Docker
WORKSPACE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Running tests in Docker container..."
echo "Image: ${DOCKER_IMAGE}"
echo "Target: ${TARGET_FRAMEWORK}/${RUNTIME} (project: ${PROJECT_PATH})"
echo "Workspace: ${WORKSPACE_DIR}"

# Run dotnet test inside Docker container
# Mount the workspace as /workspace and set it as working directory
# Use --rm to auto-remove container after completion
# Use --interactive --tty for better output formatting if running interactively
docker run --rm \
  --volume "${WORKSPACE_DIR}:/workspace" \
  --workdir /workspace \
  --interactive --tty \
  "${DOCKER_IMAGE}" \
  dotnet test "${PROJECT_PATH}" \
    -c "${CONFIG}" \
    -r "${RUNTIME}" \
    -p:TargetFramework="${TARGET_FRAMEWORK}" \
    --logger "console;verbosity=detailed" #\
#    --logger "trx;LogFileName=results.trx" \
#    --blame --blame-crash \
#    --diag:dotnet-test-diag.txt

echo "Test run completed. Check TestResults/ for blame reports and crash dumps if any test crashed."