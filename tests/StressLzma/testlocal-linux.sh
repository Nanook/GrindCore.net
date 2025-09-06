#!/bin/bash
set -e

# Publish the test project for linux-x64, Debug configuration, .NET 9.0, output to output/linux-x64
dotnet publish StressLzma.csproj -c Release -r linux-x64 -p:TargetFramework=net9.0 -p:TargetFrameworks=net9.0 -o bin/Release/net9.0/linux-x64 --self-contained false

# Copy native libraries to the output directory for easier loading
cp -a ./bin/Release/net9.0/linux-x64/runtimes/linux-x64/native/* ./bin/Release/net9.0/linux-x64/

# Run the test executable
chmod +x ./bin/Release/net9.0/linux-x64/StressLzma
./bin/Release/net9.0/linux-x64/StressLzma --level Level9 --blockSize 131072 --iterations 100000 --threads 16