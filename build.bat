@echo off
echo Building DSTS Mod Tool

echo.
echo Build debug
dotnet build DSTSModTool.csproj

echo.
echo Build release
dotnet build DSTSModTool.csproj -c Release

echo.
echo Publish self-contained (không cần cài .NET 8.0)
dotnet publish DSTSModTool.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

echo.
echo Build completed!
pause