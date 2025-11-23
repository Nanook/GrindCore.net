#dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r win-x86 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 --self-contained false
#./tests/GrindCore.Tests/bin/Debug/net10.0/win-x86/GrindCore.Tests.exe

dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r win-x86 -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0 -o output/win-x86 --self-contained false
Copy-Item -Path "./output/win-x86/runtimes/win-x86/native/*" -Destination "./output/win-x86/" -Force
./output/win-x86/GrindCore.Tests.Runtime.exe
