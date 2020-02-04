@echo off
pushd "%~dp0"

dotnet build -c Release
rmdir /S /Q "_Build\Release\packages"
dotnet build -c Release

popd
