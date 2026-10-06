#!/bin/sh
# Builds self-contained Windows x64 copies of the GreyHaven app and CLI, so they can be copied
# to a Windows PC (USB stick, network share) and run without installing .NET.
# Output: artifacts/windows/app/GreyHaven.exe, artifacts/windows/cli/greyhaven.exe and START-HERE.txt
set -eu
cd "$(dirname "$0")/.."
out=artifacts/windows
dotnet publish src/GreyHaven.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o "$out/app"
dotnet publish src/GreyHaven.Cli -c Release -r win-x64 --self-contained -o "$out/cli"
commit=$(git rev-parse --short HEAD 2>/dev/null || echo unknown)
cat > "$out/START-HERE.txt" <<TXT
GreyHaven test build ($commit, built $(TZ=America/Los_Angeles date +%Y-%m-%d))

Nothing to install. Open a terminal in this folder (Windows 11: right-click an empty space,
"Open in Terminal"; Windows 10: Shift + right-click, "Open PowerShell window here"), then:

  .\app\GreyHaven.exe --sample nvidia-desktop    the app in demo mode (no scan)
  .\app\GreyHaven.exe                            the app, scanning this PC
  .\cli\greyhaven.exe scan --dump-raw greyhaven-raw.json
                                                 scan, and save the raw data to bring back
                                                 (saved in this folder)

Step-by-step instructions and the results form: docs/windows-results.md in the repository.
TXT
echo
echo "Copy the '$out' folder to the Windows PC, then run:"
echo "  app\\GreyHaven.exe                      (the app)"
echo "  app\\GreyHaven.exe --sample nvidia-desktop   (demo mode, no scan)"
echo "  cli\\greyhaven.exe scan --dump-raw greyhaven-raw.json"
