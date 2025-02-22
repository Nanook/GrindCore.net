$build = ".build"

# Copy GrindCore.build files
Remove-Item -Path "$build\*" -Recurse -Force
foreach ($item in (Get-ChildItem -Path . -Exclude $build)) {
    Copy-Item -Path $item.FullName -Destination $build -Recurse -Force
}

# Merge in GrindCore files
foreach ($item in (Get-ChildItem -Path "..\GrindCore")) {
    Copy-Item -Path $item.FullName -Destination "$build\src\native" -Recurse -Force
}

# Test Build
pushd "$build"
# docker build -f Dockerfile.win -t grindcore.build.win:latest -m 2GB .
docker run --rm -v ${PWD}:c:/workspaces grindcore.build.win:latest c:/workspaces/src/native/libs/build-native.cmd rebuild x64 Release outconfig win-x64 -os windows -numproc 16
# docker run --rm -v ${PWD}:c:/workspaces grindcore.build.win:latest c:/workspaces/src/native/libs/build-native.cmd rebuild x86 Release outconfig win-x86 -os windows -numproc 16
popd
