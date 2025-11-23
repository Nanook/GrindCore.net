# Run in windows 64 bit - Script to run multiple dotnet publish commands

$commands = @(
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r linux-arm64 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/linux-arm64 --self-contained false",
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r linux-arm -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/linux-arm --self-contained false",
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r linux-x64 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/linux-x64 --self-contained false",
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r osx-arm64 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/osx-arm64 --self-contained false",
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r osx-x64 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/osx-x64 --self-contained false",
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r win-arm64 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/win-arm64 --self-contained false",
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r win-x64 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/win-x64 --self-contained false",
    "dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r win-x86 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/win-x86 --self-contained false"
)

foreach ($command in $commands) {
    Invoke-Expression $command
}
