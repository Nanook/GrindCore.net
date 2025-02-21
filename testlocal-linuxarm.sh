#!/bin/bash

# Enable qemu
docker run --rm --privileged multiarch/qemu-user-static --reset -p yes

# Setup Docker Buildx (check if instance exists)
if ! docker buildx inspect mybuilder > /dev/null 2>&1; then
    docker buildx create --name mybuilder --use
fi
docker buildx inspect --bootstrap

pushd output/linux-arm/
cp runtimes/linux-arm/native/* .
# rm -rf runtimes
chmod 755 GrindCore.Tests.Runtime
ls -al
docker run --rm --platform linux/arm/v7 -v "$(pwd):/app" mcr.microsoft.com/dotnet/sdk:9.0 bash -c "/app/GrindCore.Tests.Runtime"
popd