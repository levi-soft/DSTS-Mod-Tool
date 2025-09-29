@echo off
echo Building DSTS Mod Tool

echo.
echo Build debug
dotnet build

echo.
echo Build release
dotnet build -c Release

echo.
echo Publish self-contained (không cần cài .NET 8.0)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

echo.
echo Build completed!
pause