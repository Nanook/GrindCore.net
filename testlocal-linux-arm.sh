#!/usr/bin/env bash
set -euo pipefail

# Script to publish and run the tests for linux-arm (32-bit) using Docker/WSL.
# Usage: ./testlocal-linux-arm.sh
# Environment variables to override defaults:
#  TARGET_FRAMEWORK  - default: net10.0
#  RUNTIME           - default: linux-arm
#  CONFIG            - default: Release
#  PROJECT_PATH      - default: tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj
#  DOCKER_IMAGE      - default: mcr.microsoft.com/dotnet/sdk:10.0
#  DOCKER_PLATFORM   - default: linux/arm/v7
#  ENABLE_CORE_DUMPS - default: false (set to "true" to enable ulimit -c unlimited inside container)

TARGET_FRAMEWORK=${TARGET_FRAMEWORK:-net10.0}
RUNTIME=${RUNTIME:-linux-arm}
CONFIG=${CONFIG:-Release}
PROJECT_PATH=${PROJECT_PATH:-tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj}
DOCKER_IMAGE=${DOCKER_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}
DOCKER_PLATFORM=${DOCKER_PLATFORM:-linux/arm/v7}
ENABLE_CORE_DUMPS=${ENABLE_CORE_DUMPS:-false}

# Absolute path of this script directory (workspace to mount into container)
WORKSPACE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Running linux-arm tests in Docker (WSL)..."
echo "Image: ${DOCKER_IMAGE} (${DOCKER_PLATFORM})"
echo "Target: ${TARGET_FRAMEWORK}/${RUNTIME} (project: ${PROJECT_PATH})"
echo "Workspace: ${WORKSPACE_DIR}"

# Create output directory locally so publish writes into mounted volume with correct ownership
mkdir -p "${WORKSPACE_DIR}/output/${RUNTIME}"

# Compose the commands to run inside the container
INNER_CMD="set -euo pipefail; \
  echo \"Publishing project ${PROJECT_PATH} for ${RUNTIME} (${CONFIG})...\"; \
  dotnet publish \"${PROJECT_PATH}\" -c ${CONFIG} -r ${RUNTIME} -p:TargetFramework=${TARGET_FRAMEWORK} -p:TargetFrameworks=${TARGET_FRAMEWORK} -o ./output/${RUNTIME} --self-contained false; \
  echo \"Copying native runtimes into output dir (if present)...\"; \
  cp -a ./output/${RUNTIME}/runtimes/${RUNTIME}/native/* ./output/${RUNTIME}/ 2>/dev/null || true; \
  rm -rf ./output/${RUNTIME}/runtimes || true; \
  ls -al ./output/${RUNTIME}; \
  chmod +x ./output/${RUNTIME}/GrindCore.Tests.Runtime 2>/dev/null || true;"

if [ "${ENABLE_CORE_DUMPS}" = "true" ]; then
  # Enable core dumps inside container and set pattern to /tmp/core.%e.%p
  INNER_CMD="ulimit -c unlimited; echo \"/tmp/core.%e.%p\" > /proc/sys/kernel/core_pattern; ${INNER_CMD}"
fi

# Run the publish + run inside an ARM container. The workspace is mounted so build outputs are available on host.
docker run --rm \
  --platform "${DOCKER_PLATFORM}" \
  --volume "${WORKSPACE_DIR}:/workspace" \
  --workdir /workspace \
  --interactive --tty \
  "${DOCKER_IMAGE}" \
  /bin/bash -lc "${INNER_CMD} && echo 'Running test binary...'; ./output/${RUNTIME}/GrindCore.Tests.Runtime"

EXIT_CODE=$?

echo "Container exited with code ${EXIT_CODE}"

if [ ${EXIT_CODE} -ne 0 ]; then
  echo "Test run failed. Check the container logs above."
  exit ${EXIT_CODE}
fi

echo "Test run completed successfully."
