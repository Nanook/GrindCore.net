#!/bin/bash

build=".build"

# Copy GrindCore.build files
rm -rf "$build"/*
for item in *; do
    if [[ "$item" != "$build" ]]; then
        cp -r "$item" "$build/"
    fi
done

# Merge in GrindCore files
for item in ../GrindCore/*; do
    cp -r "$item" "$build/src/native/"
done

# Test Build
pushd "$build"
docker build -t grindcore.build.linux:latest -f Dockerfile.linux-x64 -m 2GB .
docker run --rm -v "$(pwd):/workspaces" grindcore.build.linux:latest /bin/sh -c "rm -rf /workspaces/artifacts && /workspaces/src/native/libs/build-native.sh x64 Release outconfig linux-x64 -os unix -numproc 16"
popd
