#!/bin/sh
# Builds self-contained Windows x64 copies of the GreyHaven app and CLI, so they can be copied
# to a Windows PC (USB stick, network share) and run without installing .NET.
# Output: artifacts/windows/app/GreyHaven.exe and artifacts/windows/cli/greyhaven.exe
set -eu
cd "$(dirname "$0")/.."
out=artifacts/windows
dotnet publish src/GreyHaven.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o "$out/app"
dotnet publish src/GreyHaven.Cli -c Release -r win-x64 --self-contained -o "$out/cli"
echo
echo "Copy the '$out' folder to the Windows PC, then run:"
echo "  app\\GreyHaven.exe                      (the app)"
echo "  app\\GreyHaven.exe --sample nvidia-desktop   (demo mode, no scan)"
echo "  cli\\greyhaven.exe scan --dump-raw greyhaven-raw.json"
