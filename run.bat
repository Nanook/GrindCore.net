@REM dotnet build tests/GrindCore.Tests.WinX86/GrindCore.Tests.WinX86.csproj -c Release -r win-x86 -p:TargetFramework=net8.0 -p:TargetFrameworks=net8.0
@REM tests\GrindCore.Tests.WinX86\bin\x86\Release\net8.0\win-x86\GrindCore.Tests.WinX86.exe

@rem dotnet publish tests/GrindCore.Tests.WinX86/GrindCore.Tests.WinX86.csproj -c Debug -r win-x86 -p:RuntimeIdentifier=win-x86 -p:RuntimeIdentifiers=win-x86 -p:TargetFramework=net8.0 -p:TargetFrameworks=net8.0  -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=false

@rem dotnet publish tests/GrindCore.Tests.Runtime/GrindCore.Tests.Runtime.csproj -c Debug -r win-x86 -p:RuntimeIdentifier=win-x86 -p:RuntimeIdentifiers=win-x86 -p:TargetFramework=net8.0 -p:TargetFrameworks=net8.0  -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=false
@rem tests\GrindCore.Tests.Runtime\bin\Debug\net8.0\win-x86\publish\GrindCore.Tests.Runtime.exe

dotnet test tests/GrindCore.Tests/GrindCore.Tests.csproj -c Debug -r win-x86 -p:RuntimeIdentifier=win-x86 -p:RuntimeIdentifiers=win-x86 -p:TargetFramework=net8.0 -p:TargetFrameworks=net8.0
@echo Returned %ERRORLEVEL%
