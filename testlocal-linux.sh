#!/bin/bash
set -e

# Publish the test project for linux-x64, Debug configuration, .NET 9.0, output to output/linux-x64
dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r linux-x64 -p:TargetFramework=net9.0 -p:TargetFrameworks=net9.0 -o output/linux-x64 --self-contained false

# Copy native libraries to the output directory for easier loading
cp -a ./output/linux-x64/runtimes/linux-x64/native/* ./output/linux-x64/

# Run the test executable
chmod +x ./output/linux-x64/GrindCore.Tests.Runtime
./output/linux-x64/GrindCore.Tests.Runtime