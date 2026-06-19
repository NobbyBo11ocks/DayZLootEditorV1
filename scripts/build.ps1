$ErrorActionPreference = "Stop"

$Project = ".\src\DZServerToolkit\DZServerToolkit.csproj"

Write-Host "Restoring DZ Server Toolkit..."
dotnet restore $Project

Write-Host "Building Release..."
dotnet build $Project -c Release --no-restore

Write-Host "Publishing Windows x64 self-contained build..."
dotnet publish $Project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64

Write-Host "Done. Open .\publish\win-x64\DZServerToolkit.exe"
