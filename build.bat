@echo off
echo Building DSTS Mod Tool

echo.
echo Build debug
dotnet build DSTSModTool.csproj

echo.
echo Build release
dotnet build DSTSModTool.csproj -c Release

echo.
echo Publish self-contained (no .NET 8.0 installation required)
dotnet publish DSTSModTool.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

echo.
echo Build completed!
pause