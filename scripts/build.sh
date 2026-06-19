#!/usr/bin/env bash
set -euo pipefail

PROJECT="./src/DZServerToolkit/DZServerToolkit.csproj"

printf 'Restoring DZ Server Toolkit...\n'
dotnet restore "$PROJECT"

printf 'Building Release...\n'
dotnet build "$PROJECT" -c Release --no-restore

printf 'Publishing Linux x64 self-contained build...\n'
dotnet publish "$PROJECT" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/linux-x64

printf 'Done. Run ./publish/linux-x64/DZServerToolkit\n'
